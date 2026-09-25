# Data Model: MonoDevelop on Linux with .NET 10 LTS and GTK3

The migration introduces no new persistent data store. The entities below are the runtime and
process artifacts whose shape the migration must define or preserve.

## TargetRuntime (DotNetCoreTargetRuntime)
- **Fields**: `Id` (`"dotnet"`), `DisplayName` (e.g. `.NET 10.0.401`), `SdkVersion`, `SdkPath`,
  `MSBuildBinPath` (= `SdkPath`), `DotNetExePath`, `ReferencePacks` (from `packs/Microsoft.NETCore.App.Ref`),
  `IsRunning` (true when the host process is CoreCLR).
- **Rules**: exactly one runtime is the *current* runtime; when no SDK is found the runtime still
  exists (for loading) but reports `CanBuild = false` with a user-facing message (edge case in spec).
- **Relationships**: selected by `SystemAssemblyService.CurrentRuntime`; used by the build engine.

## Add-in (Mono.Addins)
- **Fields**: `Id`, `Version`, `Namespace`, `Dependencies[]`, `ExtensionPoints[]` (path + node types),
  `Extensions[]` (path + nodes), `Assemblies[]`, `Enabled`.
- **Rules**: extension paths from the 70 existing manifests are preserved (constitution VI); an
  add-in that fails to load is disabled and logged, never fatal (spec edge case).
- **Location**: `main/build/AddIns/<AddinBuildDir>/`; registry cache in the XDG config dir, or a
  per-run temp dir under tests.

## BuildRequest / BuildResult
- **Request fields**: solution or project path, optional project name, configuration, target
  (`Build`|`Clean`), runtime.
- **Result fields**: `Succeeded`, `Errors[]`/`Warnings[]` (file, line, column, code, message),
  `ExitCode` (0 success, 1 build failure or load/usage error — as in 8.6), `Duration`.
- **State transitions**: `Queued → Running → (Succeeded | Failed | Cancelled)`.

## DebugSession
- **Fields**: `ProcessId`, `Breakpoints[]` (file, line, enabled, hit count), `Threads[]`,
  `Frames[]`, `State`.
- **State transitions**: `NotStarted → Running ⇄ Paused → Exited(exitCode)`; cancel → `Exited`.

## QuarantinedTest
- **Fields**: fully-qualified test name, suite, reason category, note, owner, date added, linked
  task/issue.
- **Reason categories** (as used in `docs/evidence/M4/quarantine.md`):
  - `Mono-only`: tests a Mono feature with no .NET/Linux equivalent (GAC, Mono runtime).
  - `net4x-fixture`: needs an old-style .NET Framework fixture project to build on Linux (T134).
  - `legacy-fixture`: PCL, Xamarin or netstandard1.x fixture; these target frameworks are not supported
    (`docs/BREAKING-CHANGES.md`, T134).
  - `network`: needs nuget.org or another network resource; tests run offline (T143, T151).
  - `excluded`: needs an add-in excluded from the Linux build (ADR 0017).
  - `SDK-change`: MSBuild/.NET SDK behaviour differs from Mono's MSBuild.
  - `Bug`: a product bug, with a follow-up task.
  - `Flaky`: fails intermittently, with a follow-up task (none left after T135).
- **Rules**: marked `[Category("Quarantine")]` in code and listed in
  `docs/evidence/M4/quarantine.md` (later suites append to the same file); per suite, the count may
  not increase after the suite is first converted (ratchet).

## EvidenceRecord
- **Fields**: milestone, task id, command (as run via `./scripts/pm`), exit code, output file,
  commit SHA, date.
- **Location**: `docs/evidence/M<n>/`.
