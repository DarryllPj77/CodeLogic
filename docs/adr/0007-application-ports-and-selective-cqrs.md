# ADR 0007: Application Ports and Selective CQRS

- Status: Proposed
- Date: 2026-11-01

## Context

CodeLogic uses EF Core, ASP.NET Core Identity, Roslyn, Mermaid, Blazor, and SQLite. Those tools must not leak into Domain/Application contracts. The application also has reads and writes, but their scale and data shapes are still straightforward.

The review must decide whether to add generic repositories, mediator handlers, command/query buses, or separate read models.

## Decision

Continue using purpose-specific Application ports with Infrastructure adapters and Web as the composition root.

- Ports describe product intent, such as retrieving published lessons, recording owned progress, analyzing supported syntax, or building a trace.
- Application DTOs remain framework-neutral.
- EF entities, Identity types, Roslyn syntax trees, HTTP models, and Blazor state remain at outer boundaries.
- Do not add a generic repository.
- Do not add a mediator or blanket command/query handler layer.
- Permit CQRS-style separation locally when a read and write model demonstrably differ; a package or separate store is not required.

The current lesson-authoring service may retain related owner/public queries and versioned commands in one narrow interface. Split it only if consumer permissions, query shape, performance, or independent evolution make the combined contract costly.

## Alternatives Considered

### Generic repository over EF Core

This can standardize CRUD signatures, but hides meaningful operations and often duplicates EF Core's unit-of-work and repository behavior. Purpose-specific ports communicate intent better.

### Mediator handlers for every use case

Handlers can help very large applications, cross-cutting pipelines, or independently evolving features. Here they would add files and navigation without reducing current coupling.

### Full CQRS with separate read storage

Separate models can optimize complex reporting or independent scaling, but introduce synchronization, eventual consistency, and operational cost that current workflows do not need.

### Direct EF Core use in Web

This is initially concise, but couples presentation to schema and makes authorization/ownership rules easier to bypass. Web continues to call Application ports.

## Consequences and Tradeoffs

### Benefits

- Contracts express use cases instead of database mechanics.
- Core assemblies remain testable without frameworks.
- A future adapter or external client can map at a clear seam.
- The code remains approachable for the current project size.

### Costs

- Infrastructure must map between EF/Roslyn models and Application contracts.
- A combined service interface may need a deliberate split later.
- Cross-cutting mediator pipelines are unavailable unless a measured need justifies them.

## Evidence Required for CQRS Evolution

Adopt a separate query interface, read model, handler pipeline, or store only when at least one is observed:

- a read shape is materially different from the transactional model;
- read traffic or latency needs independent optimization;
- commands require queued/retryable processing;
- authorization differs substantially between query consumers;
- an external contract must evolve independently;
- multiple contributors need an explicit dispatch/pipeline convention.

Record the concrete evidence and operational consequences in a new ADR before expanding the pattern.
