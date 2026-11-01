# Flowchart Browser Renderer

## Document Status

- Task: Prompt 16 — Flowchart browser renderer
- Last updated: 2026-11-01
- Renderer: Mermaid 12.2.0, pinned browser ESM module
- Input: flowchart IR schema 1.0

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

## Accessible and Responsive States

- Before rendering, the panel explains how to request a preview.
- Translation failures show the same positioned, actionable diagnostics as the graph service.
- Browser-renderer failures keep the deterministic text/source map available.
- The SVG has an image role and accessible label.
- Every visual graph has an ordered node list and a connection list that explicitly says `True path`, `False path`, or `Next`.
- Source-map controls are native keyboard-operable buttons.
- The diagram scales to the panel and uses contained horizontal scrolling when its minimum readable width exceeds the viewport.
- Editing, loading, or resetting source clears the old diagram so stale output is not presented.

## External Dependency and Failure Behavior

The Mermaid module is downloaded by the browser from the pinned jsDelivr URL on first preview. No learner source is sent to jsDelivr; rendering occurs locally after the module loads. An offline browser, blocked CDN, or restrictive network policy can prevent the SVG preview. CodeLogic catches that failure and retains the server-generated text/source map.

A later delivery task may vendor the reviewed Mermaid distribution or add a formal frontend dependency pipeline. Prompt 16 does not install Node.js or add build tooling.

## Verification

Automated tests lock:

- exact shapes and arrows for a branching graph;
- explicit True/False Mermaid arrows;
- stable renderer output;
- graph-to-Mermaid/source ID mapping;
- escaping of quote, markup, and directive characters.

Browser checks confirmed readable sequence labels, correct branch arrows and merge behavior, exact source selection, and the nested-branch error state.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, start `CodeLogic.Web`, and open **Playground**.
2. Select **Preview flowchart** for the default Variables sample. Confirm the diagram, ordered nodes, and connections appear.
3. Enter the Prompt 15 `if/else` golden sample and preview it. Confirm True and False arrows leave the diamond and merge into the following output node.
4. Choose **Show line 2** for the decision. Confirm the editor receives focus and selects `score >= 5`.
5. Enter a nested `if`; confirm the panel shows `FL1001` with no partial SVG.
6. Enter malformed C#; confirm syntax diagnostics replace the preview.
7. Block the Mermaid CDN or go offline and request a preview. Confirm the renderer-unavailable message appears and the text/source map remains.
8. Repeat with keyboard only, at 200% zoom, and at 320 CSS pixels. Confirm the diagram scrolls inside its panel without page-level horizontal scrolling.
