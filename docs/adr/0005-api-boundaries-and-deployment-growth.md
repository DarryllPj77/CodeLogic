# ADR 0005: Explicit API Boundaries and Evidence-Led Deployment Growth

- Status: Proposed
- Date: 2026-10-31

## Context

CodeLogic may eventually support external clients, background analysis, managed hosting, and larger workloads. Designing every possible API and distributed component now would make the initial learning application harder to understand and operate.

## Decision

Keep internal boundaries explicit while deploying one ASP.NET Core application initially.

- Blazor components invoke Application use cases.
- HTTP endpoints expose purpose-built request and response contracts only when needed.
- Domain entities and EF Core models are not public API contracts.
- API versioning begins only when a real external consumer or incompatible evolution requires it.
- Start local with SQLite, then host the monolith with a managed relational database.
- Add Docker, GitHub Actions, and Azure delivery in their designated later tasks.
- Add workers, messaging, or service extraction only after measured need.

Do not adopt broad CQRS, microservices, or Kubernetes initially.

## Alternatives Considered

### API-first system with a separate frontend

Useful when multiple clients already exist, but duplicates contracts and deployment concerns before CodeLogic has an external consumer.

### CQRS and mediator handlers for every operation

Can clarify complex read/write workflows, but adds types and indirection to simple use cases. Selective CQRS remains available where read and write models demonstrably diverge.

### Microservices from the beginning

Enables independent deployments but immediately requires network resilience, distributed observability, data ownership, and operational coordination.

### Kubernetes as the initial hosting platform

Provides powerful orchestration, but a single application does not justify cluster operations, manifests, ingress, and platform maintenance. Managed app or container hosting is simpler.

### AWS as a parallel initial target

Maintaining two cloud paths would divide learning and testing effort. Azure is the playbook default because it aligns closely with the .NET toolchain; AWS can be evaluated later as a comparison.

## Consequences and Tradeoffs

### Benefits

- Local development and deployment stay understandable.
- Contracts are protected without paying distributed-system costs.
- Delivery automation can mature before topology becomes complex.
- Future decisions can use observed load and product behavior.

### Costs

- All initial modules share scaling and deployment boundaries.
- A later extraction requires deliberate contract and data migration work.
- Internal APIs must still be kept clean even without network enforcement.

## Future Extraction Seams and Triggers

| Candidate | Extraction trigger |
| --- | --- |
| Analysis worker | Long-running work, retries, CPU contention, or required process isolation |
| Public API | A real external client and a stable support commitment |
| Progress read model | Reporting load or shape becomes materially different from transactional writes |
| Independent service | Separate ownership, scaling, security, availability, or release cadence is demonstrated |
| Kubernetes | Multiple services and availability/scale needs exceed simpler managed hosting options |

The team must record the evidence and a new ADR before adopting any of these evolutions.
