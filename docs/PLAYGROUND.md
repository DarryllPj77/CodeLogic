# Editor Playground

## Document Status

- Tasks: Prompts 13–16 — editor, diagnostics, graph IR, and browser renderer
- Last updated: 2026-11-01
- Editor: accessible HTML textarea with interactive server UI

## Scope

The Playground provides a small editing workspace for the two complete curriculum samples. A learner can load either curated snippet, edit it, save one browser-local draft, restore that draft after reload, or reset to the selected curated source.

Prompt 14 adds a server-side **Check syntax** action. Prompts 15–16 add graph translation and **Preview flowchart** for supported sequence and one-level `if/else`. CodeLogic still does not compile, execute, or type-check learner code. See [CODE_ANALYSIS.md](CODE_ANALYSIS.md), [FLOWCHART_IR.md](FLOWCHART_IR.md), and [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md).

## Why a Textarea First

The native textarea provides labels, selection, editing, screen-reader support, browser text navigation, zoom behavior, and familiar keyboard interaction without a large JavaScript editor dependency. Monaco remains a possible later enhancement only if syntax highlighting or language-service features justify its integration and accessibility cost. Any later Monaco dependency must use a reviewed, pinned version and preserve an accessible plain-text experience.

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

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, set `CodeLogic.Web` as the startup project, and press **Ctrl+F5**.
2. Open **Playground** and confirm Variables and Types is initially loaded.
3. Edit the source and select **Save browser draft**. Refresh and confirm the text returns.
4. Select Operators, choose **Load snippet**, change it, and press `Ctrl+S`. Refresh and confirm that one saved draft returns.
5. Press `Ctrl+Alt+R`. Confirm the editor returns to the selected curated snippet and a later refresh no longer restores the old draft.
6. Navigate through the selector, buttons, textarea, and limitation content using only the keyboard.
7. Confirm visible focus, labels, live status messages, character count, and usable layout at 200% zoom and 320 CSS pixels.
8. Select **Check syntax** for a valid sample, malformed syntax, and unsupported `foreach`. Confirm results are specific and the source text remains available for revision.
9. Preview the default sequence and a one-level `if/else`. Confirm readable nodes, True/False arrows, equivalent text connections, and **Show line** source selection.
10. Try nested `if/else` and malformed syntax. Confirm useful error states replace the old preview without losing source.
11. Confirm there is no Run, Compile, terminal, execution-trace, or program-output control.

## Known Limitations

- Plain text only; no syntax highlighting, autocomplete, semantic/type diagnostics, or formatting.
- One local draft only; loading another snippet does not create a separate saved file.
- Browser storage can be unavailable in restricted privacy modes.
- Drafts are device/browser-specific and are not account data.
- No arbitrary or curated code execution exists.
- Graphs cover only sequence and one-level `if/else`; loops and nested branches are not rendered.
- Mermaid loads from a pinned CDN URL, so the SVG preview needs network access on first load; the text map survives renderer failure.
- Execution traces are not available.
