# Editor Playground

## Document Status

- Tasks: Prompt 13 — Editor playground UI; Prompt 14 — Roslyn parse and diagnostics
- Last updated: 2026-11-01
- Editor: accessible HTML textarea with interactive server UI

## Scope

The Playground provides a small editing workspace for the two complete curriculum samples. A learner can load either curated snippet, edit it, save one browser-local draft, restore that draft after reload, or reset to the selected curated source.

Prompt 14 adds a server-side **Check syntax** action. It parses the current text with Roslyn and validates a fixed learning subset, but does not compile, execute, type-check, or visualize learner code. See [CODE_ANALYSIS.md](CODE_ANALYSIS.md) for the exact subset and limits.

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
9. Confirm there is no Run, Compile, Visualize, terminal, or program-output control.

## Known Limitations

- Plain text only; no syntax highlighting, autocomplete, semantic/type diagnostics, or formatting.
- One local draft only; loading another snippet does not create a separate saved file.
- Browser storage can be unavailable in restricted privacy modes.
- Drafts are device/browser-specific and are not account data.
- No arbitrary or curated code execution exists.
- Graphs and traces are not available yet.
