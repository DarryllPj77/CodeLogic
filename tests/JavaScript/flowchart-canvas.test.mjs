import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import {
    clampZoom,
    computeAnchoredScroll,
    computeRevealScroll,
    maximumZoom,
    mermaidFlowchartConfig,
    minimumZoom
} from "../../src/CodeLogic.Web/wwwroot/js/playground.js";

const css = await readFile(new URL("../../src/CodeLogic.Web/wwwroot/app.css", import.meta.url), "utf8");

test("Mermaid renders readable text, roomy spacing, and a real-size SVG", () => {
    assert.ok(mermaidFlowchartConfig.fontSize >= 13, "node text must be at least 13px at 100%");
    assert.equal(mermaidFlowchartConfig.useMaxWidth, false, "a natural-size SVG gives the canvas real scroll width and height");
    assert.ok(mermaidFlowchartConfig.nodeSpacing >= 60);
    assert.ok(mermaidFlowchartConfig.rankSpacing >= 70);
    assert.ok(mermaidFlowchartConfig.padding >= 24, "node padding keeps nodes about 56px tall");
    // CodeLogic wraps labels at 34 characters; Mermaid must not wrap those lines again (34 x ~8.4px).
    assert.ok(mermaidFlowchartConfig.wrappingWidth >= 34 * 9);
    // A 42-node program can have well over 50 connections; the old limit of 50 refused to render it.
    assert.ok(mermaidFlowchartConfig.maxEdges >= 200);
});

test("the canvas scrolls both ways and the SVG keeps its zoomed size", () => {
    // Later rules win, so check the last definition of each (an older card-style rule precedes them).
    const last = pattern => [...css.matchAll(pattern)].at(-1)[0];
    const canvas = last(/\.flowchart-canvas \{[^}]*\}/g);
    assert.match(canvas, /overflow: auto;/);
    const svg = last(/\.flowchart-svg \{[^}]*\}/g);
    assert.match(svg, /min-width: 0;/);
    assert.match(svg, /max-width: none;/);
    assert.doesNotMatch(svg, /width: 100%/);
});

test("zoom stays between 20% and 200%", () => {
    assert.equal(minimumZoom, 0.2);
    assert.equal(maximumZoom, 2);
    assert.equal(clampZoom(0.05), 0.2);
    assert.equal(clampZoom(3), 2);
    assert.equal(clampZoom(1.234), 1.23);
});

test("zooming around the cursor keeps the point under it in place", () => {
    // Content point under the cursor: (100 + 200) / 1 = 300 at 100%; at 200% it is at 600.
    const scroll = computeAnchoredScroll(100, 50, 200, 150, 1, 2);
    assert.deepEqual(scroll, { left: 400, top: 250 });
    // Zooming out near the top-left never produces a negative scroll.
    assert.deepEqual(computeAnchoredScroll(0, 0, 10, 10, 1, 0.5), { left: 0, top: 0 });
});

test("Go to End scrolls a node far below the viewport into view with a margin", () => {
    const viewport = { left: 0, top: 0, width: 900, height: 600 };
    const endNode = { x: 380, y: 3200, width: 160, height: 56 };

    const scroll = computeRevealScroll(viewport, endNode, 48);

    assert.deepEqual(scroll, { left: 0, top: 3200 + 56 + 48 - 600 });
    const visibleTop = scroll.top;
    assert.ok(endNode.y >= visibleTop + 48 && endNode.y + endNode.height <= visibleTop + viewport.height - 48 + 1);
});

test("stepping keeps an already visible node still and nudges one near the edge", () => {
    const viewport = { left: 0, top: 400, width: 900, height: 600 };

    assert.equal(computeRevealScroll(viewport, { x: 300, y: 600, width: 180, height: 56 }), null);
    assert.deepEqual(computeRevealScroll(viewport, { x: 300, y: 380, width: 180, height: 56 }), { left: 0, top: 332 });
    assert.deepEqual(computeRevealScroll(viewport, { x: 1000, y: 600, width: 180, height: 56 }), { left: 328, top: 400 });
});

test("a node larger than the viewport is aligned to its top-left corner", () => {
    const viewport = { left: 0, top: 0, width: 300, height: 200 };

    assert.deepEqual(computeRevealScroll(viewport, { x: 500, y: 900, width: 400, height: 300 }), { left: 452, top: 852 });
});
