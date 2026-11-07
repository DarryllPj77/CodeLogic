# Editor Playground

## Document Status

- Tasks: Prompts 13–19 plus the owner-directed IDE workspace redesign
- Last updated: 2026-11-07 (classic programs, What's supported, non-scrolling workspace layout)
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

`/playground` uses its own lean `WorkspaceLayout` and draws one non-scrolling viewport. `html` and `body` are locked to the viewport height only while the workspace is on screen (`html:has(.ws)`), so Lessons, Progress, Login, and Authoring keep the shared `MainLayout` header and normal page scrolling. Navigation in the workspace happens through buttons and tabs. Only panel bodies scroll.

From top to bottom and left to right:

- **Header:** brand, an **Edit** menu (Fold All / Unfold All), a centered **Analyze Code / Flowchart focus** mode switch, a *Safe sandbox · Not executed* pill, the account card (*Guest · Sign in to save progress*, or your name), and **Exit**. For a guest, Exit returns home; when signed in, it posts to `/account/logout`.
- **Left rail:** toggles the collapsible Explorer / Code structure sidebar, opens Diagnostics, and focuses the editor.
- **Editor column:** the *Monaco C# editor* panel (snippet picker, **Load snippet**, `Program.cs` tag, **Light mode** editor theme, **FULL**, collapse chevron), the editor body that fills the remaining height, and a footer. The footer holds the Tab/Esc hint, the shortcuts, the `x / 10,000` counter, the dominant yellow **Analyze code** button, and **Check syntax**, **Preview flowchart**, **Build step trace**, **Save browser draft**, and **Reset editor**.
- **Analysis panel (collapsed by default):** below the editor, behind a draggable splitter. Its tabs are **Diagnostics** (with a count badge and the syntax-result card), **Analysis output** (status plus a one-line summary of each feature), **Execution trace** (active operation, variables, console output), **Logic checks** (the instructor-defined age exercise), and **Terminal** (the non-executing safe state).
- **Control flow graph panel:** a header with node count and safety status, then one toolbar row (Play, Step, Back, Reset, Speed, Predict | zoom −, %, +, Fit | Legend | FULL) that wraps only when it lacks room. Below it are the step-explanation line, the canvas, which fills the remaining height, and a collapsible equivalent text map. With no graph, the canvas reads "Run ANALYZE CODE to generate the Control Flow Graph".
- **Node Properties drawer:** slides over the right edge of the canvas when a node is clicked, inspected from the text map, or chosen in Code structure. Moving the editor caret still highlights the matching node, but it never opens the drawer, so typing does not cover the graph.
- **Right activity bar:** Settings (light editor theme, Explorer, reset panel sizes), Save browser draft, Snippets (focuses the snippet picker), and Help (shortcuts and the safety model and limitations, previously shown above the workspace).
- **Status bar:** *Ready* or *Working*, the latest status message, and *Deterministic analysis · No arbitrary .NET execution*.

**Analyze code** runs the syntax check and builds the graph. It opens Diagnostics only when there is something to fix. **Check syntax** always opens Diagnostics; **Check known cases** and **Load age exercise** open Logic checks; a failed trace opens Execution trace.

**FULL** places its panel over the work area between the two icon bars, and the button changes to **EXIT FULL**. An open Explorer or Code Structure sidebar stays visible beside a FULL panel (in the one-column layout it overlays above it), and its rail icon stays highlighted.

**Code view.** Flowchart focus and FULL on the flowchart both show the live code (about 40%, at least 320px; the splitter width is saved separately) beside the flowchart. A vertical toolbar on the editor's edge replaces the snippet bar, button rows, and Analysis panel: **Run** (Analyze code), **Step** (builds the trace first if needed), **Back**, **Reset**, **Predict**, **Snippet** (a list of the curated snippets), **Save** (browser draft), and **Restore** (leave FULL) or **Exit** (leave Flowchart focus). The toolbar sits outside the editor's scroll area, so it stays visible while the code scrolls. In the code view the activity bar adds a **×** button under the ⌨ (Focus source editor) icon: it closes the **Monaco C# Editor** panel so the flowchart uses the full width, and changes to **⇥** to reopen it. ⌨ also reopens and focuses the editor. Closing only hides the panel, so the code, cursor line, trace step, node selection, and zoom are kept. Leaving or re-entering the code view shows the editor again. Tab/Shift+Tab, the 10,000-character limit, and Ctrl+S still work. In the one-column layout (below about 1100px) FULL on the flowchart still shows the chart alone. Each trace step highlights the same line and node, and dims the lines and nodes of the branch that was not taken (`TraceBranchHighlighter`; entering a loop body dims nothing). Clicking a node scrolls the code to its line, and moving the caret selects the node. The Explorer keeps whatever state you chose in both FULL and Flowchart focus. **Esc** pressed outside the editor and form fields closes the most temporary layer first: Help or Settings, then a FULL panel, then Node Properties. Esc inside Monaco keeps its tab-release meaning. These rules live in `PlaygroundLayoutState` and are covered by `PlaygroundLayoutStateTests`.

Below 1,100 CSS pixels the main area becomes one column. **Editor & analysis / Flowchart** buttons choose the visible column, and the Explorer opens as an overlay. The page still does not scroll. Below 768 pixels the header keeps only the brand mark, account avatar, and Exit.

Graphs open at a readable 100% (small graphs are fitted, up to 125%) and scroll inside the canvas; **Fit** and **Fit width** are available on request. **⤒ Start** / **⤓ End**, Home/End, arrow keys, `+`/`-`, Ctrl+wheel (zoom at the pointer), Shift+wheel, and drag-to-pan move around large graphs. Stepping a trace keeps the active node in view without re-fitting. Nodes can be dragged (or moved with arrow keys) to tidy the chart without touching the code; **Auto layout** puts them back. The **Legend** button opens the right-hand drawer on its Legend tab; the same drawer shows Node Properties. The editor column now starts at 440px so the canvas gets more width. See [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md#readable-large-graphs). Workspace mode, editor theme, Explorer visibility, and splitter sizes are saved in `CodeLogic.Playground.Workspace.v1`.

Source-backed nodes can be edited from the properties panel or by double-clicking the canvas node. The replacement applies only to the node's exact source span and is first translated as a complete candidate document. Empty, stale, oversized, invalid, or unsupported edits are rejected without changing the editor or last valid graph. **Reveal in source** remains as a secondary keyboard-accessible action.

The flowchart toolbar also offers optional **Predict** practice before each meaningful step. Before a changed variable or decision branch advances, the learner enters the expected value or True/False path. Answers use trimmed, case-insensitive exact comparison against the already-built bounded trace; feedback explains the expected snapshot without evaluating arbitrary expressions. Automatic Play is unavailable while this deliberate practice mode is enabled, while Back and Reset prepare a fresh prediction for the selected step.

Play, Step, Back, Reset, Speed, and Predict live only in the flowchart toolbar. The one-line step explanation and the prediction prompt sit directly under it, beside the active node. The Execution trace tab shows the full snapshot. Trace movement pans the canvas to keep the active node visible. Mermaid nodes use wider label bounds and safe punctuation wrap opportunities, while the original full statement remains available through the node's accessible name and hover title.

Resize handles use native separator semantics. Drag one with a pointer, focus it and use arrow keys, or double-click it to reset its panel. Operation status is indeterminate because analysis and rendering do not provide measured progress. Toasts report only completed success, warning, or failure states; they do not predict outcomes.

The Explorer is a size-aware container rather than a scaled desktop panel. At intermediate widths, file and structure labels stay on one line and truncate with an ellipsis. At the 148-pixel compact threshold, prose and labels are hidden while stable, centered icons remain available; expanding past the threshold restores the text. The panel itself remains bounded between 120 and 300 pixels and scrolls internally, so resizing cannot compress prose into vertical letter stacks or overlap neighboring panels.

## Classic Programs, Snippets, and What's Supported

New files, **Reset editor**, and the default snippet all use the classic template (`using System;`, `namespace MyProgram`, `class Program`, `static void Main(string[] args)`). **Edit → New classic program / New top-level program** start blank programs in either form. A badge next to the editor title shows the detected form: *Classic program*, *Top-level statements*, or *Mixed: two entry points*.

The curated snippets live in `PlaygroundSnippets` (Web), separate from lesson content: Hello World, variables, if/else if/else, while, for, do/while, arrays and foreach, a static method with parameters, and one *(top-level mode)* example. Lesson pages keep their own samples.

**What's supported** (editor footer, Edit menu, Help dialog, and next to every diagnostic) opens a dialog generated from `SupportedFeatureRegistry`. It has three sections: Program structure, Statements, and Not supported yet. Each item has an example and a **Load example** button. Loading an unsupported example runs Check syntax so its diagnostic appears straight away. A diagnostic's button opens the dialog at its own entry.

The flowchart legend shows dotted call edges (*Calls a method*) and thick *break / continue* jump edges.

Diagnostics show the code and position, then **what** is wrong, **Why**, and **Instead**. For `FL1002` in a classic program, **Rewrite as static method** moves the local function into the class. Ctrl+Z undoes the rewrite.

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
- Panel collapse, FULL, the active Analysis tab, and the compact view reset when the page is reloaded; mode, editor theme, Explorer visibility, and sizes persist.
- Node properties expose graph type, label, and exact source span. Rich semantic fields such as inferred variable type require a future semantic-analysis contract and are not fabricated.
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
