# 0008 — MSBuild hosting on .NET 10

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

MonoDevelop evaluates projects with its own in-process evaluator (`DefaultMSBuildEngine`) and runs
builds in an out-of-process builder (`MonoDevelop.MSBuildBuilder`, TCP + `BinaryMessage`), which was
launched with Mono and got MSBuild from Mono's installation (`$(MSBuildToolsPath)`), copying the
MSBuild bin directory and patching `exe.config`.

## Considered Options

1. Locator + in-proc custom evaluator + out-of-proc net10 builder (chosen)
2. In-proc builds inside the IDE (assembly conflicts, node reuse issues, UI stalls)
3. Replace the evaluator with `ProjectInstance` now (larger change; not scheduled, see `docs/future-work.md`)
4. Shell out to `dotnet build` (loses structured results and cancellation)

## Decision Outcome

- Reference `Microsoft.Build*` 18.x (≤ the SDK MSBuild, 18.9.6 today) with `ExcludeAssets=runtime`; call
  `MSBuildLocator.RegisterInstance` (SDK instance) first thing in `mdtool`, `MonoDevelop.Startup` and
  the builder, before any MSBuild type is loaded.
- Evaluation stays in-process in the custom evaluator for now (a later switch to
  `ProjectInstance` is recorded in `docs/future-work.md`); an evaluation-diff test against `dotnet msbuild -getItem` guards drift.
- The builder targets `net10.0`, is launched with `dotnet exec`, receives SDK paths via environment
  (`MSBUILD_EXE_PATH`, `MSBuildExtensionsPath`, `MSBUILDADDITIONALSDKRESOLVERSFOLDER`), no longer
  copies MSBuild or patches config; cancellation uses `BuildManager.CancelAllSubmissions` instead of
  `Thread.Abort`.

### Consequences

- Good: builds use the same MSBuild as `dotnet build`; no Mono.
- Bad: the custom evaluator may diverge from SDK 10 semantics until replaced.

## Amendment (2026-09-24, T146): generated source files of SDK projects

The type system gets a project's source files from `Project.GetSourceFilesAsync`: the evaluated `Compile` items
plus the items returned by a design-time run of the `CoreCompileDependsOn` targets in the builder. On SDK 10 that
property is only `_ComputeNonExistentFileProperty;ResolveCodeAnalysisRuleSet`. The SDK generates its source files in
targets that run before `BeforeCompile` (`BeforeTargets="BeforeCompile;CoreCompile"`): `GenerateGlobalUsings`
(`obj/<cfg>/<tfm>/<Project>.GlobalUsings.g.cs` from the `Using` items, which `ImplicitUsings` fills),
`GenerateAssemblyInfo`, `GenerateTargetFrameworkMonikerAttribute`, and `GenerateMSBuildEditorConfigFile` for the
analyzers. None of them ran, so a `dotnet new console` project showed false errors (`Console`, `ReadOnlySpan`).

Considered:

1. *Synthesize the global usings in the IDE from the evaluated `Using` items.* Works without the builder, but it
   copies the SDK's rules (`Static`, `Alias`, `Remove`, ordering, C# and VB syntax) into the IDE, reads the items
   from the custom evaluator (which may diverge, see above), and fixes only this one file.
2. *Run `BeforeCompile` in the same design-time run, for SDK projects* (chosen). The SDK's own targets write the files
   and add the `Compile` and `EditorConfigFiles` items, exactly as `dotnet build` does, so whatever the SDK or a
   NuGet package generates before `BeforeCompile` is covered. A project that was never built works (the targets
   create `obj/`), and a changed `Using` item rewrites the file on the next evaluation (the cached items are dropped
   when the project is saved or reloaded). Visual Studio does the same by running `CoreCompile` with
   `SkipCompilerExecution=true`; running only `BeforeCompile` avoids `ResolveReferences`, which the IDE runs
   separately.
3. *Run `Compile` with `SkipCompilerExecution=true`* as Visual Studio does: also covers targets hooked only on
   `CoreCompile`, but resolves the references a second time on every evaluation.

`BeforeCompile` is appended only when `UsingMicrosoftNETSdk` is true (legacy projects keep the old target list), and
last: a failing target stops the ones after it, so `PackageManagementMSBuildExtension` inserts its NuGet targets
before it. `SdkProjectExtension` no longer adds `GeneratedAssemblyInfoFile` a second time.

Left open, then resolved by T147 (amendment below): source generators. Their output exists only in the compiler, so
the workspace has to run them from its analyzer references. The generators of the shared framework
(`[GeneratedRegex]`, `[LibraryImport]`, System.Text.Json) are `Analyzer` items added by `ResolveTargetingPackAssets`,
which the design-time run did not execute: the workspace got only the NetAnalyzers, and a `[GeneratedRegex]` partial
method was a false CS8795 in the editor while `dotnet build` succeeded.

## Amendment (2026-09-24, T147): analyzers and source generators

For SDK projects the design-time run is now `<CoreCompileDependsOn>;ResolveLockFileAnalyzers;_HandlePackageFileConflicts;BeforeCompile`:
the two targets add the `Analyzer` items of the targeting pack and of NuGet packages, with the conflicts resolved as in
a build, so the workspace runs the same source generators as the compiler. `BeforeCompile` stays last. The decision,
the alternatives and how the generators are loaded are in [ADR 0025](0025-source-generators-in-the-workspace.md).
