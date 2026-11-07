# Flowchart Browser Renderer

## Document Status

- Task: Prompt 16 — Flowchart browser renderer
- Last updated: 2026-11-01
- Renderer: Mermaid 12.2.0, pinned browser ESM module
- Input: flowchart IR schema 1.1

## Rendering Flow

1. `IFlowchartTranslator` returns a complete validated `FlowGraph` or diagnostics with no partial graph.
2. `IMermaidFlowchartSerializer` converts that graph into deterministic Mermaid text and a node/source map.
3. The Playground renders the Mermaid definition in the browser.
4. The same graph produces an ordered text equivalent and connection list.
5. A source-backed node's **Show line** control selects its exact `SourceSpan` in the editor and highlights the corresponding visual node when available.

The renderer does not compile or execute learner source.

## Mermaid Selection

CodeLogic uses Mermaid 12.2.0 through an exact jsDelivr ESM URL. The version is pinned so renderer output does not silently change between page loads. Mermaid is initialized manually with:

- `startOnLoad: false`;
- `securityLevel: "strict"`;
- `htmlLabels: false`;
- `maxTextSize: 10000`;
- `maxEdges: 50`;
- a linear, responsive flowchart layout.

The [official Mermaid usage guidance](https://mermaid.js.org/config/usage) documents manual initialization/rendering and states that strict mode encodes HTML and disables click functionality. CodeLogic therefore implements source mapping with its own ordinary buttons instead of enabling Mermaid click directives.

## Formatting and Escaping

The serializer owns every Mermaid identifier and emits only `n000`, `n001`, and similar IDs. Learner-controlled graph IDs never become Mermaid identifiers.

Labels are always quoted. Whitespace is collapsed, control characters become replacement characters, and characters that could close Mermaid strings, form directives, or introduce markup are replaced with safe Unicode display equivalents. Common Boolean/comparison pairs are displayed as `∧`, `∨`, `≥`, and `≤` for readability. Branch labels are generated from the trusted `FlowEdgeType` values.

Mermaid returns SVG text. The browser module does not assign that text through `innerHTML`. It:

1. parses it with `DOMParser` as `image/svg+xml`;
2. rejects parser errors;
3. removes scripts, `foreignObject`, iframe/object/embed content, anchors, event-handler attributes, and link attributes;
4. imports the sanitized SVG node and inserts it with `replaceChildren`.

This is defense in depth on top of strict Mermaid configuration and server-side label escaping.

## Readable Large Graphs

- **Label wrapping (server side):** `MermaidFlowchartSerializer.WrapLabel` splits node labels at 34 characters per line and at most 3 lines, joined with `<br/>`, the only markup the serializer emits (escaped text can never contain `<`). Long conditions get one clause per line (`mps[i] ≥ 50` / `∧ xps[i] › 1000` / `∧ hps[i] › 10`). Text beyond 3 lines ends with `…`. The source map keeps the full label, which appears in the node tooltip, its accessible name, and Node Properties.
- **Mermaid settings** (`mermaidFlowchartConfig` in `playground.js`): 14px text, node padding 26, node spacing 60, rank spacing 72, a wrapping width wider than a 34-character line so Mermaid does not re-wrap, `useMaxWidth: false` so the SVG keeps its natural size, and `maxEdges: 400` (the earlier limit of 50 refused programs of about 40 nodes).
- **Zoom is real size:** zooming sets the SVG's width and height (natural size × zoom), so the canvas scroll area always reaches every node. The range is 20% to 200%.
- **Opening zoom:** a graph opens at 100%, scrolling rather than shrinking. Only a graph that already fits at 100% is fitted, and then never above 125%. While the learner has not zoomed or fitted, a canvas resize re-applies this rule.
- **Fit / Fit width** shrink or grow the graph on request only.
- **Movable nodes** (`wwwroot/js/flowchart.js`): press, hold, and drag a node, or move a focused node with the arrow keys (Shift for 50px steps). A press without movement still selects it. Moving changes only the node's SVG `transform`; the source, trace, and node IDs are untouched. Connectors touching a moved node are re-routed live with orthogonal lines: bottom-to-top when the target is below, side-to-side when it is beside, and around the right side for a loop back. The middle segment shifts to avoid other nodes where it can. Labels (True, False, break, ...) move to the route's longest segment. Other connectors keep Mermaid's lines. Positions survive re-renders of the same graph (Play, FULL, zoom) and reset when the graph changes or the page reloads. The canvas is unbounded: after each move the SVG `viewBox` grows to include the moved node, its connectors, and labels (plus a 32px margin), so nothing is clipped and the scroll bars reach everything. Dragging left or up gives negative coordinates; the origin shifts and the scroll position is corrected, so the other nodes keep their spacing and the chart does not jump. Dragging near or past a canvas edge auto-scrolls, so a node can be moved as far as you like, and keyboard moves scroll the node into view. The drag follows the pointer in SVG coordinates, so scrolling or zooming mid-drag never makes the node drift. **Auto layout** restores every computed position, connector, and label exactly. It also returns the canvas to its computed size at the current zoom.
- **Navigation:** **⤒ Start** and **⤓ End** scroll to and focus the program's Start and End nodes. Stepping a trace scrolls only when the active node is within 48px of an edge or outside the view, and never re-fits.

## Accessible and Responsive States

- Before rendering, the panel explains how to request a preview.
- Translation failures show the same positioned, actionable diagnostics as the graph service.
- Browser-renderer failures keep the deterministic text/source map available.
- The SVG has an image role and accessible label.
- Every visual graph has an ordered node list and a connection list that explicitly says `True path`, `False path`, `Repeat`, or `Next`.
- Source-map controls are native keyboard-operable buttons.
- The canvas is focusable (`role="application"`, described by the current step line). Arrow keys pan, `+`/`-` zoom, and Home/End go to the Start/End node. The wheel scrolls, Shift+wheel scrolls sideways, Ctrl+wheel zooms around the pointer, and dragging empty space pans.
- The diagram keeps a readable size and scrolls inside its panel in both directions; the page itself never scrolls.
- Editing, loading, or resetting source clears the old diagram so stale output is not presented.

## External Dependency and Failure Behavior

The Mermaid module is downloaded by the browser from the pinned jsDelivr URL on first preview. No learner source is sent to jsDelivr; rendering occurs locally after the module loads. An offline browser, blocked CDN, or restrictive network policy can prevent the SVG preview. CodeLogic catches that failure and retains the server-generated text/source map.

A later delivery task may vendor the reviewed Mermaid distribution or add a formal frontend dependency pipeline. Prompt 16 does not install Node.js or add build tooling.

## Verification

Automated tests lock:

- exact shapes and arrows for a branching graph;
- explicit True/False and repeat Mermaid arrows;
- stable renderer output;
- graph-to-Mermaid/source ID mapping;
- escaping of quote, markup, and directive characters.

Browser checks cover readable sequence labels, correct branch arrows and merge behavior, repeat edges, loop exits, and exact source selection.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, start `CodeLogic.Web`, and open **Playground**.
2. Select **Preview flowchart** for the default Variables sample. Confirm the diagram, ordered nodes, and connections appear.
3. Enter the Prompt 15 `if/else` golden sample and preview it. Confirm True and False arrows leave the diamond and merge into the following output node.
4. Choose **Show line 2** for the decision. Confirm the editor receives focus and selects `score >= 5`.
5. Enter a `while` loop and a nested `if`; confirm repeat, exit, True, and False paths agree with the equivalent connection text.
6. Enter `while (true) { break; }`; confirm `CL1006` appears with no partial SVG.
7. Enter malformed C#; confirm syntax diagnostics replace the preview.
8. Block the Mermaid CDN or go offline and request a preview. Confirm the renderer-unavailable message appears and the text/source map remains.
9. Repeat with keyboard only, at 200% zoom, and at 320 CSS pixels. Confirm the diagram scrolls inside its panel without page-level horizontal scrolling.
