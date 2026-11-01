# CodeLogic Build Roadmap

## Roadmap Status

- Last updated: 2026-11-01
- Current completed milestone: Prompt 25 — Observability and performance
- Current task: Prompt 26 implementation is ready; acceptance awaits a green pushed pull-request pipeline
- Exact next numbered task: Prompt 27, only after Prompt 26 external verification and owner acceptance
- Source coverage: Prompts 00–26 have been supplied; Prompts 27–29 have not yet been added to the repository
- Owner-directed work: an unnumbered IDE-style UI redesign is implemented on its own branch, including locally persisted panel resizing and source-to-flowchart synchronization; it does not change numbered-prompt acceptance status

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
| Awaiting external verification | Implementation and local gates pass, but an acceptance condition requires an external system or owner-controlled setting. |
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
| 08 | Lesson reading UI | Complete | Two seeded lessons are navigable, responsive, and tested; missing IDs recover gracefully. |
| 09 | Deterministic quiz evaluator | Complete | Correct, incorrect, and boundary tests pass with no arbitrary user-code execution. |
| 10 | First end-to-end learning vertical slice | Complete | A student completes a lesson and sees updated, explicitly temporary in-session progress. |
| 11 | Persistence foundation | Complete | Migration applies cleanly, tests use isolated SQLite databases, and seeded curriculum survives restart. |
| 12 | Identity and owned progress | Complete | Identity account flows work; authenticated progress persists and cross-user access is denied. |
| 13 | Editor playground UI | Complete | Curated snippets can be edited, saved locally, restored, and reset within visible safety and size limits. |
| 14 | Roslyn parse and diagnostics | Complete | Bounded server-side parsing reports positioned syntax errors and fixed-subset violations without compilation or execution. |
| 15 | Flowchart intermediate representation | Complete | Stable graph schema and golden tests cover sequence, one-level branching, rejection, and non-crashing errors. |
| 16 | Flowchart browser renderer | Complete | Safe Mermaid output renders correct branches with responsive visual and equivalent source-mapped text states. |
| 17 | Loops and nested control flow | Complete | Golden graph tests cover exits, repeat edges, nested conditions, and infinite-loop-looking syntax. |
| 18 | Explain execution without running arbitrary code | Complete | Deterministic bounded traces expose program counter, active node, variables, and output with tested branch, reset, and operation-limit behavior. |
| 19 | Logical mistake feedback | Complete | Authored age-boundary cases compare expected/actual output, separate syntax issues, provide hints, and disclose false-positive limits. |
| 20 | Lesson authoring and content management | Complete | Administrator-owned drafts stay private; validated published lessons are public with version and audit basics. |
| 21 | Application quality and test strategy | Complete | Warning-free quality gate, meaningful coverage floors, xUnit strategy, structured logging, cancellation tests, and refactoring evidence are documented and passing. |
| 22 | Architectural boundaries and DDD review | Complete | Context map, dependency graph tests, pragmatic model review, and ADRs support current ports/adapters and selective-CQRS choices. |
| 23 | Security hardening and safe execution design | Complete | Threat model, negative gates, bounded inputs, response headers, and an isolated-worker prerequisite contract preserve fail-closed behavior with execution disabled. |
| 24 | Isolated code runner experimental spike | Complete | Local containment audit records a no-go decision, separate-worker design, kill switch, and manual security checklist; no runner is implemented or deployed. |
| 25 | Observability and performance | Complete | Privacy-safe correlation/logs/traces/metrics, health probes, bounded immutable-content cache, repeatable load harness, baseline, and actionable budgets are documented and tested. |
| 26 | CI/CD and containers | Awaiting external verification | Locked restore, PR/main quality and container security jobs, non-root digest-pinned image, hardened local Compose, explicit migration, and delivery guide are present; first pushed PR must supply green-run evidence. |
| 27–29 | Remaining playbook tasks | Awaiting definition | Add the owner's exact prompt text before planning or implementation. |

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

## Completed Lesson Reading UI

### Prompt 08 — Lesson reading UI

Completed outcomes:

- linked both complete pilot cards to stable slug-based lesson routes;
- rendered objectives, ordered explanations, sample code, safe concepts, and quiz-goal practice previews;
- added previous/next navigation across complete lessons with clear first/last edge states;
- displayed honest practice and completion stubs without evaluation or saved state;
- handled draft, blank, and unknown lesson slugs with a recoverable unavailable state;
- added responsive reading layouts and visible keyboard focus behavior;
- added focused navigation tests plus desktop and narrow-screen visual checks.

Acceptance gate:

- both seeded lessons render and navigate in curriculum order;
- draft and unknown IDs recover without an unhandled error;
- solution builds with zero warnings and all 35 tests pass;
- detail routes pass HTTP, heading, content, link, and responsive visual smoke checks.

Prompt 08 does not implement answer evaluation, interactive exercise controls, saved completion, persistence, or code execution.

## Completed Deterministic Quiz Evaluator

### Prompt 09 — Deterministic quiz evaluator

Completed outcomes:

- implemented a pure Application-layer text comparator for multiple-choice and value-input specifications;
- defined exact, trim, and trim-and-ignore-case behavior explicitly;
- added answer-required, correct, and incorrect results with human-authored explanations;
- added validated answer options for multiple-choice specifications while keeping value inputs option-free;
- replaced the practice preview with native server-rendered forms and accessible feedback;
- treated code-shaped input only as text and introduced no compiler, scripting, reflection, or process APIs;
- added focused correct, incorrect, normalization, blank, invalid-rule, code-like input, and manifest-boundary tests.

Acceptance gate:

- both complete pilot exercises accept their specified correct answers;
- exact and normalized boundaries behave as documented;
- incorrect and blank answers return clear, deterministic feedback;
- no learner response is compiled or executed;
- solution builds with zero warnings and the full test suite passes.

Prompt 09 does not record attempts, persist answers, update completion, or implement the Prompt 10 learning journey.

## Completed First End-to-End Learning Vertical Slice

### Prompt 10 — First end-to-end learning vertical slice

Completed outcomes:

- connected deterministic quiz results to Domain `ExerciseAttempt` records;
- added an Application workflow for per-exercise attempt numbering, correct-answer completion, and snapshots;
- stored attempt metadata and completed lesson IDs in ASP.NET Core server-memory session state;
- changed quiz submission to antiforgery-protected POST-redirect-GET behavior;
- showed current-session completion on lesson pages and linked correct feedback to Progress;
- replaced the Progress placeholder with counts, percentage, lesson states, attempt history, and the next incomplete pilot;
- labeled the state as temporary, account-free, and vulnerable to session expiry or app restart;
- added workflow tests, cookie-session HTTP verification, and `docs/END_TO_END_TEST.md`.

Acceptance gate:

- an incorrect answer records an attempt without completing the lesson;
- a later correct answer completes the lesson and updates the dashboard to 1/2 and 50%;
- repeated completion remains unique while attempts continue to increment;
- a separate browser session starts at zero;
- solution builds with zero warnings and the full test suite passes.

Prompt 10 does not add a database, identity, cross-session persistence, raw-answer storage, or any Prompt 11 feature.

## Completed Persistence Foundation

### Prompt 11 — Persistence foundation

Completed outcomes:

- added EF Core 10.0.5 and SQLite behind the existing `ICurriculumCatalog` seam;
- mapped curriculum topics and lesson specifications with keys, relationship, limits, and unique indexes;
- stored small nested lesson collections as JSON instead of prematurely adding more tables;
- generated and applied the `InitialPersistence` migration with all nine deterministic curriculum seeds;
- configured the ignored development database under `CodeLogic.Web/App_Data`;
- kept learner attempts and completion in temporary session state because no accounts exist;
- added isolated SQLite migration tests and file-database reopen coverage;
- verified the web app reads the same seeded database after process restart;
- documented migration, reset, test, and future-migration workflows.

Acceptance gate:

- the initial migration applies cleanly to an empty SQLite database;
- the EF-backed catalog reconstructs a valid nine-topic, nine-lesson manifest;
- integration tests use separate databases and cannot leak changes between tests;
- a new `DbContext` and a restarted web process retain the seeded curriculum;
- dependency audit reports no vulnerable packages;
- solution builds with zero warnings and the full test suite passes.

Prompt 11 does not add Identity, user accounts, durable learner progress, curriculum authoring, automatic production migrations, or a hosted database provider.

## Completed Identity and Owned Progress

### Prompt 12 — Identity and owned progress

Completed outcomes:

- added ASP.NET Core Identity registration, sign-in, sign-out, cookie authentication, lockout, and authorization;
- allowed anonymous lesson reading while requiring an account for quiz submission and saved progress;
- replaced temporary session ownership with EF-backed, user-owned attempts and lesson completion;
- kept raw learner answers out of persistence;
- derived ownership from authenticated claims rather than browser-supplied user IDs;
- added migration, user-isolation tests, restart-persistence tests, and an account-flow manual script.

Acceptance gate:

- two users see only their own attempts and completion;
- unauthenticated progress and quiz requests are challenged;
- authenticated progress survives application restart;
- passwords are handled by ASP.NET Core Identity rather than application code;
- solution restores, builds with zero warnings, and the full test suite passes.

Prompt 12 does not add email delivery, email confirmation, password recovery, two-factor authentication, social login, account deletion, or later playbook features.

## Completed Editor Playground UI

### Prompt 13 — Editor playground UI

Completed outcomes:

- replaced the placeholder with an accessible plain-text C# editor;
- loaded the two complete curriculum samples without duplicating lesson source;
- enforced the curriculum's 1,200-character size limit in HTML and editor state;
- added one browser-local draft with save, restore, and reset behavior;
- added `Ctrl+S` save and `Ctrl+Alt+R` reset shortcuts without trapping Tab;
- documented visible limitations and the non-execution boundary;
- added focused state, boundary, reset, and invalid-snippet tests.

Acceptance gate:

- a learner can load, edit, save, restore, and reset curated snippets;
- keyboard controls and live status feedback are available;
- oversized text is rejected without replacing the current editor content;
- no learner code is parsed, compiled, executed, or submitted for analysis;
- solution restores, builds, and tests successfully.

Prompt 13 does not add Roslyn, syntax validation, code execution, Mermaid, traces, multiple saved files, account-synced code, or Monaco.

## Completed Roslyn Parse and Diagnostics

### Prompt 14 — Roslyn parse and diagnostics

Completed outcomes:

- added an Application analysis contract and neutral diagnostic result model;
- added a pinned Roslyn Infrastructure adapter using C# syntax-tree APIs only;
- enforced 1,200 characters, three control-flow nesting levels, and a two-second cooperative time limit;
- distinguished invalid input, syntax errors, unsupported constructs, timeouts, and valid supported source;
- displayed one-based line/column diagnostics and beginner explanations in the Playground;
- tested valid curriculum samples, malformed syntax, limits, caller cancellation, and unsupported language/framework features.

Acceptance gate:

- valid supported snippets pass deterministically;
- malformed snippets return Roslyn diagnostic codes, positions, and explanations;
- unsupported constructs return actionable CodeLogic diagnostics;
- the implementation contains no compilation, emit, or learner-code execution path;
- solution restores, builds, and tests successfully.

Prompt 14 does not add semantic/type checking, graph IR, Mermaid, traces, compilation, code execution, or a broader C# subset.

## Completed Flowchart Intermediate Representation

### Prompt 15 — Flowchart intermediate representation

Completed outcomes:

- defined graph schema 1.0 with start, end, action, decision, input, and output nodes;
- defined deterministic next, true, and false edges plus one-based source positions;
- kept Roslyn types in Infrastructure behind an Application translation interface;
- translated simple top-level sequence and one-level `if/else` from Roslyn syntax nodes;
- assigned stable source-order node and edge IDs and deterministic branch merge edges;
- rejected nested decisions and unimplemented statements with positioned diagnostics and no partial graph;
- added golden sequence and branching tests plus repeatability and non-crashing error cases.

Acceptance gate:

- golden sequence and `if/else` graph expectations pass;
- the same source produces the same ordered schema and IDs;
- nested branches return `FL1001` without a graph;
- malformed and unsupported source returns diagnostics rather than crashing;
- translation contains no regex parsing, compilation, rendering, or code execution.

Prompt 15 does not add nested-branch graphs, loop graphs, Mermaid rendering, Playground visualization, semantic evaluation, traces, compilation, or execution.

## Completed Flowchart Browser Renderer

### Prompt 16 — Flowchart browser renderer

Completed outcomes:

- added deterministic Mermaid serialization behind an Application interface;
- assigned renderer-owned IDs and escaped learner-controlled labels;
- emitted correct sequence and explicit True/False branch arrows;
- rendered pinned Mermaid 12.2.0 with strict security and HTML labels disabled;
- parsed and sanitized returned SVG without assigning untrusted `innerHTML`;
- added empty, translation-error, renderer-error, visual, and equivalent text states;
- connected graph nodes back to exact textarea spans with keyboard-operable controls;
- contained wide diagrams inside a responsive scrolling panel.

Acceptance gate:

- golden tests lock branch shapes, arrows, escaping, source maps, and deterministic output;
- browser verification confirms readable labels, correct branch merge arrows, and exact source selection;
- nested and malformed input produce useful non-visual error states;
- every diagram has an ordered node and connection text equivalent;
- no learner source is compiled or executed.

Prompt 16 does not add loop/nested-branch rendering, execution traces, semantic evaluation, export, compilation, execution, or frontend build tooling.

## Completed Loops and Nested Control Flow

### Prompt 17 — Loops and nested control flow

Completed outcomes:

- advanced the renderer-neutral graph contract to schema 1.1 with an explicit repeat edge;
- recursively translated nested `if/else`, `while`, `for`, and `do-while` using Roslyn syntax nodes;
- represented `for` initializers and iterators as visible action nodes;
- retained explicit loop exits and back edges, including for infinite-loop-looking syntax;
- added dedicated unsupported diagnostics for `break` and `continue` instead of guessing jump behavior;
- updated the Playground's visual and equivalent-text descriptions for repeat paths;
- added golden and boundary tests for nested decisions, all three loop forms, repeat edges, exits, and unsupported jumps.

Acceptance gate:

- golden graphs lock `while`, `for`, `do-while`, and nested decision structure;
- loop bodies return through explicit `LoopBack` edges and decisions retain explicit false exits;
- `while (true)` and `for (;;)` remain non-crashing structural graphs;
- `break` and `continue` return actionable diagnostics and no partial graph;
- translation uses Roslyn syntax nodes and does not compile or execute learner source.

Prompt 17 does not add execution traces, semantic condition evaluation, `break`/`continue` graph jumps, compilation, or execution.

## Completed Bounded Pedagogical Execution Traces

### Prompt 18 — Explain execution without running arbitrary code

Completed outcomes:

- added an Application-owned trace contract and a Roslyn-syntax Infrastructure interpreter;
- generated immutable deterministic snapshots with program counter, active graph node, scalar variables, cumulative output, and explanations;
- supported initialized scalar variables, basic expressions, interpolation, output, branches, and the three supported loop forms;
- enforced a 100-operation limit with an explicit `ET0002` result for infinite-looking input;
- rejected arrays, live input, methods, unsupported targets, and incompatible operations without exposing a partial trace;
- added tested Next step and Reset navigation plus an accessible responsive Playground trace panel;
- documented the distinction between CodeLogic simulation and general C#/.NET execution.

Acceptance gate:

- identical accepted input produces identical ordered snapshots;
- true and false branches select only their correct statements;
- loop conditions, bodies, and iterators appear in deterministic order;
- infinite-looking syntax stops at the operation limit;
- Reset returns to program counter zero with empty variables and output;
- no learner source is compiled, emitted, or executed through .NET.

Prompt 18 does not add full C# semantics, arrays, live input, learner-defined methods, arbitrary execution, persistence, or future playbook features.

## Completed Logical Mistake Feedback

### Prompt 19 — Logical mistake feedback

Completed outcomes:

- added an Application-owned known exercise specification and logical feedback result contract;
- authored below, exact-boundary, and above-boundary cases for adult age classification;
- replaced only the known `age` initializer through Roslyn syntax and delegated modeling to the bounded interpreter;
- displayed expected and actual output, explicit Boundary text, Pass/Fail, and case-specific hints;
- kept syntax, unsupported input, trace limits, and exercise setup separate from logical verdicts;
- documented and tested a false positive that passes ages 17, 18, and 19 while remaining wrong for untested age 20.

Acceptance gate:

- `age >= 18` passes 17/18/19 with age 18 visibly identified as the boundary;
- `age > 18` fails age 18 with expected `Adult`, actual `Minor`, and an actionable `>= 18` hint;
- malformed or unsupported source receives diagnostics without partial logical results;
- the UI always states that passing finite authored cases does not prove general correctness;
- no learner source is compiled or executed through .NET.

Prompt 19 does not add general logical-error detection, generated tests, AI intent inference, formal verification, persistence, or future playbook features.

## Completed Lesson Authoring and Content Management

### Prompt 20 — Lesson authoring and content management

Completed outcomes:

- added an administrator-role-protected authoring page with create, edit, private preview, and publish actions;
- kept authored lessons behind an Application contract and an EF Core Infrastructure adapter;
- required trusted claim ownership and rechecked ownership for every mutation;
- added field validation, optimistic version checks, and created/updated/published UTC audit fields;
- filtered public list/detail queries to Published records only;
- seeded the Administrator role without silently promoting any registered account;
- preserved the rule that displayed sample code is never compiled or executed.

Acceptance gate:

- draft records cannot be retrieved through public authoring queries or learner pages;
- publishing makes a validated lesson available on the public lesson list and detail route;
- tests cover validation, version conflicts, ownership denial, route-role authorization, and public visibility.

Prompt 20 does not add public submissions, runnable code, deletion, unpublishing, scheduled releases, multi-editor approval, or a full immutable audit history.

## Completed Application Quality and Test Strategy

### Prompt 21 — Application quality and test strategy

Completed outcomes:

- made compiler and analyzer warnings fail the shared build while narrowly excluding one unsuitable rule from generated migration operations;
- added one repeatable Release quality script for restore, formatting, build, xUnit, coverage floors, and package audit;
- defined a practical test pyramid and module-specific line/branch regression floors instead of a repository-wide vanity percentage;
- centralized repeated SQLite integration-test setup without changing production architecture;
- added privacy-safe source-generated logs for quiz and lesson-authoring events;
- documented the existing central exception boundary and explicit expected-error results;
- added authoring cancellation coverage alongside the existing analysis cancellation test;
- evaluated and honestly deferred Playwright until browser provisioning can be repeatable locally and in CI.

Acceptance gate:

- Release quality gate passes with zero build warnings or errors;
- 23 unit and 105 integration tests pass;
- measured coverage exceeds every documented floor;
- package audit finds no known vulnerable direct or transitive packages;
- refactoring rationale, limitations, and manual UI layer are recorded in `docs/QUALITY.md`.

Prompt 21 does not add CI, deployment, a browser installation, production monitoring, or future playbook features.

## Completed Architectural Boundaries and DDD Review

### Prompt 22 — Architectural boundaries and DDD review

Completed outcomes:

- mapped Learning, Practice, Analysis, and Identity as logical contexts inside the modular monolith;
- documented the existing Learning/Practice shared kernel and framework translation seams;
- evaluated invariant-bearing rich models, stateless domain services, boundary DTOs, EF entities, and Identity models;
- retained purpose-specific Application ports and rejected a generic repository, blanket mediator handlers, and full CQRS without evidence;
- added proposed ADRs for pragmatic context/model decisions and selective CQRS;
- added tests for the exact project-reference graph, Application assembly independence, and framework-neutral port signatures;
- made no runtime rewrite, namespace migration, new package, or service split.

Acceptance gate:

- the dependency and context maps describe current code and future seams;
- architecture tests enforce inward project dependencies and prevent outer framework types from leaking through Application ports;
- ADRs record alternatives, consequences, and evidence required before physical context or CQRS evolution;
- the complete quality gate passes.

Prompt 22 does not add microservices, a project per context, generic repositories, domain-event infrastructure, mediator packages, separate read storage, or later playbook features.

## Completed Security Hardening and Safe Execution Design

### Prompt 23 — Security hardening and safe execution design

Completed outcomes:

- documented assets, trust boundaries, attacker paths, mitigations, residual risk, and fail-closed UX for snippets/uploads, auth, XSS, CSRF, injection, exhaustion, malicious code, and account leakage;
- added restrictive browser capability, framing, referrer, opener, MIME-sniffing, and cross-domain-policy headers;
- bounded request bodies, multipart forms, form values/counts, email, password, and return URL inputs server-side;
- expanded negative tests for process, filesystem-write, reflection, and network-client snippets;
- added gates for absent execution primitives/uploads, antiforgery placement, Mermaid DOM sanitization, request limits, and empty traces on malicious input;
- proposed ADR 0008, keeping arbitrary execution disabled;
- designed a future worker contract requiring no network, OS isolation, CPU/memory/PID/time/output/file quotas, tenant capability isolation, bounded output, process-tree termination, and verified cleanup before implementation.

Acceptance gate:

- threat model and residual risks are recorded in `docs/SECURITY.md`;
- dangerous inputs fail with Invalid, Unsupported, or Limit states and never produce a partial trace;
- negative security gates and the full quality gate pass;
- no execution endpoint, compiler, emitted assembly, child process, upload feature, or worker implementation exists.

Prompt 23 does not add arbitrary .NET execution, an isolated worker implementation, public uploads, CSP, production rate limiting/WAF, MFA, secret management, or later playbook features.

## Completed Isolated Code Runner Experimental Spike

### Prompt 24 — Isolated code runner experimental spike

Completed outcomes:

- verified that Docker and Podman are absent, WSL is not installed, and Windows Sandbox and Hyper-V tooling are unavailable on the local machine;
- recorded a **NO-GO** decision and kept arbitrary compilation and execution disabled;
- documented a future separate-worker boundary with unprivileged identity, no network or secrets, disposable storage, hard quotas, bounded results, privacy-safe logs, cleanup, and fail-closed behavior;
- defined an operational kill switch and a manual adversarial checklist covering network, host access, CPU, memory, PID, time, output, filesystem, cleanup, and cross-account isolation;
- proposed ADR 0009 to require repeatable evidence, independent security review, and explicit owner acceptance before any implementation proposal;
- retained the bounded pedagogical interpreter as the supported learning path;
- installed no software, changed no Windows features, downloaded no runtime image, executed no learner code, and deployed no worker.

Acceptance gate:

- `docs/ISOLATED_RUNNER_SPIKE.md` records the local evidence, candidate boundary, mandatory controls, kill switch, and manual security checklist;
- missing sandbox support produces an explicit no-go rather than an unsafe implementation;
- security source gates and the full quality gate continue to prove that no execution primitive or worker exists.

Prompt 24 does not add Docker/Podman/WSL/Hyper-V, a worker project, compiler, package restore, execution endpoint, queue, runtime image, deployment, or future playbook features.

## Completed Observability and Performance

### Prompt 25 — Observability and performance

Completed outcomes:

- added a server-controlled correlation ID response header and structured logging scope;
- instrumented bounded analysis with privacy-safe structured events, an activity source, a counter, and a duration histogram;
- added process liveness and SQLite connectivity readiness endpoints with generic responses;
- cached the immutable persisted curriculum manifest behind one fixed, size-limited, expiring cache entry;
- added integration tests for telemetry privacy, cache reuse, correlation control, and healthy/unhealthy readiness behavior;
- added a package-free Release load harness for the same analysis service boundary used by the Blazor Playground;
- measured 10,000 mixed analysis operations at concurrency 4 with zero failures, 0.0556 ms p95, and 0.0778 ms p99 on the local workstation;
- documented budgets, profiling workflow, privacy rules, limitations, and actions for threshold failures.

Acceptance gate:

- `docs/PERFORMANCE.md` contains the repeatable command, environment, measured baseline, thresholds, and honest interpretation;
- analysis correctness must have zero failures and the documented local warm p95 budget is 50 ms;
- observability never records learner source or personal data by default;
- focused tests and the complete quality gate pass.

Prompt 25 does not add an external telemetry backend, public analysis API, dashboards, alerts, hosted load infrastructure, production scaling policy, or later playbook features.

## Implemented CI/CD and Containers — Awaiting External Verification

### Prompt 26 — CI/CD and containers

Completed outcomes:

- pinned SDK, repository-local EF tool, direct/transitive NuGet graphs, GitHub actions, and .NET container base manifests;
- made local and CI quality use the same tool restore, locked restore, formatting, Release build, xUnit/coverage, EF consistency, and vulnerability audit;
- added PR, `main`, and manual GitHub Actions triggers with read-only repository permission and concurrency cancellation;
- added a separate container job for non-root declaration, explicit migration, readiness smoke test, resource/privilege restrictions, and Trivy image scanning;
- added a multi-stage chiseled-extra application image that contains no SDK and runs as the built-in unprivileged .NET user;
- added hardened local Compose services for explicit migration and the single web app using SQLite/data-protection named volumes;
- added secret-free example environment values and weekly Dependabot proposals;
- documented the reproducible workflow, branch-protection owner action, reset warning, security tradeoffs, and honest local verification gap.

Acceptance gate:

- local locked restore, build, tests, publish, migration test, dependency audit, and configuration review pass;
- Docker/Trivy cannot run on the current machine because Docker is absent;
- the first pushed pull request must show both **Quality gate** and **Container build and security** green before Prompt 26 is finally accepted.

Prompt 26 does not push an image, deploy to cloud, run a production migration, add credentials, create a code-runner container, enable privileged access, or implement later playbook features.

## Awaiting the Next Supplied Prompt

Prompts 27–29 remain undefined in the repository. The owner must provide the exact next prompt before implementation continues.

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
- Do not fill Prompts 27–29 with guessed requirements. Add them only from the owner's playbook.
- If a new prompt conflicts with the accepted product charter or architecture, surface the conflict and obtain an owner decision before implementation.
- Preserve historical ADRs; supersede decisions with a new ADR rather than rewriting accepted history.
