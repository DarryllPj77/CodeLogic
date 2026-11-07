import assert from "node:assert/strict";
import test from "node:test";

import {
    alignOffset,
    commit,
    copyStyle,
    createHistory,
    effectiveStyle,
    handleFormatShortcut,
    mergeStyle,
    normalizeStyle,
    pasteStyle,
    redo,
    styleSchema,
    swatchPages,
    swatchStyle,
    textAnchor,
    textStyleKeys,
    undo
} from "../../src/CodeLogic.Web/wwwroot/js/format.js";
import {
    applyOverridePatch,
    overrideCss,
    parseOverrideKey,
    replaceOverride,
    resetOverrides
} from "../../src/CodeLogic.Web/wwwroot/js/flowchart-style.js";

test("styles keep only known keys with valid values, so they cannot inject CSS", () => {
    const style = normalizeStyle({
        fillColor: "#ABCDEF",
        lineColor: "red; background: url(https://example.com)",
        fontColor: "#fff",
        fontFamily: "Comic Sans",
        fontSize: 500,
        opacity: "40",
        bold: "yes",
        onclick: "alert(1)"
    });
    assert.deepEqual(style, { fillColor: "#abcdef", fontSize: 72, opacity: 40 });
});

test("a patch sets valid values and null puts a key back to automatic", () => {
    const style = mergeStyle({ fillMode: "solid", fillColor: "#112233" }, { fillColor: null, fontSize: 18, lineStyle: "wavy" });
    assert.deepEqual(style, { fillMode: "solid", fontSize: 18 });
    assert.equal(effectiveStyle(style).fillColor, styleSchema.fillColor.fallback);
});

test("the Style and Text tabs cover the requested properties", () => {
    for (const key of ["fillEnabled", "fillMode", "fillColor", "gradient", "lineEnabled", "lineColor", "lineStyle", "lineWidth", "perimeter", "opacity", "shadow"]) {
        assert.equal(styleSchema[key].group, "style", key);
    }
    for (const key of ["fontFamily", "fontSize", "bold", "italic", "underline", "strike", "align", "vertical", "direction", "textBackground", "fontColor", "borderColor", "textShadow", "textOpacity", "spacing"]) {
        assert.ok(textStyleKeys.includes(key), key);
    }
    assert.equal(swatchPages.length, 3);
    assert.deepEqual(normalizeStyle(swatchStyle(swatchPages[0][1])), swatchStyle(swatchPages[0][1]));
});

test("text alignment and spacing move the text inside its shape", () => {
    const shape = { x: 0, y: 0, width: 200, height: 100 };
    const text = { x: 80, y: 40, width: 40, height: 20 };
    assert.deepEqual(alignOffset(shape, text, {}), { dx: 0, dy: 0 });
    assert.deepEqual(alignOffset(shape, text, { align: "left", vertical: "top" }), { dx: -76, dy: -36 });
    assert.deepEqual(alignOffset(shape, text, { align: "right", vertical: "bottom", spacing: 10 }), { dx: 70, dy: 30 });
    assert.equal(textAnchor("left", "ltr"), "start");
    assert.equal(textAnchor("left", "rtl"), "end");
});

test("Ctrl+C and Ctrl+V copy one item's style and paste it onto the selection", () => {
    let applied = null;
    const target = {
        selection: () => [{ id: "a", style: { fillColor: "#123456", fontSize: 20 } }],
        replaceStyle: style => { applied = { replace: style }; },
        applyStyle: patch => { applied = { patch }; },
        undo() {},
        redo() {}
    };
    const key = (letter, extra = {}) => ({ key: letter, ctrlKey: true, metaKey: false, altKey: false, shiftKey: false, preventDefault() {}, ...extra });
    assert.equal(handleFormatShortcut(key("c"), target), true);
    assert.equal(handleFormatShortcut(key("v"), target), true);
    assert.deepEqual(applied, { replace: { fillColor: "#123456", fontSize: 20 } });

    // Copy Text Style pastes only the text keys, clearing text keys the source did not set.
    copyStyle(target, textStyleKeys);
    pasteStyle(target);
    assert.equal(applied.patch.fontSize, 20);
    assert.equal(applied.patch.bold, null);
    assert.equal("fillColor" in applied.patch, false);
});

test("Ctrl+Z and Ctrl+Y reach the target's undo and redo", () => {
    const calls = [];
    const target = { selection: () => [], undo: () => calls.push("undo"), redo: () => calls.push("redo") };
    const key = (letter, shiftKey = false) => ({ key: letter, ctrlKey: true, shiftKey, altKey: false, preventDefault() {} });
    handleFormatShortcut(key("z"), target);
    handleFormatShortcut(key("y"), target);
    handleFormatShortcut(key("Z", true), target);
    assert.deepEqual(calls, ["undo", "redo", "redo"]);
    assert.equal(handleFormatShortcut({ key: "z", ctrlKey: false, preventDefault() {} }, target), false);
});

test("graph overrides are keyed by node or edge ID only", () => {
    assert.deepEqual(parseOverrideKey("node:n002"), { type: "node", id: "n002" });
    assert.deepEqual(parseOverrideKey("edge:L_n002_n003_0"), { type: "edge", id: "L_n002_n003_0" });
    assert.equal(parseOverrideKey("node:n002\"] { } body { display:none"), null);
    assert.equal(parseOverrideKey("source:Program.cs"), null);
});

test("formatting a graph node changes only view styles, never IDs or source", () => {
    const overrides = applyOverridePatch({}, ["node:n002", "edge:L_n002_n003_0", "bad key"], { fillMode: "solid", fillColor: "#dbeafe", fontSize: 18 });
    assert.deepEqual(Object.keys(overrides), ["node:n002", "edge:L_n002_n003_0"]);
    assert.deepEqual(overrides["node:n002"], { fillMode: "solid", fillColor: "#dbeafe", fontSize: 18 });

    const css = overrideCss(overrides);
    assert.match(css, /\.node\[id\*="-flowchart-n002-"\] > \.label-container[^{]*\{ fill: #dbeafe !important; \}/);
    assert.match(css, /\.node\[id\*="-flowchart-n002-"\] > g\.label text, [^{]*\{ font-size: 18px !important; \}/);
    assert.match(css, /\.edgeLabel \.label\[data-id="L_n002_n003_0"\] text/);
    assert.match(overrideCss({ "edge:L_n002_n003_0": { lineColor: "#16a34a", lineStyle: "dashed" } }),
        /path\.flowchart-link\[data-id="L_n002_n003_0"\]:not\(\.flowchart-edge--trace-taken\) \{ stroke: #16a34a !important; stroke-dasharray: [\d.]+ [\d.]+ !important; \}/);
    // Only view properties appear: no content, no label text, no IDs other than the selectors.
    assert.doesNotMatch(css, /content:|age >= 18|Console/);
});

test("line overrides step aside while a node is selected or is the trace step", () => {
    const css = overrideCss({ "node:n001": { lineColor: "#ff0000" } });
    assert.match(css, /:not\(\.flowchart-node--selected\):not\(\.flowchart-node--trace-current\) > \.label-container[^{]*\{ stroke: #ff0000 !important; \}/);
});

test("Reset node style removes overrides; Clear Formatting removes only text keys", () => {
    const start = { "node:n001": { fillMode: "solid", fillColor: "#112233", bold: true }, "node:n002": { fontSize: 20 } };
    assert.deepEqual(resetOverrides(start, ["node:n001"]), { "node:n002": { fontSize: 20 } });
    assert.deepEqual(resetOverrides(start, ["node:n001", "node:n002"], textStyleKeys), { "node:n001": { fillMode: "solid", fillColor: "#112233" } });
    assert.deepEqual(replaceOverride(start, ["node:n002"], { italic: true }), { "node:n001": start["node:n001"], "node:n002": { italic: true } });
});

test("undo and redo restore the previous graph formatting", () => {
    let history = createHistory({});
    history = commit(history, applyOverridePatch(history.present, ["node:n001"], { fillMode: "solid", fillColor: "#112233" }));
    history = commit(history, applyOverridePatch(history.present, ["node:n001"], { fillColor: "#445566" }));
    history = undo(history);
    assert.equal(history.present["node:n001"].fillColor, "#112233");
    history = undo(history);
    assert.deepEqual(history.present, {});
    history = redo(history);
    assert.equal(history.present["node:n001"].fillColor, "#112233");
});
