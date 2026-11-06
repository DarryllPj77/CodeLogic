const shortcutHandlers = new WeakMap();
const editorTabModes = new WeakMap();
const codeEditors = new WeakMap();
const plainEditors = new WeakMap();
const sourceControllers = new WeakMap();
const canvasControllers = new WeakMap();
const suppressedSelections = new WeakSet();
const nodeTypeNames = Object.freeze({ 1: "Start", 2: "End", 3: "Action", 4: "Decision", 5: "Input", 6: "Output" });
const mermaidModuleUrl = "https://cdn.jsdelivr.net/npm/mermaid@12.2.0/dist/mermaid.esm.min.mjs";
const monacoBaseUrl = "https://cdn.jsdelivr.net/npm/monaco-editor@0.57.0/min/vs";
let mermaidPromise = null;
let monacoPromise = null;

// Returns "monaco" when the rich editor loaded, or "plain" when the accessible textarea fallback is used.
export async function initializeCodeEditor(host, initialValue, maximumCharacters, dotNetReference, foldRanges) {
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
    state.model.setValue(value);
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
    initializeCanvasInteractions(container, dotNetReference);
    fitFlowchart(container, false);
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

export function fitFlowchart(container, smooth = true) {
    const svg = container.querySelector(".flowchart-svg");
    if (svg === null) {
        return;
    }

    svg.style.minWidth = "0";
    svg.style.transform = "none";
    svg.style.width = "100%";
    container.dataset.zoom = "1";
    container.scrollTo({ top: 0, left: 0, behavior: smooth ? "smooth" : "auto" });
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

    if (current !== null) {
        centerNode(container, current);
    }
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
                dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId);
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
            dotNetReference?.invokeMethodAsync("SelectNodeFromBrowser", item.nodeId);
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
                    useMaxWidth: true,
                    wrappingWidth: 400,
                    minNodeWidth: 220,
                    nodeSpacing: 64,
                    rankSpacing: 64
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
