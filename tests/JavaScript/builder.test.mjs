import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import {
    addConnector,
    addShape,
    alignShapes,
    clearStyles,
    distributeShapes,
    reorderShapes,
    setDefaultStyle,
    setGeometry,
    setStyles,
    builderLimits,
    commit,
    connectorCatalog,
    connectorGeometry,
    createDiagram,
    createHistory,
    deleteItems,
    diagramBounds,
    filterCatalog,
    moveShapes,
    parseDiagram,
    redo,
    routeConnectors,
    serializeDiagram,
    setConnectorKind,
    setConnectorLabel,
    setShapeText,
    shapeBox,
    shapeCatalog,
    shapeOutline,
    textLayout,
    undo
} from "../../src/CodeLogic.Web/wwwroot/js/builder.js";

const css = await readFile(new URL("../../src/CodeLogic.Web/wwwroot/app.css", import.meta.url), "utf8");

// Two shapes, one above the other, joined by a smart-routed arrow.
function twoConnectedShapes(kind = "arrow") {
    let { diagram, id: first } = addShape(createDiagram(), "process", 100, 100);
    let second;
    ({ diagram, id: second } = addShape(diagram, "decision", 100, 300));
    let connector;
    ({ diagram, id: connector } = addConnector(diagram, first, second, kind));
    return { diagram, first, second, connector };
}

test("the palette offers the 18 general shapes and 4 connector styles", () => {
    assert.deepEqual(shapeCatalog.map(item => item.kind), [
        "rectangle", "rounded", "text", "heading", "ellipse", "square", "circle", "process", "decision",
        "parallelogram", "hexagon", "triangle", "cylinder", "cloud", "document", "card", "data", "actor"
    ]);
    assert.deepEqual(connectorCatalog.map(item => item.kind), ["straight", "dashed", "curved", "arrow"]);
});

test("the search box filters by name, kind, or keyword, ignoring case", () => {
    assert.deepEqual(filterCatalog(shapeCatalog, "DIAMOND").map(item => item.kind), ["decision"]);
    assert.deepEqual(filterCatalog(shapeCatalog, "data").map(item => item.kind), ["parallelogram", "cylinder", "data"]);
    assert.equal(filterCatalog(shapeCatalog, "").length, shapeCatalog.length);
    assert.equal(filterCatalog(shapeCatalog, "no such shape").length, 0);
});

test("every shape has a finite outline that fits its box", () => {
    for (const item of shapeCatalog) {
        const outline = shapeOutline(item.kind, item.width, item.height);
        for (const d of [outline.d, ...outline.details]) {
            assert.match(d, /^M/, `${item.kind} path starts with a move`);
            assert.doesNotMatch(d, /NaN|Infinity|undefined/, `${item.kind} path is finite`);
            const numbers = d.match(/-?\d*\.?\d+/g).map(Number);
            assert.ok(numbers.every(value => value >= -0.01 && value <= Math.max(item.width, item.height) + 0.01), `${item.kind} stays inside its box`);
        }
    }
});

test("adding a shape from the palette places it centred where it was dropped", () => {
    const { diagram, id } = addShape(createDiagram(), "decision", 400, 250);
    assert.equal(id, "s1");
    assert.deepEqual(diagram.shapes, [{ id: "s1", kind: "decision", x: 340, y: 205, width: 120, height: 90, text: "", rotation: 0, style: {} }]);
    assert.equal(addShape(diagram, "not-a-shape", 0, 0).id, null);
    assert.equal(addShape(diagram, "text", 0, 0).diagram.shapes[1].text, "Text");
});

test("connectors join two different existing shapes only", () => {
    const { diagram, first, second } = twoConnectedShapes();
    assert.equal(diagram.connectors.length, 1);
    assert.equal(addConnector(diagram, first, first).id, null, "no connector from a shape to itself");
    assert.equal(addConnector(diagram, first, "s99").id, null, "no connector to a missing shape");
    assert.equal(addConnector(diagram, first, second, "zigzag").id, null, "unknown styles are refused");
});

test("connectors follow and re-route when a shape moves", () => {
    const { diagram, second, connector } = twoConnectedShapes();
    const before = routeConnectors(diagram).get(connector);
    // The decision starts below the process, so the arrow leaves the bottom and enters the top.
    assert.deepEqual(before.end, { x: 100, y: 255 });

    // Move the decision to the right of the process: the arrow now runs side to side.
    const moved = moveShapes(diagram, [second], 300, -200);
    const after = routeConnectors(moved).get(connector);
    const target = shapeBox(moved.shapes.find(shape => shape.id === second));
    assert.notEqual(after.d, before.d);
    assert.deepEqual(after.end, { x: target.cx - target.width / 2, y: target.cy });
    assert.match(after.d, /^M[\d.]+,[\d.]+(L[\d.]+,[\d.]+)+$/, "smart routes stay orthogonal path data");
});

test("each connector style draws its own path between the same shapes", () => {
    const source = { cx: 100, cy: 100, width: 120, height: 60 };
    const target = { cx: 300, cy: 300, width: 120, height: 60 };
    assert.match(connectorGeometry(source, target, "straight").d, /^M100,130L300,270$/);
    assert.match(connectorGeometry(source, target, "curved").d, /^M100,130C/);
    assert.match(connectorGeometry(source, target, "arrow").d, /^M100,130L100,200L300,200L300,270$/);
    assert.equal(connectorGeometry(source, target, "dashed").d, connectorGeometry(source, target, "arrow").d);
});

test("text and labels are kept with their line breaks and capped in length", () => {
    const { diagram, first, connector } = twoConnectedShapes();
    const edited = setShapeText(diagram, first, "Read\nage");
    assert.equal(edited.shapes[0].text, "Read\nage");
    assert.deepEqual(textLayout(edited.shapes[0]).lines, ["Read", "age"]);
    assert.equal(setShapeText(diagram, first, "x".repeat(900)).shapes[0].text.length, builderLimits.text);
    assert.equal(setConnectorLabel(diagram, connector, "True").connectors[0].label, "True");
    assert.equal(setConnectorKind(diagram, [connector], "curved").connectors[0].kind, "curved");
});

test("deleting a shape also removes its connectors", () => {
    const { diagram, first } = twoConnectedShapes();
    const after = deleteItems(diagram, [first]);
    assert.equal(after.shapes.length, 1);
    assert.equal(after.connectors.length, 0);
});

test("undo and redo step through snapshots, and a new change clears redo", () => {
    let history = createHistory(createDiagram());
    const one = addShape(history.present, "rectangle", 0, 0).diagram;
    history = commit(history, one);
    const two = moveShapes(one, ["s1"], 50, 0);
    history = commit(history, two);

    history = undo(history);
    assert.equal(history.present, one);
    history = undo(history);
    assert.equal(history.present.shapes.length, 0);
    assert.equal(undo(history), history, "nothing more to undo");
    history = redo(history);
    assert.equal(history.present, one);

    history = commit(history, setShapeText(one, "s1", "new"));
    assert.equal(history.future.length, 0);
    assert.equal(redo(history), history, "nothing to redo after a new change");
});

test("the bounds follow shapes into negative coordinates (infinite canvas)", () => {
    const { diagram, first } = twoConnectedShapes();
    const bounds = diagramBounds(moveShapes(diagram, [first], -900, -700));
    assert.ok(bounds.x < -700 && bounds.y < -600);
    assert.equal(diagramBounds(createDiagram()), null);
});

test("a saved diagram round-trips as JSON, separate from the C# source", () => {
    const { diagram } = twoConnectedShapes("curved");
    const scratchpad = [{ kind: "cloud", width: 130, height: 80, text: "API", style: { fillMode: "solid", fillColor: "#dbeafe" } }];
    const parsed = parseDiagram(serializeDiagram(diagram, scratchpad));
    assert.deepEqual(parsed.diagram, diagram);
    assert.deepEqual(parsed.scratchpad, scratchpad);
    assert.doesNotMatch(serializeDiagram(diagram), /Console|static void|namespace/);
});

test("invalid or tampered drafts are rejected instead of half-loaded", () => {
    const { diagram } = twoConnectedShapes();
    const valid = JSON.parse(serializeDiagram(diagram));
    assert.equal(parseDiagram("not json"), null);
    assert.equal(parseDiagram({ ...valid, version: 99 }), null);
    assert.equal(parseDiagram({ ...valid, shapes: [{ ...valid.shapes[0], kind: "script" }, valid.shapes[1]] }), null);
    assert.equal(parseDiagram({ ...valid, shapes: [{ ...valid.shapes[0], x: Number.NaN }, valid.shapes[1]] }), null);
    assert.equal(parseDiagram({ ...valid, connectors: [{ ...valid.connectors[0], to: "s42" }] }), null);
    assert.equal(parseDiagram({ ...valid, shapes: Array.from({ length: builderLimits.shapes + 1 }, (_, i) => ({ ...valid.shapes[0], id: `s${i + 1}` })) }), null);
    // Text is data: markup-looking text is kept as plain text (it is drawn with textContent).
    const markup = parseDiagram({ ...valid, shapes: [{ ...valid.shapes[0], text: "<img src=x onerror=alert(1)>" }, valid.shapes[1]] });
    assert.equal(markup.diagram.shapes[0].text, "<img src=x onerror=alert(1)>");
});

test("Build mode replaces the code workspace and shows the palette beside the canvas", () => {
    assert.match(css, /\.ws--build \.workspace-explorer,\s*\.ws--build \.ws-main,/);
    assert.match(css, /\.ws--build \.builder \{[^}]*display: grid;[^}]*grid-template-columns: 15\.5rem minmax\(0, 1fr\);/);
    assert.match(css, /\.builder-shape:hover \.builder-handle/);
});

test("changing fill and font size in the Format panel updates the selected shape only", () => {
    const { diagram, first, second } = twoConnectedShapes();
    const styled = setStyles(diagram, [first], { fillMode: "solid", fillColor: "#dbeafe", fontSize: 22 });
    assert.deepEqual(styled.shapes.find(shape => shape.id === first).style, { fillMode: "solid", fillColor: "#dbeafe", fontSize: 22 });
    assert.deepEqual(styled.shapes.find(shape => shape.id === second).style, {});
    assert.equal(textLayout(styled.shapes.find(shape => shape.id === first)).fontSize, 22);
    // Invalid values are ignored rather than stored.
    assert.deepEqual(setStyles(diagram, [first], { fillColor: "javascript:alert(1)" }).shapes[0].style, {});
});

test("multi-select applies formatting to every selected shape and connector", () => {
    const { diagram, first, second, connector } = twoConnectedShapes();
    const styled = setStyles(diagram, [first, second, connector], { lineColor: "#16a34a" });
    assert.ok([...styled.shapes, ...styled.connectors].every(item => item.style.lineColor === "#16a34a"));
    const cleared = clearStyles(setStyles(styled, [first], { bold: true }), [first], ["bold"]);
    assert.deepEqual(cleared.shapes[0].style, { lineColor: "#16a34a" });
});

test("undo and redo restore the previous formatting", () => {
    const { diagram, first } = twoConnectedShapes();
    let history = createHistory(diagram);
    history = commit(history, setStyles(history.present, [first], { fillMode: "solid", fillColor: "#fee2e2" }));
    history = commit(history, setStyles(history.present, [first], { fillColor: "#dcfce7" }));
    history = undo(history);
    assert.equal(history.present.shapes[0].style.fillColor, "#fee2e2");
    history = undo(history);
    assert.deepEqual(history.present.shapes[0].style, {});
    history = redo(history);
    assert.equal(history.present.shapes[0].style.fillColor, "#fee2e2");
});

test("Set as Default Style is used by new shapes", () => {
    const diagram = setDefaultStyle(createDiagram(), { fillMode: "solid", fillColor: "#dcfce7", onclick: "x" });
    assert.deepEqual(diagram.defaultStyle, { fillMode: "solid", fillColor: "#dcfce7" });
    assert.deepEqual(addShape(diagram, "rectangle", 0, 0).diagram.shapes[0].style, { fillMode: "solid", fillColor: "#dcfce7" });
});

test("text style controls alignment, vertical position, and padding", () => {
    const shape = { kind: "rectangle", width: 200, height: 100, text: "a\nb", style: { align: "left", vertical: "top", spacing: 10 } };
    const layout = textLayout(shape);
    assert.equal(layout.x, 10);
    assert.equal(layout.anchor, "start");
    assert.equal(layout.firstY, 10 + layout.lineHeight / 2);
    assert.equal(textLayout({ ...shape, style: { align: "right", vertical: "bottom" } }).anchor, "end");
});

test("Arrange sets position, size, and rotation; aligns; distributes; and orders shapes", () => {
    let { diagram } = addShape(createDiagram(), "rectangle", 100, 100);
    ({ diagram } = addShape(diagram, "rectangle", 400, 160));
    ({ diagram } = addShape(diagram, "rectangle", 200, 300));
    const placed = setGeometry(diagram, ["s1"], { x: 10, y: 20, width: 150, rotation: 450, height: Number.NaN });
    assert.deepEqual([placed.shapes[0].x, placed.shapes[0].y, placed.shapes[0].width, placed.shapes[0].height, placed.shapes[0].rotation], [10, 20, 150, 60, 90]);

    const left = alignShapes(diagram, ["s1", "s2", "s3"], "left");
    assert.ok(left.shapes.every(shape => shape.x === 40));
    const middle = alignShapes(diagram, ["s1", "s2"], "middle");
    assert.equal(middle.shapes[0].y + 30, middle.shapes[1].y + 30);

    const spread = distributeShapes(diagram, ["s1", "s2", "s3"], "horizontal");
    const sorted = [...spread.shapes].sort((a, b) => a.x - b.x);
    assert.equal(Math.round(sorted[1].x - (sorted[0].x + sorted[0].width)), Math.round(sorted[2].x - (sorted[1].x + sorted[1].width)));

    assert.deepEqual(reorderShapes(diagram, ["s1"], "front").shapes.map(shape => shape.id), ["s2", "s3", "s1"]);
    assert.deepEqual(reorderShapes(diagram, ["s3"], "back").shapes.map(shape => shape.id), ["s3", "s1", "s2"]);
});

test("Perimeter spacing keeps connector ends away from the outline", () => {
    const { diagram, first, connector } = twoConnectedShapes();
    const plain = routeConnectors(diagram).get(connector);
    const spaced = routeConnectors(setStyles(diagram, [first], { perimeter: 10 })).get(connector);
    assert.equal(spaced.start.y - plain.start.y, 10);
});

test("saved styles are validated when a draft loads", () => {
    const { diagram } = twoConnectedShapes();
    const raw = JSON.parse(serializeDiagram(setStyles(diagram, ["s1"], { bold: true })));
    raw.shapes[0].style.fillColor = "url(javascript:alert(1))";
    raw.shapes[0].rotation = 720;
    const parsed = parseDiagram(raw);
    assert.deepEqual(parsed.diagram.shapes[0].style, { bold: true });
    assert.equal(parsed.diagram.shapes[0].rotation, 0);
});
