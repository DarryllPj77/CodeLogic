# Editor Playground

## Document Status

- Tasks: Prompts 13–19 plus the owner-directed IDE workspace redesign
- Last updated: 2026-11-07
- Editor: Monaco Editor 0.57.0 (pinned CDN) with Roslyn-driven code folding, falling back to an accessible HTML textarea when Monaco cannot load

## Scope

The Playground provides a desktop-first IDE workspace for the two complete curriculum samples. A user can load either curated snippet, edit it, save one browser-local draft, restore that draft after reload, or reset to the selected curated source.

Prompt 14 adds **Check syntax**. Prompts 15–17 add **Preview flowchart**. Prompt 18 adds **Build step trace** through CodeLogic's bounded teaching interpreter. Prompt 19 adds instructor-defined logical checks for the known adult-age exercise. See [CODE_ANALYSIS.md](CODE_ANALYSIS.md), [FLOWCHART_IR.md](FLOWCHART_IR.md), [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md), [EXECUTION_TRACES.md](EXECUTION_TRACES.md), and [LOGICAL_FEEDBACK.md](LOGICAL_FEEDBACK.md).

The trace can be played automatically or moved one snapshot at a time with **Step**, **Back**, and **Reset**. Its speed control changes only the delay between already-bounded snapshots; it never changes interpreter behavior. Every move synchronizes the active source range and flowchart node, identifies changed watch values, progressively reveals captured `Console.WriteLine` output, and shows repeated visits to loop conditions.

Editor changes schedule a flowchart translation after 400 milliseconds without compiling or running the source. A valid result replaces the preview; invalid or unsupported text leaves the last valid chart visible and presents the new diagnostics. **Preview flowchart** remains available as a manual retry and uses the same fail-closed translator.

## Editor: Monaco with a Plain-Text Fallback

The playground began with a native textarea for its labels, selection, screen-reader support, and familiar keyboard interaction. The owner-directed IDE redesign now uses Monaco for syntax highlighting, a line-number gutter, and code folding. It meets the earlier conditions for adding Monaco:

- **Pinned version:** Monaco loads from `cdn.jsdelivr.net/npm/monaco-editor@0.57.0`, like the pinned Mermaid URL.
- **Accessible plain-text fallback:** if the Monaco loader fails (offline, CDN blocked), `initializeCodeEditor` creates a labelled `<textarea>` in the same host. Editing, the 10,000-character limit, Tab/Shift+Tab indentation, Esc-then-Tab focus release, Ctrl+S, Ctrl+Alt+R, and source-to-node synchronization still work there. The status bar says that the fallback is active. Folding is unavailable in the fallback.
- **Keyboard parity in Monaco:** Tab indents. Esc switches Monaco to tab-focus mode, so the next Tab leaves the editor. Ctrl+Shift+[ and Ctrl+Shift+] fold and unfold. **Edit → Fold All / Unfold All** fold every block. Fold markers in the gutter are focusable buttons that respond to Enter and Space.

Fold regions come from `IEditorStructureService` (Application), implemented by `RoslynEditorStructureService` (Infrastructure). It only *parses* the source with Roslyn, never compiles it. It returns multi-line brace regions for namespaces, types, methods, `if`/`else`, loops, `switch`, and `try`/`catch`/`finally`. Updates are debounced by 150 ms. If Roslyn returns no regions, the browser falls back to a string- and comment-aware brace matcher (`braceFoldRanges`).

## IDE Workspace Layout

The redesign uses centralized dark design tokens and keeps the source editor as the source of truth. At desktop widths the workspace includes:

- a compact menu/action bar and status bar;
- an activity rail and read-only explorer representation;
- a split source editor and grid-backed flowchart canvas;
- a properties panel populated only from the selected `FlowNodeSourceMap`;
- pointer- and keyboard-resizable Explorer, editor, properties, and output panels, with browser-local size persistence and double-click reset;
- zoom-in, zoom-out, fit, Ctrl+wheel zoom, and background drag-to-pan controls for the rendered SVG;
- an output area for diagnostics, analysis, bounded trace, and the explicitly non-executing terminal state.

Selecting a node in the diagram or text map updates the property panel, highlights the generated Mermaid node, focuses the editor (unfolding the target if needed), and selects the exact source span. Moving the caret into a mapped source span selects the corresponding node. Decision properties show only real graph edges, including their True/False destinations and whether a loop-back edge targets that decision. Smaller widths turn properties into an explicit drawer and stack editor, canvas, feedback, and trace without removing their keyboard-accessible content. “Flowchart focus” hides the editor visually at desktop width but never changes source or graph data.

Source-backed nodes can be edited from the properties panel or by double-clicking the canvas node. The replacement applies only to the node's exact source span and is first translated as a complete candidate document. Empty, stale, oversized, invalid, or unsupported edits are rejected without changing the editor or last valid graph. **Reveal in source** remains as a secondary keyboard-accessible action.

The trace panel also offers optional **Predict before each meaningful step** practice. Before a changed variable or decision branch advances, the learner enters the expected value or True/False path. Answers use trimmed, case-insensitive exact comparison against the already-built bounded trace; feedback explains the expected snapshot without evaluating arbitrary expressions. Automatic Play is unavailable while this deliberate practice mode is enabled, while Back and Reset prepare a fresh prediction for the selected step.

The same Play, Step, Back, Reset, Speed, and Predict state is available in a sticky header directly above the flowchart. A one-line current-step explanation and prediction prompt keep the teaching loop beside the active node; the detailed trace remains available in the lower panel. Each rendered graph auto-fits on load, and trace movement pans the canvas to keep the active node visible. Mermaid nodes use wider label bounds and safe punctuation wrap opportunities, while the original full statement remains available through the node's accessible name and hover title.

Resize handles use native separator semantics. Drag one with a pointer, focus it and use arrow keys, or double-click it to reset its panel. Operation status is indeterminate because analysis and rendering do not provide measured progress. Toasts report only completed success, warning, or failure states; they do not predict outcomes.

The Explorer is a size-aware container rather than a scaled desktop panel. At intermediate widths, file and structure labels stay on one line and truncate with an ellipsis. At the 148-pixel compact threshold, prose and labels are hidden while stable, centered icons remain available; expanding past the threshold restores the text. The panel itself remains bounded between 120 and 300 pixels and scrolls internally, so resizing cannot compress prose into vertical letter stacks or overlap neighboring panels.

## Snippets and Size Limit

The editor loads sample code from the two complete curriculum lessons rather than maintaining duplicate examples in the UI. It uses the shared `CodeAnalysisDefaults.MaximumCharacters` policy through the curriculum manifest, currently 10,000 characters. The same limit controls authoring validation, Roslyn analysis, flowchart translation, EF mappings, the textarea, editor state, and browser draft saves.

The HTML `maxlength` attribute prevents ordinary input beyond the limit. `PlaygroundEditorState` independently rejects oversized restored or programmatic text, and the browser storage adapter refuses oversized saves, so local storage cannot bypass the limit. Live flowchart translation remains debounced by 400 milliseconds and bounded analysis retains its two-second cancellation limit.

## Save and Reset

- **Save browser draft** stores the current text under `CodeLogic.Playground.Draft.v1` in browser local storage.
- **Reset editor** asks for confirmation, then restores the currently loaded curated snippet and removes that saved draft.
- `Ctrl+S` saves while focus is in the editor.
- `Ctrl+Alt+R` resets while focus is in the editor.
- `Tab` inserts four spaces at the cursor or indents every selected line. `Shift+Tab` removes up to four leading spaces from the current or selected lines.
- Press `Esc`, then `Tab` to leave the editor. The following editor visit restores indentation behavior, so CodeLogic does not create a keyboard trap.

The draft is not attached to an Identity account, sent to the database, synced across browsers, or submitted to a server for processing. Clearing browser storage removes it.

## Security and Fail-Closed Behavior

The Playground does not provide Run, Compile, terminal, package restore, file upload, or general .NET execution. Potentially dangerous APIs such as process launch, filesystem access, reflection, networking, and unsupported object creation return an Unsupported diagnostic. They produce no partial trace.

Analysis is limited to 10,000 characters, three control-flow nesting levels, and two seconds. The teaching trace stops after 100 operations. Empty/oversized input, unsupported input, limit exhaustion, and internal failure remain visibly distinct. See [SECURITY.md](SECURITY.md) for the threat model and the disabled-execution decision.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, set `CodeLogic.Web` as the startup project, and press **Ctrl+F5**.
2. Open **Playground** and confirm Variables and Types is initially loaded.
3. Edit the source and select **Save browser draft**. Refresh and confirm the text returns.
4. Select Operators, choose **Load snippet**, change it, and press `Ctrl+S`. Refresh and confirm that one saved draft returns.
5. Press `Ctrl+Alt+R`. Confirm the editor returns to the selected curated snippet and a later refresh no longer restores the old draft.
6. Navigate through the selector, buttons, textarea, and limitation content using only the keyboard.
7. In the editor, press `Tab` at a cursor and across a multi-line selection, then use `Shift+Tab`. Confirm four-space indentation/outdent, correct selection, undo with `Ctrl+Z`, and a refreshed flowchart after the debounce. Press `Esc`, then `Tab`, and confirm focus moves to the next control.
8. Confirm visible focus, labels, live status messages, the `x / 10000 characters` counter, and usable stacked layout at 200% zoom and 320 CSS pixels.
9. Select **Check syntax** for a valid sample, malformed syntax, and unsupported `foreach`. Confirm results are specific and the source text remains available for revision.
10. Preview the default sequence, a nested `if/else`, and each supported loop form. Confirm readable nodes, True/False/repeat arrows, zoom/fit controls, equivalent text connections, selected node properties, SVG highlighting, and **Show line** source selection.
11. Drag each panel separator, resize it with arrow keys, and double-click to reset it. Refresh and confirm adjusted panel sizes persist in this browser.
12. Click a flowchart node and activate one with Enter. Confirm source selection, graph highlight, properties, and text map remain synchronized. Move the editor caret into another mapped statement and confirm the selected node changes.
13. Hold Ctrl while scrolling over the canvas to zoom, and drag empty canvas space to pan. Confirm node activation still works and the displayed zoom value changes.
14. Try `break`, `continue`, excessive nesting, and malformed syntax. Confirm useful error states replace the old preview without losing source.
15. Build a trace for the Variables sample. Confirm Next step exposes the program counter, active node, variable changes, and cumulative output.
16. Reset the trace and confirm it returns to Start with empty state.
17. Trace true and false branches, a bounded loop, and `while (true) { }`. Confirm branch correctness and the 100-operation limit.
18. Confirm there is no Run, Compile, terminal, arbitrary execution, or claim of general .NET behavior.
19. Load the age exercise and check its known cases. Confirm ages 17, 18, and 19 show expected and actual output and that 18 is labeled Boundary.
20. Change `>=` to `>`. Confirm age 18 alone fails with an actionable hint.
21. Enter malformed source. Confirm analysis diagnostics appear separately with no logical verdict.
22. Use `age == 18 || age == 19`. Confirm all known cases pass while the visible limitation warns that untested age 20 can still fail.
23. Enter process, file-write, reflection, and network-client examples. Confirm each fails closed with an Unsupported explanation and no trace steps.

## Known Limitations

- Monaco provides highlighting and folding only. There is no IntelliSense, semantic/type diagnostics, or formatting, and no C# language server is connected.
- Monaco loads from a pinned CDN URL, so the rich editor needs network access. Without it, the plain-text fallback has no highlighting, gutter, or folding.
- Analysis diagnostics outline the matching flowchart nodes after **Check syntax**, but they are not yet shown as squiggles inside Monaco.
- Source-to-node synchronization follows caret and selection changes rather than hover. Diagram nodes provide hover/focus highlights and source-backed tooltips.
- Panel collapse and maximize states reset when the page is reloaded.
- Node properties expose graph type, label, and exact source span. Rich semantic fields such as inferred variable type require a future semantic-analysis contract and are not fabricated.
- Panel sizes persist locally, but the selected sidebar panel, open/closed panels, and split/focus mode reset when the page is reloaded.
- One local draft only; loading another snippet does not create a separate saved file.
- Browser storage can be unavailable in restricted privacy modes.
- Drafts are device/browser-specific and are not account data.
- No arbitrary or curated .NET code execution exists; the trace is a separate limited interpreter.
- Graphs cover the declared sequence, nested branch, and loop subset only; `break`, `continue`, local functions, and `return` are not rendered.
- Infinite-looking conditions are not evaluated; their graphs retain a structural false exit.
- Mermaid loads from a pinned CDN URL, so the SVG preview needs network access on first load; the text map survives renderer failure.
- Traces do not support arrays, element access, live input, methods, `return`, `break`, or `continue`.
- The trace model does not perform full C# semantic or type analysis.
- Logical feedback exists only for the authored age-boundary exercise and its three fixed cases.
- Passing known cases does not prove correctness for untested inputs or detect every logical mistake.
