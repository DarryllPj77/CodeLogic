// Format panel: one style model shared by Build Flowchart shapes and control flow graph nodes.
//
// A stored style is *sparse*: it holds only the keys the learner changed. Every value is validated
// against a fixed schema (hex colours, bounded numbers, fixed choices), so a style can safely become
// SVG attributes or CSS without escaping problems. Formatting is view-only: it never touches the C#
// source, the analyzer output, the trace, or node IDs.

// ---------------------------------------------------------------------------------------------
// Style schema (pure)
// ---------------------------------------------------------------------------------------------

/** Font choices are a fixed list, so a style can never inject arbitrary CSS. */
export const fontFamilies = Object.freeze({
    default: { label: "Default", css: null },
    inter: { label: "Inter", css: "Inter, 'Segoe UI', system-ui, sans-serif" },
    arial: { label: "Arial", css: "Arial, Helvetica, sans-serif" },
    verdana: { label: "Verdana", css: "Verdana, Geneva, sans-serif" },
    georgia: { label: "Georgia", css: "Georgia, serif" },
    times: { label: "Times New Roman", css: "'Times New Roman', Times, serif" },
    courier: { label: "Courier New", css: "'Courier New', Courier, monospace" },
    cascadia: { label: "Cascadia Code", css: "'Cascadia Code', Consolas, monospace" }
});

const bool = (fallback, group) => ({ type: "bool", fallback, group });
const color = (fallback, group) => ({ type: "color", fallback, group });
const choice = (values, fallback, group) => ({ type: "enum", values, fallback, group });
const number = (min, max, fallback, group) => ({ type: "number", min, max, fallback, group });

export const styleSchema = Object.freeze({
    // Style tab
    fillEnabled: bool(true, "style"),
    fillMode: choice(["auto", "solid"], "auto", "style"),
    fillColor: color("#13243b", "style"),
    gradient: bool(false, "style"),
    gradientColor: color("#4f8fea", "style"),
    lineEnabled: bool(true, "style"),
    lineColor: color("#4f8fea", "style"),
    lineStyle: choice(["solid", "dashed", "dotted"], "solid", "style"),
    lineWidth: number(0.25, 12, 1, "style"),
    perimeter: number(0, 40, 0, "style"),
    opacity: number(0, 100, 100, "style"),
    shadow: bool(false, "style"),
    rounded: bool(false, "style"),
    // Text tab
    fontFamily: choice(Object.keys(fontFamilies), "default", "text"),
    fontSize: number(6, 72, 14, "text"),
    bold: bool(false, "text"),
    italic: bool(false, "text"),
    underline: bool(false, "text"),
    strike: bool(false, "text"),
    align: choice(["left", "center", "right"], "center", "text"),
    vertical: choice(["top", "middle", "bottom"], "middle", "text"),
    direction: choice(["auto", "ltr", "rtl"], "auto", "text"),
    textBackgroundEnabled: bool(false, "text"),
    textBackground: color("#0a1020", "text"),
    fontColorEnabled: bool(false, "text"),
    fontColor: color("#e6edf7", "text"),
    borderColorEnabled: bool(false, "text"),
    borderColor: color("#94a3b8", "text"),
    textShadow: bool(false, "text"),
    textOpacity: number(0, 100, 100, "text"),
    spacing: number(0, 40, 4, "text"),
    spacingTop: number(0, 40, 0, "text"),
    spacingRight: number(0, 40, 0, "text"),
    spacingBottom: number(0, 40, 0, "text"),
    spacingLeft: number(0, 40, 0, "text")
});

export const textStyleKeys = Object.freeze(Object.keys(styleSchema).filter(key => styleSchema[key].group === "text"));
export const shapeStyleKeys = Object.freeze(Object.keys(styleSchema).filter(key => styleSchema[key].group === "style"));

/** Preset colour swatches, shown a page at a time with a dot pager. */
export const swatchPages = Object.freeze([
    [
        { fillColor: "#13243b", lineColor: "#4f8fea", fontColor: "#e6edf7" },
        { fillColor: "#12352f", lineColor: "#34d399", fontColor: "#d1fae5" },
        { fillColor: "#332a17", lineColor: "#f5b942", fontColor: "#fef3c7" },
        { fillColor: "#3b1d3f", lineColor: "#d946ef", fontColor: "#fae8ff" },
        { fillColor: "#3a1a1a", lineColor: "#f87171", fontColor: "#fee2e2" },
        { fillColor: "#0f2a3a", lineColor: "#38bdf8", fontColor: "#e0f2fe" },
        { fillColor: "#1f2937", lineColor: "#94a3b8", fontColor: "#f1f5f9" },
        { fillColor: "#ffffff", lineColor: "#1f2937", fontColor: "#111827" }
    ],
    [
        { fillColor: "#dbeafe", lineColor: "#2563eb", fontColor: "#1e3a8a" },
        { fillColor: "#dcfce7", lineColor: "#16a34a", fontColor: "#14532d" },
        { fillColor: "#fef9c3", lineColor: "#ca8a04", fontColor: "#713f12" },
        { fillColor: "#fae8ff", lineColor: "#c026d3", fontColor: "#701a75" },
        { fillColor: "#fee2e2", lineColor: "#dc2626", fontColor: "#7f1d1d" },
        { fillColor: "#e0f2fe", lineColor: "#0284c7", fontColor: "#0c4a6e" },
        { fillColor: "#f1f5f9", lineColor: "#475569", fontColor: "#0f172a" },
        { fillColor: "#ffedd5", lineColor: "#ea580c", fontColor: "#7c2d12" }
    ],
    [
        { fillColor: "#2563eb", lineColor: "#1e3a8a", fontColor: "#ffffff" },
        { fillColor: "#16a34a", lineColor: "#14532d", fontColor: "#ffffff" },
        { fillColor: "#ca8a04", lineColor: "#713f12", fontColor: "#ffffff" },
        { fillColor: "#c026d3", lineColor: "#701a75", fontColor: "#ffffff" },
        { fillColor: "#dc2626", lineColor: "#7f1d1d", fontColor: "#ffffff" },
        { fillColor: "#0284c7", lineColor: "#0c4a6e", fontColor: "#ffffff" },
        { fillColor: "#475569", lineColor: "#0f172a", fontColor: "#ffffff" },
        { fillColor: "#000000", lineColor: "#64748b", fontColor: "#ffffff" }
    ]
].map(page => Object.freeze(page.map(Object.freeze))));

/** What a swatch changes: a solid fill, the line colour, and the font colour. */
export function swatchStyle(swatch) {
    return { fillEnabled: true, fillMode: "solid", fillColor: swatch.fillColor, gradient: false, lineEnabled: true, lineColor: swatch.lineColor, fontColorEnabled: true, fontColor: swatch.fontColor };
}

/** Validates one value; returns undefined when it is not allowed. */
export function validateStyleValue(key, value) {
    const rule = styleSchema[key];
    if (rule === undefined) {
        return undefined;
    }
    switch (rule.type) {
        case "bool":
            return typeof value === "boolean" ? value : undefined;
        case "color":
            return typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value) ? value.toLowerCase() : undefined;
        case "enum":
            return rule.values.includes(value) ? value : undefined;
        case "number": {
            const parsed = typeof value === "string" && value.trim() !== "" ? Number(value) : value;
            return typeof parsed === "number" && Number.isFinite(parsed) ? Math.round(Math.min(rule.max, Math.max(rule.min, parsed)) * 100) / 100 : undefined;
        }
        default:
            return undefined;
    }
}

/** Keeps only known keys with valid values (a sparse style). */
export function normalizeStyle(raw) {
    const style = {};
    if (raw === null || typeof raw !== "object" || Array.isArray(raw)) {
        return style;
    }
    for (const [key, value] of Object.entries(raw)) {
        const valid = validateStyleValue(key, value);
        if (valid !== undefined) {
            style[key] = valid;
        }
    }
    return style;
}

/** Applies a patch: valid values are set, null removes a key (back to automatic/default). */
export function mergeStyle(style, patch) {
    const next = { ...normalizeStyle(style) };
    for (const [key, value] of Object.entries(patch ?? {})) {
        if (value === null) {
            delete next[key];
            continue;
        }
        const valid = validateStyleValue(key, value);
        if (valid !== undefined) {
            next[key] = valid;
        }
    }
    return next;
}

export function removeStyleKeys(style, keys) {
    if (keys === null || keys === undefined) {
        return {};
    }
    const next = { ...style };
    for (const key of keys) {
        delete next[key];
    }
    return next;
}

export function pickStyleKeys(style, keys) {
    return Object.fromEntries(Object.entries(style ?? {}).filter(([key]) => keys.includes(key)));
}

/** Every key with a value: the learner's choices over the schema defaults. */
export function effectiveStyle(style) {
    const effective = Object.fromEntries(Object.entries(styleSchema).map(([key, rule]) => [key, rule.fallback]));
    return { ...effective, ...normalizeStyle(style) };
}

export function ptToPx(points) {
    return Math.round(points * (4 / 3) * 100) / 100;
}

export function dashArray(lineStyle, widthPx) {
    switch (lineStyle) {
        case "dashed": return `${round(widthPx * 4)} ${round(widthPx * 3)}`;
        case "dotted": return `${round(widthPx)} ${round(widthPx * 2)}`;
        default: return null;
    }
}

export function textDecoration(style) {
    const parts = [style.underline ? "underline" : null, style.strike ? "line-through" : null].filter(Boolean);
    return parts.length === 0 ? null : parts.join(" ");
}

/** Padding on each side: the global spacing plus that side's extra spacing. */
export function textPadding(style) {
    const s = effectiveStyle(style);
    return { top: s.spacing + s.spacingTop, right: s.spacing + s.spacingRight, bottom: s.spacing + s.spacingBottom, left: s.spacing + s.spacingLeft };
}

/**
 * How far to move a text box inside a shape box for the chosen horizontal alignment, vertical
 * position, and spacing. Boxes are { x, y, width, height } in the same coordinates.
 */
export function alignOffset(shape, text, style) {
    const s = effectiveStyle(style);
    const pad = textPadding(style);
    const dx = s.align === "left" ? shape.x + pad.left - text.x
        : s.align === "right" ? shape.x + shape.width - pad.right - (text.x + text.width)
        : shape.x + shape.width / 2 - (text.x + text.width / 2);
    const dy = s.vertical === "top" ? shape.y + pad.top - text.y
        : s.vertical === "bottom" ? shape.y + shape.height - pad.bottom - (text.y + text.height)
        : shape.y + shape.height / 2 - (text.y + text.height / 2);
    return { dx: round(dx), dy: round(dy) };
}

/** SVG text-anchor for an alignment; right-to-left text swaps start and end. */
export function textAnchor(align, direction) {
    const rtl = direction === "rtl";
    if (align === "left") {
        return rtl ? "end" : "start";
    }
    if (align === "right") {
        return rtl ? "start" : "end";
    }
    return "middle";
}

// ---------------------------------------------------------------------------------------------
// Undo / redo (pure snapshots), shared by the builder and the graph overrides
// ---------------------------------------------------------------------------------------------

export function createHistory(present, limit = 100) {
    return { past: [], present, future: [], limit };
}

export function commit(history, next) {
    if (next === history.present) {
        return history;
    }
    return { ...history, past: [...history.past, history.present].slice(-history.limit), present: next, future: [] };
}

export function undo(history) {
    if (history.past.length === 0) {
        return history;
    }
    return { ...history, past: history.past.slice(0, -1), present: history.past.at(-1), future: [history.present, ...history.future] };
}

export function redo(history) {
    if (history.future.length === 0) {
        return history;
    }
    return { ...history, past: [...history.past, history.present], present: history.future[0], future: history.future.slice(1) };
}

function round(value) {
    return Math.round(value * 100) / 100;
}

// ---------------------------------------------------------------------------------------------
// Format targets and the style clipboard (DOM-free state)
// ---------------------------------------------------------------------------------------------
//
// A target adapter describes what the panel edits:
//   label, selection() → [{ id, type, name, style, box? }], applyStyle(patch), replaceStyle(style),
//   resetStyle(keys | null), undo(), redo(), and optionally setGeometry(patch), align(mode),
//   distribute(axis), order(where), setDefaultStyle(style).

const targets = new Map();
let clipboard = null;

export function registerFormatTarget(name, adapter) {
    targets.set(name, adapter);
    notifyFormatChanged();
}

export function unregisterFormatTarget(name, adapter) {
    if (targets.get(name) === adapter) {
        targets.delete(name);
        notifyFormatChanged();
    }
}

export function formatTarget(name) {
    return targets.get(name) ?? null;
}

/** Selections and styles changed: the panel re-reads its target. */
export function notifyFormatChanged() {
    if (typeof document !== "undefined") {
        document.dispatchEvent(new CustomEvent("codelogic:format-changed"));
    }
}

/** Copy Style (Ctrl+C) keeps the first selected item's style; Copy Text Style keeps only text keys. */
export function copyStyle(target, keys = null) {
    const first = target?.selection()[0];
    if (first === undefined) {
        return false;
    }
    clipboard = keys === null ? { ...first.style } : pickStyleKeys(first.style, keys);
    clipboard.__keys = keys;
    return true;
}

/** Paste Style (Ctrl+V): the copied keys replace those keys on every selected item. */
export function pasteStyle(target) {
    if (clipboard === null || target === null || target.selection().length === 0) {
        return false;
    }
    const { __keys: keys, ...style } = clipboard;
    if (keys === null) {
        target.replaceStyle(style);
    } else {
        const patch = Object.fromEntries(keys.map(key => [key, key in style ? style[key] : null]));
        target.applyStyle(patch);
    }
    return true;
}

export function hasCopiedStyle() {
    return clipboard !== null;
}

// ---------------------------------------------------------------------------------------------
// Format panel (DOM). FormatPanel.razor renders the controls; this binds them to the active target.
// ---------------------------------------------------------------------------------------------

const panels = new WeakMap();

export function mountFormatPanel(panel, workspaceRoot = panel.closest(".ws") ?? document.body) {
    unmountFormatPanel(panel);
    const controller = new AbortController();
    const state = { panel, workspaceRoot, controller, swatchPage: 0, tab: "style" };
    panels.set(panel, state);
    const signal = controller.signal;
    const refresh = () => refreshPanel(state);

    renderSwatches(state);
    document.addEventListener("codelogic:format-changed", refresh, { signal });
    const observer = new MutationObserver(refresh);
    observer.observe(workspaceRoot, { attributes: true, attributeFilter: ["class"] });
    signal.addEventListener("abort", () => observer.disconnect());

    panel.addEventListener("change", event => {
        const input = event.target.closest?.("[data-format-key], [data-arrange-key]");
        if (input === null || input === undefined) {
            return;
        }
        const target = activeTarget(state);
        if (target === null) {
            return;
        }
        if (input.dataset.arrangeKey !== undefined) {
            const value = Number(input.value);
            if (Number.isFinite(value)) {
                target.setGeometry?.({ [input.dataset.arrangeKey]: value });
            }
            return;
        }
        const key = input.dataset.formatKey;
        const value = input.type === "checkbox" ? input.checked : input.value;
        // The colour pickers imply a solid fill and an enabled line or font colour.
        const implied = key === "fillColor" ? { fillEnabled: true, fillMode: "solid" }
            : key === "lineColor" ? { lineEnabled: true }
            : key === "fontColor" ? { fontColorEnabled: true }
            : key === "textBackground" ? { textBackgroundEnabled: true }
            : key === "borderColor" ? { borderColorEnabled: true }
            : key === "gradientColor" ? { gradient: true, fillEnabled: true, fillMode: "solid" }
            : {};
        target.applyStyle({ ...implied, [key]: styleSchema[key]?.type === "number" ? Number(value) : value });
    }, { signal });

    panel.addEventListener("click", event => {
        const button = event.target.closest?.("button");
        if (button === null || button === undefined || !panel.contains(button)) {
            return;
        }
        const target = activeTarget(state);
        if (button.dataset.formatTab !== undefined) {
            selectTab(state, button.dataset.formatTab);
            return;
        }
        if (button.dataset.formatPage !== undefined) {
            state.swatchPage = Number(button.dataset.formatPage);
            renderSwatches(state);
            return;
        }
        if (target === null) {
            return;
        }
        if (button.dataset.formatSwatch !== undefined) {
            const [page, index] = button.dataset.formatSwatch.split(":").map(Number);
            target.applyStyle(swatchStyle(swatchPages[page][index]));
            announce(state, "Applied a colour swatch.");
        } else if (button.dataset.formatToggle !== undefined) {
            const key = button.dataset.formatToggle;
            target.applyStyle({ [key]: button.getAttribute("aria-pressed") !== "true" });
        } else if (button.dataset.formatSet !== undefined) {
            const [key, value] = button.dataset.formatSet.split(":");
            target.applyStyle({ [key]: value });
        } else if (button.dataset.formatStep !== undefined) {
            const [key, step] = button.dataset.formatStep.split(":");
            const current = effectiveStyle(target.selection()[0]?.style)[key];
            target.applyStyle({ [key]: current + Number(step) });
        } else if (button.dataset.formatCommand !== undefined) {
            runCommand(state, target, button.dataset.formatCommand);
        }
    }, { signal });

    panel.addEventListener("keydown", event => {
        // Tabs: Left/Right (and Home/End) move between Style, Text, and Arrange.
        const tab = event.target.closest?.("[data-format-tab]");
        if (tab && ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
            const tabs = [...panel.querySelectorAll("[data-format-tab]")];
            const index = tabs.indexOf(tab);
            const next = event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1
                : (index + (event.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length;
            event.preventDefault();
            selectTab(state, tabs[next].dataset.formatTab);
            tabs[next].focus();
            return;
        }
        if (event.target.closest?.("input, textarea, select")) {
            return;
        }
        if (handleFormatShortcut(event, activeTarget(state))) {
            refresh();
        }
    }, { signal });

    panel.querySelector("[data-format-edit]")?.addEventListener("toggle", event => {
        if (event.target.open) {
            const first = activeTarget(state)?.selection()[0];
            panel.querySelector("[data-format-edit-text]").value = JSON.stringify(first?.style ?? {}, null, 2);
        }
    }, { signal });

    refresh();
}

export function unmountFormatPanel(panel) {
    panels.get(panel)?.controller.abort();
    panels.delete(panel);
}

/**
 * Ctrl+C / Ctrl+V copy and paste a style, Ctrl+Z / Ctrl+Y undo and redo formatting. Returns true
 * when the key was handled. Used by the panel, the builder canvas, and the flowchart canvas.
 */
export function handleFormatShortcut(event, target) {
    if (target === null || !(event.ctrlKey || event.metaKey) || event.altKey) {
        return false;
    }
    const key = event.key.toLowerCase();
    if (key === "c" && !event.shiftKey) {
        if (copyStyle(target)) {
            event.preventDefault();
            notifyFormatChanged();
            return true;
        }
        return false;
    }
    if (key === "v" && !event.shiftKey) {
        event.preventDefault();
        pasteStyle(target);
        return true;
    }
    if (key === "z" && !event.shiftKey) {
        event.preventDefault();
        target.undo();
        return true;
    }
    if (key === "y" || (key === "z" && event.shiftKey)) {
        event.preventDefault();
        target.redo();
        return true;
    }
    return false;
}

function activeTarget(state) {
    return formatTarget(state.workspaceRoot.classList.contains("ws--build") ? "builder" : "cfg");
}

function announce(state, message) {
    const status = state.panel.querySelector("[data-format-status]");
    if (status !== null) {
        status.textContent = message;
    }
}

function selectTab(state, tab) {
    state.tab = tab;
    for (const button of state.panel.querySelectorAll("[data-format-tab]")) {
        const selected = button.dataset.formatTab === tab;
        button.setAttribute("aria-selected", String(selected));
        button.tabIndex = selected ? 0 : -1;
    }
    for (const pane of state.panel.querySelectorAll("[data-format-pane]")) {
        pane.hidden = pane.dataset.formatPane !== tab;
    }
}

function renderSwatches(state) {
    const grid = state.panel.querySelector("[data-format-swatches]");
    const pager = state.panel.querySelector("[data-format-pager]");
    grid?.replaceChildren(...swatchPages[state.swatchPage].map((swatch, index) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "format-swatch";
        button.dataset.formatSwatch = `${state.swatchPage}:${index}`;
        button.style.background = swatch.fillColor;
        button.style.borderColor = swatch.lineColor;
        button.style.color = swatch.fontColor;
        button.textContent = "Aa";
        button.setAttribute("aria-label", `Swatch ${index + 1}: fill ${swatch.fillColor}, line ${swatch.lineColor}, text ${swatch.fontColor}`);
        return button;
    }));
    pager?.replaceChildren(...swatchPages.map((_, page) => {
        const dot = document.createElement("button");
        dot.type = "button";
        dot.className = "format-pager__dot";
        dot.dataset.formatPage = String(page);
        dot.setAttribute("aria-label", `Swatch page ${page + 1} of ${swatchPages.length}`);
        dot.setAttribute("aria-current", String(page === state.swatchPage));
        return dot;
    }));
}

function runCommand(state, target, command) {
    switch (command) {
        case "copy-style":
            announce(state, copyStyle(target) ? "Style copied. Select items and paste it." : "Select an item to copy its style.");
            break;
        case "copy-text-style":
            announce(state, copyStyle(target, textStyleKeys) ? "Text style copied." : "Select an item to copy its text style.");
            break;
        case "paste-style":
            announce(state, pasteStyle(target) ? "Style pasted." : "Copy a style first, then select items to paste it.");
            break;
        case "clear-formatting":
            target.resetStyle(textStyleKeys);
            announce(state, "Text formatting cleared.");
            break;
        case "reset-style":
            target.resetStyle(null);
            announce(state, "Default style restored.");
            break;
        case "default-style": {
            const first = target.selection()[0];
            if (first !== undefined && target.setDefaultStyle !== undefined) {
                target.setDefaultStyle(first.style);
                announce(state, "New shapes will use this style.");
            }
            break;
        }
        case "apply-edit": {
            let parsed;
            try {
                parsed = JSON.parse(state.panel.querySelector("[data-format-edit-text]").value);
            } catch {
                announce(state, "The style is not valid JSON, so nothing changed.");
                break;
            }
            target.replaceStyle(normalizeStyle(parsed));
            announce(state, "Style applied. Unknown or invalid values were ignored.");
            break;
        }
        case "undo": target.undo(); break;
        case "redo": target.redo(); break;
        default:
            if (command.startsWith("align-")) {
                target.align?.(command.slice(6));
            } else if (command.startsWith("distribute-")) {
                target.distribute?.(command.slice(11));
            } else if (command === "front" || command === "back") {
                target.order?.(command);
            }
    }
    refreshPanel(state);
}

function refreshPanel(state) {
    const { panel } = state;
    const target = activeTarget(state);
    const items = target?.selection() ?? [];
    const first = items[0];
    const style = effectiveStyle(first?.style);

    const summary = panel.querySelector("[data-format-summary]");
    if (summary !== null) {
        summary.textContent = target === null ? "Analyze code or open Build Flowchart to format items."
            : items.length === 0 ? `${target.label}: select an item to format it.${target.selectHint ? ` ${target.selectHint}` : ""}`
            : items.length === 1 ? `${target.label}: ${first.name}`
            : `${target.label}: ${items.length} items selected (changes apply to all).`;
    }

    const none = items.length === 0;
    for (const control of panel.querySelectorAll("[data-format-key], [data-format-toggle], [data-format-set], [data-format-step], [data-format-swatch], [data-format-command]")) {
        const command = control.dataset.formatCommand;
        const alwaysAvailable = command === "undo" || command === "redo";
        control.disabled = target === null || (none && !alwaysAvailable);
    }

    for (const input of panel.querySelectorAll("[data-format-key]")) {
        const key = input.dataset.formatKey;
        if (input.type === "checkbox") {
            input.checked = Boolean(style[key]);
        } else if (document.activeElement !== input) {
            input.value = String(style[key]);
        }
        // Mixed selections show the first item's value; the field is marked so it can be read aloud.
        input.toggleAttribute("data-mixed", items.some(item => effectiveStyle(item.style)[key] !== style[key]));
    }
    for (const button of panel.querySelectorAll("[data-format-toggle]")) {
        button.setAttribute("aria-pressed", String(Boolean(style[button.dataset.formatToggle])));
    }
    for (const button of panel.querySelectorAll("[data-format-set]")) {
        const [key, value] = button.dataset.formatSet.split(":");
        button.setAttribute("aria-pressed", String(style[key] === value));
    }
    const fillColor = panel.querySelector('[data-format-key="fillColor"]');
    if (fillColor !== null && !none) {
        fillColor.disabled = !style.fillEnabled || style.fillMode !== "solid";
    }
    const gradientColor = panel.querySelector('[data-format-key="gradientColor"]');
    if (gradientColor !== null && !none) {
        gradientColor.disabled = !style.gradient;
    }

    // Arrange applies to builder shapes only; graph nodes are placed by the layout (drag them instead).
    const shapes = items.filter(item => item.box !== undefined);
    const canArrange = target?.setGeometry !== undefined && shapes.length > 0;
    for (const control of panel.querySelectorAll("[data-arrange-key], [data-format-pane='arrange'] button")) {
        control.disabled = !canArrange;
    }
    for (const button of panel.querySelectorAll("[data-format-command^='align-']")) {
        button.disabled = !canArrange || shapes.length < 2;
    }
    for (const button of panel.querySelectorAll("[data-format-command^='distribute-']")) {
        button.disabled = !canArrange || shapes.length < 3;
    }
    for (const input of panel.querySelectorAll("[data-arrange-key]")) {
        if (canArrange && document.activeElement !== input) {
            input.value = String(Math.round(shapes[0].box[input.dataset.arrangeKey] ?? 0));
        } else if (!canArrange) {
            input.value = "";
        }
    }
    const arrangeNote = panel.querySelector("[data-format-arrange-note]");
    if (arrangeNote !== null) {
        arrangeNote.hidden = canArrange;
        arrangeNote.textContent = target?.setGeometry === undefined
            ? "Arrange applies to Build Flowchart shapes. Graph nodes keep their computed layout; drag a node to move it."
            : "Select one or more shapes to arrange them.";
    }

    // Target-specific commands.
    const defaultButton = panel.querySelector('[data-format-command="default-style"]');
    if (defaultButton !== null) {
        defaultButton.hidden = target?.setDefaultStyle === undefined;
    }
    const resetButton = panel.querySelector('[data-format-command="reset-style"]');
    if (resetButton !== null) {
        resetButton.textContent = target?.resetLabel ?? "Reset style";
    }
    const paste = panel.querySelector('[data-format-command="paste-style"]');
    if (paste !== null) {
        paste.disabled = paste.disabled || !hasCopiedStyle();
    }
    const perimeter = panel.querySelector('[data-format-key="perimeter"]');
    if (perimeter !== null && target?.supportsPerimeter === false) {
        perimeter.disabled = true;
    }
    panel.toggleAttribute("data-has-selection", !none);
}
