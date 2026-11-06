# Accessibility Smoke Checks

## Document Status

- Implemented through: Prompt 26 plus the owner-directed IDE workspace redesign
- Last checked: 2026-11-01
- Scope: Home, Lessons, lesson detail and deterministic quiz feedback, account forms, saved progress dashboard, lesson authoring and preview, recovery states, Playground, not-found page, and shared navigation shell
- Standard target: WCAG 2.2 AA for the core learning experience

These checks cover the application shell, lesson reading, deterministic practice forms, and temporary progress dashboard. Every later interactive feature needs its own keyboard, screen-reader, zoom, contrast, error, and reduced-motion review.

## Implemented Accessibility Foundations

- A “Skip to main content” link is the first interactive element in the layout.
- The shared layout uses `header`, labeled `nav`, `main`, and `footer` landmarks.
- Every routed page has one primary `h1` and a meaningful page title.
- Blazor's `FocusOnNavigate` moves focus to the new page heading after navigation.
- Keyboard focus uses a high-visibility outline and is not communicated by color alone.
- The compact application menu and workspace actions use ordinary links or native buttons and do not require a pointer.
- Secondary panels collapse at narrower widths; editor, canvas, feedback, and trace remain in document order and stack on small screens.
- Content grids collapse to one column on narrow screens.
- Code examples scroll inside their own region rather than forcing page-wide horizontal scrolling.
- Lesson details use a breadcrumb, one primary heading, ordered section headings, a labeled code example, and descriptive previous/next links.
- Multiple-choice answers use a grouped `fieldset` and `legend`; value answers use an explicitly labeled text input.
- Quiz controls use native radio, text-input, and submit-button semantics and remain usable without client-side JavaScript.
- Correct, incorrect, and required states use text headings and feedback rather than color alone; the result region has status semantics.
- Practice states clearly that answers are compared as text, raw answers are not stored, and saved outcomes belong to the signed-in account.
- Progress uses a native `progress` element plus an equivalent text percentage and numeric summary.
- Lesson and attempt states include explicit words such as “Complete,” “Not complete,” “Correct,” and “Incorrect.”
- Attempt history is an ordered list with exercise, lesson, attempt number, outcome, and UTC time in text.
- The dashboard explains that session state can disappear and is not connected to an account or database.
- Status words such as “Planned,” “Preview only,” and “No progress data” accompany color styling.
- The playground safety message is available as text rather than only as a visual treatment.
- Playground analysis results use a heading, diagnostic codes, line/column text, explanations, and a polite live status; meaning does not depend on color.
- Every flowchart has an ordered node list and explicit connection list; True/False meaning is available in text, not only arrow position or color.
- Native **Show line** buttons connect source-backed nodes to the exact editor selection without requiring Mermaid click directives.
- Node selection is represented in the equivalent text map, property text, source selection, and a high-contrast SVG outline rather than color alone.
- Flowchart zoom and fit are native buttons with accessible names and a text zoom value.
- Generated SVG nodes are focusable buttons with node type, label, and source-line names; Enter and Space perform the same synchronized selection as a pointer click.
- Panel resize handles use separator roles, orientation and value metadata, keyboard arrows, visible focus, and double-click reset. Resizing is not pointer-only.
- The Explorer's compact-width state hides visual labels without removing each control's accessible name or title; expanding restores visible text while icons remain stable throughout resizing.
- The responsive node-properties panel is opened and closed by a native button with `aria-expanded`; no core graph detail exists only in the drawer.
- In-progress analysis uses an indeterminate busy state and text rather than fabricated percentages. Completion notifications use status or alert semantics and can be dismissed.
- Resetting the editor and deleting its browser-local draft requires explicit confirmation; cancelling preserves both source and saved data.
- The SVG is labeled as an image, remains inside a horizontally scrollable panel, and renderer failures retain the text equivalent.
- Motion is non-essential and reduced when `prefers-reduced-motion` is enabled.
- Forced-colors mode receives an explicit focus outline color.
- The error banner uses an alert role and assertive live region.
- Authoring fields use explicit labels, native controls, visible character limits, text validation errors, status announcements, and a responsive single-column layout at narrow widths.
- Draft, Published, version, and read-only states are written in text rather than communicated by color alone.

## Automated and Markup Smoke Checks

| Check | Expected result | Latest result |
| --- | --- | --- |
| All primary routes return successfully | `/`, `/lessons`, `/playground`, and `/progress` return HTTP 200 | Pass |
| Page titles are unique and meaningful | Each route contains a route-specific `<title>` | Pass |
| One primary heading per page | Each route contains one `<h1>` | Pass |
| Landmarks are present | Shared HTML contains `header`, labeled `nav`, `main`, and `footer` | Pass |
| Skip link target exists | `href="#main-content"` matches `id="main-content"` | Pass |
| Navigation destinations exist | All four primary navigation links return HTTP 200 | Pass |
| Complete lessons are navigable | Both seeded lesson links and detail routes return HTTP 200 with one `<h1>` | Pass |
| Lesson sequence is understandable | First and second lessons expose the correct previous/next destinations and edge messages | Pass |
| Missing lesson is recoverable | Draft and unknown lesson slugs show an unavailable message and return-to-lessons link | Pass |
| Quiz forms have accessible names | Choice groups have a legend and value input has a label | Pass |
| Feedback states are explicit | Correct, incorrect, and missing-answer results have distinct text and status semantics | Pass |
| Practice works without script execution | Native POST forms produce server-rendered deterministic feedback | Pass |
| Progress has a text equivalent | Completion count, percentage, attempt totals, and lesson states accompany visual styling | Pass |
| Session isolation is honest | A separate cookie session starts at zero while the learner session retains its attempts | Pass |
| Unknown route is recoverable | Unknown paths render the not-found page with a home link | Pass |
| Build and analyzer checks | Solution builds with warnings treated as errors | Pass |

## Manual Browser Checks for Owner Review

Perform these checks in Microsoft Edge or Chrome before accepting Prompt 10:

1. Press `Tab` immediately after loading a page. Confirm the skip link appears and moves focus to the main heading when activated.
2. Continue pressing `Tab`. Confirm application-menu links, workspace actions, activity-rail links, editor controls, canvas controls, and output controls receive a visible focus outline in a logical order.
3. Activate File, View, Analysis, and Settings links with `Enter`. Confirm each moves to its named workspace region.
4. At 200% browser zoom, confirm content remains readable and actions do not overlap.
5. Resize the viewport to 320 CSS pixels wide. Confirm there is no page-level horizontal scrolling; the code sample may scroll inside its own block.
6. Enable a reduced-motion preference. Confirm the interface remains understandable without animation.
7. Enable Windows high contrast or browser forced-colors mode. Confirm text, links, borders, and focus remain visible.
8. Visit an unknown path such as `/missing-page`. Confirm the recovery message and “Return home” link are understandable.
9. Check that “Planned,” “Preview only,” and empty-progress states remain understandable without relying on their colors.
10. Open both complete lessons. Confirm the objective, explanations, code, practice preview, completion stub, and previous/next links remain readable at desktop width and 320 CSS pixels.
11. Follow the lesson previous/next links with the keyboard and confirm the focused lesson heading has a visible outline.
12. Visit `/lessons/loops` and `/lessons/missing-lesson`. Confirm both unavailable states explain the problem and provide a return path.
13. On Variables and Types, select each radio answer using only the keyboard and submit. Confirm focus remains visible and the result is understandable without its color.
14. On Operators, enter ` TRUE `, `false`, and an empty value. Confirm correct, incorrect, and browser-required states are clear.
15. Disable JavaScript and repeat one answer on each pilot. Confirm the native forms still return deterministic feedback.
16. Follow `docs/END_TO_END_TEST.md`. Confirm an incorrect answer adds an attempt without completion and a correct answer updates the lesson and dashboard.
17. Review the dashboard at 200% zoom and 320 CSS pixels. Confirm summary cards, lesson cards, attempt history, and the recommended-next action remain readable.
18. Open a separate InPrivate window. Confirm progress requires sign-in and a second account cannot see the first learner's data.
19. Open Playground and reach the snippet selector, load button, textarea, save button, and reset button using only `Tab` and `Shift+Tab`.
20. Confirm Tab leaves the textarea instead of inserting indentation or trapping focus.
21. With focus in the editor, confirm `Ctrl+S` saves and `Ctrl+Alt+R` resets; verify the live status text announces each result.
22. Enter text up to the displayed 1,200-character limit and confirm the character count remains understandable without color.
23. At 200% zoom and 320 CSS pixels, confirm the editor, controls, shortcuts, limitations, and status remain readable without page-level horizontal scrolling.
24. Activate **Check syntax** using only the keyboard for valid, malformed, and unsupported source. Confirm the result heading, code, line/column, message, and explanation are readable and announced without relying on color.
25. Edit the source after analysis and confirm stale feedback disappears without losing the current text.
26. Preview sequence, nested branching, and loops using only the keyboard. Confirm the visual, ordered nodes, and explicit True/False/Repeat connection text agree.
27. Activate **Show line** and confirm focus returns to the textarea with the corresponding source selected.
28. At 200% zoom and 320 CSS pixels, confirm the SVG scrolls inside its panel and the text map remains readable without page-level horizontal scrolling.
29. Select a source-backed node and confirm its properties, text-map selection, SVG outline, and exact textarea selection agree. Use zoom in/out/fit with keyboard only.
30. Focus every panel separator. Use its arrow keys and confirm the panel changes size, `aria-valuenow` changes, and focus remains visible. Double-click each handle and confirm it resets.
31. Activate a generated diagram node with Enter and Space, then move the editor caret into another mapped statement. Confirm the source, diagram, text map, and property panel synchronize without pointer-only interaction.
32. At a tablet width, open and close Node properties using its labeled button; confirm focus remains visible and the drawer does not hide the underlying text alternative.
33. Test unsupported `break`, malformed input, and a blocked renderer. Confirm each state has text feedback and does not depend on the SVG.
34. Build a step trace using only the keyboard. Confirm program counter, active node, variables, output, and explanation are exposed as text.
35. Activate **Next step** and **Reset trace**. Confirm the live status changes and reset returns to the empty Start state.
36. At 200% zoom and 320 CSS pixels, confirm the three trace-state sections stack without page-level horizontal scrolling.
37. Load and check the age-boundary exercise using only the keyboard. Confirm each case exposes input, expected output, actual output, Pass/Fail text, and the Boundary label.
38. Trigger a failing boundary and confirm its hint is readable without relying on color.
39. Trigger malformed source and confirm syntax/support diagnostics are presented separately from logical results.
40. At 200% zoom and 320 CSS pixels, confirm the logical-results table scrolls inside its container without page-level horizontal scrolling.
41. As an administrator, reach every authoring field and action using only the keyboard; confirm labels, focus, validation errors, and save/publish status are announced clearly.
42. At 200% zoom and 320 CSS pixels, confirm the owned-lesson list, form, private preview, and published lesson stack without page-level horizontal scrolling.

## Deferred Accessibility Checks

The following checks belong to later feature tasks because their controls do not exist yet:

- retry focus management beyond native page navigation and enhanced screen-reader announcement testing;
- enhanced screen-reader testing of Mermaid SVG output; the equivalent structural text map is implemented;
- authenticated session timeout and identity-form errors;
- automated browser accessibility tooling such as Playwright plus axe.

Deferred checks are not waived. They must be added when the relevant feature becomes interactive.
