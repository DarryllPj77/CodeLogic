# CodeLogic Windows and Visual Studio Setup

## Document Status

- Status: Verified locally
- Playbook task: 03 — Windows and Visual Studio setup
- Verification date: 2026-10-31
- Scope: Prerequisite validation only; no software or system configuration was changed

## Setup Goal

CodeLogic development should work on Windows through Visual Studio with the .NET 10 SDK and ASP.NET Core tooling. This guide separates required tools from optional tools so a beginner can start the application without installing infrastructure that later tasks do not yet need.

## Verified Machine Summary

| Prerequisite | Required now? | Observed state | Result |
| --- | --- | --- | --- |
| Windows | Yes | Registry reports Windows 10 Pro, version 25H2, build 26200.9711, AMD64 | Pass |
| Visual Studio | Yes | Visual Studio Community 2026 18.11.3 at `D:\Visual Studio` | Pass |
| ASP.NET and web development workload | Yes | `Microsoft.VisualStudio.Workload.NetWeb` detected for the Visual Studio installation | Pass |
| .NET 10 SDK | Yes | SDKs 10.0.114 and 10.0.403 installed; 10.0.403 currently selected | Pass |
| ASP.NET Core 10 runtime | Yes | `Microsoft.AspNetCore.App` 10.0.14 installed | Pass |
| Git | Yes | Git for Windows 2.49.0 | Pass |
| Modern browser | Yes | Microsoft Edge 156.0.4314.50 and Google Chrome 156.0.8078.79 detected | Pass |
| SQLite inspection tool | Recommended | `sqlite3` 3.44.4 available on `PATH` | Pass |
| EF Core CLI | Not needed until persistence tasks | `dotnet-ef` 10.0.5 available | Pass |
| Docker Desktop | Optional | Docker command not found | Optional item missing |

## Exact Missing Prerequisites

**No required prerequisite is currently missing for the solution-skeleton task.**

The only detected missing item is:

- **Docker Desktop / Docker CLI — optional.** Do not install it merely to begin CodeLogic. It becomes useful only when a later task explicitly introduces containers or a container-based dependency.

Two repository governance files are also missing, although they are not Windows software prerequisites:

- `AGENTS.md`
- `docs/ROADMAP.md`

They should be restored or created through an explicitly scoped documentation task before application implementation proceeds.

## Required Tools

### 1. Compatible Windows environment

Use a supported 64-bit Windows version capable of running Visual Studio 2026 and the .NET 10 SDK. Keep Windows Update and security protections enabled.

Validation:

```powershell
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' |
    Select-Object ProductName, DisplayVersion, CurrentBuild, UBR
$env:PROCESSOR_ARCHITECTURE
```

Expected on the verified machine:

- `DisplayVersion`: `25H2`
- `CurrentBuild`: `26200`
- `UBR`: `9711`
- Architecture: `AMD64`

The registry's `ProductName` can retain an older marketing name after Windows feature upgrades. Build and support status are more useful than the label alone.

### 2. Visual Studio Community 2026 or a compatible edition

Visual Studio Community 2026 is installed. Professional or Enterprise is also acceptable when it supports .NET 10 and the same workload.

Use Visual Studio Installer to inspect or modify the installation. The required workload is:

- **ASP.NET and web development**

Do not select every optional component. The workload and .NET 10 support are sufficient for the initial Blazor solution.

Command-line validation with Visual Studio Installer's `vswhere`:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

& $vswhere -products '*' `
    -format json

& $vswhere -products '*' `
    -requires Microsoft.VisualStudio.Workload.NetWeb `
    -property installationPath
```

Expected on the verified machine:

```text
D:\Visual Studio
```

If the second command produces no path, open **Visual Studio Installer**, choose **Modify**, select **ASP.NET and web development**, review the change, and install it only with owner approval.

Manual validation:

1. Open Visual Studio Installer.
2. Find Visual Studio Community 2026.
3. Confirm the installation reports version 18.11.3 or another .NET 10-compatible version.
4. Select **Modify** only to inspect the workload list.
5. Confirm **ASP.NET and web development** is checked.
6. Close the installer without changing selections when no update is required.

### 3. .NET 10 SDK and runtime

The SDK compiles, tests, and runs .NET applications. A runtime alone is not enough for development.

Validation:

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

Minimum expected results:

- At least one `10.0.x` SDK.
- `Microsoft.NETCore.App 10.0.x`.
- `Microsoft.AspNetCore.App 10.0.x`.

Verified active SDK:

```text
10.0.403
```

The repository does not yet contain `global.json`, so the .NET CLI selects the newest compatible installed SDK. SDK pinning belongs to the later solution-skeleton task if the project requires it.

Optional workload validation:

```powershell
dotnet workload list
```

No optional .NET workloads are installed on the verified machine. This is **not a missing prerequisite**: ordinary ASP.NET Core and Blazor Web App development uses the SDK and Visual Studio web workload, not a separate `dotnet workload` pack.

### 4. Git for Windows

Git provides source control and GitHub synchronization.

Validation:

```powershell
git --version
git status
git remote -v
```

Verified version:

```text
git version 2.49.0.windows.1
```

Run `git status` from the CodeLogic repository root. Do not commit Visual Studio's `.vs/` directory.

### 5. Modern browser

Use a current browser with developer tools. Microsoft Edge or Google Chrome is sufficient for the initial Blazor application.

File and version validation:

```powershell
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
$chrome = Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'

Get-Item $edge, $chrome -ErrorAction SilentlyContinue |
    Select-Object FullName, @{ Name = 'Version'; Expression = { $_.VersionInfo.ProductVersion } }
```

At least one browser should be found. The verified machine has both Edge and Chrome.

Keep browser developer tools available for later responsive-layout, network, console, and accessibility checks.

## SQLite Tooling

SQLite is the planned local development database, but the database is not introduced in this task. EF Core will access SQLite from application code in a later persistence task.

### SQLite command-line tool

Validation:

```powershell
Get-Command sqlite3
sqlite3 --version
```

Verified result:

```text
3.44.4
```

The detected executable comes from the local Android SDK platform-tools directory and is available on `PATH`. It is adequate for basic inspection. A separate SQLite GUI is optional and should be installed only if a later task identifies a real need.

### EF Core command-line tool

Validation:

```powershell
Get-Command dotnet-ef
dotnet ef --version
```

Verified result:

```text
Entity Framework Core .NET Command-line Tools 10.0.5
```

`dotnet-ef` is not required until a task creates an EF Core project and migrations. Later tasks should prefer a repository-local tool manifest when reproducible tool versions become necessary; this setup task does not change the installed global tool.

## Optional Docker Desktop

Docker Desktop is not required for local Blazor, SQLite, or unit-test work. It may later support repeatable containers, PostgreSQL, or deployment exercises.

Validation:

```powershell
Get-Command docker -ErrorAction SilentlyContinue
docker --version
docker compose version
```

Current result:

```text
Docker command not found.
```

This is an optional gap, not a blocker. Do not install Docker Desktop until a numbered task requires it and the owner approves the installation and licensing/resource implications.

## One-Pass Validation Script

Run these non-destructive commands from PowerShell:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

& $vswhere -products '*' -property displayName
& $vswhere -products '*' -property installationVersion
& $vswhere -products '*' -requires Microsoft.VisualStudio.Workload.NetWeb -property installationPath

dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
git --version

Get-Command sqlite3 -ErrorAction SilentlyContinue
sqlite3 --version

Get-Command docker -ErrorAction SilentlyContinue
```

Interpretation:

- A Visual Studio name, version, and web-workload installation path should appear.
- A .NET `10.0.x` SDK and ASP.NET Core `10.0.x` runtime should appear.
- Git should report a version.
- `sqlite3` is recommended but can be added only when persistence work begins.
- An empty Docker result is acceptable until a container task.

## Visual Studio Readiness Check for Prompt 04

When the solution-skeleton task begins:

1. Open Visual Studio.
2. Choose **Open a local folder** and select the CodeLogic repository root, or open the solution after Prompt 04 creates it.
3. Do not use **Create a new project** before the prompt defines the full solution and project names.
4. Confirm the **Blazor Web App** template appears when searching templates. Do not create it during this setup task.
5. Confirm the target framework selector offers `.NET 10.0` when the project-creation step is authorized.

## Troubleshooting

### `dotnet` is not recognized

Close and reopen PowerShell after an approved SDK installation. Then run:

```powershell
where.exe dotnet
dotnet --info
```

Do not alter machine `PATH` until the installed SDK location has been confirmed.

### Visual Studio does not show Blazor templates

Verify the web workload with `vswhere`. If it is absent, use Visual Studio Installer to add **ASP.NET and web development** after owner approval. Do not install unrelated mobile, desktop, gaming, or data workloads to solve this problem.

### The wrong SDK is selected

Run:

```powershell
dotnet --version
dotnet --list-sdks
```

Check for a parent-directory `global.json` before changing SDK installations. A repository `global.json`, if later created, should document its intended SDK policy.

### `sqlite3` is not recognized

This does not block the initial solution. EF Core can create and access SQLite databases without the standalone CLI. Add a CLI or approved GUI only when manual database inspection is needed.

## Verification Outcome

The machine is ready for .NET 10 ASP.NET Core and Blazor solution creation. No required installation is missing. Docker Desktop remains optional and absent. No software was installed, updated, or reconfigured during this verification.
