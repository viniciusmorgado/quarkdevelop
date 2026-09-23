# MonoDevelop (Linux / .NET 10) Constitution

This constitution governs the modernization of MonoDevelop from Mono/.NET Framework 4.7.2 + Gtk# 2
to a Linux-first IDE running on .NET 10 LTS (CoreCLR) with a GTK3 UI. It overrides any older
practice found in the repository (autotools, `make`, Mono-only scripts, Mac/Windows build flows).

## Core Principles

### I. Linux-First, Container-First

- Linux is the primary and only required platform for building, running, debugging and developing.
- The reference environment is the development container defined by the repository `Containerfile`
  and executed with rootless podman through `./scripts/pm`. Every documented command MUST work via
  `./scripts/pm <command>` (including its documented profiles, e.g. `PM_PROFILE=flatpak` for the
  Flatpak builder image); the only host prerequisites are `podman` and `git`.
- macOS and Windows are secondary. Code that only serves them MAY be broken, excluded from the Linux
  solution or removed when it blocks Linux work; every such removal is listed in
  `docs/BREAKING-CHANGES.md`.

Rationale: a single reproducible Linux environment removes "works on my machine" drift and lets the
same image power local work and CI.

### II. .NET 10 LTS, Explicitly Pinned

- `global.json` pins the SDK feature band `10.0.x` (`rollForward: latestFeature`); the container
  image pins the exact SDK build by tag.
- Every migrated project MUST be SDK-style, target `net10.0` (or `netstandard2.0` only when a
  documented consumer requires it) and take package versions from Central Package Management
  (`main/Directory.Packages.props`).
- Nothing may depend on Mono, the GAC, `mono`/`mono-sgen`/`mcs`, `<dllmap>`, or assemblies copied from
  a Mono installation.

### III. Reproducible Builds

- Restore, build, test, run, debug and package MUST succeed from a clean clone with the scripts in
  `scripts/`, run inside the container, with no manual steps.
- NuGet sources are limited to nuget.org plus feeds explicitly approved by an ADR; lock files and
  deterministic builds are enabled for migrated projects.
- Third-party code that must be modified is vendored under `main/vendor/<name>/` with an
  `UPSTREAM.md` (origin URL, source commit, license, local patches). No git submodule is added.

### IV. Incremental, Reversible, Verifiable Migration

- Work proceeds in waves recorded in `specs/001-linux-dotnet10-migration/tasks.md`. Each commit is
  small, lists the task IDs it implements in a `Tasks:` trailer (enforced by `scripts/git-commit`),
  keeps `main/MonoDevelop.Linux.sln` building, and is revertible with `git revert` without breaking
  earlier waves. One task per commit is the default; a commit may cover several tasks only when
  they cannot build independently, and it then says why in a `Coupled:` line.
- Porting of UI code happens one area (project or folder) at a time; no commit ports more than one
  UI project wholesale.
- A walking skeleton comes first: headless core → minimal GTK3 window → full IDE.
- Every task names the command or artifact that proves it is done.

### V. Quality Gates (NON-NEGOTIABLE)

- Behavior changes ship with automated tests in the same commit or before it.
- `dotnet format` (whitespace per `.editorconfig`) passes for every C# file added by this fork;
  legacy files are reformatted one project at a time in dedicated, behaviour-free commits. SDK
  analyzers run at `AnalysisLevel=latest-recommended`; warnings are errors except legacy warning IDs
  recorded in the project's generated baseline, which may never contain security rules (ADR 0018).
- Line coverage is measured with coverlet on every test run. Targets: `MonoDevelop.Core` ≥ 60% and
  global ≥ 40% by the end of milestone M4; afterwards coverage may not drop (ratchet).
- A failing legacy test may only be quarantined with `[Category("Quarantine")]` and an entry in
  `docs/evidence/M4/quarantine.md` stating the reason; the quarantine list shrinks over time.

### VI. Compatibility and Explicit Breaking Changes

- Preserve MonoDevelop's extension model (Mono.Addins extension paths and `*.addin.xml` manifests),
  the `.sln`/`.csproj` formats it reads and writes, and the `mdtool` command-line contract.
- Any removed add-in, platform, public API or behavior change is recorded in
  `docs/BREAKING-CHANGES.md` and, when architectural, in an ADR.

### VII. Security

- `dotnet list package --vulnerable --include-transitive` reports no High or Critical advisories for
  the Linux solution; NuGet audit is enabled.
- `BinaryFormatter`, .NET Remoting, and executing downloaded code without checksum verification are
  forbidden. Secrets never enter the repository; CI uses least-privilege tokens.

### VIII. Observability

- Logging goes through `LoggingService`, backed by a structured sink (Microsoft.Extensions.Logging)
  with level and format (`text`/`json`) configurable by environment variable.
- Existing instrumentation (`InstrumentationService`) is exposed via `System.Diagnostics.Metrics` /
  `ActivitySource` where applicable. Startup logs record IDE version, runtime and SDK versions.

## Delivery Standards

- **CI/CD**: a Linux pipeline (GitHub Actions) builds the same container image, restores with a
  cache keyed on lock files, builds, tests with coverage, checks formatting and vulnerabilities,
  runs a headless GUI smoke test under Xvfb, and uploads versioned artifacts (SemVer + commit SHA).
  Releases are produced from tags. Flatpak is the primary distribution format.
- **Documentation**: `README.md`, `docs/linux/setup.md`, `docs/linux/troubleshooting.md`,
  `CONTRIBUTING.md`, `docs/architecture.md` and `docs/adr/` are kept current in the same change that
  alters the behavior they describe.
- **Automation**: `scripts/{setup,restore,build,test,run,debug,package-flatpak}.sh` and `scripts/pm`
  use `set -euo pipefail`, are idempotent, and pass `shellcheck`.

## Development Workflow

- **Spec-Driven Development**: changes flow through the specification artifacts (constitution → spec →
  plan → tasks → implementation) under `specs/`. Artifacts are reviewed by independent reviewers before
  implementation, and the consistency analysis must report no CRITICAL issue before implementation.
- **ADRs**: every architectural decision is recorded in `docs/adr/NNNN-title.md` (MADR format).
- **Review before merge**: nothing merges into `main` without review and a green pipeline.
- **Git identity**: commits use only `Vinicius Morgado <34577818+viniciusmorgado@users.noreply.github.com>`
  (enforced by `scripts/git-commit`); pushing requires explicit maintainer authorization.
- **Definition of Done (per milestone)**: all acceptance criteria met; validation commands pass inside
  the container with their output stored under `docs/evidence/Mx/`; documentation updated; no new
  warnings; coverage at or above the ratchet; tasks checked off in `tasks.md`.

## Governance

This constitution supersedes other practices in the repository. Amendments are made by a change that
edits this file together with an ADR explaining the motivation and migration impact. Versioning
follows SemVer: MAJOR for removing or redefining a principle, MINOR for adding a principle or
materially expanding guidance, PATCH for clarifications. Every review and every the consistency analysis
run checks compliance; deviations must be justified in the plan's Complexity Tracking table.

**Version**: 1.2.1 | **Ratified**: 2026-09-23 | **Last Amended**: 2026-09-23 (amendment log: ADR 0001)
