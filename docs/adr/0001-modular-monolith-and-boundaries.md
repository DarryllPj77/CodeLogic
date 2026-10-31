# ADR 0001: Modular Monolith and Clean Boundaries

- Status: Proposed
- Date: 2026-10-31

## Context

CodeLogic needs enough structure to keep learning, exercise, progress, persistence, and code-analysis concerns understandable. It does not yet have demonstrated scale, separate teams, or independent deployment requirements.

## Decision

Build CodeLogic as one deployable modular monolith with four primary project boundaries:

- Domain contains framework-independent business concepts and invariants.
- Application contains use cases and ports.
- Infrastructure contains EF Core, identity, content, and analysis adapters.
- Web contains Blazor UI, HTTP concerns, and the composition root.

Dependencies point inward: Web and Infrastructure may depend on Application and Domain; Application may depend on Domain; Domain depends on none of the other projects.

Modules communicate through explicit application contracts. Sharing a process or database does not permit one module to reach into another module's internal implementation.

## Alternatives Considered

### Traditional layered monolith with unrestricted references

Simpler initially, but UI and persistence concerns commonly leak into business rules. Refactoring becomes harder once direct dependencies spread.

### Microservices from the start

Provides independent deployment and scaling, but adds remote calls, failure handling, observability, deployment coordination, and distributed data decisions before those capabilities are needed.

### Full Clean Architecture per feature

Creates strong isolation, but repeating projects and abstractions for every small feature would be excessive for the current team and product stage.

## Consequences and Tradeoffs

### Benefits

- One application is easy to run, debug, test, and deploy.
- Project references make major dependency rules visible.
- Module seams support later extraction if evidence justifies it.
- Beginners can follow a request without crossing network boundaries.

### Costs

- Discipline and tests are required to prevent boundary erosion.
- All modules share one deployment cadence initially.
- Process-level failures and resource contention are shared.

## Future Extraction Seams

Code analysis is the leading candidate for a worker or service if security isolation or CPU load requires it. Curriculum, progress, and practice remain module-owned and can later expose contracts without moving technical layers into separate services.

Extraction requires evidence of independent scaling, security, availability, ownership, or release needs. A desire to appear enterprise-ready is not sufficient.
