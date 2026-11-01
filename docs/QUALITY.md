# Application Quality and Test Strategy

## Document Status

- Task: Prompt 21 — Application quality and test strategy
- Last updated: 2026-11-01
- Test framework: xUnit
- Repeatable gate: `scripts/quality-gate.ps1`

## Quality Goals

CodeLogic protects behavior that matters to a learner: domain invariants, deterministic answers, ownership, safe parsing, bounded traces, public/draft visibility, and relational persistence. Quality work should make those behaviors easier to trust and change. It should not add abstractions or tests solely to increase a metric.

## Test Pyramid

### Unit tests — broad and fast

`CodeLogic.UnitTests` tests framework-independent Domain behavior and architecture boundaries. These tests should remain the largest conceptual layer because they are fast, isolated, and explain rules clearly.

### Application and integration tests — focused seams

`CodeLogic.IntegrationTests` contains two established kinds of tests:

- fast application-behavior tests for curriculum navigation, deterministic evaluation, progress state, graph contracts, and trace navigation;
- real adapter tests using Roslyn, EF Core migrations, SQLite constraints, Identity ownership, and serialization.

The project name is historical; moving every fast application test merely to make the folder pyramid look perfect would create churn without changing behavior. New pure Domain tests belong in UnitTests. A future test-project split is justified only if integration execution time or dependency isolation becomes a measured problem.

SQLite tests use `SqliteTestDatabase` so migrations, connection lifetime, context creation, and disposal are implemented once. This is observed duplication removed from three existing test classes—not a generic repository or production abstraction.

### UI smoke tests — manual today

The accessibility and feature documents contain keyboard, narrow-screen, zoom, authentication, and end-to-end browser checks. Playwright was evaluated for Prompt 21 but is not added yet:

- Node and Microsoft Edge are installed on the current workstation;
- the repository does not provision a pinned Playwright runtime or matching browser;
- no delivery/CI environment currently installs and caches that browser;
- making the required gate depend on one developer's Edge installation would not be repeatable.

Adding Playwright becomes feasible when a later delivery task can pin the package, provision its browser without manual machine assumptions, start the app against an isolated migrated database, and run the same tests locally and in CI. Until then, the limitation is explicit rather than represented by permanently skipped tests.

## Coverage Policy

Coverage is a regression signal, not proof of correctness. `CodeLogic.runsettings` excludes generated EF migration operations and generated C# because authored behavior is the useful measurement.

The quality script enforces these floors:

| Test suite | Module | Line floor | Branch floor | Prompt 21 measured baseline |
| --- | --- | ---: | ---: | ---: |
| Unit | Domain | 75% | 70% | 82.4% / 79.6% |
| Integration | Application | 75% | 65% | 88.8% / 74.3% |
| Integration | Infrastructure | 85% | 60% | 87.7% / 70.6% |

The floors sit below the measured baseline so a harmless compiler or test-layout change does not block work, while a substantial untested regression still fails. They must not be lowered simply to make a build green; review whether behavior moved, generated code entered the report, or meaningful tests are missing.

Web has no percentage floor. Razor-generated code and composition code make a low number easy to misread, and raising it with constructor or markup-touch tests would be vanity coverage. Web quality is currently protected by build/analyzer checks, authorization metadata tests, application tests, and documented manual browser checks. Add a Web threshold only after repeatable HTTP/browser tests measure user-visible behavior.

## Repeatable Quality Gate

From the repository root in Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality-gate.ps1 -Configuration Release
```

The script stops at the first failure and runs:

1. NuGet restore;
2. `dotnet format --verify-no-changes`;
3. Release build with warnings treated as errors;
4. unit tests with coverage;
5. integration tests with coverage;
6. module line and branch regression floors;
7. EF migration/model consistency check;
8. direct and transitive NuGet vulnerability audit.

Reports are written beneath ignored `artifacts/quality/<timestamp>/`. The script does not commit, push, migrate the development database, or contact production systems.

## SOLID and Refactoring Evidence

- **Single responsibility:** the shared SQLite test helper owns only test database setup/lifetime; test classes retain scenario data and assertions.
- **Dependency inversion:** Web and Infrastructure continue to use Application ports such as `ILessonAuthoringService`; no database access moved into UI components.
- **Interface segregation:** no broad quality or repository interface was introduced. Existing narrow interfaces remain sufficient.
- **Open/closed and substitution:** no speculative hierarchy was added because no observed variation required one.
- **DRY with restraint:** only repeated SQLite connection/context/migration code was centralized. Similar-looking business assertions remain local because they describe different behavior.

## Logging and Privacy

Source-generated `LoggerMessage` methods record meaningful state transitions and rejected mutations:

- authored draft created, updated, published, ownership rejected, or concurrency rejected;
- quiz request missing its trusted owner claim, targeting an unknown lesson, or successfully recording an attempt.

The new application event logs may contain lesson IDs, attempt IDs, slugs, versions, and result names. They do not contain passwords, secrets, email addresses, user IDs, raw quiz answers, authored prose, or learner source code. Framework logging remains `Information` by default and `Warning` for ASP.NET Core noise.

Prompt 25 adds a server-controlled `CorrelationId` scope and analysis events containing only fixed outcome, source character count, duration, and—on unexpected failure—exception type. The client cannot supply the correlation value. Analysis traces and metrics use the same privacy rule and fixed-cardinality outcome tags. See `docs/PERFORMANCE.md`.

## Performance Regression Check

`tests/CodeLogic.LoadTests` is a package-free Release console harness rather than an xUnit timing assertion. This keeps workstation timing noise out of the required unit/integration suites while still providing a repeatable command, non-zero failure exit, and explicit p95 budget. It exercises the same `ICodeAnalysisService` boundary used by the Playground with valid, syntax-error, and unsupported samples.

The initial 10,000-operation, concurrency-4 baseline has zero failures and a 0.0556 ms p95 against a deliberately conservative 50 ms local budget. Re-run and profile repeated failures rather than weakening the threshold. This is not an HTTP or production capacity measurement.

## Error Handling

- Expected validation, missing-resource, forbidden-owner, and version-conflict outcomes use explicit result types and learner-safe messages.
- Production uses ASP.NET Core's exception handler and the `/Error` page; the response exposes only a correlation/request ID, not exception details.
- Unknown routes and status codes use recoverable pages.
- Unexpected exceptions are allowed to reach the central boundary so ASP.NET Core logs them. Broad catch-and-ignore blocks are not added.
- EF concurrency exceptions are the one adapter-specific expected exception translated into an authoring conflict.

## Cancellation

- HTTP request cancellation flows into quiz persistence.
- EF Core authoring and progress methods accept and pass cancellation tokens to database operations.
- Roslyn parsing, graph translation, feedback, and bounded interpretation accept caller cancellation.
- The analysis service distinguishes its own time limit from caller cancellation: time limits become a stable diagnostic; caller cancellation remains `OperationCanceledException`.
- Tests prove analysis and authored-draft persistence honor an already-cancelled token.

Do not catch `OperationCanceledException` as an application failure unless the code is specifically translating its own internal timeout.

## Manual Visual Studio Review

1. Open `CodeLogic.slnx` and build the solution in Release configuration; confirm zero warnings and errors.
2. Open **Test Explorer**, run all tests, and confirm all unit and integration tests pass.
3. Run `scripts/quality-gate.ps1` in the Visual Studio terminal and review the three printed coverage results.
4. Create, update, and publish an authored lesson; confirm Information-level lifecycle logs appear without account identifiers or lesson content.
5. Submit a valid quiz and an unknown lesson request; confirm structured event fields appear without the submitted answer.
6. Trigger an application error only in local development, then verify the production error page description remains generic and includes a request ID.
7. Complete the relevant manual browser checks in `docs/ACCESSIBILITY.md` and feature documents.
8. Verify `/health/live` and `/health/ready`, inspect `X-Correlation-ID`, and run the load command from `docs/PERFORMANCE.md`.

## Known Limitations

- Playwright and automated browser accessibility checks remain deferred until browser provisioning is repeatable.
- Web has no numeric coverage floor for the reasons above.
- Coverage reports are local artifacts; publishing them in CI belongs to a later delivery task.
- The existing integration project contains both fast application behavior and external-adapter tests; split it only when measured execution or dependency problems justify the change.
- External telemetry export, dashboards, alerts, and hosted retention remain deferred until a deployment environment and data-access policy exist.
