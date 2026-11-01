# Observability and Performance Baseline

## Document Status

- Task: Prompt 25 — Observability and performance
- Measured: 2026-11-01
- Environment: Windows build 26200, .NET SDK 10.0.403, .NET runtime 10.0.14, 8 logical processors
- Scope: Local modular monolith and its bounded analysis service

## Outcome

CodeLogic now has server-controlled request correlation, privacy-safe structured analysis logs, an analysis trace and meter, liveness/readiness health endpoints, a one-entry cache for immutable curriculum content, and a repeatable package-free analysis load harness.

The measured baseline passed its initial budget. This is a local warm-process measurement for regression comparison, not a production capacity claim.

## Privacy Rules

Observability data must never contain learner source, quiz answers, authored prose, password/email, user/account ID, cookies, database connection strings, or secrets by default.

Allowed low-cardinality fields are:

- server-generated correlation ID;
- analysis outcome;
- source character count, never source text;
- elapsed time;
- fixed health state;
- fixed metric and activity names;
- lesson/attempt technical IDs already approved in `docs/QUALITY.md` where needed for lifecycle events.

Do not add source fragments, diagnostics containing learner text, route query strings, request bodies, or arbitrary exception data to telemetry. Unexpected analysis logs record only the exception type; the health response records only a generic state.

## Request Correlation

`CorrelationIdMiddleware` creates a new 32-character server GUID for every request. It:

- replaces `HttpContext.TraceIdentifier`;
- returns the value in `X-Correlation-ID`;
- opens a structured `CorrelationId` logging scope;
- attaches the value to the current request Activity for trace-to-log lookup;
- ignores a client-supplied `X-Correlation-ID`, preventing untrusted values from entering the scope.

Console scope display is enabled in `appsettings.json` for local review. A later hosted logging provider can ingest the same structured scope without changing application behavior.

## Analysis Traces and Metrics

The Roslyn analysis adapter instruments the existing bounded operation with built-in .NET APIs; it adds no external telemetry package or exporter.

| Signal | Name | Fields |
| --- | --- | --- |
| Activity source | `CodeLogic.Analysis` | operation `analyze`; source length, outcome, duration |
| Counter | `codelogic.analysis.operations` | outcome |
| Histogram | `codelogic.analysis.duration` | milliseconds, outcome |
| Structured event | Event 3001 | outcome, source length, elapsed milliseconds |
| Fault event | Event 3002 | exception type, source length, elapsed milliseconds |

Outcomes are the fixed analysis statuses plus `cancelled` and `faulted`, which keeps metric cardinality bounded. No learner-provided value is used as a metric tag.

No OpenTelemetry exporter is configured. Export destination, retention, sampling, cost, and access controls require a later deployment decision.

## Health Checks

| Endpoint | Meaning | Dependency work | Response exposure |
| --- | --- | --- | --- |
| `/health/live` | The ASP.NET Core process can respond | None | Overall status only |
| `/health/ready` | The app can connect to its configured database | SQLite connection check | Overall status only |

An unhealthy readiness response uses HTTP 503 through ASP.NET Core's health-check defaults. It does not reveal the database path, connection string, exception, schema, or account data. Liveness deliberately does not query SQLite so a database problem does not imply that the process should be restarted.

## Immutable Curriculum Cache

`CachedCurriculumCatalog` caches only the reviewed curriculum manifest returned by `EfCurriculumCatalog`:

- one fixed server-owned key;
- cache size limit of one entry;
- entry size of one unit;
- 30-minute absolute expiration;
- database fallback on a miss;
- no user-controlled keys;
- no student source, progress, attempts, identity data, or administrator-authored drafts.

The manifest is rebuilt after expiration and naturally after application restart. A future workflow that edits the seeded curriculum at runtime must introduce explicit invalidation before relying on this cache.

## Repeatable Analysis Load Test

Run from the repository root:

```powershell
dotnet run --project .\tests\CodeLogic.LoadTests\CodeLogic.LoadTests.csproj --configuration Release -- --operations 10000 --concurrency 4 --p95-budget-ms 50
```

The harness performs 50 warm-up operations, then mixes valid, syntax-error, and unsupported snippets. It checks every expected outcome and reports latency percentiles, throughput, approximate process-wide allocation, working-set delta, status counts, failures, and the p95 budget. It returns a non-zero exit code if any outcome is wrong or p95 exceeds the supplied budget.

The current Playground invokes `ICodeAnalysisService` through the Blazor Server circuit; there is no public HTTP analysis API. The harness therefore measures the exact analysis service boundary without adding an unused public endpoint. It excludes SignalR, HTTP, network, browser rendering, database, and multi-host overhead.

## Measured Baseline

Release run on 2026-11-01 after a warm-up:

| Measure | Result |
| --- | ---: |
| Operations | 10,000 |
| Concurrency | 4 |
| Failures or unexpected outcomes | 0 |
| Valid / syntax error / unsupported | 3,334 / 3,333 / 3,333 |
| Total measured duration | 106.409 ms |
| Throughput | 93,977 operations/second |
| p50 | 0.0422 ms |
| p95 | 0.0556 ms |
| p99 | 0.0778 ms |
| Maximum | 1.9045 ms |
| Approximate allocated bytes/operation | 6,506 bytes |
| Working-set delta | 17,321,984 bytes |

Allocation and working-set values are coarse process-wide observations and are not stable enough for a hard gate. Latency and correctness are the initial regression signals.

## Budgets and Actions

| Signal | Initial threshold | Action when exceeded |
| --- | --- | --- |
| Analysis correctness | Zero exceptions or unexpected outcomes | Block the change and investigate before merge |
| Local warm p95 | 50 ms for the documented 10,000/4 run | Repeat three times; profile if two runs fail |
| Analysis hard stop | 2 seconds per operation | Treat any timeout on the small baseline samples as a defect |
| Readiness | Database check returns before the hosting probe timeout | Investigate connection/file contention; do not restart solely from liveness |
| Cache growth | Exactly one curriculum entry | Block user-derived keys or additional entries until a memory budget exists |
| Metric cardinality | Fixed outcome values only | Remove learner/user/path values before deployment |
| Log privacy | Zero source, answer, personal-data, cookie, or secret fields | Treat as a security defect and remove/rotate affected logs |

The 50 ms p95 gate intentionally has headroom above the workstation baseline so ordinary machine noise does not create a vanity/flaky test. Tighten it only after CI provides repeatable hardware and at least several historical runs.

## Profiling Workflow

Visual Studio is the available profiler on this machine:

1. Select `CodeLogic.LoadTests` as the startup project.
2. Use **Debug > Performance Profiler**.
3. Select **CPU Usage** and **.NET Object Allocation Tracking**.
4. Run the documented 10,000-operation command arguments.
5. Inspect Roslyn parsing, supported-syntax validation, task scheduling, allocations, and unexpected exceptions.
6. Save profiler output outside source control unless a later task defines an artifact policy.

`dotnet-counters` and `dotnet-trace` were checked and are not installed. No tool was installed during Task 25. If a later approved task provisions them, useful built-in providers are `System.Runtime`, `Microsoft.AspNetCore.Hosting`, and the `CodeLogic.Analysis` meter. Follow the official .NET diagnostics documentation and do not record request bodies or learner source.

## Manual Verification

1. Run Web and open `/health/live`; expect HTTP 200 and `Healthy`.
2. Open `/health/ready`; expect HTTP 200 and `Healthy` while SQLite is reachable.
3. Inspect either response and confirm it contains no connection string, path, exception, or account data.
4. Inspect the response headers for `X-Correlation-ID`; confirm it is 32 hexadecimal characters.
5. Submit valid, invalid, and unsupported Playground snippets. Confirm logs show correlation, outcome, length, and duration but not the source text.
6. Navigate between lesson pages twice; behavior must remain identical while the fixed manifest is served from the bounded cache.
7. Run the load command and confirm `Passed` is `true`.

## Limitations and Next Triggers

- No external trace/metric/log backend, dashboards, alerts, retention, or sampling policy exists.
- The load baseline is local, warm, in-process, and synthetic; it is not an HTTP, browser, soak, stress, or production capacity test.
- The health checks prove process response and database connectivity only; they do not verify every lesson row, disk capacity, or external CDN availability.
- Cache hit/miss telemetry is intentionally omitted because there is one fixed entry and no demonstrated operational need.
- Repeatable HTTP/SignalR load tests should wait for a delivery environment with an isolated database and stable host configuration.
- Production thresholds must be remeasured on the actual hosting tier before alerts or scaling decisions are configured.

## References

- [ASP.NET Core health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [.NET metric instrumentation](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation)
- [ASP.NET Core in-memory caching](https://learn.microsoft.com/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)
- [`dotnet-counters`](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters)
- [`dotnet-trace`](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace)
