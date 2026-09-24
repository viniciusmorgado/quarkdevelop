# Tasks: MonoDevelop on Linux with .NET 10 LTS and GTK3

**Input**: Design documents from `/specs/001-linux-dotnet10-migration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Revision 2 (2026-09-23)** — incorporates the three M1 reviews (`docs/evidence/M1/review-*.md`):
every task names its proof (→), tests precede or accompany behaviour changes, multi-project UI
tasks are split one project per task, new tasks for gaps found in review.

**Tests**: required by the constitution (principle V) and the spec (SC-002…SC-006).

**Conventions**
- Every command runs inside the container: `./scripts/pm <cmd>` (omitted below for brevity).
- Each task = one or more commits via `./scripts/git-commit`; evidence under `docs/evidence/Mx/`.
- `[P]` = parallelizable; `[USn]` = user story from spec.md; `Mx` = milestone.
- "→" = the command or artifact that proves the task is done.

---

## Phase 1: Setup — M0 baseline, M1 specification

- [x] T001 M0: `Containerfile` + `scripts/pm` → `./scripts/pm dotnet --info` shows SDK 10.0.401, no Mono
- [x] T002 M0: `scripts/git-commit` (no-reply identity) → `git log -1 --format=%ae`
- [x] T003 M0: specification layout (constitution, `specs/001-linux-dotnet10-migration/`) → files exist
- [x] T004 M0: initialize 15 submodules in the container → `git submodule status | grep -c '^ '` = 15
- [x] T005 [P] M0: `scripts/inventory.sh` → `docs/evidence/M0/inventory.md`
- [x] T006 [P] M0: GtkSharp 3 spike `spikes/gtk3-hello` → `docs/evidence/M0/T006-gtk3-hello.png`
- [x] T007 [P] M0: Mono.Addins spike `spikes/addins-*` → output "addins-spike: OK"
- [x] T008 [P] M0: Roslyn IVT + Publicizer spikes `spikes/roslyn-*` → "roslyn-publicizer: OK"
- [x] T009 [P] M0: legacy Mono baseline attempt → `docs/evidence/M0/T009-legacy-baseline.md`
- [x] T010 [P] M0: DotDevelop prior-art review → `docs/evidence/M0/T010-dotdevelop.md`
- [x] T011 M0: spike outcomes in `research.md` + `docs/evidence/M0/README.md`
- [x] T012 M1: constitution + spec + plan + research + data-model + contracts + quickstart + tasks → `docs/constitution.md`, files under `specs/001-linux-dotnet10-migration/`
- [x] T013 [P] M1: ADRs 0001–0017 + index → `ls docs/adr/*.md | wc -l` ≥ 18
- [x] T014 [P] M1: `docs/BREAKING-CHANGES.md` → file lists every ADR 0017 exclusion
- [x] T015 M1: three independent reviews (analyze, feasibility, traceability) → `docs/evidence/M1/review-{A,B,C}.md`
- [ ] T016 M1: apply review findings (this revision, constitution 1.2.0 and its amendments in ADR 0001, ADR fixes, spec SC-002/003/005/007, quickstart/contract fixes) and re-run the consistency analysis → `docs/evidence/M1/analyze.md` with 0 CRITICAL
- [x] T017 [P] M1: ADR 0018 warning policy (TreatWarningsAsErrors + per-project `WarningsNotAsErrors` baseline) → `docs/adr/0018-warning-policy.md`

---

## Phase 2: Foundational — M2 toolchain (blocks every story)

- [x] T018 M2: `global.json` (10.0.100, latestFeature) → `dotnet --version` = 10.0.401
- [x] T019 M2: nuget.org-only `NuGet.config` with source mapping → `grep -c '<add key' NuGet.config` = 1
- [x] T020 M2: `main/Directory.Packages.props` (CPM, transitive pinning, Microsoft.Build 18.9.6 ≤ SDK MSBuild) → restore succeeds with no NU1605/NU1608
- [x] T021 M2: `main/msbuild/Linux/{Common.props,Common.targets,OutputLayout.targets}`; `Directory.Build.*` branch on `UsingMicrosoftNETSdk` → Core output lands in `main/build/bin/`
- [x] T022 M2: `BuildVariables.targets` generates `MonoDevelop.BuildInfo` from `version.config` + `build/bin/buildinfo` → `grep 8.6 main/src/core/MonoDevelop.Core/obj/Debug/BuildVariables.cs`
- [x] T023 M2: `main/MonoDevelop.Linux.sln` (classic sln) → `dotnet sln main/MonoDevelop.Linux.sln list`
- [x] T024 [P] M2: `scripts/{lib,setup,restore,build,test,run,debug,lint}.sh` → `./scripts/lint.sh` + two consecutive `./scripts/build.sh` succeed
- [x] T025 [P] M2: netcoredbg 3.2.0-1092 + actionlint 1.7.12 (sha256-verified), images pinned by digest; `.devcontainer/devcontainer.json` → `netcoredbg --version`, `actionlint`
- [x] T026 M2: minimal `.github/workflows/ci.yml` (least privilege, NuGet cache, build + format + lint) → `actionlint` clean
- [x] T027 M2: README Linux section, `docs/linux/setup.md`, `docs/linux/troubleshooting.md` → files exist and are linked from README
- [x] T028 M2: `InternalsVisibleTo` emitted with the MonoDevelop key (GenerateAssemblyInfo=true, legacy attributes off) → generated `obj/Debug/MonoDevelop.Core.AssemblyInfo.cs` contains the IVT lines
- [x] T029 M2: per-project warning baseline (`scripts/warnings-baseline.sh`, `main/msbuild/Linux/warning-baselines/`), `TreatWarningsAsErrors=true`, security IDs never baselined → Core builds with 0 errors
- [x] T030 M2: `CONTRIBUTING.md` (container workflow, commit identity, spec/ADR flow, warning baseline) → file exists
- [x] T031 M2: fresh-clone check inside the container: `./scripts/pm bash -lc 'rm -rf ~/fc && git clone -q . ~/fc && cd ~/fc && ./scripts/setup.sh && ./scripts/build.sh'` → `docs/evidence/M2/fresh-clone.log` (SC-001)
- [x] T032 M2: evidence → `docs/evidence/M2/README.md` (versions, build ×2 logs, lint, actionlint)

**Checkpoint**: toolchain and conventions ready.

---

## Phase 3: User Story 1 — headless core builds and tests (P1, MVP) — M3/M4

**Goal**: `MonoDevelop.Core` + UI-free C# project support on net10.0 with a running test suite.
**Independent Test**: `./scripts/build.sh && ./scripts/test.sh` → TRX + coverage; no Mono.

### Core compiles (M3)

- [x] T033 [US1] M3: SDK-style `MonoDevelop.Core.csproj` (545 explicit sources, CPM, only used packages: Mono.Addins(.Setup), Cecil, Mono.Unix, MSBuild* compile-only, Locator, CodeAnalysis.Common, Newtonsoft, ObjectPool, CodeDom, ConfigurationManager) in `MonoDevelop.Linux.sln` → `dotnet build main/src/core/MonoDevelop.Core` 0 errors
- [x] T034 [US1] M3: Remoting removed from Core (RemotingService, ProcessHostController, DisposerFormatterSink out of the build; `ProcessHostConsole` extracted; `CreateExternalProcessObject` throws `NotSupportedException`; instrumentation autosave → JSON; remote/binary instrumentation → `PlatformNotSupportedException`) → no `System.Runtime.Remoting`/`BinaryFormatter` in Core compile items
- [x] T035 [US1] M3: `CallContext` → `AsyncLocal` (`MonoDevelop.Projects/ItemInitializationContext.cs`) → covered by T132 tests
- [x] T036 [US1] M3: `Debug.Listeners` → `Trace.Listeners`; `RegistryHive.DynData` removed; monodoc `HelpService` behind `#if MONODOC`; WCF STS behind `#if WCF_STS`; obsolete serialization members removed (SYSLIB0051/0003); unused Decompiler using removed → build clean of these IDs
- [x] T037 [US1] M3: `MonoDevelop.Core.addin.xml` imports only assemblies shipped next to Core (Newtonsoft, ObjectPool) → add-in registry loads Core without "assembly not found" (T046)

### Test harness first (M4 infrastructure, needed before further behaviour changes)

- [x] T038 [US1] M3: `main/tests/UnitTests/UnitTests.csproj` → SDK-style net10.0, NUnit 3.14, no GuiUnit; `TestHost.EnsureInitialized` (called from `TestBase`) initializes `Runtime` once on an emulated main loop with an isolated profile (`main/tests/config`); `MonoDevelop.Tests.addins` → `../AddIns` → `dotnet build main/tests/UnitTests`
- [x] T039 [US1] M3: `main/tests/MonoDevelop.Core.Tests.Addin` → SDK-style test add-in, output next to the test assemblies (`build/tests/`) → builds
- [x] T040 [US1] M3: `main/tests/MonoDevelop.Core.Tests` → SDK-style, `OneTimeSetUp`/`OneTimeTearDown`, `Assert.Throws` instead of `ExpectedException`, `Assert.IsInstanceOf`, `Does.EndWith`, Moq 4.20/Castle 5.2 → `dotnet test --list-tests` lists 860 cases
- [x] T132 [US1] M3: retroactive tests for T034–T036 and the CA5369 fix (JSON instrumentation autosave round-trip, `CreateExternalProcessObject` throws `NotSupportedException`, `ItemInitializationContext` delays initialization across awaits, property deserialization rejects DTDs) → tests pass
- [x] T133 [US1] M3: test host infrastructure: `TestHost` (main-loop sync context, isolated profile), `MonoDevelop.TestStartupHook` (MSBuildLocator before discovery), generated `.runsettings`, `Mono.Addins.CecilReflector` → `dotnet test --list-tests` discovers ≥ 850 Core tests
- [x] T041 [US1] M3: first run + quarantine record (reason, owner, date, task) → `docs/evidence/M4/quarantine.md`; `./scripts/test.sh` green with `Category!=Quarantine`

### Runtime on .NET (M3) — each with tests

- [x] T042 [US1] M3: `DotNetCoreTargetRuntime` + factory in `MonoDevelop.Core.Assemblies/` (SDK discovery via Locator / `dotnet --list-sdks`, reference packs, `CanBuild=false` + message when no SDK), registered in `MonoDevelop.Core.addin.xml`; null-safe `SystemAssemblyService` → tests `DotNetCoreTargetRuntimeTests` (SDK found; no-SDK case)
- [x] T043 [US1] M3: `DotNetCoreExecutionHandler` for `DotNetExecutionCommand` (`dotnet exec`) → test runs `linux-smoke` Hello through the handler
- [x] T044 [US1] M3: `MSBuildRegistration.EnsureRegistered` (MSBuildLocator, SDK instance) called first in mdtool/test hosts; builder registers the path sent by the IDE → `DotNetCoreTargetRuntimeTests.InstalledSdkProvidesMSBuild`, mdtool builds (docs/evidence/M3/mdtool.md)
- [x] T045 [US1] M3: `NativeLibraryMap` (`MonoDevelop.Core/NativeLibraryMap.cs`) + module initializer; `MonoDevelop.Core.dll.config` deleted → test resolves `libglib-2.0-0.dll` → `libglib-2.0.so.0`
- [x] T046 [US1] M3: `Mono.Unix` usage verified on net10 (`LoggingService`, `ProcessService`, `Runtime`, `FileService`, `TextFile`), `mono_pmip` removed from `SampleProfiler.cs`, `CodePagesEncodingProvider` registered at start-up → tests: log redirection, legacy code page decode, `AsyncLocal` delayed initialization
- [x] T047 [US1] M3: NGettext-based `GettextCatalog` (`MonoDevelop.Core/Gettext.cs`) → test translates a known string with `LANG=de_DE` from a compiled catalog
- [x] T048 [US1] M3: MSBuild target compiling `main/po/*.po` → `main/build/locale/<lang>/LC_MESSAGES/monodevelop.mo` (replaces autotools msgfmt) → one `.mo` per `.po` (FR-018)
- [x] T049 [US1] M3: `Type.GetType("Mono.Runtime")` checks and `Assembly.LoadFrom` sites in Core (`Runtime.cs`, `SdkResolution.cs`, `MonoRuntimeInfo.cs`) behave on CoreCLR → test `Platform`/runtime info reports CoreCLR
- [x] T050 [US1] M3: add-in failure resilience: an add-in with a missing dependency is skipped and logged → test with a broken test add-in
- [x] T051 [US1] M3: sln/csproj round-trip test (load + save `linux-smoke/Smoke.sln` and a fixture without diff) → test in Core.Tests
- [x] T052 [US1] M3: vulnerability gate `scripts/audit.sh` (fails on High/Critical in `dotnet list package --vulnerable --include-transitive`) → exit 0

### UI-free C# project support (M3)

- [x] T053 [US1] M3: new `main/src/addins/CSharpBinding/MonoDevelop.CSharpBinding.Core/` (net10.0 add-in): `CSharpProject` (class name kept), `CSharpProjectExtension`, `CSharpCompilerParameters` (IdeApp calls → hook), `CSharpLanguageVersionHelper`, `CSharpResourceIdBuilder`, `PortableCSharpProjectFlavor`, manifest registering `DotNetProjectType` + language binding with plain `CSharpCodeProvider` → Core.Tests C# project tests pass (`TestProjectsChecks`)
- [x] T054 [US1] M3: headless SDK helpers stay in Core's runtime; `MonoDevelop.DotNetCore.Core` (if needed) only extends via `/MonoDevelop/Core/Runtimes` / global-property providers (no cycle) → project graph acyclic (`dotnet build` succeeds)

### Coverage (M4)

- [x] T055 [US1] M4: `scripts/test.sh` coverage + ratchet (`docs/evidence/M4/coverage-baseline.txt`; fails if lower) → `out/coverage/Summary.txt`
- [x] T056 [US1] M4: Core ≥ 60% and Linux solution ≥ 40% line coverage (targeted tests for new code) → Summary.txt
- [x] T057 [US1] M4: evidence → `docs/evidence/M3/README.md`, `docs/evidence/M4/README.md` (TRX summary, coverage, quarantine ≤ 15%)
- [ ] T134 [US1] M4: legacy .NET Framework fixtures in `main/tests/test-projects` resolve reference assemblies on Linux (Microsoft.NETFramework.ReferenceAssemblies via `TargetFrameworkRootPath`; `CodeTaskFactory` → `RoslynCodeTaskFactory`) → `net4x-fixture` quarantine entries removed
- [ ] T135 [US1] M4: triage the `Bug`/`Flaky` quarantine entries of T041 → each fixed or linked to a follow-up task; quarantine ≤ 15% per suite

**Checkpoint**: US1 done.

---

## Phase 4: User Story 2 — `mdtool` builds C# projects (P1) — M3

**Goal**: `contracts/mdtool-cli.md`. **Independent Test**: quickstart § M3.

- [x] T058 [P] [US2] M3: `main/tests/linux-smoke/{Hello,Greeter,Broken,Smoke.sln}` isolated from repo props (own `Directory.Build.*`, `Directory.Packages.props`); not part of `MonoDevelop.Linux.sln` → `dotnet build main/tests/linux-smoke/Smoke.sln` ok, Broken fails with CS0103
- [x] T059 [US2] M3: MSBuild builder `main/src/core/MonoDevelop.Projects.Formats.MSBuild` → net10.0 exe; no Remoting/`System.Net.Configuration`; `Thread.Abort`/`SetApartmentState` removed (`BuildManager.CancelAllSubmissions`); `Main.cs` no longer sets `MSBUILD_EXE_PATH` nor `AssemblyResolve`; MSBuildLocator at entry → builds to `build/bin/MonoDevelop.MSBuildBuilder.dll`
- [x] T060 [US2] M3: `RemoteBuildEngineManager`: launch builder with `dotnet exec` (+ `DOTNET_HOST_PATH`), SDK paths via environment, no MSBuild copy / `exe.config`; add-in MSBuild import search paths via environment or global property (or documented as dropped) → test builds Hello through the builder
- [x] T061 [US2] M3: build cancellation test (cancel a long build; builder stops; caller not blocked) → test passes
- [x] T062 [US2] M3: `main/src/tools/mdtool` → net10.0 exe `mdtool.dll`; Locator bootstrap; `.addins` → `../AddIns`; `-r:` ignored with warning → `dotnet main/build/bin/mdtool.dll` lists `build`
- [x] T063 [US2] M3: cherry-pick DotDevelop evaluator fixes for net5+ projects (`7045264a30`, `4518519b5e`, `21031632fd`, provenance in commit) + evaluation diff test vs `dotnet msbuild -getItem:Compile` → test passes
- [x] T064 [US2] M3: contract test `MdtoolContractTests` (exit codes 0/1, `-p:`, `-t:Clean`, `-c:Release`, missing file) → test passes
- [x] T065 [US2] M3: quickstart § M3 block run → `docs/evidence/M3/mdtool.md`

**Checkpoint**: WS-1 (US1 + US2) done.

---

## Phase 5: User Story 3 — graphical IDE on GTK3 (P2) — M5

**Goal**: `contracts/smoke-test.md`. One project (or Ide area) per task.

### M5a — GUI foundation (WS-2)

- [x] T066 [US3] M5a: vendor xwt → `main/vendor/xwt/` + `UPSTREAM.md`; remove submodule; `Xwt` + `Xwt.Gtk/Xwt.Gtk3.csproj` on net10.0 + GtkSharp 3.24.24 → `xvfb-run -a dotnet run --project main/vendor/xwt/TestApps/Gtk3Test` shows a window (screenshot)
- [x] T067 [US3] M5a: vendor `Mono.Addins.Gui` → `main/vendor/mono-addins-gui/` + `UPSTREAM.md`, GTK3 → builds
- [x] T068 [US3] M5a: vendor vs-editor-api text subset → `main/vendor/vs-editor-api/` + `UPSTREAM.md` (no FPF/WindowsBase) → builds; duplicate-assembly check passes
- [x] T069 [US3] M5a: ADR 0019 NRefactory (remove usages from Ide vs vendor subset) + implementation → Ide has no NRefactory project reference

### M5b — `MonoDevelop.Ide` compiles on GTK3 (one area per task)

Every port task adds or extends tests in `main/tests/MonoDevelop.Ide.Gtk3.Tests` (run under Xvfb by
`scripts/test.sh`) for the behaviour it changes; its proof includes those tests passing.

- [x] T137 [US3] M5b: Roslyn 5.9 host layer of the Ide (TypeSystem, RoslynServices, options, EditorConfig, task list, navigation, search) ported to the Roslyn 5.9 APIs through Publicizer, removed host services taken out of the build (list in the commit) → 0 declaration-level errors in those files (full Ide compile); bodies continue in T071–T082
- [x] T136 [US3] M5b: `main/tests/MonoDevelop.Ide.Gtk3.Tests` in the Linux solution (Xvfb) with tests for the M5b behaviour changes so far: `SyncContext.AsyncDispatch` without `Delegate.BeginInvoke`, `Gtk3ExposeEvent` offsets/area/context restore, `Gtk3CompatExtensions.SizeRequest` → `./scripts/test.sh` runs them green

- [x] T070 [US3] M5b: SDK-style `MonoDevelop.Ide.csproj` (GtkSharp 3, Publicizer for Roslyn internals, VS Composition explicit) with tracked `Gtk3PortPending.props`; `SyncContext.BeginInvoke` fixed → Ide builds with pending areas excluded
- [x] T071 [US3] M5b: port `MonoDevelop.Components/` (+ Theming, Extensions) → removed from pending list; builds
- [x] T072 [US3] M5b: port `Components.Docking`, `Components.DockNotebook` → builds
- [x] T073 [US3] M5b: port `Components.MainToolbar` → builds
- [x] T074 [US3] M5b: port `Components.Commands` + `Ide.Commands` → builds
- [x] T075 [US3] M5b: port `Components.PropertyGrid*`, `Components.Chart` → builds
- [x] T076 [US3] M5b: freeze Stetic output of Ide (`main/src/core/MonoDevelop.Ide/Gui/*.cs`) as hand-maintained GTK3 code → builds
- [x] T077 [US3] M5b: port `Ide.Gui` + `Ide.Gui.Shell` → builds
- [x] T078 [US3] M5b: port `Ide.Gui.Pads*` + `Ide.Gui.Components` → builds
- [x] T079 [US3] M5b: port `Ide.Gui.Dialogs`, `Ide.Gui.OptionPanels`, `Ide.Gui.Wizard` → builds
- [x] T080 [US3] M5b: port `Ide.Projects*` (+ OptionPanels) → builds
- [x] T081 [US3] M5b: port `Ide.Editor*`, `Ide.CodeCompletion`, `Ide.CodeTemplates`, `Ide.Fonts` → builds
- [x] T082 [US3] M5b: port `Ide.Execution`, `Ide.FindInFiles`, `Ide.WelcomePage`, remaining areas; `Gtk3PortPending.props` empty → `./scripts/inventory.sh --linux-sln` reports 0 GTK2-only APIs
- [x] T083 [US3] M5b: GTK3 CSS themes (light/dark) in `MonoDevelop.Components/…/IdeTheme.cs` → screenshots light + dark
- [x] T084 [US3] M5b: NativeLibraryMap entries for gtk/gdk/glib/pango/cairo; `MonoDevelop.Ide.dll.config` deleted → test

### M5c — IDE runs (one project per task)

- [x] T085 [US3] M5c: `Mono.TextEditor.Shared` on GTK3/Cairo → builds; editor unit tests subset pass
- [x] T086 [US3] M5c: `MonoDevelop.Startup` → net10.0 exe `MonoDevelop.dll`; Locator bootstrap → `./scripts/run.sh --headless` reaches main window (log)
- [x] T087 [US3] M5c: `GnomePlatform` (gio via NativeLibraryMap; `GnomePlatform.dll.config` deleted) → `xdg-open` test
- [x] T088 [US3] M5c: `MonoDevelop.SourceEditor2` on GTK3 (`MonoDevelop.SourceEditor.dll.config` deleted) → opens a C# file (smoke screenshot)
- [x] T089 [US3] M5c: `CSharpBinding` (GUI) on Roslyn 5.9 + Publicizer (EditorFeatures from dnceng feed only via ADR 0004 amendment) → completion test (US3-2)
- [x] T090 [US3] M5c: `MonoDevelop.Refactoring` → builds; refactoring tests subset pass
- [x] T091 [P] [US3] M5c: `Xml` add-in → Xml tests pass
- [x] T092 [P] [US3] M5c: `DesignerSupport` (Stetic output frozen; `gui.stetic` deleted) → builds
- [x] T093 [P] [US3] M5c: `AssemblyBrowser` → builds
- [x] T094 [P] [US3] M5c: `RegexToolkit` (Thread.Abort removed) → builds
- [x] T095 [P] [US3] M5c: `HexEditor` (Stetic frozen) → builds
- [x] T096 [P] [US3] M5c: `DocFood` (Stetic frozen) → builds
- [x] T097 [P] [US3] M5c: `ChangeLogAddIn` → builds
- [x] T098 [P] [US3] M5c: `MonoDevelop.Gettext` add-in → builds
- [x] T099 [US3] M5c: `MonoDevelop.DotNetCore` (GUI) → DotNetCore tests subset pass
- [x] T100 [US3] M5c: ADR 0020 NuGet client version + `MonoDevelop.PackageManagement` on NuGet 6.x/7.x → test add/update/remove/restore a package on an SDK project (FR-010)
- [x] T101 [US3] M5c: `MonoDevelop.UnitTesting` + VSTest → test discovers/runs NUnit + xUnit + MSTest samples (FR-011)
- [x] T102 [US3] M5c: `VersionControl` + `VersionControl.Git` on LibGit2Sharp 0.32 (reference DotDevelop `216f01c79f`, `2356bb926d`); libgit2/libgit-binary/libgit2sharp submodules removed → Git tests (status/diff/log on temp repo) pass (FR-009)
- [x] T103 [US3] M5c: `--smoke-test` in `IdeStartup.cs` per `contracts/smoke-test.md` → exit 0 under `xvfb-run`
- [x] T104 [US3] M5c: Wayland smoke (weston headless backend in the container, `XDG_RUNTIME_DIR` set, `GDK_BACKEND=wayland`) → exit 0 (FR-006)
- [x] T105 [US3] M5c: error-list navigation test (build Broken; activate error; editor at line) (US3-3) → test passes
- [x] T106 [US3] M5c: main-loop stall probe during `MonoDevelop.Linux.sln` load (≤ 1 s) → evidence
- [ ] T107 [US3] M5c: `Ide.Tests`, `IdeUnitTests`, `MonoDevelop.CSharpBinding.Tests` on NUnit 3.14 under Xvfb; quarantine per suite → `docs/evidence/M4/quarantine.md` updated
- [x] T108 [US3] M5c: `Main.sln` replaced by `MonoDevelop.Linux.sln` (ADR 0002); legacy build files removed or marked obsolete → `docs/BREAKING-CHANGES.md` updated
- [ ] T109 [US3] M5c: evidence → `docs/evidence/M5/` (smoke logs, screenshots X11 + Wayland, grep = 0 for GTK2 APIs and for the ADR 0011 port helpers, startup time)

---

## Phase 6: User Story 4 — debugging .NET 10 programs (P2) — M5

- [x] T110 [US4] M5: vendor `Mono.Debugging` → `main/vendor/debugger-libs/Mono.Debugging/` + `UPSTREAM.md`, net10.0; submodule removed → builds
- [x] T111 [US4] M5: `MonoDevelop.Debugger` + `MonoDevelop.Debugger.VSCodeDebugProtocol` (VsCodeDebugProtocol 17.x/18.x) → builds
- [ ] T112 [US4] M5: `NetCoreDbgSession` (cherry-pick DotDevelop `379883b7c5`, MIT) + engine registration → debugger engine listed
- [ ] T113 [US4] M5: `NetCoreDbgTests` in `main/src/addins/MonoDevelop.Debugger/MonoDevelop.Debugger.Tests/` (breakpoint, locals, step, exit code) → `dotnet test … --filter FullyQualifiedName~NetCoreDbg` passes (SC-006)
- [ ] T114 [US4] M5: `.vscode/launch.json` (coreclr attach/launch via netcoredbg) + `scripts/debug.sh` docs → `docs/linux/setup.md` section

---

## Phase 7: User Story 6 — CI/CD (P3) — M6

- [x] T115 [US6] M6: `scripts/ci.sh` (lint, build --check, test + coverage ratchet, audit, mdtool smoke, GUI smoke under Xvfb), timed → `out/ci/summary.txt`, ≤ 15 min (SC-007)
- [x] T116 [US6] M6: `ci.yml` runs `scripts/ci.sh` in the dev image; artifacts named `<version>+<sha>`; job summary → `actionlint` clean
- [x] T117 [P] [US6] M6: ADR 0021 versioning (version.config + SHA) and release; `release.yml` on `v*` (least privilege, attaches Flatpak + sha256 + SBOM once M7 exists) → `actionlint` clean
- [x] T118 [P] [US6] M6: Dependabot (nuget, github-actions, docker) + CodeQL workflow → `actionlint` clean
- [ ] T119 [US6] M6: evidence → `docs/evidence/M6/` (local `ci.sh` run + timing; hosted run only after push authorization)

---

## Phase 8: User Story 5 — Flatpak (P3) — M7

- [ ] T120 [US5] M7: ADR 0022 Flatpak (app id `io.github.viniciusmorgado.MonoDevelop`, runtime `org.gnome.Platform`, .NET 10 SDK extension, SDK access strategy, bundled deps)
- [ ] T121 [US5] M7: `PM_PROFILE=flatpak` in `scripts/pm` (flatpak-builder image, required podman flags) → `PM_PROFILE=flatpak ./scripts/pm flatpak --version`
- [ ] T122 [US5] M7: `packaging/flatpak/io.github.viniciusmorgado.MonoDevelop.yml` + launcher; desktop entry, icon, AppStream, MIME from `main/monodevelop.{desktop,appdata.xml,xml}` → `appstreamcli validate` / `desktop-file-validate` pass
- [ ] T123 [US5] M7: `scripts/package-flatpak.sh` → `out/monodevelop.flatpak` + `out/monodevelop.flatpak.sha256` + CycloneDX SBOM `out/monodevelop.cdx.json` (names used by `release.yml`)
- [ ] T124 [US5] M7: install test in a clean container: `--version`, `mdtool build Hello`, IDE under Xvfb → `docs/evidence/M7/`

---

## Phase 9: Polish — M8 hardening, M9 release

- [ ] T125 [P] M8: ADR 0023 logging/observability + structured logging backend (`MD_LOG_LEVEL`, `MD_LOG_FORMAT=json`) and start-up version log → `MD_LOG_FORMAT=json dotnet mdtool.dll | jq` shows version fields
- [ ] T126 [P] M8: metrics/tracing for `InstrumentationService` (`System.Diagnostics.Metrics`, `ActivitySource`) → test listener shows instruments
- [ ] T127 [P] M8: security sweep (Remoting/BinaryFormatter grep = 0 in the Linux solution, `Process` with shell, audit) → `docs/evidence/M8/security.md`
- [ ] T128 [P] M8: `docs/architecture.md` (layers, add-in model, build/run flow), README refresh → markdown link check passes
- [ ] T129 M8: start-up time ≤ 10 s measured; quarantine reduced; warning baselines shrunk → `docs/evidence/M8/README.md`
- [ ] T130 M9: full regression (`./scripts/test.sh --all` informational + gate run), acceptance checklist SC-001…SC-009 → `docs/evidence/M9/acceptance.md`
- [ ] T131 M9: final the consistency analysis, release notes (BREAKING-CHANGES) → tag/release only with maintainer authorization

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 (T033–T057) → US2 (T058–T065) → US3 M5a → M5b → M5c → US4 → US6 → US5 → Polish.
- Within US1: T038–T041 (test harness) precede T042–T052 (behaviour changes with tests).
- US2's builder work (T059–T061) may run in parallel with T042–T052 once T041 is done.
- US4 needs M5b (Ide compiles); US5 needs M5c; US6 full CI needs US2 + M5c smoke.

## Parallel Example: M5c add-ins

```text
T091 Xml   T092 DesignerSupport   T093 AssemblyBrowser   T094 RegexToolkit
T095 HexEditor   T096 DocFood   T097 ChangeLogAddIn   T098 Gettext
```

## Implementation Strategy

MVP = WS-1 (US1 + US2): headless core + `mdtool build` inside the container. Then WS-2 (M5a),
WS-3 (M5b/M5c), debugging, CI/CD, Flatpak, hardening. Tasks are checked off here as they land;
this file is the resume point after interruptions.
