# Codex Workflow

## Purpose

CodeLogic is developed through small, reviewable tasks. Each Codex session implements one numbered playbook prompt and then stops for owner review. The owner remains responsible for product, architecture, risk, and release decisions.

## One Prompt, One Task

- Work only on the numbered prompt supplied for the current task.
- Keep the change to the smallest coherent slice that satisfies that prompt.
- Do not begin later prompts, even when their work appears convenient.
- Reuse existing behavior and documentation when present instead of replacing them.
- Stop after reporting the acceptance-gate evidence and the exact next prompt number.

## Before Making Changes

1. Read `AGENTS.md` and `docs/ROADMAP.md` when they exist.
2. Inspect the repository, relevant documentation, Git status, installed SDK, solution and project files, and test setup.
3. State a short plan and list the exact files expected to change.
4. Call out missing prerequisites, ambiguous requirements, or conflicts before relying on assumptions.

## Change Boundaries

- Avoid surprise refactors, unrelated cleanup, dependency upgrades, and speculative abstractions.
- Prefer simple, idiomatic C# and established repository conventions.
- Do not create application code during documentation-only tasks.
- Never execute untrusted student C# in the web application process.
- Preserve owner changes already present in the worktree.

## Local-First Operation

- Prefer local inspection, builds, and tests before external services.
- Do not require cloud infrastructure for ordinary development or testing.
- Do not deploy, contact production systems, or run production migrations as part of routine tasks.
- Keep secrets out of source files, logs, documentation, and prompts.

## Approval Required

Obtain explicit owner approval before:

- committing, pushing, publishing, or deploying;
- installing system software or changing machine-wide configuration;
- deleting data or performing destructive or difficult-to-reverse changes;
- running migrations against production or shared data stores;
- handling, rotating, or exposing secrets;
- materially changing an accepted product or architecture decision.

## Task Evidence and Quality Gate

For every task, report:

- a concise summary of completed work;
- every file changed;
- commands run and their results;
- restore, build, and test results, including negative and edge cases where applicable;
- known limitations, open questions, and honest TODOs;
- manual verification steps for Visual Studio on Windows;
- the suggested exact next numbered prompt.

If a command cannot run, state precisely why. A passing command does not replace human diff review. The owner reviews the diff, evidence, and decisions before authorizing the next prompt.

## Human Ownership

Codex may recommend an approach and explain tradeoffs, but the owner decides:

- product scope and learning outcomes;
- architecture and dependency choices;
- acceptable risk and security posture;
- whether a task meets its acceptance gate;
- when changes may be committed, pushed, deployed, or released.

Uncertainty must be surfaced rather than hidden behind an implementation choice.

## Task 00 Repository Inventory

Inventory captured on 2026-10-31:

| Area | Observed state |
| --- | --- |
| Workspace | The owner confirmed this directory as the intended local workspace for CodeLogic. No repository files existed before this workflow document was added. |
| Git | Local Git repository initialized on `main`; `origin` points to `https://github.com/DarryllPj77/CodeLogic.git`. The remote currently has no branches or commits. |
| Required starting docs | `AGENTS.md` and `docs/ROADMAP.md` are missing. |
| .NET SDK | .NET SDKs 10.0.114 and 10.0.403 are installed; 10.0.403 is currently selected. |
| Runtime | ASP.NET Core and .NET 10.0.14 runtimes are installed. |
| SDK pinning | No `global.json` exists. |
| Solutions and projects | No `.sln`, `.slnx`, or project files exist. |
| Tests | No test projects, test packages, or test source files exist. |
| Workloads | `dotnet --info` reports no optional .NET workloads installed. |

## Unknowns Requiring Owner Resolution

- What content should be placed in the missing `AGENTS.md` and `docs/ROADMAP.md`.
- Whether Visual Studio and its ASP.NET/web workload are installed; task 00 inspected the .NET CLI only.

The governing-document question should be resolved before task 01 so future work begins with the intended operating contract and roadmap.

## Next Prompt

After the owner restores or approves creation of the missing governing documents, the exact next prompt is **Prompt 01 — Product charter and scope**.
