import { enableNodeDragging, prepareLayout, resetLayout } from "./flowchart.js?v=2";

const shortcutHandlers = new WeakMap();
const editorTabModes = new WeakMap();
const codeEditors = new WeakMap();
const plainEditors = new WeakMap();
const sourceControllers = new WeakMap();
const canvasControllers = new WeakMap();
const canvasSourceMaps = new WeakMap();
const suppressedSelections = new WeakSet();
const nodeTypeNames = Object.freeze({ 1: "Start", 2: "End", 3: "Action", 4: "Decision", 5: "Input", 6: "Output", 7: "Call" });
const mermaidModuleUrl = "https://cdn.jsdelivr.net/npm/mermaid@12.2.0/dist/mermaid.esm.min.mjs";
const monacoBaseUrl = "https://cdn.jsdelivr.net/npm/monaco-editor@0.57.0/min/vs";
let mermaidPromise = null;
let monacoPromise = null;

// Returns "monaco" when the rich editor loaded, or "plain" when the accessible textarea fallback is used.
export async function initializeCodeEditor(host, initialValue, maximumCharacters, dotNetReference, foldRanges, lightTheme = false) {
    disposeCodeEditor(host);
    let monaco;
    try {
        monaco = await loadMonaco();
    } catch {
        monacoPromise = null;
        initializePlainEditor(host, initialValue, maximumCharacters, dotNetReference);
        return "plain";
    }
    const model = monaco.editor.createModel(initialValue, "csharp");
    model.updateOptions({ tabSize: 4, insertSpaces: true });
    monaco.editor.setTheme(lightTheme ? "vs" : "vs-dark");
    const editor = monaco.editor.create(host, {
        model,
        ariaLabel: "C# source text. Tab indents. Press Escape, then Tab to leave the editor.",
        accessibilitySupport: "auto",
        automaticLayout: true,
        bracketPairColorization: { enabled: true },
        folding: true,
        foldingHighlight: true,
        foldingStrategy: "auto",
        fontFamily: "Cascadia Code, Consolas, monospace",
        fontSize: 13,
        lineHeight: 21,
        lineNumbers: "on",
        lineNumbersMinChars: 3,
        minimap: { enabled: false },
        padding: { top: 16, bottom: 16 },
        renderLineHighlight: "all",
        scrollBeyondLastLine: false,
        selectOnLineNumbers: true,
        showFoldingControls: "mouseover",
        tabFocusMode: false,
        wordWrap: "off"
    });

    const state = {
        editor,
        model,
        dotNetReference,
        maximumCharacters,
        revertingLimit: false,
        sourceSelectionDisposable: null,
        foldRanges: normalizeFoldRanges(foldRanges, initialValue),
        foldListeners: new Set(),
        foldingProvider: null,
        markerObserver: null,
        disposables: []
    };
    codeEditors.set(host, state);
    state.foldingProvider = monaco.languages.registerFoldingRangeProvider("csharp", {
        onDidChange(listener) {
            state.foldListeners.add(listener);
            return { dispose: () => state.foldListeners.delete(listener) };
        },
        provideFoldingRanges() {
            return state.foldRanges.map(region => ({
                start: region.startLine,
                end: region.endLine,
                collapsedText: `{ ... } ${region.endLine - region.startLine} hidden line${region.endLine - region.startLine === 1 ? "" : "s"}`
            }));
        }
    });
    state.disposables.push(state.foldingProvider);
    state.disposables.push(model.onDidChangeContent(() => {
        if (state.revertingLimit) {
            return;
        }
        const value = model.getValue();
        if (value.length > maximumCharacters) {
            state.revertingLimit = true;
            editor.trigger("codelogic", "undo", null);
            state.revertingLimit = false;
            return;
        }
        dotNetReference?.invokeMethodAsync("UpdateCodeFromBrowser", value);
    }));
    state.disposables.push(editor.onDidBlurEditorWidget(() => editor.updateOptions({ tabFocusMode: false })));
    editor.addCommand(monaco.KeyCode.Escape, () => editor.updateOptions({ tabFocusMode: true }));
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS,
        () => dotNetReference?.invokeMethodAsync("SaveEditorDraftFromBrowser"));
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Alt | monaco.KeyCode.KeyR,
        () => dotNetReference?.invokeMethodAsync("ResetEditorDraftFromBrowser"));
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.BracketLeft,
        () => editor.getAction("editor.fold")?.run());
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.BracketRight,
        () => editor.getAction("editor.unfold")?.run());

    const menuHandler = event => {
        const command = event.target.closest?.("[data-editor-command]")?.dataset.editorCommand;
        if (command === "foldAll" || command === "unfoldAll") {
            event.preventDefault();
            foldEditor(host, command);
        }
    };
    document.addEventListener("click", menuHandler);
    state.disposables.push({ dispose: () => document.removeEventListener("click", menuHandler) });
    state.markerObserver = new MutationObserver(() => makeFoldMarkersAccessible(host, editor));
    state.markerObserver.observe(host, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"] });
    state.disposables.push({ dispose: () => state.markerObserver.disconnect() });
    makeFoldMarkersAccessible(host, editor);
    return "monaco";
}

function initializePlainEditor(host, initialValue, maximumCharacters, dotNetReference) {
    const textarea = document.createElement("textarea");
    textarea.className = "code-editor__fallback";
    textarea.value = initialValue;
    textarea.maxLength = maximumCharacters;
    textarea.spellcheck = false;
    textarea.setAttribute("autocomplete", "off");
    textarea.setAttribute("autocapitalize", "off");
    textarea.setAttribute("aria-label", "C# source text (plain-text editor)");
    const describedBy = host.getAttribute("aria-describedby");
    if (describedBy) {
        textarea.setAttribute("aria-describedby", describedBy);
    }
    host.replaceChildren(textarea);
    plainEditors.set(host, textarea);

    textarea.addEventListener("input", () => dotNetReference?.invokeMethodAsync("UpdateCodeFromBrowser", textarea.value));
    textarea.addEventListener("keydown", event => {
        const key = event.key.toLowerCase();
        if (event.ctrlKey && !event.altKey && !event.shiftKey && key === "s") {
            dotNetReference?.invokeMethodAsync("SaveEditorDraftFromBrowser");
        } else if (event.ctrlKey && event.altKey && !event.shiftKey && key === "r") {
            dotNetReference?.invokeMethodAsync("ResetEditorDraftFromBrowser");
        }
    });
    initializeEditorShortcuts(textarea);
}

// The element that owns text selection: the fallback textarea when present, otherwise the host itself.
function plainEditorFor(host) {
    return plainEditors.get(host) ?? host;
}

export function disposeCodeEditor(host) {
    const textarea = plainEditors.get(host);
    if (textarea !== undefined) {
        disposeEditorShortcuts(textarea);
        textarea.remove();
        plainEditors.delete(host);
    }
    const state = codeEditors.get(host);
    if (state === undefined) {
        return;
    }
    state.sourceSelectionDisposable?.dispose();
    for (const disposable of state.disposables) {
        disposable.dispose();
    }
    state.editor.dispose();
    state.model.dispose();
    codeEditors.delete(host);
}

export function setEditorValue(host, value) {
    const textarea = plainEditors.get(host);
    if (textarea !== undefined) {
        if (textarea.value !== value) {
            textarea.value = value;
        }
        return;
    }
    const state = codeEditors.get(host);
    if (state === undefined || state.model.getValue() === value) {
        return;
    }
    // An edit operation (rather than setValue) keeps the change on the undo stack, so Ctrl+Z can
    // undo a loaded example or an automatic rewrite.
    state.editor.pushUndoStop();
    state.editor.executeEdits("codelogic", [{ range: state.model.getFullModelRange(), text: value }]);
    state.editor.pushUndoStop();
}

export function updateFoldRegions(host, foldRanges) {
    const state = codeEditors.get(host);
    if (state === undefined) {
        return;
    }
    state.foldRanges = normalizeFoldRanges(foldRanges, state.model.getValue());
    for (const listener of state.foldListeners) {
        listener(undefined);
    }
}

export function foldEditor(host, command) {
    const state = codeEditors.get(host);
    const actionId = command === "foldAll" ? "editor.foldAll" : "editor.unfoldAll";
    return state?.editor.getAction(actionId)?.run();
}

export function initializeEditorShortcuts(editor) {
    editorTabModes.set(editor, true);
    const handler = event => {
        const key = event.key.toLowerCase();
        let command = null;

        if (event.ctrlKey && !event.altKey && !event.shiftKey && key === "s") {
            command = "save";
        } else if (event.ctrlKey && event.altKey && !event.shiftKey && key === "r") {
            command = "reset";
        }

        if (command !== null) {
            event.preventDefault();
            return;
        }

        const tabAction = resolveEditorTabKey(event.key, event.shiftKey, editorTabModes.get(editor) ?? true);
        editorTabModes.set(editor, tabAction.tabIndentEnabled);
        if (tabAction.action === "release") {
            event.preventDefault();
            return;
        }

        if (tabAction.action === "focus-next" || tabAction.action === "none") {
            return;
        }

        event.preventDefault();
        const edit = createIndentEdit(
            editor.value,
            editor.selectionStart,
            editor.selectionEnd,
            tabAction.action === "outdent");
        const editedLength = edit === null
            ? editor.value.length
            : editor.value.length - (edit.rangeEnd - edit.rangeStart) + edit.replacement.length;
        if (edit !== null && (editor.maxLength < 0 || editedLength <= editor.maxLength)) {
            applyEditorEdit(editor, edit);
        }
    };

    editor.addEventListener("keydown", handler);
    shortcutHandlers.set(editor, handler);
}

export function disposeEditorShortcuts(editor) {
    const handler = shortcutHandlers.get(editor);
    if (handler !== undefined) {
        editor.removeEventListener("keydown", handler);
        shortcutHandlers.delete(editor);
        editorTabModes.delete(editor);
    }
    sourceControllers.get(editor)?.abort();
    sourceControllers.delete(editor);
}

export function loadDraft(storageKey) {
    return window.localStorage.getItem(storageKey);
}

export function saveDraft(storageKey, code, maximumCharacters) {
    if (!Number.isInteger(maximumCharacters) || maximumCharacters <= 0) {
        throw new RangeError("The draft character limit must be a positive integer.");
    }
    if (code.length > maximumCharacters) {
        throw new RangeError(`The draft exceeds the ${maximumCharacters.toLocaleString()}-character limit.`);
    }

    window.localStorage.setItem(storageKey, code);
}

export function resolveEditorTabKey(key, shiftKey, tabIndentEnabled) {
    if (key === "Escape") {
        return { action: "release", tabIndentEnabled: false };
    }
    if (key !== "Tab") {
        return { action: "none", tabIndentEnabled };
    }
    if (!tabIndentEnabled) {
        return { action: "focus-next", tabIndentEnabled: true };
    }

    return { action: shiftKey ? "outdent" : "indent", tabIndentEnabled: true };
}

export function createIndentEdit(value, selectionStart, selectionEnd, outdent = false) {
    if (typeof value !== "string"
        || !Number.isInteger(selectionStart)
        || !Number.isInteger(selectionEnd)
        || selectionStart < 0
        || selectionEnd < selectionStart
        || selectionEnd > value.length) {
        throw new RangeError("The editor selection must be within the source text.");
    }

    if (!outdent && selectionStart === selectionEnd) {
        return {
            rangeStart: selectionStart,
            rangeEnd: selectionEnd,
            replacement: "    ",
            selectionStart: selectionStart + 4,
            selectionEnd: selectionEnd + 4
        };
    }

    const rangeStart = value.lastIndexOf("\n", Math.max(0, selectionStart - 1)) + 1;
    let rangeEnd;
    if (selectionEnd > selectionStart && value[selectionEnd - 1] === "\n") {
        rangeEnd = selectionEnd - 1;
    } else {
        const nextLineBreak = value.indexOf("\n", selectionEnd);
        rangeEnd = nextLineBreak === -1 ? value.length : nextLineBreak;
    }
    if (rangeEnd > rangeStart && value[rangeEnd - 1] === "\r") {
        rangeEnd--;
    }

    const block = value.slice(rangeStart, rangeEnd);
    const lines = block.split("\n");
    const changes = [];
    let lineStart = rangeStart;
    const changedLines = lines.map(line => {
        if (!outdent) {
            changes.push({ start: lineStart, removed: 0, inserted: 4 });
            lineStart += line.length + 1;
            return `    ${line}`;
        }

        const leadingSpaces = line.match(/^ {1,4}/)?.[0].length ?? 0;
        if (leadingSpaces > 0) {
            changes.push({ start: lineStart, removed: leadingSpaces, inserted: 0 });
        }
        lineStart += line.length + 1;
        return line.slice(leadingSpaces);
    });

    if (changes.length === 0) {
        return null;
    }

    return {
        rangeStart,
        rangeEnd,
        replacement: changedLines.join("\n"),
        selectionStart: mapSelectionPosition(selectionStart, changes),
        selectionEnd: mapSelectionPosition(selectionEnd, changes)
    };
}

function mapSelectionPosition(position, changes) {
    let delta = 0;
    for (const change of changes) {
        if (change.removed === 0) {
            if (position >= change.start) {
                delta += change.inserted;
            }
            continue;
        }
        if (position < change.start) {
            break;
        }
        if (position <= change.start + change.removed) {
            return change.start + delta;
        }

        delta += change.inserted - change.removed;
    }

    return position + delta;
}

function applyEditorEdit(editor, edit) {
    editor.focus();
    editor.setSelectionRange(edit.rangeStart, edit.rangeEnd);
    let inputObserved = false;
    editor.addEventListener("input", () => inputObserved = true, { once: true });

    const insertedWithUndo = typeof document.execCommand === "function"
        && document.execCommand("insertText", false, edit.replacement);
    if (!insertedWithUndo) {
        editor.setRangeText(edit.replacement, edit.rangeStart, edit.rangeEnd, "end");
    }

    editor.setSelectionRange(edit.selectionStart, edit.selectionEnd);
    if (!inputObserved) {
        editor.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
    }
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
    initializeCanvasInteractions(container, sourceMap, dotNetReference);
    // Positions the learner dragged survive a re-render of the same graph.
    prepareLayout(container, definition);

    // Open at a readable zoom: large graphs scroll instead of shrinking until the text disappears.
    const zoom = openingZoomFor(container);
    applyZoom(container, zoom);
    container.dataset.autoZoom = "true";
    container.scrollTo({ top: 0, left: 0 });
    return zoom;
}

export function selectSource(editor, container, mermaidId, startOffset, length) {
    suppressedSelections.add(editor);
    selectEditorRange(editor, startOffset, length);
    window.setTimeout(() => suppressedSelections.delete(editor), 0);

    container.querySelectorAll(".flowchart-node--selected")
        .forEach(node => node.classList.remove("flowchart-node--selected"));
    const escapedId = CSS.escape(`-flowchart-${mermaidId}-`);
    const node = container.querySelector(`[id*="${escapedId}"]`);
    if (node !== null) {
        node.classList.add("flowchart-node--selected");
        revealNode(container, node);
    }
}

export const minimumZoom = 0.2;
export const maximumZoom = 2;

/** Mermaid settings that keep large graphs legible: 14px text, roomy spacing, natural SVG size. */
export const mermaidFlowchartConfig = Object.freeze({
    fontSize: 14,
    curve: "linear",
    useMaxWidth: false,
    padding: 26,
    // Wider than a 34-character line of 14px text, so Mermaid never re-wraps CodeLogic's own lines.
    wrappingWidth: 340,
    nodeSpacing: 60,
    rankSpacing: 72,
    diagramPadding: 24,
    maxEdges: 400
});

export function clampZoom(zoom) {
    return Math.round(Math.min(maximumZoom, Math.max(minimumZoom, zoom)) * 100) / 100;
}

/**
 * The zoom a freshly rendered graph opens at. A graph that fits at 100% is fitted (up to 125%);
 * anything larger opens at 100% so its text stays readable and the canvas scrolls instead.
 */
export function computeOpeningZoom(naturalWidth, naturalHeight, availableWidth, availableHeight) {
    const fit = computeFitZoom(naturalWidth, naturalHeight, availableWidth, availableHeight, "all");
    return fit === null || fit < 1 ? 1 : Math.min(fit, 1.25);
}

/** Zoom that shows the whole graph ("all") or its full width ("width"), within the zoom limits. */
export function computeFitZoom(naturalWidth, naturalHeight, availableWidth, availableHeight, mode = "all") {
    if (!(naturalWidth > 0) || !(naturalHeight > 0) || !(availableWidth > 0)) {
        return null;
    }
    const byWidth = availableWidth / naturalWidth;
    const zoom = mode === "width" || !(availableHeight > 0)
        ? byWidth
        : Math.min(byWidth, availableHeight / naturalHeight);
    return clampZoom(zoom);
}

/**
 * Scroll position that keeps the same content point under an anchor (the cursor, or the viewport
 * centre) when zooming from oldZoom to newZoom.
 */
export function computeAnchoredScroll(scrollLeft, scrollTop, anchorX, anchorY, oldZoom, newZoom) {
    const ratio = newZoom / oldZoom;
    return {
        left: Math.max(0, (scrollLeft + anchorX) * ratio - anchorX),
        top: Math.max(0, (scrollTop + anchorY) * ratio - anchorY)
    };
}

/**
 * Scroll position that brings a target box (in scrolled-content coordinates) into the viewport
 * with a margin, moving as little as possible. Returns null when it is already comfortably visible.
 */
export function computeRevealScroll(viewport, target, margin = 48) {
    const scroll = { left: viewport.left, top: viewport.top };
    const fitsWide = target.width + 2 * margin <= viewport.width;
    const fitsTall = target.height + 2 * margin <= viewport.height;

    if (target.x - margin < viewport.left || !fitsWide && target.x < viewport.left) {
        scroll.left = target.x - margin;
    } else if (target.x + target.width + margin > viewport.left + viewport.width) {
        scroll.left = fitsWide ? target.x + target.width + margin - viewport.width : target.x - margin;
    }

    if (target.y - margin < viewport.top || !fitsTall && target.y < viewport.top) {
        scroll.top = target.y - margin;
    } else if (target.y + target.height + margin > viewport.top + viewport.height) {
        scroll.top = fitsTall ? target.y + target.height + margin - viewport.height : target.y - margin;
    }

    scroll.left = Math.max(0, scroll.left);
    scroll.top = Math.max(0, scroll.top);
    return scroll.left === viewport.left && scroll.top === viewport.top ? null : scroll;
}

/** Applies a zoom by resizing the SVG itself, so scroll bars always reach the whole graph. */
export function setFlowchartZoom(container, scale) {
    const anchor = { x: container.clientWidth / 2, y: container.clientHeight / 2 };
    container.dataset.autoZoom = "false";
    return applyZoom(container, scale, anchor);
}

export function fitFlowchart(container, mode = "all") {
    const svg = container.querySelector(".flowchart-svg");
    const natural = naturalSize(svg);
    if (natural === null) {
        return 1;
    }
    const available = availableSize(container);
    const zoom = computeFitZoom(natural.width, natural.height, available.width, available.height, mode) ?? 1;
    container.dataset.autoZoom = "false";
    applyZoom(container, zoom);
    container.scrollTo({ top: 0, left: 0 });
    return zoom;
}

/** Auto layout: puts every dragged node and its connectors back where Mermaid placed them. */
export function resetFlowchartLayout(container) {
    const moved = resetLayout(container);
    // The canvas returns to the computed size; keep the current zoom.
    applyZoom(container, Number.parseFloat(container.dataset.zoom || "1"));
    return moved;
}

/** Scrolls to the program's Start or End node and focuses it, so keyboard users land on it too. */
export function goToFlowchartNode(container, which) {
    const sourceMap = canvasSourceMaps.get(container) ?? [];
    const wanted = which === "end" ? 2 : 1;
    // The entry flow's Start/End come first; method sub-flowcharts follow.
    const item = sourceMap.find(candidate => candidate.nodeType === wanted);
    const node = item === undefined ? null : findDiagramNode(container, item.mermaidId);
    if (node === null) {
        return false;
    }
    revealNode(container, node, false);
    node.focus?.({ preventScroll: true });
    return true;
}

function applyZoom(container, scale, anchor) {
    const svg = container.querySelector(".flowchart-svg");
    const natural = naturalSize(svg);
    const zoom = clampZoom(scale);
    if (natural === null) {
        return zoom;
    }

    const previous = Number.parseFloat(container.dataset.zoom || "1");
    const scroll = anchor === undefined
        ? null
        : computeAnchoredScroll(container.scrollLeft, container.scrollTop, anchor.x, anchor.y, previous, zoom);
    svg.style.transform = "none";
    svg.style.width = `${Math.round(natural.width * zoom)}px`;
    svg.style.height = `${Math.round(natural.height * zoom)}px`;
    container.dataset.zoom = String(zoom);
    if (scroll !== null) {
        container.scrollTo(scroll);
    }
    return zoom;
}

function naturalSize(svg) {
    const box = svg?.viewBox?.baseVal;
    return box && box.width > 0 && box.height > 0 ? { width: box.width, height: box.height } : null;
}

function availableSize(container) {
    const style = window.getComputedStyle(container);
    return {
        width: container.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
        height: container.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom)
    };
}

function openingZoomFor(container) {
    const natural = naturalSize(container.querySelector(".flowchart-svg"));
    if (natural === null) {
        return 1;
    }
    const available = availableSize(container);
    return computeOpeningZoom(natural.width, natural.height, available.width, available.height);
}

/**
 * Keeps a node visible with a margin; the canvas only moves when the node is near or past an edge.
 * The jump is instant: during Play, steps can arrive faster than a smooth scroll can finish.
 */
function revealNode(container, node, smooth = false) {
    const nodeBox = node.getBoundingClientRect();
    const canvasBox = container.getBoundingClientRect();
    const target = {
        x: nodeBox.left - canvasBox.left + container.scrollLeft,
        y: nodeBox.top - canvasBox.top + container.scrollTop,
        width: nodeBox.width,
        height: nodeBox.height
    };
    const viewport = { left: container.scrollLeft, top: container.scrollTop, width: container.clientWidth, height: container.clientHeight };
    const scroll = computeRevealScroll(viewport, target);
    if (scroll !== null) {
        container.scrollTo({ ...scroll, behavior: smooth ? "smooth" : "auto" });
    }
}

// Monaco's theme is global, so one call switches the only editor on the page. The plain-text
// fallback follows the host's code-editor--light class instead.
export function setEditorTheme(lightTheme) {
    window.monaco?.editor.setTheme(lightTheme ? "vs" : "vs-dark");
}

export function focusById(elementId) {
    document.getElementById(elementId)?.focus();
}

export function showTraceStep(editor, container, currentMermaidId, startOffset, length, previousMermaidId, skippedMermaidIds) {
    container.querySelectorAll(".flowchart-node--trace-current, .flowchart-node--trace-skipped")
        .forEach(node => node.classList.remove("flowchart-node--trace-current", "flowchart-node--trace-skipped"));
    container.querySelectorAll(".edgePath, .flowchart-link")
        .forEach(edge => edge.classList.remove("flowchart-edge--trace-taken"));

    const current = findDiagramNode(container, currentMermaidId);
    current?.classList.add("flowchart-node--trace-current");
    for (const mermaidId of skippedMermaidIds ?? []) {
        findDiagramNode(container, mermaidId)?.classList.add("flowchart-node--trace-skipped");
    }

    if (previousMermaidId !== null && previousMermaidId !== undefined) {
        const edge = [...container.querySelectorAll(".edgePath, .flowchart-link")]
            .find(candidate => candidate.id.includes(previousMermaidId) && candidate.id.includes(currentMermaidId));
        edge?.classList.add("flowchart-edge--trace-taken");
    }

    if (Number.isInteger(startOffset) && Number.isInteger(length)) {
        suppressedSelections.add(editor);
        selectEditorRange(editor, startOffset, length, false);
        window.setTimeout(() => suppressedSelections.delete(editor), 0);
    }

    highlightTraceLines(editor, container, startOffset, length, skippedMermaidIds);

    if (current !== null) {
        revealNode(container, current);
    }
}

/** Removes the step and skipped-branch line highlights from the code view. */
export function clearTraceHighlights(editor) {
    const state = codeEditors.get(editor);
    state?.traceDecorations?.clear();
}

// The active statement's lines are highlighted and the skipped branch's lines are dimmed, matching
// the nodes highlighted and dimmed in the flowchart.
function highlightTraceLines(editor, container, startOffset, length, skippedMermaidIds) {
    const state = codeEditors.get(editor);
    const monaco = window.monaco;
    if (state === undefined || monaco === undefined) {
        return;
    }

    const lines = (offset, size) => {
        const start = state.model.getPositionAt(offset).lineNumber;
        const end = state.model.getPositionAt(offset + Math.max(size, 1) - 1).lineNumber;
        return new monaco.Range(start, 1, end, 1);
    };
    const decorations = [];
    if (Number.isInteger(startOffset) && Number.isInteger(length)) {
        decorations.push({
            range: lines(startOffset, length),
            options: { isWholeLine: true, className: "trace-current-line", linesDecorationsClassName: "trace-current-gutter" }
        });
    }

    const sourceMap = canvasSourceMaps.get(container) ?? [];
    for (const mermaidId of skippedMermaidIds ?? []) {
        const span = sourceMap.find(item => item.mermaidId === mermaidId)?.sourceSpan;
        if (span !== null && span !== undefined) {
            decorations.push({ range: lines(span.startOffset, span.length), options: { isWholeLine: true, inlineClassName: "trace-skipped-line" } });
        }
    }

    state.traceDecorations?.clear();
    state.traceDecorations = state.editor.createDecorationsCollection(decorations);
}

export function focusElement(element) {
    const state = codeEditors.get(element);
    if (state !== undefined) {
        state.editor.focus();
    } else if (element) {
        plainEditorFor(element).focus();
    }
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

        const typeName = nodeTypeNames[item.nodeType];
        const nodeType = typeName ?? "Flowchart node";
        node.classList.add("flowchart-node--interactive");
        if (typeName !== undefined) {
            node.classList.add(`flowchart-node--${typeName.toLowerCase()}`);
        }
        node.setAttribute("role", "button");
        node.setAttribute("tabindex", "0");
        const line = item.sourceSpan?.startLine;
        node.setAttribute("aria-label", `${nodeType}: ${item.label}${line ? `, source line ${line}` : ""}`);
        const title = document.createElementNS("http://www.w3.org/2000/svg", "title");
        title.textContent = `${nodeType}: ${item.label}${line ? ` (line ${line})` : ""}`;
        node.prepend(title);
        const activate = () => dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId, true);
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

export function markFlowchartErrors(container, sourceMap, diagnosticLines) {
    container.querySelectorAll(".flowchart-node--error")
        .forEach(node => node.classList.remove("flowchart-node--error"));
    const lines = new Set((diagnosticLines ?? []).filter(Number.isInteger));
    for (const item of sourceMap ?? []) {
        const span = item.sourceSpan;
        if (span === null || span === undefined) {
            continue;
        }
        const matches = [...lines].some(line => line >= span.startLine && line <= span.endLine);
        if (matches) {
            findDiagramNode(container, item.mermaidId)?.classList.add("flowchart-node--error");
        }
    }
}

function initializeSourceSynchronization(editor, container, sourceMap, dotNetReference) {
    if (editor === null) {
        return;
    }

    const monacoState = codeEditors.get(editor);
    if (monacoState !== undefined) {
        monacoState.sourceSelectionDisposable?.dispose();
        let lastNodeId = null;
        monacoState.sourceSelectionDisposable = monacoState.editor.onDidChangeCursorSelection(() => {
            if (suppressedSelections.has(editor)) {
                return;
            }
            const position = monacoState.editor.getPosition();
            if (position === null) {
                return;
            }
            const caret = monacoState.model.getOffsetAt(position);
            const item = findSourceMapItem(sourceMap, caret);
            if (item !== undefined && item.nodeId !== lastNodeId) {
                lastNodeId = item.nodeId;
                highlightNode(container, item.mermaidId);
                dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId, false);
            }
        });
        return;
    }

    const textarea = plainEditorFor(editor);
    sourceControllers.get(editor)?.abort();
    const controller = new AbortController();
    sourceControllers.set(editor, controller);
    let lastNodeId = null;
    const synchronize = () => {
        if (suppressedSelections.has(editor)) {
            return;
        }

        const caret = textarea.selectionStart;
        const item = findSourceMapItem(sourceMap, caret);
        if (item !== undefined && item.nodeId !== lastNodeId) {
            lastNodeId = item.nodeId;
            highlightNode(container, item.mermaidId);
            dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId, false);
        }
    };

    for (const eventName of ["click", "keyup", "select"]) {
        textarea.addEventListener(eventName, synchronize, { signal: controller.signal });
    }
}

function findSourceMapItem(sourceMap, caret) {
    return (sourceMap ?? []).find(candidate => {
        const span = candidate.sourceSpan;
        return span !== null && span !== undefined
            && caret >= span.startOffset
            && caret <= span.startOffset + Math.max(span.length, 1);
    });
}

function selectEditorRange(host, startOffset, length, focus = true) {
    const state = codeEditors.get(host);
    if (state === undefined) {
        const textarea = host ? plainEditorFor(host) : null;
        if (focus) {
            textarea?.focus();
        }
        textarea?.setSelectionRange?.(startOffset, startOffset + length);
        return;
    }
    const start = state.model.getPositionAt(startOffset);
    const end = state.model.getPositionAt(startOffset + length);
    const range = {
        startLineNumber: start.lineNumber,
        startColumn: start.column,
        endLineNumber: end.lineNumber,
        endColumn: end.column
    };
    state.editor.setSelection(range);
    state.editor.getAction("editor.unfold")?.run();
    state.editor.revealRangeInCenterIfOutsideViewport(range);
    if (focus) {
        state.editor.focus();
    }
}

export function normalizeFoldRanges(ranges, source) {
    const supplied = (ranges ?? [])
        .map(region => ({
            startLine: Number(region.startLine),
            endLine: Number(region.endLine),
            kind: region.kind ?? "block"
        }))
        .filter(region => Number.isInteger(region.startLine)
            && Number.isInteger(region.endLine)
            && region.startLine > 0
            && region.endLine > region.startLine);
    return supplied.length > 0 ? supplied : braceFoldRanges(source);
}

export function braceFoldRanges(source) {
    const regions = [];
    const stack = [];
    let line = 1;
    let quote = null;
    let escaped = false;
    let lineComment = false;
    let blockComment = false;

    for (let index = 0; index < source.length; index++) {
        const character = source[index];
        const next = source[index + 1];
        if (character === "\n") {
            line++;
            lineComment = false;
            continue;
        }
        if (lineComment) {
            continue;
        }
        if (blockComment) {
            if (character === "*" && next === "/") {
                blockComment = false;
                index++;
            }
            continue;
        }
        if (quote !== null) {
            if (escaped) {
                escaped = false;
            } else if (character === "\\") {
                escaped = true;
            } else if (character === quote) {
                quote = null;
            }
            continue;
        }
        if (character === "/" && next === "/") {
            lineComment = true;
            index++;
        } else if (character === "/" && next === "*") {
            blockComment = true;
            index++;
        } else if (character === "\"" || character === "'") {
            quote = character;
        } else if (character === "{") {
            stack.push(line);
        } else if (character === "}") {
            const startLine = stack.pop();
            if (startLine !== undefined && line > startLine) {
                regions.push({ startLine, endLine: line, kind: "block" });
            }
        }
    }
    return regions.sort((left, right) => left.startLine - right.startLine || right.endLine - left.endLine);
}

const foldMarkerSelector = ".margin-view-overlays .codicon-folding-expanded, .margin-view-overlays .codicon-folding-collapsed";

// Monaco folds on mouse-down in the gutter, so a synthetic click does nothing. Keyboard activation
// instead finds the marker's line and runs Monaco's own toggle-fold action there. Each gutter row is
// positioned at the same `top` Monaco reports for its line, which is steadier than pixel hit-testing.
function foldMarkerLine(marker, editor) {
    const top = parseFloat(marker.parentElement?.style.top ?? "");
    if (!Number.isFinite(top)) {
        return undefined;
    }
    const lineCount = editor.getModel()?.getLineCount() ?? 0;
    for (let lineNumber = 1; lineNumber <= lineCount; lineNumber++) {
        if (Math.abs(editor.getTopForLineNumber(lineNumber) - top) < 1) {
            return lineNumber;
        }
    }
    return undefined;
}

function makeFoldMarkersAccessible(host, editor) {
    for (const marker of host.querySelectorAll(foldMarkerSelector)) {
        const collapsed = marker.classList.contains("codicon-folding-collapsed");
        marker.setAttribute("role", "button");
        marker.setAttribute("tabindex", "0");
        marker.setAttribute("aria-expanded", String(!collapsed));
        marker.setAttribute("aria-label", collapsed ? "Unfold code block" : "Fold code block");
        if (marker.dataset.keyboardFoldBound === "true") {
            continue;
        }
        marker.dataset.keyboardFoldBound = "true";
        marker.addEventListener("keydown", event => {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }
            event.preventDefault();
            const lineNumber = foldMarkerLine(marker, editor);
            if (lineNumber !== undefined) {
                editor.setPosition({ lineNumber, column: 1 });
                editor.getAction("editor.toggleFold")?.run();
            }
        });
    }
}

function initializeCanvasInteractions(container, sourceMap, dotNetReference) {
    canvasControllers.get(container)?.abort();
    const controller = new AbortController();
    canvasControllers.set(container, controller);
    canvasSourceMaps.set(container, sourceMap ?? []);
    container.classList.add("flowchart-canvas--interactive");
    container.dataset.zoom = container.dataset.zoom || "1";

    const reportZoom = zoom => dotNetReference?.invokeMethodAsync("UpdateZoomFromBrowser", zoom);
    const zoomBy = (step, anchor) => {
        const current = Number.parseFloat(container.dataset.zoom || "1");
        container.dataset.autoZoom = "false";
        reportZoom(applyZoom(container, current + step, anchor));
    };

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
    enableNodeDragging(container, controller.signal);
    container.addEventListener("pointercancel", stopPanning, { signal: controller.signal });

    // The wheel scrolls (Shift+wheel sideways) natively; Ctrl+wheel zooms around the cursor.
    container.addEventListener("wheel", event => {
        if (!event.ctrlKey) {
            return;
        }
        event.preventDefault();
        const box = container.getBoundingClientRect();
        zoomBy(event.deltaY < 0 ? 0.1 : -0.1, { x: event.clientX - box.left, y: event.clientY - box.top });
    }, { passive: false, signal: controller.signal });

    container.addEventListener("keydown", event => {
        if (event.ctrlKey || event.altKey || event.metaKey) {
            return;
        }
        const step = 80;
        const actions = {
            ArrowUp: () => container.scrollBy({ top: -step }),
            ArrowDown: () => container.scrollBy({ top: step }),
            ArrowLeft: () => container.scrollBy({ left: -step }),
            ArrowRight: () => container.scrollBy({ left: step }),
            "+": () => zoomBy(0.1),
            "=": () => zoomBy(0.1),
            "-": () => zoomBy(-0.1),
            "_": () => zoomBy(-0.1),
            Home: () => goToFlowchartNode(container, "start"),
            End: () => goToFlowchartNode(container, "end")
        };
        const action = actions[event.key];
        if (action !== undefined) {
            event.preventDefault();
            action();
        }
    }, { signal: controller.signal });

    // Until the learner zooms or fits, keep the opening zoom right for the canvas size.
    if (typeof ResizeObserver === "function") {
        let timer = 0;
        const observer = new ResizeObserver(() => {
            window.clearTimeout(timer);
            timer = window.setTimeout(() => {
                if (container.dataset.autoZoom === "true") {
                    reportZoom(applyZoom(container, openingZoomFor(container)));
                }
            }, 80);
        });
        observer.observe(container);
        controller.signal.addEventListener("abort", () => {
            window.clearTimeout(timer);
            observer.disconnect();
        }, { once: true });
    }
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

async function loadMermaid() {
    if (mermaidPromise === null) {
        mermaidPromise = import(mermaidModuleUrl).then(module => {
            const mermaid = module.default;
            mermaid.initialize({
                startOnLoad: false,
                securityLevel: "strict",
                htmlLabels: false,
                maxTextSize: 10000,
                maxEdges: mermaidFlowchartConfig.maxEdges,
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
                    fontFamily: "Cascadia Code, Consolas, monospace",
                    fontSize: `${mermaidFlowchartConfig.fontSize}px`
                },
                flowchart: {
                    curve: mermaidFlowchartConfig.curve,
                    useMaxWidth: mermaidFlowchartConfig.useMaxWidth,
                    padding: mermaidFlowchartConfig.padding,
                    wrappingWidth: mermaidFlowchartConfig.wrappingWidth,
                    nodeSpacing: mermaidFlowchartConfig.nodeSpacing,
                    rankSpacing: mermaidFlowchartConfig.rankSpacing,
                    diagramPadding: mermaidFlowchartConfig.diagramPadding
                }
            });
            return mermaid;
        });
    }

    return mermaidPromise;
}

async function loadMonaco() {
    if (monacoPromise !== null) {
        return monacoPromise;
    }
    monacoPromise = new Promise((resolve, reject) => {
        const start = () => {
            window.require.config({ paths: { vs: monacoBaseUrl } });
            window.require(["vs/editor/editor.main"], () => resolve(window.monaco), reject);
        };
        if (typeof window.require === "function" && typeof window.require.config === "function") {
            start();
            return;
        }
        const script = document.createElement("script");
        script.src = `${monacoBaseUrl}/loader.js`;
        script.addEventListener("load", start, { once: true });
        script.addEventListener("error", () => reject(new Error("Monaco Editor could not be loaded.")), { once: true });
        document.head.append(script);
    });
    return monacoPromise;
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
