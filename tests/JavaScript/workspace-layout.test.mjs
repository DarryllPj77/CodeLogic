import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { computeFitZoom, computeOpeningZoom } from "../../src/CodeLogic.Web/wwwroot/js/playground.js";
import {
    clampPanel,
    panelDefaults,
    panelLimits,
    shouldHandleWorkspaceEscape
} from "../../src/CodeLogic.Web/wwwroot/js/workspace.js";

const css = await readFile(new URL("../../src/CodeLogic.Web/wwwroot/app.css", import.meta.url), "utf8");

// Returns the declarations of the first rule whose selector text matches exactly (whitespace-normalized).
function ruleBody(selector) {
    const normalized = css.replace(/\s+/g, " ");
    const start = normalized.indexOf(`${selector} {`);
    assert.notEqual(start, -1, `Missing CSS rule for ${selector}`);
    const open = normalized.indexOf("{", start);
    return normalized.slice(open + 1, normalized.indexOf("}", open));
}

test("the workspace locks html and body to the viewport so the page never scrolls", () => {
    const rootRule = ruleBody("html:has(.ws), body:has(.ws)");
    assert.match(rootRule, /height: 100%;/);
    assert.match(rootRule, /overflow: hidden;/);

    const shell = ruleBody(".ws");
    assert.match(shell, /height: 100dvh;/);
    assert.match(shell, /overflow: hidden;/);
    assert.match(shell, /grid-template-rows: var\(--ws-header-height\) minmax\(0, 1fr\) var\(--ws-status-height\);/);
});

test("the no-scroll lock is scoped to the workspace so other pages keep scrolling", () => {
    assert.doesNotMatch(css, /(^|\})\s*(html|body)\s*\{[^}]*overflow:\s*hidden/);
});

test("panel bodies, not the page, own the scrolling", () => {
    assert.match(ruleBody(".ws-panel__body"), /overflow: auto;/);
    assert.match(ruleBody(".ws-panel"), /min-height: 0;/);
    assert.match(ruleBody(".flowchart-canvas"), /overflow: auto;/);
});

test("the compact breakpoint stacks to one column with Editor and Flowchart tab buttons", () => {
    const compact = css.slice(css.indexOf("@media (max-width: 68.75rem)"));
    assert.match(compact, /\.ws-compact-switch \{[^}]*display: flex;/);
    assert.match(compact, /\.ws--compact-editor \.flowchart-panel \{ display: none; \}/);
    assert.match(compact, /\.ws--compact-flowchart \.ws-left \{ display: none; \}/);
});

test("FULL panels cover the work area and collapsed panels keep only their header", () => {
    assert.match(css, /\.ws--max-flowchart \.flowchart-panel \{[^}]*position: absolute;[^}]*z-index: 40;/s);
    assert.match(ruleBody(".ws-panel--collapsed > :not(.ws-panel__header)"), /display: none !important;/);
});

test("Esc is left to Monaco and form fields but handled everywhere else", () => {
    const element = (tagName, insideEditor = false) => ({
        tagName,
        closest: selector => (selector === ".code-editor" && insideEditor ? {} : null)
    });

    assert.equal(shouldHandleWorkspaceEscape(element("BUTTON")), true);
    assert.equal(shouldHandleWorkspaceEscape(element("BODY")), true);
    assert.equal(shouldHandleWorkspaceEscape(element("DIV", true)), false);
    assert.equal(shouldHandleWorkspaceEscape(element("INPUT")), false);
    assert.equal(shouldHandleWorkspaceEscape(element("textarea")), false);
    assert.equal(shouldHandleWorkspaceEscape(element("SELECT")), false);
    assert.equal(shouldHandleWorkspaceEscape({ ...element("DIV"), isContentEditable: true }), false);
    assert.equal(shouldHandleWorkspaceEscape(null), true);
});

test("panel sizes clamp to their limits and defaults sit inside them", () => {
    for (const [panel, value] of Object.entries(panelDefaults)) {
        const [minimum, maximum] = panelLimits[panel];
        assert.ok(value >= minimum && value <= maximum, `${panel} default is out of range`);
        assert.equal(clampPanel(panel, minimum - 50), minimum);
        assert.equal(clampPanel(panel, maximum + 50), maximum);
    }
    assert.equal(clampPanel("analysis", 300.4), 300);
});

test("fit zoom shows the whole graph or its width, within the zoom limits", () => {
    // A tall 400x1600 graph in an 800x600 canvas: whole graph needs 37.5%, width allows 200%.
    assert.equal(computeFitZoom(400, 1600, 800, 600, "all"), 0.38);
    assert.equal(computeFitZoom(400, 1600, 800, 600, "width"), 2);
    // Never below the 20% floor, however large the graph.
    assert.equal(computeFitZoom(400, 20000, 800, 600, "all"), 0.2);
    // Missing geometry gives no answer.
    assert.equal(computeFitZoom(0, 600, 800, 600), null);
});

test("large graphs open at a readable 100% instead of shrinking", () => {
    assert.equal(computeOpeningZoom(500, 3000, 900, 650), 1);
    // A small graph that fits is fitted, but never enlarged past 125%.
    assert.equal(computeOpeningZoom(300, 200, 900, 650), 1.25);
    assert.equal(computeOpeningZoom(700, 600, 800, 650), 1.08);
});
