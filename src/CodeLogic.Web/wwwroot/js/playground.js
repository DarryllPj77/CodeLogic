const shortcutHandlers = new WeakMap();
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

export async function renderMermaid(container, definition, renderId) {
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
}

export function selectSource(editor, container, mermaidId, startOffset, length) {
    editor.focus();
    editor.setSelectionRange(startOffset, startOffset + length);
    editor.scrollIntoView({ behavior: "smooth", block: "center" });

    container.querySelectorAll(".flowchart-node--selected")
        .forEach(node => node.classList.remove("flowchart-node--selected"));
    const escapedId = CSS.escape(`flowchart-${mermaidId}-`);
    const node = container.querySelector(`[id^="${escapedId}"]`);
    if (node !== null) {
        node.classList.add("flowchart-node--selected");
    }
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
                    primaryColor: "#eef2ff",
                    primaryTextColor: "#172554",
                    primaryBorderColor: "#3b5ccc",
                    lineColor: "#334155",
                    secondaryColor: "#ecfeff",
                    tertiaryColor: "#f8fafc"
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
