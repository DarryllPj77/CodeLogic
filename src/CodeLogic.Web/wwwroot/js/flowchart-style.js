// View-only formatting for the control flow graph.
//
// Overrides are kept in the page's in-memory state, keyed by graph node ID ("node:n002") or Mermaid
// edge ID ("edge:L_n002_n003_0"). They become one scoped <style> element (plus SVG gradients) after
// each render. They never change the C# source, the analyzer output, the trace, node IDs, or the
// Mermaid definition, and they reset when the page reloads. Values come from format.js's validated
// schema (hex colours, bounded numbers, fixed font list), so the generated CSS cannot be injected.

import {
    alignOffset,
    commit,
    createHistory,
    dashArray,
    effectiveStyle,
    fontFamilies,
    mergeStyle,
    normalizeStyle,
    notifyFormatChanged,
    ptToPx,
    registerFormatTarget,
    removeStyleKeys,
    redo,
    textDecoration,
    undo
} from "./format.js?v=1";

// ---------------------------------------------------------------------------------------------
// Pure part
// ---------------------------------------------------------------------------------------------

/** Parses an override key; returns null for anything that is not a node or edge ID. */
export function parseOverrideKey(key) {
    const node = /^node:(n\d{1,6})$/.exec(key ?? "");
    if (node !== null) {
        return { type: "node", id: node[1] };
    }
    const edge = /^edge:(L_[A-Za-z0-9_]{1,80})$/.exec(key ?? "");
    return edge === null ? null : { type: "edge", id: edge[1] };
}

/** Sets (merges) a style patch on several keys; returns a new overrides object. */
export function applyOverridePatch(overrides, keys, patch) {
    const next = { ...overrides };
    for (const key of keys) {
        if (parseOverrideKey(key) === null) {
            continue;
        }
        const style = mergeStyle(next[key] ?? {}, patch);
        if (Object.keys(style).length === 0) {
            delete next[key];
        } else {
            next[key] = style;
        }
    }
    return next;
}

export function replaceOverride(overrides, keys, style) {
    return applyOverridePatch(Object.fromEntries(Object.entries(overrides).filter(([key]) => !keys.includes(key))), keys, normalizeStyle(style));
}

/** Reset node style: removes the given keys' overrides (or only some style keys). */
export function resetOverrides(overrides, keys, styleKeys = null) {
    const next = { ...overrides };
    for (const key of keys) {
        if (next[key] === undefined) {
            continue;
        }
        const style = removeStyleKeys(next[key], styleKeys);
        if (Object.keys(style).length === 0) {
            delete next[key];
        } else {
            next[key] = style;
        }
    }
    return next;
}

const nodeSelector = id => `.flowchart-canvas .node[id*="-flowchart-${id}-"]`;
const edgeSelector = id => `.flowchart-canvas path.flowchart-link[data-id="${id}"]`;
const edgeLabelSelector = id => `.flowchart-canvas .edgeLabel .label[data-id="${id}"]`;

function declarations(entries) {
    const body = entries.filter(([, value]) => value !== null && value !== undefined).map(([name, value]) => `${name}: ${value} !important;`).join(" ");
    return body;
}

function rule(selector, entries) {
    const body = declarations(entries);
    return body === "" ? "" : `${selector} { ${body} }`;
}

function gradientId(key) {
    return `codelogic-format-gradient-${key.replace(/[^A-Za-z0-9_-]/g, "-")}`;
}

/** The shape declarations that only exist when the learner set them ("automatic" keeps Mermaid's). */
function shapeEntries(key, sparse) {
    const s = effectiveStyle(sparse);
    const fill = "fillEnabled" in sparse && !s.fillEnabled ? "none"
        : s.fillMode === "solid" ? (s.gradient ? `url(#${gradientId(key)})` : s.fillColor)
        : null;
    return {
        always: [
            ["fill", fill],
            ["fill-opacity", "fillEnabled" in sparse && !s.fillEnabled ? "0" : null],
            ["opacity", "opacity" in sparse ? String(s.opacity / 100) : null],
            ["filter", s.shadow ? "drop-shadow(0 3px 4px rgb(0 0 0 / 0.55))" : null],
            ["rx", s.rounded ? "10px" : null],
            ["ry", s.rounded ? "10px" : null]
        ],
        // Line changes step aside while a node is selected or is the current trace step, so those
        // highlights stay visible.
        line: [
            ["stroke", "lineEnabled" in sparse && !s.lineEnabled ? "none" : "lineColor" in sparse ? s.lineColor : null],
            ["stroke-width", "lineWidth" in sparse ? `${ptToPx(s.lineWidth)}px` : null],
            ["stroke-dasharray", "lineStyle" in sparse ? (dashArray(s.lineStyle, ptToPx(s.lineWidth)) ?? "none") : null]
        ]
    };
}

function textEntries(sparse) {
    const s = effectiveStyle(sparse);
    return [
        ["font-family", "fontFamily" in sparse ? fontFamilies[s.fontFamily].css : null],
        ["font-size", "fontSize" in sparse ? `${s.fontSize}px` : null],
        ["font-weight", "bold" in sparse ? (s.bold ? "700" : "400") : null],
        ["font-style", "italic" in sparse ? (s.italic ? "italic" : "normal") : null],
        ["text-decoration", "underline" in sparse || "strike" in sparse ? (textDecoration(s) ?? "none") : null],
        ["fill", s.fontColorEnabled ? s.fontColor : null],
        ["direction", "direction" in sparse && s.direction !== "auto" ? s.direction : null],
        ["unicode-bidi", "direction" in sparse && s.direction !== "auto" ? "plaintext" : null]
    ];
}

function textBlockEntries(sparse) {
    const s = effectiveStyle(sparse);
    return [
        ["opacity", "textOpacity" in sparse ? String(s.textOpacity / 100) : null],
        ["filter", s.textShadow ? "drop-shadow(0 1px 2px rgb(0 0 0 / 0.8))" : null]
    ];
}

function backgroundEntries(sparse, box) {
    const s = effectiveStyle(sparse);
    if (!s.textBackgroundEnabled && !s.borderColorEnabled) {
        return [];
    }
    const geometry = box === undefined ? [] : [["x", `${box.x - 3}px`], ["y", `${box.y - 2}px`], ["width", `${box.width + 6}px`], ["height", `${box.height + 4}px`]];
    return [
        ["fill", s.textBackgroundEnabled ? s.textBackground : "none"],
        ["stroke", s.borderColorEnabled ? s.borderColor : "none"],
        ["stroke-width", s.borderColorEnabled ? "1px" : null],
        ["opacity", "1"],
        ...geometry
    ];
}

/**
 * Builds the CSS for every override. `measurements` (from the DOM) supplies, per key, the label's
 * text box (for background and border) and its aligned position (for alignment and spacing).
 */
export function overrideCss(overrides, measurements = new Map()) {
    const rules = [];
    for (const [key, raw] of Object.entries(overrides)) {
        const parsed = parseOverrideKey(key);
        if (parsed === null) {
            continue;
        }
        const style = normalizeStyle(raw);
        const measured = measurements.get(key) ?? {};
        const { always, line } = shapeEntries(key, style);
        if (parsed.type === "node") {
            const node = nodeSelector(parsed.id);
            const guarded = `${node}:not(.flowchart-node--selected):not(.flowchart-node--trace-current)`;
            const shapes = suffix => [`${suffix} > .label-container`, `${suffix} > .label-container > path`].join(", ");
            rules.push(rule(shapes(node), always));
            rules.push(rule(shapes(guarded), line));
            rules.push(rule(`${node} > g.label text, ${node} > g.label tspan`, textEntries(style)));
            rules.push(rule(`${node} > g.label text`, textBlockEntries(style)));
            rules.push(rule(`${node} > g.label rect.background`, backgroundEntries(style, measured.textBox)));
            if (measured.labelTransform !== undefined) {
                rules.push(rule(`${node} > g.label`, [["transform", measured.labelTransform]]));
            }
        } else {
            const edge = edgeSelector(parsed.id);
            rules.push(rule(edge, always.filter(([name]) => name === "opacity" || name === "filter")));
            rules.push(rule(`${edge}:not(.flowchart-edge--trace-taken)`, line));
            const label = edgeLabelSelector(parsed.id);
            rules.push(rule(`${label} text, ${label} tspan`, textEntries(style)));
            rules.push(rule(`${label} text`, textBlockEntries(style)));
            rules.push(rule(`${label} rect.background`, backgroundEntries(style, measured.textBox)));
        }
    }
    return rules.filter(Boolean).join("\n");
}

// ---------------------------------------------------------------------------------------------
// DOM part
// ---------------------------------------------------------------------------------------------

const states = new WeakMap();
const svgNamespace = "http://www.w3.org/2000/svg";

/**
 * Lets the learner select graph nodes and edges for formatting and registers the "cfg" target.
 * Click selects one item; Shift+click adds or removes one; a click on a connector or its label
 * selects that connector. Ctrl+C/V/Z/Y on the canvas copy, paste, undo, and redo formatting.
 */
export function enableFlowchartFormatting(container, signal, handleShortcut) {
    const state = states.get(container) ?? { history: createHistory({}), selection: [] };
    states.set(container, state);

    container.addEventListener("click", event => {
        const key = keyFromTarget(event.target);
        if (key === null) {
            return;
        }
        if (event.shiftKey) {
            // Shift+click only changes the format selection; it does not jump the editor.
            event.stopPropagation();
            event.preventDefault();
            state.selection = state.selection.includes(key) ? state.selection.filter(other => other !== key) : [...state.selection, key];
        } else {
            state.selection = [key];
        }
        markSelection(container, state);
        notifyFormatChanged();
    }, { capture: true, signal });

    container.addEventListener("focusin", event => {
        const key = keyFromTarget(event.target);
        if (key !== null && !state.selection.includes(key)) {
            state.selection = [key];
            markSelection(container, state);
            notifyFormatChanged();
        }
    }, { signal });

    container.addEventListener("keydown", event => {
        if (handleShortcut(event, adapterFor(container))) {
            event.stopPropagation();
        }
    }, { signal });

    registerFormatTarget("cfg", adapterFor(container));
}

/** Re-applies every override after the graph is drawn again (Play, zoom, FULL, new analysis). */
export function applyFlowchartStyles(container) {
    const state = states.get(container);
    if (state === undefined) {
        return;
    }
    const svg = container.querySelector(".flowchart-svg");
    container.querySelector("style[data-format-overrides]")?.remove();
    svg?.querySelector("defs[data-format-gradients]")?.remove();
    // Selected items that no longer exist (the graph changed) drop out of the selection.
    state.selection = state.selection.filter(key => elementFor(container, key) !== null);
    markSelection(container, state);
    const overrides = state.history.present;
    if (svg === null || Object.keys(overrides).length === 0) {
        return;
    }

    // Pass 1 applies fonts without geometry, so pass 2 measures the text at its new size.
    const sheet = document.createElement("style");
    sheet.setAttribute("data-format-overrides", "");
    sheet.textContent = overrideCss(overrides);
    container.prepend(sheet);

    const measurements = new Map();
    const gradients = svg.ownerDocument.createElementNS(svgNamespace, "defs");
    gradients.setAttribute("data-format-gradients", "");
    for (const [key, style] of Object.entries(overrides)) {
        const element = elementFor(container, key);
        if (element === null) {
            continue;
        }
        measurements.set(key, measure(element, key, style));
        const effective = effectiveStyle(style);
        if (effective.gradient && effective.fillMode === "solid" && effective.fillEnabled) {
            gradients.append(gradient(svg.ownerDocument, key, effective.fillColor, effective.gradientColor));
        }
    }
    if (gradients.childElementCount > 0) {
        svg.prepend(gradients);
    }
    sheet.textContent = overrideCss(overrides, measurements);
}

/** For tests in the browser: the current overrides. */
export function flowchartStyleOverrides(container) {
    return structuredClone(states.get(container)?.history.present ?? {});
}

function adapterFor(container) {
    const state = states.get(container);
    const change = next => {
        state.history = commit(state.history, next);
        applyFlowchartStyles(container);
        notifyFormatChanged();
    };
    return {
        label: "Control flow graph",
        selectHint: "Click a node or connector; Shift+click adds more.",
        resetLabel: "Reset node style",
        supportsPerimeter: false,
        selection: () => state.selection.map(key => {
            const parsed = parseOverrideKey(key);
            const element = elementFor(container, key);
            const name = parsed.type === "node"
                ? (element?.getAttribute("aria-label") ?? parsed.id)
                : `Connector ${parsed.id.replace(/^L_/, "").replace(/_\d+$/, "").replace("_", " → ")}`;
            return { id: key, type: parsed.type, name, style: { ...(state.history.present[key] ?? {}) } };
        }),
        applyStyle: patch => change(applyOverridePatch(state.history.present, state.selection, patch)),
        replaceStyle: style => change(replaceOverride(state.history.present, state.selection, style)),
        resetStyle: keys => change(resetOverrides(state.history.present, state.selection, keys)),
        undo: () => {
            state.history = undo(state.history);
            applyFlowchartStyles(container);
            notifyFormatChanged();
        },
        redo: () => {
            state.history = redo(state.history);
            applyFlowchartStyles(container);
            notifyFormatChanged();
        }
    };
}

function keyFromTarget(target) {
    const node = target?.closest?.(".node");
    const nodeId = /-flowchart-(n\d+)-/.exec(node?.id ?? "")?.[1];
    if (nodeId !== undefined) {
        return `node:${nodeId}`;
    }
    const edge = target?.closest?.("path.flowchart-link") ?? target?.closest?.(".edgeLabel")?.querySelector(".label[data-id]");
    const edgeId = edge?.getAttribute("data-id");
    return edgeId && parseOverrideKey(`edge:${edgeId}`) !== null ? `edge:${edgeId}` : null;
}

function elementFor(container, key) {
    const parsed = parseOverrideKey(key);
    if (parsed === null) {
        return null;
    }
    return parsed.type === "node"
        ? container.querySelector(`.node[id*="${CSS.escape(`-flowchart-${parsed.id}-`)}"]`)
        : container.querySelector(`path.flowchart-link[data-id="${CSS.escape(parsed.id)}"]`);
}

function markSelection(container, state) {
    container.querySelectorAll(".flowchart-format-selected").forEach(element => element.classList.remove("flowchart-format-selected"));
    for (const key of state.selection) {
        elementFor(container, key)?.classList.add("flowchart-format-selected");
    }
}

// Measures the label so background, border, alignment, and spacing can be drawn in CSS.
function measure(element, key, style) {
    const parsed = parseOverrideKey(key);
    const label = parsed.type === "node"
        ? element.querySelector(":scope > g.label")
        : element.closest("svg")?.querySelector(`.edgeLabel .label[data-id="${CSS.escape(parsed.id)}"]`);
    const text = label?.querySelector("text");
    if (label === null || label === undefined || text === null || text === undefined) {
        return {};
    }
    const textBox = text.getBBox();
    const result = { textBox: { x: round(textBox.x), y: round(textBox.y), width: round(textBox.width), height: round(textBox.height) } };
    const effective = effectiveStyle(style);
    const moves = parsed.type === "node" && ("align" in style || "vertical" in style || ["spacing", "spacingTop", "spacingRight", "spacingBottom", "spacingLeft"].some(name => name in style));
    const shape = element.querySelector(":scope > .label-container");
    if (moves && shape !== null) {
        const original = /translate\(\s*([-\d.e]+)[ ,]+([-\d.e]+)\s*\)/.exec(label.getAttribute("transform") ?? "");
        const baseX = original === null ? 0 : Number(original[1]);
        const baseY = original === null ? 0 : Number(original[2]);
        const shapeBox = boxInNode(element, shape);
        // The text box in node coordinates (the label is translated by its original transform).
        const inNode = { x: textBox.x + baseX, y: textBox.y + baseY, width: textBox.width, height: textBox.height };
        // Diamonds and other non-rectangles have less room near their edges; keep text inside half.
        const usable = shape.tagName === "polygon"
            ? { x: shapeBox.x + shapeBox.width / 4, y: shapeBox.y + shapeBox.height / 4, width: shapeBox.width / 2, height: shapeBox.height / 2 }
            : shapeBox;
        const { dx, dy } = alignOffset(usable, inNode, effective);
        result.labelTransform = `translate(${round(baseX + dx)}px, ${round(baseY + dy)}px)`;
    }
    return result;
}

function boxInNode(node, shape) {
    const box = shape.getBBox();
    const matrix = node.getCTM().inverse().multiply(shape.getCTM());
    const svg = node.ownerSVGElement;
    const corner = (x, y) => {
        const point = svg.createSVGPoint();
        point.x = x;
        point.y = y;
        return point.matrixTransform(matrix);
    };
    const a = corner(box.x, box.y);
    const b = corner(box.x + box.width, box.y + box.height);
    return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), width: Math.abs(b.x - a.x), height: Math.abs(b.y - a.y) };
}

function gradient(document, key, from, to) {
    const element = document.createElementNS(svgNamespace, "linearGradient");
    element.id = gradientId(key);
    element.setAttribute("x1", "0");
    element.setAttribute("y1", "0");
    element.setAttribute("x2", "0");
    element.setAttribute("y2", "1");
    for (const [offset, color] of [["0", from], ["1", to]]) {
        const stop = document.createElementNS(svgNamespace, "stop");
        stop.setAttribute("offset", offset);
        stop.setAttribute("stop-color", color);
        element.append(stop);
    }
    return element;
}

function round(value) {
    return Math.round(value * 100) / 100;
}
