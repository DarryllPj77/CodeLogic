# ADR 0003: EF Core with SQLite for Local Development

- Status: Accepted and implemented by Prompt 11
- Date: 2026-10-31

## Context

CodeLogic will eventually persist curriculum, identity, attempts, and progress. Local development should work with minimal machine configuration, while hosted deployment may need a managed relational database.

## Decision

Use Entity Framework Core as the relational persistence adapter and SQLite for local development. Keep EF Core configuration and migrations in Infrastructure. Domain and Application code must not use provider-specific types or behavior.

Use application-focused persistence ports or narrow query abstractions only where a boundary or test requires them. Do not introduce a generic repository solely to wrap EF Core.

Production database selection is deferred. Azure SQL and PostgreSQL are candidates and must be validated with provider-specific integration tests before adoption.

Prompt 11 implements this decision for curriculum reads. Anonymous learner progress remains session-only until Identity provides a meaningful ownership key.

## Alternatives Considered

### SQL Server LocalDB

Closer to Azure SQL behavior, but Windows-specific and requires more local setup. It may be used in provider compatibility tests later.

### PostgreSQL in a local container

Provides production-like behavior when PostgreSQL is selected, but requires Docker and container lifecycle management too early in the learning path.

### In-memory database as the primary development store

Easy to start, but it does not reproduce relational constraints, transactions, or SQL translation accurately enough for persistence confidence.

### Handwritten SQL or a micro-ORM

Offers explicit query control, but adds mapping work that is not currently justified. It remains an option for measured hot paths later.

## Consequences and Tradeoffs

### Benefits

- File-based local setup is simple and portable.
- EF Core migrations and mappings support an incremental relational model.
- Persistence stays replaceable behind application behavior.

### Costs

- SQLite differs from hosted databases in concurrency, types, functions, and migration behavior.
- Provider switching is not automatic; tests and migration review are required.
- Careless EF Core usage can leak persistence concerns into domain or UI code.

## Future Extraction Seams

Infrastructure owns the database provider and mappings. Moving to Azure SQL or PostgreSQL should change Infrastructure and deployment configuration, not core learning rules. A module may receive separate storage only when ownership, scale, or isolation evidence justifies it.

## Implementation Notes

- EF Core and SQLite packages are version `10.0.5`.
- A patched `SQLitePCLRaw.bundle_e_sqlite3` `2.1.14` overrides the vulnerable transitive native bundle from the EF provider.
- `ICurriculumCatalog` is the narrow Application seam; no generic repository was added.
- The initial migration creates topic and lesson tables and inserts the nine-item development curriculum.
- Local migrations are applied explicitly with `dotnet ef database update`; the web app does not migrate at startup.
- Integration tests apply the real migration to isolated SQLite databases.
- Prompt 20 adds `AuthoredLessons` through `ILessonAuthoringService`, with an owner foreign key, Draft/Published status, optimistic version token, and UTC audit fields.
- SQLite cannot order `DateTimeOffset` values in SQL, so the small owner/public result sets are filtered in SQL and ordered in memory; a hosted provider should revisit this query plan.
