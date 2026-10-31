# Accessibility Smoke Checks

## Document Status

- Task: Prompt 08 — Lesson reading UI
- Last checked: 2026-10-31
- Scope: Home, Lessons, lesson detail and recovery states, Playground, Progress, not-found page, and shared navigation shell
- Standard target: WCAG 2.2 AA for the core learning experience

These checks cover the application shell and the display-only lesson reading experience. Every later interactive feature needs its own keyboard, screen-reader, zoom, contrast, error, and reduced-motion review.

## Implemented Accessibility Foundations

- A “Skip to main content” link is the first interactive element in the layout.
- The shared layout uses `header`, labeled `nav`, `main`, and `footer` landmarks.
- Every routed page has one primary `h1` and a meaningful page title.
- Blazor's `FocusOnNavigate` moves focus to the new page heading after navigation.
- Keyboard focus uses a high-visibility outline and is not communicated by color alone.
- Navigation works through ordinary links without requiring a pointer or client-side JavaScript.
- Navigation wraps on medium screens and becomes a two-column list on narrow screens.
- Content grids collapse to one column on narrow screens.
- Code examples scroll inside their own region rather than forcing page-wide horizontal scrolling.
- Lesson details use a breadcrumb, one primary heading, ordered section headings, a labeled code example, and descriptive previous/next links.
- The practice preview and completion stub state clearly that they are not interactive and do not save learner data.
- Status words such as “Planned,” “Preview only,” and “No progress data” accompany color styling.
- The playground safety message is available as text rather than only as a visual treatment.
- Motion is non-essential and reduced when `prefers-reduced-motion` is enabled.
- Forced-colors mode receives an explicit focus outline color.
- The error banner uses an alert role and assertive live region.

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
| Unknown route is recoverable | Unknown paths render the not-found page with a home link | Pass |
| Build and analyzer checks | Solution builds with warnings treated as errors | Pass |

## Manual Browser Checks for Owner Review

Perform these checks in Microsoft Edge or Chrome before accepting Prompt 08:

1. Press `Tab` immediately after loading a page. Confirm the skip link appears and moves focus to the main heading when activated.
2. Continue pressing `Tab`. Confirm Home, Lessons, Playground, Progress, and page actions receive a visible focus outline in a logical order.
3. Activate every primary navigation link with `Enter`. Confirm the correct page and browser title appear.
4. At 200% browser zoom, confirm content remains readable and actions do not overlap.
5. Resize the viewport to 320 CSS pixels wide. Confirm there is no page-level horizontal scrolling; the code sample may scroll inside its own block.
6. Enable a reduced-motion preference. Confirm the interface remains understandable without animation.
7. Enable Windows high contrast or browser forced-colors mode. Confirm text, links, borders, and focus remain visible.
8. Visit an unknown path such as `/missing-page`. Confirm the recovery message and “Return home” link are understandable.
9. Check that “Planned,” “Preview only,” and empty-progress states remain understandable without relying on their colors.
10. Open both complete lessons. Confirm the objective, explanations, code, practice preview, completion stub, and previous/next links remain readable at desktop width and 320 CSS pixels.
11. Follow the lesson previous/next links with the keyboard and confirm the focused lesson heading has a visible outline.
12. Visit `/lessons/loops` and `/lessons/missing-lesson`. Confirm both unavailable states explain the problem and provide a return path.

## Deferred Accessibility Checks

The following checks belong to later feature tasks because their controls do not exist yet:

- editor labels, keyboard commands, error association, and code-reading experience;
- exercise feedback announcements and retry focus management;
- lesson completion controls and progress semantics;
- Mermaid diagram alternatives and equivalent text traces;
- authenticated session timeout and identity-form errors;
- automated browser accessibility tooling such as Playwright plus axe.

Deferred checks are not waived. They must be added when the relevant feature becomes interactive.
