# Future work

What is left after the Linux / .NET 10 migration: studies that start only when their precondition holds, and the
tasks of the migration that were not done.

## UI framework study: coupling of the GTK UI and a possible move to Avalonia

- **Recorded:** 2026-09-24, maintainer's request.
- **Precondition:** the migration is complete (M9 accepted) and the current editor works normally on .NET 10 LTS.
- **Goal:** decide whether the IDE's user interface can move from GTK 3 (GtkSharp + Xwt) to a modern
  cross-platform framework such as [Avalonia UI](https://avaloniaui.net/), and at what cost.

**Scope of the study.** Map how tightly the UI code is coupled to the rest of the IDE:
- Which assemblies and namespaces reference `Gtk`, `Gdk`, `Pango`, `Cairo`, `GLib`, `Atk` or Xwt directly, and how
  much code that is.
- Which IDE services and add-in extension points expose GTK or Xwt types in their public API. Some pass
  `Gtk.Widget`, `Control`, `Xwt.Widget` or GTK events through interfaces used by add-ins, for example pads,
  document views, option panels, commands and the text editor extension points.
- Which layers are already toolkit-neutral: Core (project model, build, MSBuild integration, runtime), the Roslyn
  type system, the VS editor text model subset, the debugger session layer.
- The text editor: Mono.TextEditor draws with Cairo on GTK. The editor would be the largest single piece to
  replace. Candidates are AvaloniaEdit, or a new view over the existing text model.
- Docking, the workbench shell, the pads and the dialogs.

**Expected outputs.**
- A coupling report with numbers per assembly and per extension point.
- A list of the seams where an abstraction would let GTK and Avalonia coexist.
- A rough effort and risk estimate.
- A proof of concept of one pad or dialog in Avalonia hosted next to the GTK workbench, or a clear reason why
  that is not practical.
- An ADR with the decision.

## MSBuild evaluation through `ProjectInstance`

- **Recorded:** 2026-09-23 (ADR 0008, research D7), named here on 2026-09-25.
- **Precondition:** a case where MonoDevelop's own evaluator diverges from `dotnet msbuild` in a way that the
  evaluation-diff test (T063) or a user report shows, or T141 (the .NET SDK resolver) needs more than loading the
  resolver.
- **Goal:** evaluate projects with MSBuild's `ProjectInstance` instead of MonoDevelop's custom evaluator, so that
  evaluation follows SDK 10 semantics exactly (SDK resolution, `global.json`, property functions).
- **Expected outputs:** an ADR amending ADR 0008, the evaluator behind the project model's evaluation interface,
  and the evaluation-diff and SDK tests green with it.

## Open tasks from the migration

- **T134.** legacy .NET Framework fixtures in `main/tests/test-projects` resolve reference assemblies on Linux (Microsoft.NETFramework.ReferenceAssemblies via `TargetFrameworkRootPath`; `CodeTaskFactory` → `RoslynCodeTaskFactory`) → `net4x-fixture` quarantine entries removed
- **T140.** the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders that add-ins register (`MSBuildProjectService.RegisterProjectImportSearchPath`): the Mono builder got them from the `MSBuild.exe.config` toolset it patched, which the .NET builder no longer writes (ADR 0008; `RemoteBuildEngineManager.GetExeLocation`, `main/src/core/MonoDevelop.Core/MonoDevelop.Projects.MSBuild/`, `MonoDevelop.MSBuildBuilder`) → send the paths to the builder (fallback `MSBuildExtensionsPath` imports, an SDK resolver over the registered `MSBuildSDKsPath` folders, refreshed on `ImportSearchPathsChanged`); the 6 `MSBuildSearchPathTests` leave quarantine: `./scripts/pm bash -lc 'dotnet build main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj -v q && dotnet test main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj --no-build --filter "FullyQualifiedName~MSBuildSearchPathTests"'`
- **T141.** MonoDevelop's evaluator does not run the .NET SDK resolver: `SdkResolution.LoadResolvers` only loads `<sdk>/SdkResolvers/*`, and SDK 10 ships `Microsoft.DotNet.SdkResolver.dll` at the SDK root (MSBuild loads it in-box), so SDK lookup skips the global.json rules of `dotnet msbuild` (`main/src/core/MonoDevelop.Core/MonoDevelop.Projects.MSBuild/SdkResolution.cs`) → load it from `MSBuildBinPath`, keep `SdkEvaluationTests` green, check the expected message of `SdkResolverTests.UnknownSdk_DotNetMSBuildSdkResolverDoesNotFatalReportError` against SDK 10: `./scripts/pm bash -lc 'dotnet build main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj -v q && dotnet test main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj --no-build --filter "FullyQualifiedName~SdkResolverTests|FullyQualifiedName~SdkEvaluationTests"'`
- **T142.** Core tests of Mono features that Linux/.NET does not have: `ProjectTests.RefreshReferences` expects a missing local `gtk-sharp.dll` to fall back to the Mono GAC package (no GAC, ADR 0007); `MakefileTests.MakefileSynchronization` needs `MonoDevelop.Autotools` (excluded, ADR 0017) → rewrite RefreshReferences on an assembly of the framework packages (fixture `main/tests/test-projects/reference-refresh`); remove MakefileTests and `console-project-with-makefile` with the excluded add-in and list them in BREAKING-CHANGES: `./scripts/pm bash -lc 'dotnet build main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj -v q && dotnet test main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj --no-build --filter "FullyQualifiedName~ProjectTests.RefreshReferences"'`
- **T143.** Core fixtures that need NuGet packages (no network in tests; the repository `NuGet.config` and its package source mapping apply under `main/tests/tmp`): `ProjectTests.UnknownNuGetPackageReferenceId_DesignTimeBuilds` (`nuget restore` from nuget.org, and no `nuget` executable), `DotNetCoreProjectTests.BuildMultiTargetProject` (netcoreapp1.1/netstandard1.0 packages), `ProjectTests.Resources` (with MSBuild on .NET, the non-string `.resx` resources of a .NET Framework project need `System.Resources.Extensions` and `GenerateResourceUsePreserializedResources`: MSB3822/MSB3823) → restore with `dotnet restore` from a local feed of pinned packages (as `DependenciesNodeSdkProjectTests` does), retarget `multi-target2` to supported frameworks: `./scripts/pm bash -lc 'dotnet build main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj -v q && dotnet test main/tests/MonoDevelop.Core.Tests/MonoDevelop.Core.Tests.csproj --no-build --filter "FullyQualifiedName~UnknownNuGetPackageReferenceId_DesignTimeBuilds|FullyQualifiedName~BuildMultiTargetProject|FullyQualifiedName~ProjectTests.Resources"'`
- **T144.** intermittent crash of the GTK test host ("Gdk-WARNING: losing last reference to undestroyed window", 1 in ~20 runs of MonoDevelop.Ide.Gtk3.Tests, never caught by `--blame-crash`; seen before and after the toggle-reference workaround, ADR 0024) → root cause found and fixed, 50 consecutive runs green. T153 found a probable cause (a GdkWindow freed while its widget was realized, reproduced with an offscreen window in `ToplevelReferenceTests`); the 50-run check is still open
- **T149.** source generators, remaining gaps of T147 (ADR 0025): generators from project references (`OutputItemType="Analyzer"`), generated files under the Dependencies node, live (not snapshot) generated documents → a project-reference generator sample has 0 editor errors
- **T150.** ADR 0011 port helpers to 0 (344 uses of `Gtk3SizeRequest`/`Gtk3ExposeEvent`/`Gtk3BaseSizeRequest`/`Gtk3BaseGetSize`/`Gtk3CompatExtensions` in 93 compiled files on 2026-09-24): native `OnGetPreferredWidth/Height` and `OnDrawn` per widget, with screenshot checks → no ADR 0011 helper left in the compiled files
- **T151.** Ide.Tests multi-target fixture that needs NuGet packages: `TypeSystemServiceTests.MultiTargetFramework_ReloadProject_TargetFrameworksChanged` restores `main/tests/test-projects/multi-target` (netcoreapp1.1;netstandard1.0 plus Newtonsoft.Json 10.0.1: Microsoft.NETCore.App 1.1 and the netstandard1.x package graph) from nuget.org, which fails with NU1100 under the repository package source mapping and without network in tests → restore it from a local feed of pinned packages (as `DependenciesNodeSdkProjectTests` does; see T143 for the Core fixtures) or retarget the fixture to supported frameworks keeping the target-frameworks change the test makes; the entry leaves quarantine: `./scripts/pm bash -lc 'dotnet build main/tests/Ide.Tests/MonoDevelop.Ide.Tests.csproj -v q && xvfb-run -a dotnet test main/tests/Ide.Tests/MonoDevelop.Ide.Tests.csproj --no-build --filter "FullyQualifiedName~MultiTargetFramework_ReloadProject_TargetFrameworksChanged"'`
- **T154.** F# language binding (highlighting, completion, build integration) for SDK-style F# projects: the New Project dialog creates F# projects (T152), which load as unsupported projects → a `dotnet new console --language F#` project opens with F# highlighting and completion and builds from the IDE
