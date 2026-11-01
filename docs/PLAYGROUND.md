# Editor Playground

## Document Status

- Tasks: Prompts 13–19 plus the owner-directed IDE workspace redesign
- Last updated: 2026-11-01
- Editor: accessible HTML textarea with interactive server UI

## Scope

The Playground provides a desktop-first IDE workspace for the two complete curriculum samples. A user can load either curated snippet, edit it, save one browser-local draft, restore that draft after reload, or reset to the selected curated source.

Prompt 14 adds **Check syntax**. Prompts 15–17 add **Preview flowchart**. Prompt 18 adds **Build step trace** through CodeLogic's bounded teaching interpreter. Prompt 19 adds instructor-defined logical checks for the known adult-age exercise. See [CODE_ANALYSIS.md](CODE_ANALYSIS.md), [FLOWCHART_IR.md](FLOWCHART_IR.md), [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md), [EXECUTION_TRACES.md](EXECUTION_TRACES.md), and [LOGICAL_FEEDBACK.md](LOGICAL_FEEDBACK.md).

## Why a Textarea First

The native textarea provides labels, selection, editing, screen-reader support, browser text navigation, zoom behavior, and familiar keyboard interaction without a large JavaScript editor dependency. Monaco remains a possible later enhancement only if syntax highlighting or language-service features justify its integration and accessibility cost. Any later Monaco dependency must use a reviewed, pinned version and preserve an accessible plain-text experience.

## IDE Workspace Layout

The redesign uses centralized dark design tokens and keeps the source editor as the source of truth. At desktop widths the workspace includes:

- a compact menu/action bar and status bar;
- an activity rail and read-only explorer representation;
- a split source editor and grid-backed flowchart canvas;
- a properties panel populated only from the selected `FlowNodeSourceMap`;
- zoom-in, zoom-out, and fit controls for the rendered SVG;
- an output area for diagnostics, analysis, bounded trace, and the explicitly non-executing terminal state.

Selecting a source-mapped node updates the property panel, highlights the generated Mermaid node, focuses the textarea, and selects the exact source span. Smaller widths hide secondary panels and stack editor, canvas, feedback, and trace without removing their keyboard-accessible content. “Flowchart focus” hides the editor visually at desktop width but never changes source or graph data.

## Snippets and Size Limit

The editor loads sample code from the two complete curriculum lessons rather than maintaining duplicate examples in the UI. It uses the curriculum `SafeSnippetPolicy.MaximumCharacters` value, currently 1,200 characters.

The HTML `maxlength` attribute prevents ordinary input beyond the limit. `PlaygroundEditorState` independently rejects oversized restored or programmatic text, so browser storage cannot bypass the limit.

## Save and Reset

- **Save browser draft** stores the current text under `CodeLogic.Playground.Draft.v1` in browser local storage.
- **Reset editor** restores the currently loaded curated snippet and removes that saved draft.
- `Ctrl+S` saves while focus is in the editor.
- `Ctrl+Alt+R` resets while focus is in the editor.
- Tab retains its native behavior and moves focus out of the textarea; CodeLogic does not create a keyboard trap.

The draft is not attached to an Identity account, sent to the database, synced across browsers, or submitted to a server for processing. Clearing browser storage removes it.

## Security and Fail-Closed Behavior

The Playground does not provide Run, Compile, terminal, package restore, file upload, or general .NET execution. Potentially dangerous APIs such as process launch, filesystem access, reflection, networking, and unsupported object creation return an Unsupported diagnostic. They produce no partial trace.

Analysis is limited to 1,200 characters, three control-flow nesting levels, and two seconds. The teaching trace stops after 100 operations. Empty/oversized input, unsupported input, limit exhaustion, and internal failure remain visibly distinct. See [SECURITY.md](SECURITY.md) for the threat model and the disabled-execution decision.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, set `CodeLogic.Web` as the startup project, and press **Ctrl+F5**.
2. Open **Playground** and confirm Variables and Types is initially loaded.
3. Edit the source and select **Save browser draft**. Refresh and confirm the text returns.
4. Select Operators, choose **Load snippet**, change it, and press `Ctrl+S`. Refresh and confirm that one saved draft returns.
5. Press `Ctrl+Alt+R`. Confirm the editor returns to the selected curated snippet and a later refresh no longer restores the old draft.
6. Navigate through the selector, buttons, textarea, and limitation content using only the keyboard.
7. Confirm visible focus, labels, live status messages, character count, and usable stacked layout at 200% zoom and 320 CSS pixels.
8. Select **Check syntax** for a valid sample, malformed syntax, and unsupported `foreach`. Confirm results are specific and the source text remains available for revision.
9. Preview the default sequence, a nested `if/else`, and each supported loop form. Confirm readable nodes, True/False/repeat arrows, zoom/fit controls, equivalent text connections, selected node properties, SVG highlighting, and **Show line** source selection.
10. Try `break`, `continue`, excessive nesting, and malformed syntax. Confirm useful error states replace the old preview without losing source.
11. Build a trace for the Variables sample. Confirm Next step exposes the program counter, active node, variable changes, and cumulative output.
12. Reset the trace and confirm it returns to Start with empty state.
13. Trace true and false branches, a bounded loop, and `while (true) { }`. Confirm branch correctness and the 100-operation limit.
14. Confirm there is no Run, Compile, terminal, arbitrary execution, or claim of general .NET behavior.
15. Load the age exercise and check its known cases. Confirm ages 17, 18, and 19 show expected and actual output and that 18 is labeled Boundary.
16. Change `>=` to `>`. Confirm age 18 alone fails with an actionable hint.
17. Enter malformed source. Confirm analysis diagnostics appear separately with no logical verdict.
18. Use `age == 18 || age == 19`. Confirm all known cases pass while the visible limitation warns that untested age 20 can still fail.
19. Enter process, file-write, reflection, and network-client examples. Confirm each fails closed with an Unsupported explanation and no trace steps.

## Known Limitations

- Plain text only; no syntax highlighting, autocomplete, semantic/type diagnostics, or formatting.
- The split-pane host is ready for a future reviewed Monaco integration, but Monaco is not installed in this redesign.
- Panels use responsive CSS layouts rather than pointer-driven drag resizing; explicit drag handles are deferred.
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
