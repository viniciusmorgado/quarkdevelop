# Tasks: MonoDevelop on Linux with .NET 10 LTS and GTK3

**Input**: Design documents from `/specs/001-linux-dotnet10-migration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Requested by the constitution (principle V) and the spec (SC-002…SC-006): test tasks are
included per story.

**Organization**: Grouped by user story; each task also carries its milestone (`M0`…`M9`) in the
description. Every command runs inside the container: prefix with `./scripts/pm` (see quickstart).
Each task's "→" names the command or artifact that proves completion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on unfinished tasks)
- **[Story]**: user story (US1…US6) from spec.md

## Path Conventions

Repository root = worktree root; product code under `main/`; docs under `docs/`; specification under
`specs/001-linux-dotnet10-migration/`.

---

## Phase 1: Setup (M0 baseline, M1 specification)

**Purpose**: reference environment, inventory, spikes, governance artifacts.

- [x] T001 M0: Create `Containerfile` (dotnet/sdk:10.0.401-noble + GTK3 + Xvfb + specify 1.0.6) and `scripts/pm` podman wrapper → `./scripts/pm dotnet --info`
- [x] T002 M0: Create `scripts/git-commit` enforcing the no-reply identity → `git log -1 --format=%ae`
- [x] T003 M0: Specification layout (constitution, `specs/001-linux-dotnet10-migration/`) → files exist
- [x] T004 M0: Initialize all 15 submodules inside the container → `git submodule status`
- [x] T005 [P] M0: Write `scripts/inventory.sh` and generate `docs/evidence/M0/inventory.md`
- [x] T006 [P] M0: Spike GtkSharp 3 window on .NET 10 in `spikes/gtk3-hello/` → `docs/evidence/M0/T006-gtk3-hello.png`
- [x] T007 [P] M0: Spike Mono.Addins 1.4.1 on CoreCLR in `spikes/addins-host/` + `spikes/addins-plugin/` → "addins-spike: OK"
- [x] T008 [P] M0: Spike Roslyn 5.9 IVT dump (`spikes/roslyn-ivt/`) and Publicizer access (`spikes/roslyn-publicizer/`) → "roslyn-publicizer: OK"
- [x] T009 [P] M0: Time-boxed legacy Mono baseline attempt → `docs/evidence/M0/T003-legacy-baseline.md`
- [x] T010 [P] M0: DotDevelop prior-art review with cherry-pick candidates → `docs/evidence/M0/T007-dotdevelop.md`
- [x] T011 M0: Record spike outcomes in `specs/001-linux-dotnet10-migration/research.md` and `docs/evidence/M0/README.md`
- [ ] T012 M1: Constitution v1.0.0 in `docs/constitution.md`; spec, plan, research, data-model, contracts, quickstart, tasks under `specs/001-linux-dotnet10-migration/`
- [ ] T013 [P] M1: ADRs 0001–0017 in `docs/adr/` (MADR) + `docs/adr/README.md` index
- [ ] T014 [P] M1: `docs/BREAKING-CHANGES.md` listing excluded platforms/add-ins (ADR 0017)
- [ ] T015 M1: Independent review of spec/plan/tasks + the consistency analysis; fix all CRITICAL/HIGH findings → `docs/evidence/M1/analyze.md`

---

## Phase 2: Foundational (M2 toolchain + W0) — BLOCKS all stories

**Purpose**: SDK pin, package management, Linux props, primary solution, scripts, minimal CI.

- [ ] T016 M2: Add `global.json` (SDK 10.0.100, `rollForward: latestFeature`) at repo root → `./scripts/pm dotnet --version`
- [ ] T017 M2: Replace root `NuGet.config` with nuget.org-only sources (+ `<packageSourceMapping>`), document removed feeds in ADR 0004
- [ ] T018 M2: Create `main/Directory.Packages.props` (CPM) with W0 versions: Mono.Addins* 1.4.1, Mono.Cecil 0.11.6, Mono.Unix 7.1.0-final.1.21458.1, Microsoft.Build* 17.x, Microsoft.Build.Locator 1.11.2, Microsoft.CodeAnalysis* 5.9.0, Krafs.Publicizer 2.3.0, System.CodeDom, StreamJsonRpc 2.x, Microsoft.VisualStudio.Composition 17.x, Newtonsoft.Json 13.0.4, SharpZipLib 1.4.2, NGettext, NUnit 3.14, Microsoft.NET.Test.Sdk, coverlet.collector
- [ ] T019 M2: Create `main/msbuild/Linux/Common.props` (net10.0, output to `main/build/bin`, `AppendTargetFrameworkToOutputPath=false`, `GenerateAssemblyInfo=false`, LangVersion 8, PublicSign with `MonoDevelop-Public.snk`, `DefineConstants GNOME;LINUX`, NuGet audit, lock files), `Addin.props` (`build/AddIns/$(AddinBuildDir)`, non-private refs), `Test.props`
- [ ] T020 M2: Make `main/Directory.Build.props`/`.targets` branch on `$(UsingMicrosoftNETSdk)` so SDK-style projects import only `msbuild/Linux/*.props`; drop `MdAddinsDirectory`/md-addins dependency for SDK projects
- [ ] T021 M2: Add `main/msbuild/Linux/BuildVariables.targets` generating `BuildVariables.cs` from `version.config` + git SHA (replaces Mono `configure.exe` step in `MonoDevelop.Core.csproj`)
- [ ] T022 M2: Create `main/MonoDevelop.Linux.sln` (initially `msbuild/MDBuildTasks` converted to net10.0 or excluded per ADR) → `./scripts/pm dotnet build main/MonoDevelop.Linux.sln`
- [ ] T023 [P] M2: Scripts `scripts/{setup,restore,build,test,run,debug}.sh` per `contracts/scripts.md` → `shellcheck` + two consecutive `build.sh` runs
- [ ] T024 [P] M2: Add netcoredbg (pinned release + sha256) to `Containerfile`; `.devcontainer/devcontainer.json` using the same Containerfile
- [ ] T025 [P] M2: Extend `.editorconfig` with analyzer severities for migrated projects; `dotnet format --verify-no-changes` wired in `scripts/build.sh --check`
- [ ] T026 M2: Minimal CI `.github/workflows/ci.yml`: build image, restore (cache on lock files), build Linux solution, format check
- [ ] T027 M2: Evidence `docs/evidence/M2/` (build logs ×2, shellcheck, dotnet --version)

**Checkpoint**: Linux solution builds (even if small); scripts and CI green.

---

## Phase 3: User Story 1 — Headless core builds and tests on Linux (P1) 🎯 MVP (M3/M4)

**Goal**: `MonoDevelop.Core` (+ C# project support) compiles on net10.0 and its test suite runs with coverage.
**Independent Test**: `./scripts/pm ./scripts/build.sh && ./scripts/pm ./scripts/test.sh` → TRX + coverage summary; no Mono in the container.

### Implementation for User Story 1

- [ ] T028 [US1] M3: Convert `main/src/core/MonoDevelop.Core/MonoDevelop.Core.csproj` to SDK-style net10.0 (explicit Compile list; remove ReferencesGtk/ReferencesVSEditor imports; remove System.Web/ServiceModel/Remoting/monodoc refs; PackageReferences via CPM)
- [ ] T029 [US1] M3: Exclude/guard Linux-irrelevant Core files: `MonoDevelop.Core.Web/STSAuthHelper.cs`, `WIFTypeProvider.cs`, `MSBuildEngineV12.cs`, monodoc part of `MonoDevelop.Projects/HelpService.cs` (`#if MONODOC`)
- [ ] T030 [US1] M3: Remove Remoting/BinaryFormatter in Core (`MonoDevelop.Core.Execution/RemotingService.cs`, `ProcessHostController.cs`, `RemoteProcessObject`, `DisposerFormatterSink`, `InstrumentationService` remoting) per ADR 0009
- [ ] T031 [US1] M3: Replace `CallContext.LogicalSetData` with `AsyncLocal<T>` in `MonoDevelop.Projects.MSBuild/MSBuildProjectService.cs` and `MonoDevelop.Projects/WorkspaceObject.cs`
- [ ] T032 [US1] M3: Add `DotNetCoreTargetRuntime` + factory in `main/src/core/MonoDevelop.Core/MonoDevelop.Core.Assemblies/`, register in `MonoDevelop.Core.addin.xml` (`/MonoDevelop/Core/Runtimes`), null-guard `SystemAssemblyService.cs` (data-model: TargetRuntime)
- [ ] T033 [US1] M3: `MSBuildLocator` bootstrap helper in Core (`MonoDevelop.Core/Runtime.cs` `LoadMSBuildLibraries` no-op on .NET, `GetMSBuildBinPath` → SDK dir)
- [ ] T034 [US1] M3: `NativeLibraryMap` helper (`main/src/core/MonoDevelop.Core/MonoDevelop.Core/NativeLibraryMap.cs`) replacing `MonoDevelop.Core.dll.config` dllmaps (ADR 0013)
- [ ] T035 [US1] M3: Switch Mono.Posix → Mono.Unix 7.1 NuGet (`LoggingService.cs`, `ProcessService.cs`, `Runtime.cs`, `FileService.cs`, `TextFile.cs`); remove `mono_pmip` use in `Utilities/SampleProfiler.cs`
- [ ] T036 [US1] M3: NGettext-backed `GettextCatalog` in `main/src/core/MonoDevelop.Core/MonoDevelop.Core/Gettext.cs` (ADR 0014)
- [ ] T037 [US1] M3: Replace `Assembly.LoadFrom` paths needing ALC awareness (`Runtime.cs`, `SdkResolution.cs`) and `Type.GetType("Mono.Runtime")` checks with a `Platform.IsMono`-free runtime check
- [ ] T038 [US1] M3: Create `main/src/addins/CSharpBinding/MonoDevelop.CSharpBinding.Core/` (SDK-style, net10.0) with `CSharpProjectExtension`, `CSharpCompilerParameters` (IdeApp calls → hook), `CSharpLanguageVersionHelper`, `CSharpResourceIdBuilder`, `PortableCSharpProjectFlavor` and an addin manifest registering the C# `DotNetProjectType`
- [ ] T039 [US1] M3: Upgrade vulnerable/obsolete packages (Newtonsoft 13.0.4, SharpZipLib 1.4.x, Cecil 0.11.6, StreamJsonRpc 2.x, VS Composition 17.x, drop ValueTuple/System.Net.Http shims) → `dotnet list package --vulnerable`
- [ ] T040 [US1] M3: Add Core + CSharpBinding.Core to `main/MonoDevelop.Linux.sln` → `dotnet build -warnaserror`

### Tests for User Story 1

- [ ] T041 [US1] M4: Convert `main/tests/UnitTests/UnitTests.csproj` to SDK-style net10.0 on NUnit 3.14; replace GuiUnit main-thread plumbing with a `[SetUpFixture]` synchronization context; per-run Mono.Addins registry dir
- [ ] T042 [US1] M4: Convert `main/tests/MonoDevelop.Core.Tests.Addin` and `main/tests/MonoDevelop.Core.Tests` (TestFixtureSetUp→OneTimeSetUp, ExpectedException→Assert.Throws); `TestBase` default runtime → DotNetCore
- [ ] T043 [US1] M4: Retarget/reference-pack strategy for `main/tests/test-projects` fixtures (Microsoft.NETFramework.ReferenceAssemblies where net4x is required)
- [ ] T044 [US1] M4: Run suite, quarantine failures with reasons in `docs/evidence/M4/quarantine.md` (≤ 15 %), coverage via coverlet + ReportGenerator in `scripts/test.sh`
- [ ] T045 [US1] M4: Raise Core coverage to ≥ 60 % (targeted tests for new runtime/interop/localization code); add coverage ratchet file `docs/evidence/M4/coverage-baseline.txt`
- [ ] T046 [US1] M4: Evidence `docs/evidence/M3/` and `docs/evidence/M4/` (build log, TRX summary, coverage summary)

**Checkpoint**: US1 complete — the MVP walking skeleton step 1.

---

## Phase 4: User Story 2 — `mdtool` builds C# projects (P1) (M3)

**Goal**: `mdtool build` builds/cleans SDK-style net10 projects and solutions per `contracts/mdtool-cli.md`.
**Independent Test**: quickstart M3 block.

### Tests for User Story 2

- [ ] T047 [P] [US2] M3: Sample projects `main/tests/linux-smoke/Hello/Hello.csproj` (net10 console printing "Hello"), `Broken/Broken.csproj` (compile error), `Smoke.sln` (Hello + a class library)
- [ ] T048 [P] [US2] M3: Contract test `main/tests/MonoDevelop.Core.Tests/.../MdtoolContractTests.cs` running the mdtool build scenarios (exit codes 0/1, `-p:`, `-t:Clean`, `-c:`)

### Implementation for User Story 2

- [ ] T049 [US2] M3: Convert MSBuild builder `main/src/core/MonoDevelop.Projects.Formats.MSBuild/MonoDevelop.MSBuildBuilder.csproj` to net10.0 exe; drop Remoting ref; `Thread.Abort` → `BuildManager.CancelAllSubmissions` in `BuildEngine.Shared.cs`; MSBuildLocator at entry
- [ ] T050 [US2] M3: `RemoteBuildEngineManager.cs`: launch builder with `dotnet exec`, pass SDK paths via environment, stop copying MSBuild bin dir / patching `exe.config`
- [ ] T051 [US2] M3: Convert `main/src/tools/mdtool/mdtool.csproj` to net10.0 exe; MSBuildLocator bootstrap in `mdtool.cs`; `.addins` file pointing to `../AddIns`; `-r:` accepted and ignored with warning in `BuildTool.cs`
- [ ] T052 [US2] M3: Move headless SDK discovery (`DotNetCorePath`, `DotNetCoreSdk*`, `MSBuildSdks*`) into `main/src/addins/MonoDevelop.DotNetCore/MonoDevelop.DotNetCore.Core/` (net10.0) and reference it from Core's runtime
- [ ] T053 [US2] M3: Cherry-pick DotDevelop MSBuild evaluator fixes for net5+ projects (`7045264a30`, `4518519b5e`, `21031632fd`, provenance in commit message) and add an evaluation diff test vs `dotnet msbuild -getItem:Compile -getProperty:TargetPath` for linux-smoke projects (R4)
- [ ] T054 [US2] M3: Add builder, mdtool, DotNetCore.Core, linux-smoke to the Linux solution; run the quickstart M3 block → `docs/evidence/M3/mdtool.md`

**Checkpoint**: US1 + US2 = headless walking skeleton (WS-1) done.

---

## Phase 5: User Story 3 — Graphical IDE on GTK3 (P2) (M5)

**Goal**: IDE starts on GTK3, opens/edits/builds C# solutions (contracts/smoke-test.md).
**Independent Test**: `xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test main/tests/linux-smoke/Smoke.sln` exits 0.

### M5a — GUI foundation (WS-2)

- [ ] T055 [US3] M5: Vendor xwt into `main/vendor/xwt/` (+ `UPSTREAM.md`); remove submodule; port `Xwt` + `Xwt.Gtk3` to net10.0 on GtkSharp 3.24.24 (NuGet); sample window under Xvfb
- [ ] T056 [US3] M5: Vendor `Mono.Addins.Gui` into `main/vendor/mono-addins-gui/` ported to GTK3
- [ ] T057 [US3] M5: Vendor vs-editor-api text subset into `main/vendor/vs-editor-api/` (Text.Data/Logic/Implementation only; no FPF/WindowsBase) targeting net10.0
- [ ] T058 [US3] M5: Remove NRefactory usages from `MonoDevelop.Ide` (replace with Roslyn/Cecil equivalents) or vendor minimal subset into `main/vendor/nrefactory/`

### M5b — Ide compiles on GTK3 (one area per commit)

- [ ] T059 [US3] M5: Convert `main/src/core/MonoDevelop.Ide/MonoDevelop.Ide.csproj` to SDK-style net10.0 with GtkSharp 3 + Publicizer (Roslyn internals); exclude not-yet-ported areas via tracked `Compile Remove` list `main/src/core/MonoDevelop.Ide/Gtk3PortPending.props`
- [ ] T060 [US3] M5: Fix `Ide.Gui/SyncContext.cs` delegate `BeginInvoke` and other CoreCLR-only failures
- [ ] T061 [US3] M5: Port (using DotDevelop PR #10 `c5374d0332` / PR #150 `dcd1055dbb` as guides) `MonoDevelop.Components/` + `Components.Theming` + `Components.Extensions` (Expose→Drawn, size negotiation, styles→CSS)
- [ ] T062 [US3] M5: Port `Components.Docking/` + `Components.DockNotebook/` + `Components.MainToolbar/`
- [ ] T063 [US3] M5: Port `Components.Commands/` + `Ide.Commands/` + `Components.PropertyGrid*/` + `Components.Chart/`
- [ ] T064 [US3] M5: Freeze Stetic output: `main/src/core/MonoDevelop.Ide/Gui/` generated code ported to GTK3 as hand-maintained source; delete `gui.stetic`
- [ ] T065 [US3] M5: Port `Ide.Gui*` (Shell, Pads, ProjectPad, Components, Dialogs, OptionPanels, Documents, Wizard)
- [ ] T066 [US3] M5: Port `Ide.Projects*`, `Ide.Execution`, `Ide.FindInFiles`, `Ide.WelcomePage`, `Ide.CodeCompletion`, `Ide.Editor*`, `Ide.Fonts`, remaining areas; `Gtk3PortPending.props` empty
- [ ] T067 [US3] M5: GTK3 CSS themes replacing `Gtk.Rc` usage (light/dark) in `main/src/core/MonoDevelop.Ide/MonoDevelop.Ide.Gui/IdeTheme.cs`
- [ ] T068 [US3] M5: `NativeLibraryMap` entries for gtk/gdk/glib/pango/cairo; remove `MonoDevelop.Ide.dll.config`

### M5c — IDE runs

- [ ] T069 [US3] M5: Convert `MonoDevelop.Startup` (net10.0 exe `MonoDevelop.dll`), `Mono.TextEditor.Shared`, `GnomePlatform` (GTK3, gio P/Invoke via NativeLibraryMap)
- [ ] T070 [US3] M5: Port `MonoDevelop.SourceEditor2` + Mono.TextEditor rendering to GTK3/Cairo
- [ ] T071 [US3] M5: Port `CSharpBinding` (GUI) + `MonoDevelop.Refactoring` on Roslyn 5.9 (Publicizer; EditorFeatures from dnceng feed if required, ADR 0004 amendment)
- [ ] T072 [P] [US3] M5: Port `Xml`, `DesignerSupport`, `AssemblyBrowser`, `RegexToolkit`, `HexEditor`, `DocFood`, `ChangeLogAddIn`, `Gettext` add-ins
- [ ] T073 [P] [US3] M5: Port `MonoDevelop.DotNetCore` (GUI), `MonoDevelop.PackageManagement` (NuGet 6.x/7.x client), `MonoDevelop.UnitTesting` (+ VSTest)
- [ ] T074 [P] [US3] M5: Port `VersionControl` + `VersionControl.Git` on LibGit2Sharp 0.32 (reference DotDevelop `216f01c79f`, `2356bb926d`) (remove libgit2/libgit-binary/libgit2sharp submodules)
- [ ] T075 [US3] M5: `--smoke-test` option in `IdeStartup.cs` per `contracts/smoke-test.md`
- [ ] T076 [US3] M5: Convert `main/tests/Ide.Tests`, `IdeUnitTests`, `MonoDevelop.CSharpBinding.Tests` to NUnit 3.14 under Xvfb; quarantine with reasons
- [ ] T077 [US3] M5: `Main.sln` replaced by `MonoDevelop.Linux.sln` content (ADR 0002); excluded projects removed from the build; update `docs/BREAKING-CHANGES.md`
- [ ] T078 [US3] M5: Evidence `docs/evidence/M5/` (smoke log + screenshot, GTK2-API grep = 0)

**Checkpoint**: IDE usable on GTK3.

---

## Phase 6: User Story 4 — Debugging .NET 10 programs (P2) (M5)

**Goal**: breakpoints, stepping, locals via netcoredbg.
**Independent Test**: automated DAP scenario test passes.

- [ ] T079 [US4] M5: Vendor `Mono.Debugging` (debugger-libs) into `main/vendor/debugger-libs/Mono.Debugging/` on net10.0; remove submodule
- [ ] T080 [US4] M5: Port `MonoDevelop.Debugger` + `MonoDevelop.Debugger.VSCodeDebugProtocol` (Microsoft.VisualStudio.Shared.VsCodeDebugProtocol 17.x)
- [ ] T081 [US4] M5: Implement `NetCoreDbgSession` (starting from DotDevelop cherry-pick `379883b7c5` `DotNetCoreDebuggerSession`, MIT) + engine registration in `main/src/addins/MonoDevelop.Debugger.VSCodeDebugProtocol/` (netcoredbg path from container/Flatpak)
- [ ] T082 [US4] M5: Debug scenario test (breakpoint, locals, step, exit code) in `main/src/addins/MonoDevelop.Debugger/MonoDevelop.Debugger.Tests/NetCoreDbgTests.cs`
- [ ] T083 [US4] M5: `.vscode/launch.json` + `scripts/debug.sh` for debugging the IDE itself (coreclr/netcoredbg attach)

---

## Phase 7: User Story 6 — CI/CD (P3) (M6)

- [ ] T084 [US6] M6: Full `ci.yml`: build+test+coverage+format+audit+GUI smoke (Xvfb) in the dev image; artifacts with SemVer+SHA (MinVer or version.config+SHA)
- [ ] T085 [P] [US6] M6: `release.yml` on `v*` tags; Dependabot; CodeQL; README badges
- [ ] T086 [US6] M6: `scripts/ci.sh` reproducing the pipeline locally in the container → `docs/evidence/M6/`

---

## Phase 8: User Story 5 — Flatpak (P3) (M7)

- [ ] T087 [US5] M7: ADR 0018 Flatpak packaging (SDK access strategy, app id, bundled deps)
- [ ] T088 [US5] M7: `packaging/flatpak/com.monodevelop.MonoDevelop.yml` + launcher; reuse `main/monodevelop.desktop`, `.appdata.xml`, `monodevelop.xml`
- [ ] T089 [US5] M7: `scripts/pm-flatpak` (container with flatpak-builder) + `scripts/package-flatpak.sh` → `out/monodevelop.flatpak`
- [ ] T090 [US5] M7: Install test in a clean container (version + mdtool build Hello + Xvfb launch); SBOM + checksums → `docs/evidence/M7/`

---

## Phase 9: Polish & Cross-Cutting (M8/M9)

- [ ] T091 [P] M8: Structured logging backend (Microsoft.Extensions.Logging, `MD_LOG_LEVEL`, `MD_LOG_FORMAT=json`) in `LoggingService`; start-up version log
- [ ] T092 [P] M8: Metrics/tracing for `InstrumentationService` (`System.Diagnostics.Metrics`, `ActivitySource`)
- [ ] T093 [P] M8: Security sweep (BinaryFormatter/Remoting grep = 0, `Process` shell usage, audit clean)
- [ ] T094 [P] M8: Docs: `README.md`, `docs/linux/setup.md`, `docs/linux/troubleshooting.md`, `CONTRIBUTING.md`, `docs/architecture.md`
- [ ] T095 M8: Start-up time measurement (SC-005 ≤ 10 s) and global coverage ≥ 40 %; shrink quarantine
- [ ] T096 M9: Full regression + acceptance checklist SC-001…SC-009 → `docs/evidence/M9/acceptance.md`; final the consistency analysis; release notes (tag only with maintainer authorization)

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 → US2 (WS-1) → US3 (M5a → M5b → M5c) → US4 → US6 (full CI) → US5 → Polish.
- US1 and US2 share Core; US2's builder work (T049–T051) can proceed in parallel with US1 tests (T041–T045) once T028–T040 are done.
- US3 requires US1+US2; US4 requires US3 M5b (Ide compiles); US5 requires US3 (M5c); US6 minimal CI starts in Phase 2 (T026).

### Parallel Opportunities

- Phase 1: T005–T010 in parallel (done/ongoing).
- Phase 2: T023, T024, T025 in parallel after T016–T020.
- US3: T072, T073, T074 in parallel after T069–T071.

## Parallel Example: User Story 3 (M5c add-ins)

```text
T072 Port Xml/DesignerSupport/AssemblyBrowser/... add-ins
T073 Port DotNetCore GUI/PackageManagement/UnitTesting
T074 Port VersionControl + Git on LibGit2Sharp 0.32
```

## Implementation Strategy

### MVP First

WS-1 (US1 + US2): headless core + `mdtool build` on .NET 10 inside the container. Validate with the
quickstart M3/M4 blocks before any GUI work.

### Incremental Delivery

WS-2 (M5a Xwt/GTK3 window) → WS-3 (M5b/M5c IDE) → debugging → CI/CD → Flatpak → hardening.
Each task = one commit via `scripts/git-commit`, evidence under `docs/evidence/Mx/`.

## Notes

- Mark tasks `[x]` as they complete; the file is the resume point after context compaction.
- Never push; never run build commands on the host.
