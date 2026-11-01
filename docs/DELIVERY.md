# CI/CD and Container Delivery

## Document Status

- Task: Prompt 26 — CI/CD and containers
- Last updated: 2026-11-01
- Deployment status: Build and validation only; no image is pushed and no environment is deployed
- Local container status: Docker is not installed on the verified workstation

## Outcome

CodeLogic now has a pull-request/main-branch GitHub Actions pipeline, deterministic .NET and NuGet inputs, a non-root multi-stage container image, hardened local Compose services, explicit database migration mode, and automated dependency/image vulnerability checks.

The container runs the existing modular monolith. It is **not** an arbitrary-code worker and does not compile or execute learner C#. No workflow has deployment credentials, write permission, registry login, cloud secret, or production connection string.

## Reproducible Inputs

| Input | Control |
| --- | --- |
| .NET SDK | `global.json` pins 10.0.403 with patch-only roll-forward |
| NuGet graph | Every project commits `packages.lock.json`; restore uses `--locked-mode` |
| EF CLI | `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.5 |
| NuGet audit | Moderate-or-higher direct/transitive findings are warnings-as-errors; the quality gate also lists vulnerable packages explicitly |
| Build images | SDK and ASP.NET Core image tags are pinned to multi-platform manifest digests |
| Workflow actions | Every `uses:` reference is pinned to a full commit SHA with a readable version comment |
| Updates | Dependabot proposes reviewed NuGet, Actions, and Docker updates weekly |

To intentionally update dependencies:

1. review the upstream release and security notes;
2. change the direct version, tool version, action SHA, or image digest;
3. run `dotnet restore CodeLogic.slnx --force-evaluate` only for an approved dependency change;
4. review every lock-file diff;
5. run the full quality gate and container CI before merging.

Normal builds must use locked restore and must not silently rewrite lock files.

## GitHub Actions Pipeline

`.github/workflows/ci.yml` runs for pull requests, pushes to `main`, and manual dispatch. Concurrent obsolete runs for the same ref are cancelled. The workflow has only `contents: read` permission.

### Quality gate job

On `ubuntu-24.04` it:

1. checks out source using a full-SHA-pinned official action;
2. installs the SDK from `global.json` using a full-SHA-pinned official action;
3. restores the local EF tool;
4. restores locked NuGet dependencies;
5. verifies formatting;
6. builds Release with warnings as errors;
7. runs unit and integration tests with coverage floors;
8. checks EF migration/model consistency;
9. audits direct and transitive packages;
10. runs the bounded analysis performance regression check.

The job reuses `scripts/quality-gate.ps1`, so local and CI quality rules do not drift.

### Container job

On a separate clean runner it:

1. builds `codelogic:ci` from digest-pinned .NET 10 images;
2. asserts that the final image declares a non-root user;
3. runs the explicit migration command against a temporary named SQLite volume;
4. starts the app with a read-only root filesystem, all Linux capabilities dropped, `no-new-privileges`, PID/memory/CPU limits, and only dedicated data/key volumes writable;
5. polls `/health/ready` and fails if the migrated container does not become ready;
6. removes the temporary container and volumes;
7. scans OS and library packages with a full-SHA-pinned Trivy action, failing on fixable High or Critical vulnerabilities.

Unfixed upstream findings are reported but do not block this initial gate because the repository cannot remediate them directly. Reassess that choice if the hosting risk or compliance requirement changes.

### Required GitHub owner setting

After the first successful pull-request run, the owner should configure a branch rule/ruleset for `main` requiring:

- **Quality gate**;
- **Container build and security**;
- at least one reviewed pull request if collaborators are later added.

Repository settings are not changed by source code, so this remains an explicit owner action.

## Container Design

The `Dockerfile` uses an SDK build stage and a smaller ASP.NET Core 10 chiseled-extra runtime stage. The final image:

- contains published output, not the SDK or source tree;
- runs as the Microsoft image's unprivileged `app`/`APP_UID` user;
- listens on container port 8080;
- stores SQLite under `/data` and Data Protection keys under `/keys`;
- contains no shell or package manager in the final chiseled image;
- has no Docker socket, host source mount, privileged flag, added capability, compiler endpoint, or code-runner service.

The `chiseled-extra` runtime retains globalization/time-zone support needed by a learner-facing application while keeping the final image smaller than the SDK image.

## Explicit Database Migration

Application startup still does **not** apply migrations automatically. The dedicated command is:

```text
dotnet CodeLogic.Web.dll --migrate
```

`DatabaseMigrationCommand` recognizes the exact case-sensitive `--migrate` argument, opens a scoped `CodeLogicDbContext`, applies reviewed EF migrations, then exits without starting the web server.

Compose runs this as a one-shot `migrate` service. The `app` service starts only after migration exits successfully. This is intended for the local named volume only. It is not approval to migrate production; a future deployment must use reviewed credentials, backup/rollback procedures, and explicit owner approval.

## Local Compose Workflow

Docker Desktop/Engine with Compose v2 is required but is currently absent on this workstation. Do not install it without owner approval.

When Docker is available:

```powershell
Copy-Item .env.example .env
docker compose config
docker compose build
docker compose up
```

Then verify:

```powershell
Invoke-WebRequest http://127.0.0.1:8080/health/live -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:8080/health/ready -UseBasicParsing
```

Open `http://127.0.0.1:8080`. Stop without deleting learning data:

```powershell
docker compose down
```

`docker compose down --volumes` permanently removes the local SQLite database and Data Protection keys. Use it only when the owner intentionally wants a complete local reset.

### Compose security controls

Both services use:

- non-root image user;
- read-only root filesystem;
- `cap_drop: ALL`;
- `no-new-privileges`;
- bounded CPU, memory, and process counts;
- small `tmpfs` for temporary runtime files;
- named volumes rather than host/repository mounts.

Only the app publishes a port, bound to host loopback. SQLite and key volumes are not externally exposed.

## Secret-Free Configuration

`.env.example` contains only the Compose project name, loopback HTTP port, and local image name. It contains no password, token, registry credential, encryption key, connection credential, or production host.

SQLite's local connection string has no password. Data Protection key material is generated at runtime into the ignored Docker named volume and is never committed. Those local keys are not protected by an external key-encryption service, so this Compose setup is for local development—not production.

Production secrets must come from the selected platform's secret store in a later deployment task. Never put them in `.env.example`, Compose YAML, appsettings, Docker build arguments, image layers, workflow YAML, or repository variables visible to pull requests.

## Local Quality Commands

Without Docker:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\quality-gate.ps1 -Configuration Release
```

Explicit publish verification:

```powershell
dotnet publish .\src\CodeLogic.Web\CodeLogic.Web.csproj --configuration Release --no-restore --output .\artifacts\publish
```

With Docker:

```powershell
docker build --tag codelogic:local .
docker compose config
docker compose up --build
```

## Acceptance and Limitations

Verified locally:

- SDK/tool/locked NuGet restore;
- formatting, Release build, tests, coverage, EF consistency, and NuGet audit;
- explicit migration command against an empty isolated SQLite database;
- ordinary framework-dependent publish;
- workflow, Dockerfile, Compose, Dependabot, and example configuration review.

Not verifiable on the current workstation:

- actual Docker/Compose build and runtime, because Docker is absent;
- Trivy image results, because there is no local image/runtime;
- a green GitHub pull-request pipeline, because changes are not committed or pushed and repository settings are owner-controlled.

The first PR run is therefore the remaining external acceptance evidence. If it fails, fix the configuration in Prompt 26 scope rather than weakening security gates without review.

No image push, registry publication, cloud deployment, production migration, release automation, signing, SBOM attestation, or environment promotion is implemented in this task.

## References

- [GitHub Actions: build and test .NET](https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net)
- [GitHub workflow dependency pinning](https://docs.github.com/actions/concepts/security/workflow-dependency-lockfiles)
- [Microsoft .NET container introduction](https://learn.microsoft.com/dotnet/core/docker/introduction)
- [ASP.NET Core Docker images](https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0)
- [Docker Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/)
- [Docker Compose service security fields](https://docs.docker.com/reference/compose-file/services/)
