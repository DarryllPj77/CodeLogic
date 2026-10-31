# CodeLogic Persistence Foundation

## Document Status

- Task: Updated through Prompt 12 — Identity and owned progress
- Last updated: 2026-10-31
- Provider: EF Core 10.0.5 with SQLite
- Development database: `src/CodeLogic.Web/App_Data/CodeLogic.db`

## Scope

Prompt 11 introduced curriculum persistence. Prompt 12 adds standard ASP.NET Core Identity tables and durable, account-owned attempt and completion records. See `docs/IDENTITY.md` for the anonymous-study and security decisions.

## Boundary and Data Model

`CodeLogicDbContext`, mappings, migrations, seed conversion, and `EfCurriculumCatalog` live in Infrastructure. Web supplies the configured connection string and selects the EF adapter. Domain and Application do not reference EF Core or SQLite.

The application-owned schema has four tables in addition to standard Identity tables:

| Table | Purpose |
| --- | --- |
| `CurriculumTopics` | Stable topic ID, order, slug, title, and description. |
| `CurriculumLessons` | Lesson, exercise, sample, quiz, status, and difficulty fields with a required topic foreign key. |
| `UserExerciseAttempts` | Outcome-only attempt history owned by a user; raw answers are not stored. |
| `UserLessonProgress` | One completion row per user and lesson. |

Sections, allowed concepts, and answer options are stored as JSON text inside the lesson row. They are small ordered values loaded with the lesson and do not yet need independent querying or lifecycle. If authoring or section-level queries become real requirements, a reviewed migration can normalize them later.

Unique indexes protect topic slug/order and lesson/exercise order. Deleting a topic is restricted while lessons reference it.

## Development Seed

The typed `SeededCurriculumCatalog` remains the reviewed source used when scaffolding seed rows. The initial migration contains deterministic IDs and inserts:

- nine curriculum topics;
- two complete pilot lessons;
- seven draft lessons;
- exercise and deterministic quiz specifications for each lesson.

At runtime, `EfCurriculumCatalog` reads the database and reconstructs the validated `CurriculumManifest`. It refuses to return an invalid persisted manifest.

## Apply the Migration

From the repository root in PowerShell:

```powershell
dotnet restore CodeLogic.slnx
dotnet ef database update --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
```

The application does not apply migrations automatically. This keeps schema changes explicit and avoids granting production application processes migration permissions.

To inspect migration state:

```powershell
dotnet ef migrations list --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
dotnet ef migrations has-pending-model-changes --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
```

## Add a Future Migration

Change the EF model first, then run:

```powershell
dotnet ef migrations add DescriptiveMigrationName --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext --output-dir Persistence/Migrations
```

Review every generated migration before applying it. Do not use production credentials or connection strings from source control.

## Reset the Local Development Database

Warning: this intentionally deletes only the ignored local development database and all data inside it. Stop `CodeLogic.Web` first. Never use this procedure for production or a database containing data you need.

From the repository root:

```powershell
dotnet ef database drop --force --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
dotnet ef database update --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
```

The recreated database contains the migration seed. SQLite `.db`, `.db-shm`, and `.db-wal` files are ignored by Git.

## Test Isolation

Integration tests use a newly opened SQLite in-memory connection per test. Keeping each connection open for the test lifetime preserves that test's in-memory database while isolating it from every other test.

A separate temporary file-database test disposes its first `DbContext`, opens a new one, and proves the seeded catalog remains available. Test databases never use the development database path.

## Known Limitations

- SQLite behavior is not identical to Azure SQL or PostgreSQL; provider-specific testing is required before switching.
- Nested lesson collections use JSON text and are not independently queryable.
- Curriculum writes and an authoring UI are not implemented.
- Email confirmation, password recovery, two-factor authentication, and account management are not implemented yet.
- Only Development has a checked-in local connection string; other environments must supply `ConnectionStrings__CodeLogic` through their configuration or secret system.
