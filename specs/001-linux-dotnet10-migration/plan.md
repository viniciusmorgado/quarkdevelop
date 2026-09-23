# Implementation Plan: MonoDevelop on Linux with .NET 10 LTS and GTK3

**Branch**: `dotnet-linux-migration` (feature `001-linux-dotnet10-migration`) | **Date**: 2026-09-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-linux-dotnet10-migration/spec.md`

## Summary

Port MonoDevelop 8.6 (164 projects, .NET Framework 4.7.2 on Mono, Gtk# 2, Mono.Addins, autotools, no
CI) to a Linux-only IDE on .NET 10 LTS with a GTK3 UI, delivered as a Flatpak. The approach is a
three-step walking skeleton driven by a new primary solution `main/MonoDevelop.Linux.sln` that grows
in dependency-ordered waves:

1. **WS-1 headless** — `MonoDevelop.Core`, the out-of-process MSBuild builder, `mdtool` and a new
   UI-free `MonoDevelop.CSharpBinding.Core` build and test on `net10.0`; `mdtool build` compiles a
   .NET 10 console project.
2. **WS-2 GUI foundation** — Xwt + Xwt.Gtk3 (vendored) on GtkSharp 3.24.24 open a window on
   `net10.0`.
3. **WS-3 IDE** — `MonoDevelop.Ide`, Startup, GnomePlatform, the source editor and the essential
   add-ins run on GTK3; debugging through netcoredbg.

M0 spikes proved the three riskiest assumptions on this stack (see [research.md](research.md)):
GtkSharp 3 on .NET 10 under Xvfb, Mono.Addins 1.4.1 on CoreCLR (in-process registry scan, XML and
type extensions), and Roslyn 5.9 internals via Krafs.Publicizer (Roslyn no longer grants
`InternalsVisibleTo` to MonoDevelop).

## Technical Context

**Language/Version**: C# on .NET 10 LTS (SDK 10.0.401 in the container; `global.json` feature band
10.0.100 + `latestFeature`); `LangVersion` stays 8.0 during conversion, then SDK default.

**Primary Dependencies**: GtkSharp 3.24.24.x (NuGet), Xwt + Xwt.Gtk3 (vendored), Mono.Addins /
Mono.Addins.Setup / Mono.Addins.CecilReflector 1.4.1, Microsoft.CodeAnalysis 5.9.0 (+ EditorFeatures
from the public dnceng `dotnet-tools` feed when needed), Krafs.Publicizer 2.3.0,
Microsoft.Build 17.x (`ExcludeAssets=runtime`) + Microsoft.Build.Locator 1.11, Mono.Cecil 0.11.6,
Mono.Unix 7.1, NGettext, StreamJsonRpc 2.x, LibGit2Sharp 0.32, Microsoft.VisualStudio.Composition
17.x, Microsoft.VisualStudio.Shared.VsCodeDebugProtocol 17.x + netcoredbg.

**Storage**: Files only (IDE configuration under XDG dirs, add-in registry cache, solution files).

**Testing**: NUnit 3.14 (→ 4.x later) via Microsoft.NET.Test.Sdk / `dotnet test`; coverlet
(XPlat Code Coverage) + ReportGenerator; GUI smoke under `xvfb-run`; DAP-driven debug test.

**Target Platform**: Linux x86-64 (arm64 desirable), X11 and Wayland, GTK 3.24; reference
environment = podman container `Containerfile` (Ubuntu 24.04 based .NET SDK image); distribution =
Flatpak on `org.gnome.Platform`.

**Project Type**: Desktop application (IDE) with a plug-in architecture + CLI tool (`mdtool`).

**Performance Goals**: IDE start-up ≤ 10 s to main window on the reference machine; no UI-thread
stall > 1 s while loading a solution; clean CI run ≤ 15 min with warm cache.

**Constraints**: No Mono, no GAC, no BinaryFormatter/Remoting, no dead feeds or private repos; every
command runs inside the container; each commit keeps the Linux solution green; Linux-only focus
(macOS/Windows may break).

**Scale/Scope**: 99 non-fixture projects (164 in `Main.sln` incl. externals), ~5.3k C# files,
~1.2k files touching the GTK stack, 70 add-in manifests, ~3.8k tests, 700 P/Invoke declarations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Gate for this plan | Status |
|---|---|---|---|
| I | Linux-first, container-first | `Containerfile` + `scripts/pm`; all validation commands prefixed with `./scripts/pm` | PASS (T000 done) |
| II | .NET 10 pinned | `global.json`, `net10.0` SDK-style projects, CPM, no Mono/GAC/dllmap | PASS (planned M2/M3) |
| III | Reproducible builds | nuget.org only (+ADR-approved feeds), lock files, vendoring in `main/vendor/` | PASS (ADR 0004/0005) |
| IV | Incremental & reversible | waves W0–W4, one task per commit, UI ported per area | PASS |
| V | Quality gates | NUnit + coverlet + format + analyzers; coverage 60%/40% by M4; quarantine record | PASS (M4) |
| VI | Compatibility | add-in paths, sln/csproj formats, mdtool CLI preserved; `docs/BREAKING-CHANGES.md` | PASS |
| VII | Security | vulnerable packages upgraded; BinaryFormatter/Remoting removed; NuGet audit | PASS (M3) |
| VIII | Observability | structured logging backend, metrics/tracing where instrumentation exists | PASS (M8) |

Post-design re-check (after Phase 1): PASS — no violations; see Complexity Tracking for the two
justified deviations.

## Project Structure

### Documentation (this feature)

```text
specs/001-linux-dotnet10-migration/
├── plan.md              # This file
├── research.md          # Phase 0: decisions, spike results, alternatives
├── data-model.md        # Phase 1: runtime entities (target runtime, add-in, build result, ...)
├── quickstart.md        # Phase 1: runnable validation scenarios
├── contracts/           # Phase 1: mdtool CLI, scripts/, smoke-test and packaging contracts
├── checklists/          # spec quality checklist
└── tasks.md             # Phase 2 (the tasks step)
docs/
├── adr/                 # 0001..0017 architectural decisions (MADR)
├── evidence/M0..M9/     # validation outputs per milestone
├── linux/               # setup.md, troubleshooting.md
├── architecture.md
└── BREAKING-CHANGES.md
```

### Source Code (repository root)

```text
Containerfile                     # reference dev/CI environment
global.json                       # SDK pin
scripts/                          # pm (podman wrapper), git-commit, setup/restore/build/test/run/debug/package-flatpak/inventory
spikes/                           # M0 throwaway experiments (not part of the product build)
packaging/flatpak/                # Flatpak manifest + launcher (M7)
.github/workflows/                # ci.yml, release.yml (M6)
main/
├── MonoDevelop.Linux.sln         # primary solution (grows per wave; replaces Main.sln at end of M5)
├── Directory.Build.props         # branches on UsingMicrosoftNETSdk → msbuild/Linux/*.props
├── Directory.Packages.props      # central package versions
├── msbuild/Linux/                # Common.props, Addin.props, Test.props, BuildVariables.targets
├── vendor/                       # vendored third-party code with UPSTREAM.md (xwt, mono-addins.gui, debugger-libs, vs-editor-api subset, ...)
├── src/core/                     # MonoDevelop.Core, MonoDevelop.Ide, MonoDevelop.Startup, MSBuild builder, Mono.TextEditor.Shared
├── src/addins/                   # add-ins (CSharpBinding{,.Core}, SourceEditor2, DotNetCore{,.Core}, VersionControl, ...)
├── src/tools/mdtool/             # CLI host
└── tests/                        # UnitTests (base), MonoDevelop.Core.Tests, Ide.Tests, linux-smoke/{Hello,Broken,Smoke.sln}
```

**Structure Decision**: Keep the existing `main/` layout and convert projects in place (SDK-style,
explicit `Compile` lists at first). New build infrastructure lives in `main/msbuild/Linux/`;
vendored forks in `main/vendor/`; the Linux solution is the single entry point for build and test.

## Architecture & Key Decisions

Each decision has an ADR in `docs/adr/` (details and alternatives in [research.md](research.md)).

| ADR | Decision |
|---|---|
| 0001 | Spec-driven development; ADRs in MADR format |
| 0002 | `main/MonoDevelop.Linux.sln` (classic `.sln`, readable by the IDE's own `SlnFile`) grows per wave; replaces `Main.sln` after M5 |
| 0003 | In-place SDK-style conversion (`net10.0`, `EnableDefaultCompileItems=false` first); new props in `main/msbuild/Linux/`; no parallel legacy build |
| 0004 | Central Package Management, nuget.org-only `NuGet.config`, lock files, NuGet audit |
| 0005 | Per-dependency destination: NuGet package if maintained, else vendored in `main/vendor/<name>/` + `UPSTREAM.md`; submodules removed; DotDevelop only via cherry-pick with provenance |
| 0006 | Mono.Addins 1.4.1 on CoreCLR, single default `AssemblyLoadContext`, in-process registry scan, per-test-run registry dir |
| 0007 | `DotNetCoreTargetRuntime` (+ factory) replaces Mono/MS.NET runtimes on Linux; null-safe `SystemAssemblyService` |
| 0008 | MSBuild: `MSBuildLocator.RegisterInstance` at entry points; evaluation stays in-proc (custom evaluator); builds out-of-proc in a `net10.0` builder via the existing `BinaryMessage` protocol; cancellation via `BuildManager.CancelAllSubmissions` |
| 0009 | Remove Remoting/BinaryFormatter; `CallContext` → `AsyncLocal`; instrumentation IPC → StreamJsonRpc |
| 0010 | Roslyn 5.9 + Krafs.Publicizer for internal APIs (no IVT for MonoDevelop in Roslyn 5.x); exact version pin |
| 0011 | GTK2 → GTK3 per project/area, no compatibility shim; Stetic output frozen as hand-maintained code; `Gtk.Rc` themes → GTK3 CSS |
| 0012 | Editor: keep Mono.TextEditor + SourceEditor2 over a vendored vs-editor-api text subset; Cocoa/WPF editor excluded |
| 0013 | Native interop: shared `NativeLibraryMap` (`NativeLibrary.SetDllImportResolver`) replaces `.dll.config` dllmaps |
| 0014 | Localization: managed `.mo` reader (NGettext) behind `GettextCatalog`, replacing `Mono.Unix.Catalog` |
| 0015 | Tests: GuiUnit/NUnit 2 → NUnit 3.14 + Microsoft.NET.Test.Sdk + coverlet; later NUnit 4 |
| 0016 | Debugging user programs: concrete `NetCoreDbgSession` over `VSCodeDebuggerSession` + netcoredbg |
| 0017 | Exclusion list for the Linux solution (platform-specific and legacy add-ins) |

### Conversion waves

| Wave | Content | Milestone |
|---|---|---|
| W0 | NuGet replacements (Mono.Addins*, Cecil, Mono.Unix, Microsoft.Build*, Locator, Roslyn 5.9, System.CodeDom, StreamJsonRpc 2, VS Composition 17, Newtonsoft 13.0.4, SharpZipLib 1.4) | M2–M3 |
| W1 | Core, MSBuild builder, mdtool, CSharpBinding.Core, UnitTests, Core.Tests.Addin, Core.Tests | M3 |
| W2 | DotNetCore.Core (SDK discovery), MSBuildResolver, Mono.Debugging (vendored), TextTemplating (deferred) | M3 |
| W3 | Xwt + Xwt.Gtk3 (vendored), Mono.Addins.Gui (GTK3), vs-editor-api subset, NRefactory removal, Mono.TextEditor.Shared, Ide, Startup, GnomePlatform | M5a–M5b |
| W4 | SourceEditor2, CSharpBinding (GUI), Refactoring, Xml, DesignerSupport, Debugger + VSCodeDebugProtocol + NetCoreDbg, DotNetCore, PackageManagement, UnitTesting, VersionControl + Git, AssemblyBrowser, RegexToolkit, HexEditor, DocFood, ChangeLogAddIn, Gettext | M5c |

**Excluded (ADR 0017)**: MacPlatform, WindowsPlatform (+ WindowsAPICodePack), Debugger.Win32,
Mono.Debugging.Win32, CorApi*, Xwt.WPF/XamMac/Gtk.Mac/Gtk.Windows, Xamarin.PropertyEditing, macdoc,
MonoDevelop.TextEditor (Cocoa/WPF), GtkCore/Stetic designer, mdhost, mdmonitor,
PerformanceDiagnostics, AspNet (WebForms), WebReferences, Autotools, Subversion (all),
Debugger.Soft (Mono), Debugger.Gdb, fsharpbinding, VBNetBinding, ILAsmBinding, UserInterfaceTests,
`tests/ui`, MacPlatform.Tests, WindowsPlatform.Tests, mono-tools, mdtestharness, nuget-binary,
sharpsvn-binary.

### Milestones

M0 baseline & spikes → M1 constitution/spec/ADRs → M2 toolchain → M3 headless WS-1 (W0–W2) →
M4 tests & coverage → M5 GUI (M5a WS-2, M5b Ide compiles, M5c IDE runs + debug) → M6 CI/CD →
M7 Flatpak → M8 hardening & docs → M9 final validation & release. Acceptance criteria and
validation commands per milestone are in [quickstart.md](quickstart.md) and `tasks.md`.

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Volume of GTK2→GTK3 port (~1.2k files, custom drawing) | per-area commits, grep metric of remaining GTK2 APIs, Xvfb screenshots, WS-2 first |
| R2 | Roslyn internals (~160 files) | Publicizer (proven in T004b), exact pin, rewrite to public API where cheap |
| R3 | Mono.Addins under test hosts / ALC | proven in T005; per-run registry, single ALC; vendor if a patch is needed |
| R4 | Custom MSBuild evaluator vs SDK 10 targets | evaluation diff test against `dotnet msbuild -getItem`; later `ProjectInstance` (B35) |
| R5 | Archived submodules, dead feeds, private md-addins repo | vendor/NuGet per ADR 0005; drop `MdAddinsDirectory` |
| R6 | No pre-migration test baseline | static inventory + first net10 run + tracked quarantine; smoke tests from M3 |
| R7 | Debugger Mono → netcoredbg | minimal concrete session + automated DAP test |
| R8 | GtkSharp 3 community-maintained | pin, vendor-ready, GTK from system/Flatpak runtime |
| R9 | FPF `WindowsBase` clash; VS Text 16 vs 17 | vendored text-only subset; duplicate-assembly check in CI |
| R10 | Theme loss (Gtk.Rc) and dropped features | GTK3 CSS; `docs/BREAKING-CHANGES.md` |
| R11 | Licensing of bundled packages | nuget.org-only + SBOM; Roslyn/dnceng packages are MIT |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Explicit `Compile` lists kept after SDK-style conversion (vs. default globs) | Source trees contain files that must not compile on Linux (Mac/Win folders, generated files, dead code) | Globs would pull in hundreds of excluded files; switch to globs per project once green |
| Accessing Roslyn internals via Publicizer | MonoDevelop's C# support is built on ~160 files of internal Roslyn APIs | Rewriting all C# services to public APIs first would block the GUI milestone for months |
