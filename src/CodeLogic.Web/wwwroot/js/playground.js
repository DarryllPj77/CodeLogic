const shortcutHandlers = new WeakMap();
const sourceControllers = new WeakMap();
const canvasControllers = new WeakMap();
const suppressedSelections = new WeakSet();
const nodeTypeNames = Object.freeze({ 1: "Start", 2: "End", 3: "Action", 4: "Decision", 5: "Input", 6: "Output" });
const mermaidModuleUrl = "https://cdn.jsdelivr.net/npm/mermaid@12.2.0/dist/mermaid.esm.min.mjs";
let mermaidPromise = null;

export function initializeEditorShortcuts(editor) {
    const handler = event => {
        const key = event.key.toLowerCase();
        let command = null;

        if (event.ctrlKey && !event.altKey && !event.shiftKey && key === "s") {
            command = "save";
        } else if (event.ctrlKey && event.altKey && !event.shiftKey && key === "r") {
            command = "reset";
        }

        if (command === null) {
            return;
        }

        event.preventDefault();
    };

    editor.addEventListener("keydown", handler);
    shortcutHandlers.set(editor, handler);
}

export function disposeEditorShortcuts(editor) {
    const handler = shortcutHandlers.get(editor);
    if (handler !== undefined) {
        editor.removeEventListener("keydown", handler);
        shortcutHandlers.delete(editor);
    }
    sourceControllers.get(editor)?.abort();
    sourceControllers.delete(editor);
}

export function loadDraft(storageKey) {
    return window.localStorage.getItem(storageKey);
}

export function saveDraft(storageKey, code) {
    window.localStorage.setItem(storageKey, code);
}

export function clearDraft(storageKey) {
    window.localStorage.removeItem(storageKey);
}

export function confirmReset() {
    return window.confirm("Reset the editor and remove this browser's saved draft?");
}

export async function renderMermaid(container, definition, renderId, sourceMap, dotNetReference) {
    const mermaid = await loadMermaid();
    const rendered = await mermaid.render(renderId, definition);
    const parsed = new DOMParser().parseFromString(rendered.svg, "image/svg+xml");
    if (parsed.querySelector("parsererror") !== null) {
        throw new Error("Mermaid returned invalid SVG.");
    }

    const svg = parsed.documentElement;
    sanitizeSvg(svg);
    svg.removeAttribute("height");
    svg.removeAttribute("width");
    svg.setAttribute("role", "img");
    svg.setAttribute("aria-label", "CodeLogic flowchart preview");
    svg.classList.add("flowchart-svg");

    container.replaceChildren(document.importNode(svg, true));
    bindDiagramNodes(container, sourceMap, dotNetReference);
    initializeSourceSynchronization(document.getElementById("code-editor"), container, sourceMap, dotNetReference);
    initializeCanvasInteractions(container, dotNetReference);
}

export function selectSource(editor, container, mermaidId, startOffset, length) {
    suppressedSelections.add(editor);
    editor.focus();
    editor.setSelectionRange(startOffset, startOffset + length);
    window.setTimeout(() => suppressedSelections.delete(editor), 0);

    container.querySelectorAll(".flowchart-node--selected")
        .forEach(node => node.classList.remove("flowchart-node--selected"));
    const escapedId = CSS.escape(`-flowchart-${mermaidId}-`);
    const node = container.querySelector(`[id*="${escapedId}"]`);
    if (node !== null) {
        node.classList.add("flowchart-node--selected");
        centerNode(container, node);
    }
}

export function setFlowchartZoom(container, scale) {
    const svg = container.querySelector(".flowchart-svg");
    if (svg === null) {
        return;
    }

    svg.style.transform = `scale(${scale})`;
    svg.style.transformOrigin = "top center";
    container.dataset.zoom = String(scale);
}

export function fitFlowchart(container) {
    const svg = container.querySelector(".flowchart-svg");
    if (svg === null) {
        return;
    }

    svg.style.minWidth = "0";
    svg.style.transform = "none";
    svg.style.width = "100%";
    container.dataset.zoom = "1";
    container.scrollTo({ top: 0, left: 0, behavior: "smooth" });
}

export function showTraceStep(editor, container, currentMermaidId, startOffset, length, previousMermaidId, skippedMermaidIds) {
    container.querySelectorAll(".flowchart-node--trace-current, .flowchart-node--trace-skipped")
        .forEach(node => node.classList.remove("flowchart-node--trace-current", "flowchart-node--trace-skipped"));
    container.querySelectorAll(".edgePath")
        .forEach(edge => edge.classList.remove("flowchart-edge--trace-taken"));

    const current = findDiagramNode(container, currentMermaidId);
    current?.classList.add("flowchart-node--trace-current");
    for (const mermaidId of skippedMermaidIds ?? []) {
        findDiagramNode(container, mermaidId)?.classList.add("flowchart-node--trace-skipped");
    }

    if (previousMermaidId !== null && previousMermaidId !== undefined) {
        const edge = [...container.querySelectorAll(".edgePath")]
            .find(candidate => candidate.id.includes(previousMermaidId) && candidate.id.includes(currentMermaidId));
        edge?.classList.add("flowchart-edge--trace-taken");
    }

    if (Number.isInteger(startOffset) && Number.isInteger(length)) {
        suppressedSelections.add(editor);
        editor.setSelectionRange(startOffset, startOffset + length);
        window.setTimeout(() => suppressedSelections.delete(editor), 0);
    }

    if (current !== null) {
        centerNode(container, current);
    }
}

export function focusElement(element) {
    element?.focus();
}

export function scrollToElement(elementId) {
    document.getElementById(elementId)?.scrollIntoView({ behavior: "smooth", block: "start" });
}

function bindDiagramNodes(container, sourceMap, dotNetReference) {
    for (const item of sourceMap ?? []) {
        const selector = `[id*="${CSS.escape(`-flowchart-${item.mermaidId}-`)}"]`;
        const node = container.querySelector(selector);
        if (node === null) {
            continue;
        }

        node.classList.add("flowchart-node--interactive");
        node.setAttribute("role", "button");
        node.setAttribute("tabindex", "0");
        const line = item.sourceSpan?.startLine;
        const nodeType = nodeTypeNames[item.nodeType] ?? "Flowchart node";
        node.setAttribute("aria-label", `${nodeType}: ${item.label}${line ? `, source line ${line}` : ""}`);
        const title = document.createElementNS("http://www.w3.org/2000/svg", "title");
        title.textContent = `${nodeType}: ${item.label}${line ? ` (line ${line})` : ""}`;
        node.prepend(title);
        const activate = () => dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId);
        node.addEventListener("click", activate);
        if (item.sourceSpan !== null && item.sourceSpan !== undefined) {
            node.addEventListener("dblclick", event => {
                event.preventDefault();
                event.stopPropagation();
                const replacement = window.prompt(
                    "Edit the mapped C# statement. CodeLogic will reject invalid or unsupported syntax.",
                    item.label);
                if (replacement !== null && replacement !== item.label) {
                    dotNetReference?.invokeMethodAsync("EditNodeFromBrowser", item.nodeId, replacement);
                }
            });
        }
        node.addEventListener("keydown", event => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                activate();
            }
        });
    }
}

function initializeSourceSynchronization(editor, container, sourceMap, dotNetReference) {
    if (editor === null) {
        return;
    }

    sourceControllers.get(editor)?.abort();
    const controller = new AbortController();
    sourceControllers.set(editor, controller);
    let lastNodeId = null;
    const synchronize = () => {
        if (suppressedSelections.has(editor)) {
            return;
        }

        const caret = editor.selectionStart;
        const item = (sourceMap ?? []).find(candidate => {
            const span = candidate.sourceSpan;
            return span !== null && span !== undefined
                && caret >= span.startOffset
                && caret <= span.startOffset + Math.max(span.length, 1);
        });
        if (item !== undefined && item.nodeId !== lastNodeId) {
            lastNodeId = item.nodeId;
            highlightNode(container, item.mermaidId);
            dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId);
        }
    };

    for (const eventName of ["click", "keyup", "select"]) {
        editor.addEventListener(eventName, synchronize, { signal: controller.signal });
    }
}

function initializeCanvasInteractions(container, dotNetReference) {
    canvasControllers.get(container)?.abort();
    const controller = new AbortController();
    canvasControllers.set(container, controller);
    container.classList.add("flowchart-canvas--interactive");
    container.dataset.zoom = container.dataset.zoom || "1";

    let drag = null;
    container.addEventListener("pointerdown", event => {
        if (event.button !== 0 || event.target.closest?.(".node") !== null) {
            return;
        }
        drag = { x: event.clientX, y: event.clientY, left: container.scrollLeft, top: container.scrollTop };
        container.setPointerCapture(event.pointerId);
        container.classList.add("flowchart-canvas--panning");
    }, { signal: controller.signal });
    container.addEventListener("pointermove", event => {
        if (drag !== null) {
            container.scrollLeft = drag.left - (event.clientX - drag.x);
            container.scrollTop = drag.top - (event.clientY - drag.y);
        }
    }, { signal: controller.signal });
    const stopPanning = () => {
        drag = null;
        container.classList.remove("flowchart-canvas--panning");
    };
    container.addEventListener("pointerup", stopPanning, { signal: controller.signal });
    container.addEventListener("pointercancel", stopPanning, { signal: controller.signal });
    container.addEventListener("wheel", event => {
        if (!event.ctrlKey) {
            return;
        }
        event.preventDefault();
        const current = Number.parseFloat(container.dataset.zoom || "1");
        const next = Math.min(2, Math.max(0.5, current + (event.deltaY < 0 ? 0.1 : -0.1)));
        setFlowchartZoom(container, next);
        dotNetReference?.invokeMethodAsync("UpdateZoomFromBrowser", next);
    }, { passive: false, signal: controller.signal });
}

function highlightNode(container, mermaidId) {
    container.querySelectorAll(".flowchart-node--selected")
        .forEach(node => node.classList.remove("flowchart-node--selected"));
    const node = container.querySelector(`[id*="${CSS.escape(`-flowchart-${mermaidId}-`)}"]`);
    node?.classList.add("flowchart-node--selected");
}

function findDiagramNode(container, mermaidId) {
    return container.querySelector(`[id*="${CSS.escape(`-flowchart-${mermaidId}-`)}"]`);
}

function centerNode(container, node) {
    const left = node.getBoundingClientRect().left - container.getBoundingClientRect().left
        + container.scrollLeft - (container.clientWidth / 2) + (node.getBoundingClientRect().width / 2);
    const top = node.getBoundingClientRect().top - container.getBoundingClientRect().top
        + container.scrollTop - (container.clientHeight / 2) + (node.getBoundingClientRect().height / 2);
    container.scrollTo({ left, top, behavior: "smooth" });
}

async function loadMermaid() {
    if (mermaidPromise === null) {
        mermaidPromise = import(mermaidModuleUrl).then(module => {
            const mermaid = module.default;
            mermaid.initialize({
                startOnLoad: false,
                securityLevel: "strict",
                htmlLabels: false,
                maxTextSize: 10000,
                maxEdges: 50,
                theme: "base",
                themeVariables: {
                    background: "#0a1020",
                    primaryColor: "#172b49",
                    primaryTextColor: "#e6edf7",
                    primaryBorderColor: "#4f8fea",
                    lineColor: "#64748b",
                    secondaryColor: "#332a17",
                    secondaryTextColor: "#e6edf7",
                    secondaryBorderColor: "#f5b942",
                    tertiaryColor: "#2d1635",
                    tertiaryTextColor: "#f5d0fe",
                    tertiaryBorderColor: "#d946ef",
                    edgeLabelBackground: "#111b2b",
                    fontFamily: "Cascadia Code, Consolas, monospace"
                },
                flowchart: {
                    curve: "linear",
                    useMaxWidth: true
                }
            });
            return mermaid;
        });
    }

    return mermaidPromise;
}

function sanitizeSvg(svg) {
    svg.querySelectorAll("script, foreignObject, iframe, object, embed")
        .forEach(element => element.remove());

    svg.querySelectorAll("a").forEach(anchor => {
        anchor.replaceWith(...anchor.childNodes);
    });

    for (const element of [svg, ...svg.querySelectorAll("*")]) {
        for (const attribute of [...element.attributes]) {
            const name = attribute.name.toLowerCase();
            if (name.startsWith("on") || name === "href" || name === "xlink:href") {
                element.removeAttribute(attribute.name);
            }
        }
    }
}
