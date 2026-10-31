# CodeLogic Build Roadmap

## Roadmap Status

- Last updated: 2026-10-31
- Current completed milestone: Prompt 07 — Curriculum specification
- Exact next numbered task: Prompt 08 — Lesson reading UI
- Source coverage: Prompts 00–10 have been supplied; Prompts 11–29 have not yet been added to the repository

## How to Use This Roadmap

1. Work on one numbered prompt at a time.
2. Read `AGENTS.md` and this roadmap before starting.
3. Review the plan and expected file list before edits.
4. Review the final diff, verification evidence, limitations, and manual test steps.
5. Commit and push only with explicit owner approval.
6. Mark a task complete only after its acceptance gate is satisfied.
7. Do not start the next prompt until the owner approves proceeding.

## Status Definitions

| Status | Meaning |
| --- | --- |
| Complete | Acceptance evidence exists in the repository and the change has been committed. |
| Next | The next task eligible for execution after owner approval. |
| Planned | The prompt is known but must not be started yet. |
| Awaiting definition | The playbook refers to the number, but its exact prompt has not been supplied. |

## Numbered Build Sequence

| Prompt | Task | Status | Primary evidence or acceptance gate |
| --- | --- | --- | --- |
| 00 | Codex operating contract and repository inventory | Complete | `docs/CODEX_WORKFLOW.md` records workflow, inventory, unknowns, and owner decision rights. |
| 01 | Product charter and scope | Complete | `docs/PRODUCT.md` defines users, outcomes, accessible MVP, deterministic learning, deferred features, and non-goals. |
| 02 | Architecture decisions and six-level mapping | Complete | `docs/ARCHITECTURE.md` and ADRs define boundaries, tradeoffs, diagrams, and extraction seams. |
| 03 | Windows and Visual Studio setup | Complete | `docs/SETUP_WINDOWS.md` records validated prerequisites and exact optional gaps without installing software. |
| 04 | Solution skeleton | Complete | Solution restores, builds, tests, runs locally, and loads through Visual Studio. |
| 05 | Design system and application shell | Complete | Pages render, navigation works, and accessibility smoke checks are documented. |
| 06 | Core domain model | Complete | Domain tests prove invariants and the Domain project has no framework dependencies. |
| 07 | Curriculum specification | Complete | Curriculum schema validates and two complete pilot lessons display. |
| 08 | Lesson reading UI | **Next** | Two seeded lessons are navigable, responsive, and tested as feasible. |
| 09 | Deterministic quiz evaluator | Planned | Correct, incorrect, and boundary tests pass with no arbitrary user-code execution. |
| 10 | First end-to-end learning vertical slice | Planned | A student completes a lesson and sees updated in-session progress. |
| 11–29 | Remaining playbook tasks | Awaiting definition | Add the owner's exact prompt text before planning or implementation. |

## Completed Foundation

### Prompt 00 — Codex operating contract and repository inventory

Completed outcomes:

- established the one-prompt-one-task workflow;
- recorded approval boundaries and task evidence requirements;
- inventoried the initially empty repository and local .NET environment;
- connected the local repository to the owner's GitHub remote.

### Prompt 01 — Product charter and scope

Completed outcomes:

- defined primary and secondary users and the problems CodeLogic solves;
- defined beginner learning outcomes and the end-to-end learning journey;
- prioritized lessons, exercises, a simple editor, deterministic visualization, accessibility, and honest progress;
- explicitly deferred arbitrary execution, AI dependency, advanced IDE features, and distributed infrastructure;
- established measurable MVP acceptance criteria.

### Prompt 02 — Architecture decisions and six-level mapping

Completed outcomes:

- selected a modular monolith with Domain, Application, Infrastructure, and Web boundaries;
- selected Blazor Web App and ASP.NET Core;
- chose EF Core with SQLite for local development and provider independence for hosted environments;
- planned Roslyn analysis, a custom graph IR, and Mermaid as the first renderer;
- documented why broad CQRS, microservices, and Kubernetes are premature;
- recorded future extraction seams and five proposed ADRs.

The ADRs remain proposed until the owner explicitly accepts or revises their decisions.

### Prompt 03 — Windows and Visual Studio setup

Completed outcomes:

- verified Visual Studio Community 2026 and the ASP.NET/web workload;
- verified .NET 10 SDK and ASP.NET Core runtime availability;
- verified Git, supported browsers, SQLite CLI, and EF Core CLI;
- recorded Docker Desktop as optional and currently absent;
- confirmed no required prerequisite is missing for Prompt 04.

## Completed Solution Skeleton

### Prompt 04 — Solution skeleton

Completed scope:

- create `CodeLogic.slnx`, or `.sln` only if the IDE requires it;
- create `src/CodeLogic.Web` as the minimal executable Blazor Web App;
- create `src/CodeLogic.Domain`;
- create `src/CodeLogic.Application`;
- create `src/CodeLogic.Infrastructure`;
- create `tests/CodeLogic.UnitTests`;
- create `tests/CodeLogic.IntegrationTests`;
- target .NET 10 with nullable reference types and analyzers;
- add central project settings only when they provide a clear benefit;
- provided a minimal executable home page without beginning the design-system task;
- added shared nullable and analyzer settings, SDK selection, and repository ignores;
- established inward project references and two test-project smoke checks.

Acceptance gate:

- solution restores successfully;
- all six projects build with zero warnings and zero errors;
- both smoke tests pass;
- the Blazor home page returns HTTP 200 with the expected content;
- Visual Studio Community 2026 loads the `.slnx` and builds all six projects.

Prompt 04 must not implement design-system navigation, domain features, curriculum, lesson UI, quizzes, persistence, code analysis, or deployment.

## Completed Application Shell

### Prompt 05 — Design system and application shell

Completed outcomes:

- created responsive primary navigation and a shared header, main, and footer shell;
- expanded the home page with clear entry points and honest feature previews;
- added Lessons, Playground, and Progress placeholder routes without backend state;
- established accessible color, spacing, typography, focus, reduced-motion, and mobile design tokens;
- added recoverable not-found and application-error experiences;
- documented automated, markup, and manual accessibility smoke checks in `docs/ACCESSIBILITY.md`;
- verified all primary routes, navigation destinations, page titles, headings, landmarks, and unknown-route recovery.

Acceptance gate:

- all four primary pages return HTTP 200;
- navigation destinations render the expected page content;
- the responsive shell passes desktop visual review and includes narrow-screen breakpoints;
- solution builds with zero warnings and both existing smoke tests pass;
- accessibility foundations and owner-review steps are documented.

Prompt 05 does not implement lessons, exercises, an editor, visualization, progress storage, or authentication.

## Completed Core Domain Model

### Prompt 06 — Core domain model

Completed outcomes:

- introduced typed identifiers for topics, lessons, lesson sections, exercises, and attempts;
- implemented `Topic`, `Lesson`, `LessonSection`, `Exercise`, `ExerciseAttempt`, and `ProgressSnapshot`;
- encoded required text, identifier, ordering, uniqueness, count consistency, enum, and UTC timestamp invariants;
- kept Lesson ownership limited to its sections while exercises reference `LessonId` independently;
- added focused positive, negative, boundary, immutability, and dependency tests;
- documented the model and explicitly deferred evaluator, persistence, repository, identity, and domain-event concerns.

Acceptance gate:

- all 23 domain unit tests pass;
- full solution has 24 passing tests and zero failures;
- Domain builds with zero warnings and zero errors;
- Domain has no package or project references;
- an architecture test rejects ASP.NET Core, EF Core, Roslyn, Web, Infrastructure, and Application references.

## Completed Curriculum Specification

### Prompt 07 — Curriculum specification

Completed outcomes:

- created a versioned typed curriculum manifest and catalog contract in Application;
- reused Domain topics, lessons, sections, and exercises in the seed;
- seeded complete Variables and Types and Operators pilot specifications;
- added Input/Output, Sequence, Branching, Loops, Methods, Algorithms, and Debugging drafts;
- defined objectives, sample code, quiz criteria, difficulty, ordering, and allowed concepts for every lesson;
- defined a global static-analysis-only safe snippet policy and forbidden capabilities;
- added semantic schema validation and negative safety/schema tests;
- rendered both pilots and all seven drafts from the manifest on the Lessons page.

Acceptance gate:

- manifest validation passes for all nine topics;
- exactly two complete pilots and seven drafts are present;
- four focused curriculum integration tests pass;
- `/lessons` returns HTTP 200 and displays both pilot specifications and samples;
- full solution builds with zero warnings and all 28 tests pass.

Prompt 07 does not implement lesson detail navigation, answer evaluation, persistence, or code execution.

## Next Task and Planned Tasks 08–10

### Prompt 08 — Lesson reading UI

Build the lesson detail experience with objectives, explanations, code examples, practice navigation, previous/next links, and graceful missing-ID handling.

### Prompt 09 — Deterministic quiz evaluator

Implement specification-driven multiple-choice and value-based evaluation with normalized rules and human-authored feedback. Do not execute learner code.

### Prompt 10 — First end-to-end learning vertical slice

Connect lessons, quiz attempts, and explicitly temporary in-memory progress. Demonstrate a complete learner journey and provide an end-to-end manual test script.

## Six-Level Growth Map

| Level | Roadmap application |
| --- | --- |
| L1 — Idiomatic C# | Begins with Prompt 04 and applies to every implementation task. |
| L2 — SOLID and tests | Begins with the solution test structure and grows with behavior introduced in Prompts 06–10. |
| L3 — Domain design | Starts pragmatically in Prompt 06 after product language is established. |
| L4 — Clean/Hexagonal boundaries | Project boundaries begin in Prompt 04; selective CQRS remains deferred until justified. |
| L5 — Deployment evolution | One deployable monolith first; workers, events, and extraction require later evidence. |
| L6 — Delivery infrastructure | Docker, GitHub Actions, and Azure belong to later supplied prompts; Kubernetes and AWS remain optional studies. |

## Roadmap Change Rules

- Do not mark future implementation complete based only on documentation.
- When a task is completed, update its status and evidence in this file within that task's allowed documentation scope.
- Do not fill Prompts 11–29 with guessed requirements. Add them only from the owner's playbook.
- If a new prompt conflicts with the accepted product charter or architecture, surface the conflict and obtain an owner decision before implementation.
- Preserve historical ADRs; supersede decisions with a new ADR rather than rewriting accepted history.
