# CodeLogic Codex Operating Contract

## Repository Context

CodeLogic is a beginner-friendly interactive C# learning web application. It teaches programming fundamentals through lessons and deterministic exercises, then turns a deliberately supported subset of C# into explanatory flowcharts and bounded execution traces.

The owner is learning C# and .NET using Visual Studio on Windows. Explanations and implementation choices must remain practical, reviewable, and appropriate for that learning context.

Read this file and `docs/ROADMAP.md` before starting every numbered task. Also read the documentation directly relevant to the requested task.

## Non-Negotiable Working Rules

1. Work on only the numbered task supplied by the owner. Do not begin future tasks or silently expand the scope.
2. Limit changes to the smallest coherent slice that satisfies the task's acceptance gate.
3. Before changing files, inspect the repository state, relevant documentation, Git status, installed SDK, solution structure, and test setup as applicable.
4. If a requested feature already exists, reuse and improve it rather than overwriting it.
5. State a short plan and list the exact files expected to change before editing.
6. Prefer simple, idiomatic C# and explicit boundaries. Avoid overengineering, speculative generic abstractions, unnecessary packages, and patterns without a demonstrated benefit.
7. Run the available restore, build, and tests after changes. Include meaningful negative and edge cases. If verification cannot run, explain exactly why.
8. Keep `README.md`, `docs/ROADMAP.md`, `docs/CHANGELOG.md`, and affected architecture, ADR, feature, or setup documentation accurate when those files exist and the current task changes their subject. Record only work that is actually complete and mark TODOs honestly.
9. Never commit, push, deploy, publish, install system software, alter machine-wide configuration, delete data, run production migrations, or handle secrets without explicit owner approval.
10. Never execute untrusted student C# inside the web application process. Arbitrary-code execution remains out of scope unless a separate, security-approved task explicitly introduces an isolated runner.
11. Preserve unrelated owner changes and local-only files. Do not stage or modify `.vs/`, build output, secrets, or unrelated work.
12. Keep explanations practical so the owner can understand how the code works and why it is structured that way.
13. Stop when the current task is complete and wait for owner review. Do not silently continue to the next numbered prompt.

## Architecture Direction

- Begin as a modular monolith with one deployable ASP.NET Core application.
- Use a Blazor Web App for the web experience.
- Keep Domain independent of Application, Infrastructure, and Web.
- Keep Application independent of Infrastructure and Web.
- Put EF Core, SQLite, Identity adapters, external tools, and persistence details in Infrastructure.
- Use Web as the UI, HTTP boundary, and composition root.
- Introduce selective CQRS, background workers, messaging, or service extraction only when a measured need justifies them.
- Do not introduce microservices or Kubernetes merely to make the project appear enterprise-ready.

The authoritative architecture description and proposed decisions are in `docs/ARCHITECTURE.md` and `docs/adr/`.

## Product and Safety Boundaries

- Core learning must work without generative AI or paid AI APIs.
- Lessons and feedback are human-authored and reviewable.
- Exercise evaluation is deterministic.
- Code visualization uses bounded static analysis of an explicitly supported subset.
- Parsing or analyzing source is not permission to compile or execute it.
- Clearly distinguish unsupported input, invalid input, and an internal failure.
- Accessible keyboard operation and equivalent text explanations are required for core learning flows.

The authoritative product scope is in `docs/PRODUCT.md`.

## Six Engineering Levels

Apply the levels gradually; they are a growth path, not permission to introduce every technique immediately.

1. **L1 — Idiomatic C#:** clear naming, readable LINQ, and Strategy, Factory, or Adapter only when beneficial.
2. **L2 — Engineering fundamentals:** SOLID, DRY, KISS, YAGNI, encapsulation, and behavior-focused tests.
3. **L3 — Domain design:** bounded language and pragmatic rich models where invariants exist.
4. **L4 — Clean/Hexagonal boundaries:** inward dependencies in a modular monolith and selective CQRS only where justified.
5. **L5 — Evolution:** monolith first; optional workers, events, or service extraction after evidence.
6. **L6 — Delivery:** Docker, GitHub Actions, and Azure in their designated tasks; Kubernetes and AWS only as later comparisons.

## Required Task Completion Report

Finish each task with:

- summary of what was completed;
- exact files changed;
- commands run and their results;
- restore, build, and test results;
- known limitations, open questions, and honest TODOs;
- manual verification steps in Visual Studio on Windows;
- suggested exact next numbered task.

Do not claim that a command, commit, push, test, or deployment occurred unless it actually occurred.

## Approval and Decision Ownership

Codex may recommend options and explain tradeoffs. The owner decides product scope, architecture, dependencies, security posture, acceptance, commits, releases, and deployments.

Ask for owner direction before an ambiguous choice that would materially change scope, architecture, security, data, cost, or external systems. Otherwise make the smallest reasonable assumption, state it, and keep the work reversible.

