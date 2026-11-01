# ADR 0009: Defer the Isolated Runner Until Containment Is Verified

- Status: Proposed
- Date: 2026-11-01
- Deciders: CodeLogic owner

## Context

Prompt 24 authorizes an experimental feasibility and security review for a separate code runner, but permits implementation only after sandbox support is verified. Arbitrary learner code is hostile input: a runner must protect the web application, database, host, network, credentials, other learners, and service availability even when code intentionally attempts escape or exhaustion.

The Task 24 audit found no Docker or Podman command, no Docker Desktop installation, no installed WSL capability/distribution, no Windows Sandbox executable, and no Hyper-V PowerShell module. Therefore the proposed machine cannot supply repeatable evidence for an OS-enforced sandbox, networking isolation, quotas, process-tree termination, or cleanup.

Microsoft also states that `AssemblyLoadContext` has no security features and recommends OS isolation for unknown code. Container defaults alone are insufficient: Docker documents that containers have no resource constraints unless limits are configured.

## Decision

Defer the arbitrary-code runner. Keep all compilation, emission, assembly loading, child-process execution, and general .NET execution disabled.

Continue using the deterministic pedagogical interpreter for the explicitly supported teaching subset. Preserve the future worker as an architecture seam only.

A later implementation requires:

1. a separate owner-approved task and environment;
2. a dedicated unprivileged worker boundary with no network or secrets;
3. hard CPU, memory, swap, PID, wall-time, input, output, and filesystem quotas;
4. disposable read-only runtime plus per-job temporary storage;
5. complete process-tree termination, cleanup verification, and worker quarantine;
6. tenant-capability isolation, bounded result contracts, privacy-safe logs, and an operational kill switch;
7. repeatable adversarial evidence from `docs/ISOLATED_RUNNER_SPIKE.md`;
8. independent security review and explicit owner acceptance of residual risk.

Any unavailable control must fail closed. CodeLogic must never fall back to execution in the web process.

## Alternatives Considered

### Execute in the ASP.NET Core process

Rejected. Learner code would inherit the web process's permissions, memory, network, filesystem, credentials, and database reachability. Timeouts and cancellation tokens are not security boundaries.

### Use `AssemblyLoadContext`, reflection, or a child process

Rejected. `AssemblyLoadContext` isolates dependency loading, not permissions. A normal child process still inherits meaningful host access unless an external OS security boundary removes it.

### Treat WSL or Windows Sandbox as sufficient

Rejected for the current environment because neither capability is installed and no policy or adversarial evidence exists. Even if present, availability alone would not prove headless per-job isolation, quotas, network denial, cleanup, or multi-tenant operation.

### Install Docker Desktop and build a container immediately

Rejected in this task. Installation and system changes were not approved, and an ordinary container with default settings does not meet the contract. A future candidate must prove rootless/unprivileged operation, reduced capabilities, syscall policy, no network, quotas, immutable image provenance, disposal, and host separation.

### Use a dedicated rootless container host or micro-VM later

Retained as a future experiment. It offers a plausible separate-worker boundary, but only the complete manual checklist and independent review can establish a go decision for a specific configuration.

### Keep the bounded pedagogical interpreter

Accepted. It supports the learning goals without compiling or executing general .NET code and already has explicit syntax and operation limits.

## Consequences

### Positive

- The current web, identity, database, and development machine are not exposed to experimental hostile execution.
- The product retains deterministic traces and explanations without claiming general C# runtime behavior.
- Future evaluation has a concrete evidence checklist and fail-closed decision rule.
- No container dependency, deployment topology, or operational cost is introduced prematurely.

### Negative

- Learners cannot run arbitrary C#, libraries, packages, file access, networking, or full .NET semantics in CodeLogic.
- A future runner requires separate infrastructure, security expertise, operational ownership, patching, monitoring, and recurring adversarial validation.
- The design remains unproven until a suitable isolated environment is explicitly approved and tested.

## Reconsideration Triggers

Reconsider only when the owner supplies a new numbered task, approves a dedicated non-sensitive sandbox host, and every mandatory control can be tested on the exact proposed runtime. Record a new ADR rather than weakening this decision silently.

## References

- [Task 24 spike evidence and checklist](../ISOLATED_RUNNER_SPIKE.md)
- [CodeLogic security threat model](../SECURITY.md)
- [ADR 0008: Disable arbitrary execution and require an isolated worker](0008-disable-arbitrary-execution-and-require-isolated-worker.md)
- [Microsoft secure coding guidelines](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
- [Microsoft `AssemblyLoadContext` remarks](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
- [Docker resource constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [Docker rootless mode](https://docs.docker.com/engine/security/rootless/)

