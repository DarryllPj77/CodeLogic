import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import {
    canvasPadding,
    chooseSides,
    edgeEnds,
    edgeScrollSpeed,
    growBounds,
    isDrag,
    labelAnchor,
    pathData,
    routeOrthogonal,
    simplify
} from "../../src/CodeLogic.Web/wwwroot/js/flowchart.js";
import { panelLimits, resolvePanel } from "../../src/CodeLogic.Web/wwwroot/js/workspace.js";

const css = await readFile(new URL("../../src/CodeLogic.Web/wwwroot/app.css", import.meta.url), "utf8");
const box = (cx, cy, width = 160, height = 48) => ({ cx, cy, width, height });
const isOrthogonal = points => points.slice(1).every((point, index) =>
    point.x === points[index].x || point.y === points[index].y);

test("a press without movement is a click; moving a few pixels starts a drag", () => {
    assert.equal(isDrag(0, 0), false);
    assert.equal(isDrag(2, 2), false);
    assert.equal(isDrag(4, 0), true);
    assert.equal(isDrag(-3, -3), true);
});

test("connector sides follow where the target is", () => {
    assert.deepEqual(chooseSides(box(100, 100), box(100, 300)), { exit: "bottom", entry: "top" });
    assert.deepEqual(chooseSides(box(100, 100), box(400, 110)), { exit: "right", entry: "left" });
    assert.deepEqual(chooseSides(box(400, 100), box(100, 110)), { exit: "left", entry: "right" });
    // A target above (a loop back) is reached around the right side.
    assert.deepEqual(chooseSides(box(100, 300), box(110, 100)), { exit: "right", entry: "right" });
});

test("routes are orthogonal and start and end on the chosen sides", () => {
    const below = routeOrthogonal(box(100, 100), box(300, 300));
    assert.ok(isOrthogonal(below));
    assert.deepEqual(below[0], { x: 100, y: 124 });
    assert.deepEqual(below.at(-1), { x: 300, y: 276 });

    const beside = routeOrthogonal(box(100, 100), box(500, 160));
    assert.ok(isOrthogonal(beside));
    assert.deepEqual(beside[0], { x: 180, y: 100 });
    assert.deepEqual(beside.at(-1), { x: 420, y: 160 });

    const loop = routeOrthogonal(box(100, 400), box(100, 100));
    assert.ok(isOrthogonal(loop));
    assert.ok(loop.every(point => point.x >= 180), "a loop back stays to the right of both nodes");
});

test("the middle segment moves to avoid crossing another node", () => {
    const source = box(100, 100);
    const target = box(300, 300);
    // An obstacle sitting exactly on the default middle line (y = 200).
    const obstacle = box(200, 200, 120, 40);

    const route = routeOrthogonal(source, target, [obstacle]);
    const middle = route.find((point, index) => index > 0 && point.y === route[index - 1].y);

    assert.ok(isOrthogonal(route));
    assert.ok(Math.abs(middle.y - 200) > 26, `middle segment at ${middle?.y} should clear the obstacle`);
});

test("labels sit on the longest segment, so they move with the connector", () => {
    const points = [{ x: 0, y: 0 }, { x: 0, y: 20 }, { x: 200, y: 20 }, { x: 200, y: 40 }];
    assert.deepEqual(labelAnchor(points), { x: 100, y: 20 });
});

test("routes become compact SVG path data", () => {
    assert.deepEqual(simplify([{ x: 0, y: 0 }, { x: 0, y: 10 }, { x: 0, y: 20 }, { x: 0, y: 20 }]), [{ x: 0, y: 0 }, { x: 0, y: 20 }]);
    assert.equal(pathData([{ x: 1, y: 2 }, { x: 1, y: 30.333 }]), "M1,2L1,30.33");
});

test("Mermaid connector ids name the two nodes they join", () => {
    assert.deepEqual(edgeEnds("L_n003_n006_0"), { from: "n003", to: "n006" });
    assert.equal(edgeEnds("something-else"), null);
});

test("an open Explorer stays beside a FULL panel and above it in the one-column layout", () => {
    assert.match(css, /\.ws--explorer-open\.ws--max-analysis \.analysis-panel \{[^}]*left: calc\(3rem \+ var\(--explorer-size\) \+ 0\.75rem\);/s);
    const compact = css.slice(css.indexOf("@media (max-width: 68.75rem)"));
    const explorerLayer = Number(/\.ws--explorer-open \.workspace-explorer \{[^}]*z-index: (\d+);/.exec(compact)[1]);
    const fullLayer = Number(/\.ws--max-flowchart \.flowchart-panel \{[^}]*z-index: (\d+);/s.exec(css)[1]);
    assert.ok(explorerLayer > fullLayer, `Explorer z-index ${explorerLayer} must be above FULL ${fullLayer}`);
});

test("the code view (Flowchart focus and FULL) shows the code beside the chart at about 40%, at least 320px", () => {
    assert.match(css, /\.ws--codeview \.ws-main \{ grid-template-columns: minmax\(20rem, var\(--focus-size, 40%\)\) 0\.6rem minmax\(24rem, 1fr\); \}/);
    assert.doesNotMatch(css, /\.ws--focus \.ws-left[^{]*\{ display: none/);
    assert.equal(panelLimits.focus[0], 320);
});

test("the column splitter sizes the focus code column only while in Flowchart focus", () => {
    const root = mode => ({ classList: { contains: name => name === mode } });
    assert.equal(resolvePanel(root("ws--codeview"), "editor"), "focus");
    assert.equal(resolvePanel(root("ws"), "editor"), "editor");
    assert.equal(resolvePanel(root("ws--codeview"), "analysis"), "analysis");
});

test("FULL on the flowchart keeps the editor beside it on desktop and overlays only in one column", () => {
    const compactStart = css.indexOf("@media (max-width: 68.75rem)");
    const desktop = css.slice(0, compactStart);
    const compact = css.slice(compactStart, css.indexOf("@media (max-width: 48rem)"));
    assert.doesNotMatch(desktop, /\.ws--max-flowchart \.flowchart-panel[^{]*\{[^}]*position: absolute/);
    assert.match(compact, /\.ws--max-flowchart \.flowchart-panel \{[^}]*position: absolute;/);
    assert.doesNotMatch(css, /\.ws--codeview \.editor-panel \.code-editor[^{]*\{[^}]*display: none/);
});

test("the vertical editor toolbar shows only in the code view and never scrolls with the code", () => {
    assert.match(css, /\.editor-rail \{ display: none; \}/);
    assert.match(css, /\.ws--codeview \.editor-rail \{[^}]*display: flex;[^}]*flex-direction: column;/);
    assert.match(css, /\.ws--codeview \.editor-panel \.ws-primary-action,/);
});

test("closing the editor in the code view gives the flowchart the full width", () => {
    assert.match(css, /\.ws--codeview\.ws--editor-hidden \.ws-main \{ grid-template-columns: minmax\(0, 1fr\); \}/);
    assert.match(css, /\.ws--codeview\.ws--editor-hidden \.ws-left,\s*\.ws--codeview\.ws--editor-hidden \.ws-column-splitter \{ display: none; \}/);
    assert.match(css, /\.ws--codeview\.ws--editor-hidden \.flowchart-panel \{ grid-column: 1; \}/);
    assert.match(css, /\.editor-panel \.ws-panel__title \{ flex: none; \}/);
});

const base = { x: 0, y: 0, width: 400, height: 600 };

test("the canvas keeps the computed size while every node is inside it", () => {
    assert.deepEqual(growBounds(base, { x: 8, y: 8, width: 384, height: 584 }), base);
    assert.deepEqual(growBounds(base, null), base);
});

test("a node dragged far past the left and top edges gets negative coordinates and stays inside the canvas", () => {
    const content = { x: -900, y: -1200, width: 1300, height: 1800 };
    const grown = growBounds(base, content);
    assert.deepEqual(grown, { x: -900 - canvasPadding, y: -1200 - canvasPadding, width: 900 + canvasPadding + 400, height: 1200 + canvasPadding + 600 });
    // The origin shifts, but the right and bottom edges stay where the computed layout put them.
    assert.equal(grown.x + grown.width, 400);
    assert.equal(grown.y + grown.height, 600);
    assert.ok(grown.x < content.x && grown.y < content.y, "nothing is clipped on the left or top");
});

test("a node dragged past the right and bottom edges grows the canvas only on those sides", () => {
    const grown = growBounds(base, { x: 8, y: 8, width: 3000, height: 5000 });
    assert.deepEqual(grown, { x: 0, y: 0, width: 3008 + canvasPadding, height: 5008 + canvasPadding });
});

test("dragging near or past a canvas edge scrolls that way; the middle does not scroll", () => {
    assert.equal(edgeScrollSpeed(500, 0, 1000), 0);
    assert.ok(edgeScrollSpeed(10, 0, 1000) < 0);
    assert.ok(edgeScrollSpeed(990, 0, 1000) > 0);
    assert.equal(edgeScrollSpeed(-300, 0, 1000), -18, "past the edge scrolls at full speed");
    assert.equal(edgeScrollSpeed(1300, 0, 1000), 18);
});
