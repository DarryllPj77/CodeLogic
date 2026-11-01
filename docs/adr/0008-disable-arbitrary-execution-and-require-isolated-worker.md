# ADR 0008: Disable Arbitrary Execution and Require an Isolated Worker

- Status: Proposed
- Date: 2026-11-01

## Context

CodeLogic accepts short C# snippets for syntax diagnostics, flowcharts, and bounded pedagogical traces. Executing arbitrary .NET code would expose the web process, credentials, database, filesystem, network, and other users to hostile code. Roslyn parsing and a deliberately limited interpreter do not make general execution safe.

The product may consider execution later, so the boundary must be decided before an implementation creates unsafe momentum.

## Decision

Keep arbitrary C#/.NET compilation, emission, assembly loading, and execution disabled.

- The web process may use Roslyn syntax APIs but may not compile or execute submitted source.
- The current pedagogical interpreter remains an allowlisted teaching model with size, nesting, time, and operation limits; it is not a general sandbox.
- No upload or execution endpoint is added.
- Static tests reject named execution primitives in production source.

Any future arbitrary execution must use a separately deployed and security-reviewed worker boundary. It must enforce no network, no host/secret mounts, read-only runtime, per-job writable quota, unprivileged per-job identity, CPU/memory/PID/time/output/file limits, process-tree termination, tenant-partitioned job capabilities, bounded results, and cleanup on every terminal path.

This ADR and `docs/SECURITY.md` define prerequisites only. A new explicit owner-approved security task and superseding/implementing ADR are required before code execution can be enabled.

## Alternatives Considered

### Execute inside the ASP.NET Core process

Rejected. A timeout or `try/catch` does not prevent process termination, memory exhaustion, secret access, filesystem/network access, or runtime exploitation. Failure compromises every user sharing the process.

### Load into a separate `AssemblyLoadContext` or AppDomain

Rejected as a security boundary. These mechanisms support loading/unloading and compatibility, not hostile-code isolation from the operating system or host process.

### Start a local child `dotnet` process

Rejected without an OS sandbox. A separate process alone can still inherit identity, environment, filesystem, network, handles, and host resource access.

### Allow only reviewed snippets in-process

Authored examples may be displayed and modeled, but review is not a durable sandbox control and cannot safely authorize arbitrary compilation. The bounded interpreter remains the supported path.

### Implement a container worker immediately

Deferred. Container defaults are not sufficient by themselves, and the repository has no approved image, orchestrator, network policy, quota enforcement, cleanup evidence, or operational monitoring.

## Consequences and Tradeoffs

### Benefits

- A malicious snippet cannot become arbitrary host code through the current feature set.
- Product language remains honest about simulation versus .NET execution.
- Future work has a clear fail-closed contract and evidence checklist.
- Tenant identity and secrets stay outside any future worker.

### Costs

- CodeLogic cannot demonstrate full .NET runtime behavior or arbitrary libraries.
- The supported interpreter requires explicit semantics for every added construct.
- A future execution feature will require significant security and operational investment.

## Enablement Gate

Execution remains disabled unless all are true:

1. the owner explicitly approves a dedicated execution task and threat model;
2. an OS-specific isolation design and conservative limits are accepted;
3. no-network, filesystem, identity, quota, cleanup, and cross-tenant adversarial tests pass;
4. the web application holds no execution primitive or worker secret;
5. worker image provenance, patching, monitoring, incident response, and kill switch are defined;
6. a new ADR records implementation evidence and residual risk.

If any control cannot be applied or verified, the system rejects the job and presents execution as unavailable.
