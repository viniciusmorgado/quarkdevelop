# 0025 — Source generators in the IDE workspace

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

`dotnet build` compiles a `[GeneratedRegex]` partial method, a `[LibraryImport]` partial method and a
`JsonSerializerContext`, but the editor marked them as errors (CS8795, CS0534, CS0117). Their implementations come
from source generators, which the compiler runs from its `Analyzer` items. The IDE builds its Roslyn projects from
`Project.GetAnalyzerFilesAsync`: the `Analyzer` items returned by the design-time run of the `CoreCompileDependsOn`
targets in the builder (plus `BeforeCompile` since T146, [ADR 0008](0008-msbuild-hosting.md)). For an SDK project that
run returned only the NetAnalyzers, which the SDK adds as static items:

- the generators of the shared framework (`System.Text.RegularExpressions.Generator`,
  `Microsoft.Interop.LibraryImportGenerator` with `Microsoft.Interop.SourceGeneration`,
  `System.Text.Json.SourceGeneration`, ...) are added by `ResolveTargetingPackAssets` from
  `Microsoft.NETCore.App.Ref/<version>/analyzers/dotnet/cs`; in a build it runs as a dependency of
  `ResolveAssemblyReferences`;
- NuGet package analyzers (CommunityToolkit.Mvvm, Microsoft.Extensions.Logging, ...) are added by
  `ResolveLockFileAnalyzers` from `project.assets.json`; only the NuGet add-in ran it in that design-time run;
- `_HandlePackageFileConflicts` then keeps one copy of an analyzer that both a package and the targeting pack provide.

`ResolveTargetingPackAssets` and `_HandlePackageFileConflicts` did not run, so the workspace had no framework
generator. Once the generators are analyzer references, Roslyn 5.9 runs them itself: the `Solution` adds their
documents to the compilation (`Project.GetSourceGeneratedDocumentsAsync`), so semantic models, diagnostics and
completion see the generated members.

## Considered Options

1. **Run the analyzer targets of the SDK in the same design-time run** (chosen): `ResolveLockFileAnalyzers` and
   `_HandlePackageFileConflicts` (which runs `ResolveTargetingPackAssets`), before `BeforeCompile`.
2. Collect the `Analyzer` items of the reference run (`ResolveAssemblyReferencesDesignTime`), which already runs these
   targets: no extra target, but references and analyzers are cached and invalidated separately (the `DotNetProject`
   reference cache and the `CoreCompileEvaluator` items), so both caches would have to be merged.
3. Read the analyzers of the targeting pack from its `data/FrameworkList.xml` in the IDE: copies the rules of the SDK
   (language, off-by-default generators such as the configuration binder, conflicts with packages) into the IDE.
4. Run `CoreCompile` with `SkipCompilerExecution=true`, as Visual Studio does: covers every target, but resolves the
   references a second time on every evaluation (ADR 0008, T146).

## Decision Outcome

Option 1. For SDK projects (`UsingMicrosoftNETSdk`), `Project` runs
`<CoreCompileDependsOn>;ResolveLockFileAnalyzers;_HandlePackageFileConflicts;BeforeCompile`
(`Project.SdkAnalyzerTargets`). At design time `ResolvePackageAssets` is skipped when the project was never restored,
and `ResolveTargetingPackAssets` does not fail on a missing targeting pack, so a project that was never built gets the
framework generators. `PackageManagementMSBuildExtension` inserts its NuGet targets before these, so that the package
analyzers exist when the conflicts are resolved. `ResolveOffByDefaultAnalyzers` runs after `ResolveTargetingPackAssets`
as in a build. Nothing is compiled. `_HandlePackageFileConflicts` is a private target of the SDK, present in
`Microsoft.NET.Sdk` since 2.0; `BeforeCompile` stays last (ADR 0008).

**Loading.** Analyzer references stay `AnalyzerFileReference`s with the loader of Roslyn's `IAnalyzerService`
(`MonoDevelopAnalyzer`). On .NET that is Roslyn's `AnalyzerAssemblyLoader`: each analyzer directory gets a
`DirectoryLoadContext` of its own, and `Microsoft.CodeAnalysis` and the other compiler assemblies resolve to the IDE's
copies in the default context, so a generator implements the IDE's `IIncrementalGenerator`. Generators are therefore
not loaded into the single default context of [ADR 0006](0006-mono-addins-on-coreclr.md), and their own dependencies
(`Microsoft.Interop.SourceGeneration` for `LibraryImportGenerator`) load from their directory. The files are loaded in
place, without a shadow copy (Linux does not lock them).

**Generated documents in the editor.** A source-generated document exists only in the workspace: its `FilePath` is
virtual and relative (`<generator assembly>/<generator type>/<hint name>`). Go to definition, the list of partial
declarations and Find References show it through `SourceGeneratedFiles`, which writes its current text to a read-only
file in the cache directory (`<cache>/SourceGenerated/<project>-<hash>/...`) and opens that file.

### Consequences

- Good: `[GeneratedRegex]`, `[LibraryImport]`, `JsonSerializerContext`, logging and NuGet generators work in the editor
  with the analyzer list of `dotnet build`, also before the first build.
- Good: the design-time run grows by about 15 ms of target time (Modern: `ResolveTargetingPackAssets` 5 ms,
  `ResolveFrameworkReferences` 6 ms, `_HandlePackageFileConflicts` 3 ms, in a run of about 200 ms).
- Bad: generator load contexts are not collectible: a generator changed on disk (a package update) is used after an
  IDE restart.
- Bad: the generated file opened in the editor is a snapshot. It does not follow later edits, and it is not a workspace
  document, so it only has the grammar highlighting.
- Open: analyzers from project references (`ProjectReference` with `OutputItemType="Analyzer"`) come from
  `ResolveProjectReferences` and are not collected; generated documents are not listed under the project's
  Dependencies node.
