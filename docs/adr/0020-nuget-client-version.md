# 0020 — NuGet client 7.9, loaded from the .NET SDK

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

`MonoDevelop.PackageManagement` (the NuGet add-in, 308 sources) targeted the NuGet 5.x client
(`NuGet.PackageManagement`, `NuGet.Indexing`, `$(NuGetVersionNuGet)`), shipped every NuGet assembly next to the add-in
and ran restore, install, update and uninstall in the IDE process. On .NET 10 the IDE process also hosts the MSBuild of
the installed .NET SDK (ADR 0008): the in-process evaluator runs MSBuild's SDK resolvers, and
`Microsoft.Build.NuGetSdkResolver` depends on the SDK's own NuGet client (SDK 10.0.401 ships NuGet **7.9.0**,
assembly version `7.9.0.0`, built from `7.9.0-rc.42413`).

Before this ADR the host already had NuGet in its `deps.json`: `Microsoft.TemplateEngine.Edge` 10.0.401 (Ide) brought
`NuGet.Common/Configuration/Credentials/Packaging/Protocol/Versioning` **6.13.2** into `main/build/bin`, while
`NuGet.Frameworks` was compile-only (`ExcludeAssets="runtime"`) and loaded from the SDK. The IDE log showed:

    [MSBuild] The SDK resolver type "NuGetSdkResolver" failed to load. Could not load file or assembly
    'NuGet.Common, Version=7.9.0.0'. The located assembly's manifest definition does not match the assembly reference.

The default load context binds `NuGet.Common` to the lower version of the trusted platform assemblies (6.13.2 from the
host's `deps.json`) and refuses the resolver's 7.9.0.0 reference; MSBuild's plug-in load context falls back to the
default context for assemblies of the MSBuild directory, so the resolver cannot get its own copy either. Any MSBuild
SDK resolved from NuGet (`msbuild-sdks` in `global.json`, e.g. `Microsoft.Build.NoTargets`) failed in the IDE.

## Decision Drivers

- One NuGet client per process: the IDE, MSBuild's `NuGetSdkResolver`, `Microsoft.TemplateEngine.Edge` and the NuGet
  add-in must bind to the same `NuGet.*` assemblies (a second copy cannot be loaded in the default context).
- API compatibility with the upstream add-in code written for NuGet 5.x (`NuGetPackageManager`,
  `BuildIntegratedNuGetProject`, `DependencyGraphRestoreUtility`, `PackageRestoreManager`, ...).
- Security: `NuGetAudit` (all, low) stays clean; no .NET Framework-only packages (NU1701).
- nuget.org only (ADR 0004); versions pinned centrally (`main/Directory.Packages.props`).
- Keep working when users run a newer SDK feature band or patch (`global.json`: `rollForward: latestFeature`).

## Considered Options

1. **NuGet 7.9.0 from nuget.org, compile-only for the assemblies the SDK carries; load them from the SDK at run time**
   (like MSBuild, ADR 0008). Ship only `NuGet.PackageManagement`, `NuGet.Resolver` and `Microsoft.Web.Xdt` (not in
   the SDK) in the add-in folder.
2. NuGet 7.9.0 shipped in `main/build/bin` (host `deps.json`) for every assembly: the pinned SDK's resolver works, but
   the default context binds the host's `7.9.0.0` and fails again as soon as an SDK patch or feature band ships a newer
   NuGet (e.g. `7.9.1.0` or `7.10.0.0`).
3. Stay on NuGet 6.x (6.14.3, the last 6.x): smaller API delta from 5.x but the same `NuGetSdkResolver` failure, and
   a client older than the SDK's reading the SDK's `dgspec`/assets files.
4. Load the add-in's NuGet in a separate `AssemblyLoadContext`: NuGet types cross the Ide/add-in boundary
   (`NuGet.Frameworks` in the project model, `NuGet.Versioning` in public add-in APIs), so the add-in would need a
   rewrite into a service boundary.

## Decision Outcome

Option 1.

- `NuGetClientVersion` = **7.9.0** in `main/Directory.Packages.props`: the NuGet the pinned SDK (10.0.4xx) ships, the
  newest on nuget.org (`NuGet.PackageManagement` 7.9.0 exists there). All 13 client packages are pinned
  (`NuGet.Commands`, `.Common`, `.Configuration`, `.Credentials`, `.DependencyResolver.Core`, `.Frameworks` (was
  6.13.2), `.LibraryModel`, `.PackageManagement`, `.Packaging`, `.ProjectModel`, `.Protocol`, `.Resolver`,
  `.Versioning`); transitive pinning lifts `Microsoft.TemplateEngine.Edge`'s NuGet 6.13.2 closure to 7.9.0.
- `main/msbuild/Linux/Common.targets` gives every project under `main/src` and `main/tests` a compile-only reference
  (`ExcludeAssets="runtime" PrivateAssets="all"`) to the 11 packages whose assemblies are in the SDK directory
  (`NuGet.Commands`, `.Common`, `.Configuration`, `.Credentials`, `.DependencyResolver.Core`, `.Frameworks`,
  `.LibraryModel`, `.Packaging`, `.ProjectModel`, `.Protocol`, `.Versioning`). A direct reference's asset flags win over
  the transitive ones, so no host, add-in or test host copies them or lists them in its `deps.json`, whichever package
  brings them in. At run time `MSBuildLocator` (registered first thing by every host) resolves them from the SDK
  directory; MSBuild's plug-in context and the IDE then share the same assemblies. `MDNuGetClientFromSdk=false` opts a
  project out. The earlier per-project `NuGet.Frameworks` compile-only references (Ide, test projects) are kept; the
  rule skips packages a project already references.
- `MonoDevelop.PackageManagement` references `NuGet.PackageManagement` and `NuGet.Resolver` normally and copies them,
  and `Microsoft.Web.Xdt` (`Microsoft.Web.XmlTransform.dll`), into `main/build/AddIns/MonoDevelop.PackageManagement/`
  (they are not in the SDK; `scripts/check-assemblies.sh` finds no duplicate). The add-in manifest imports those three
  assemblies instead of the eleven NuGet assemblies it used to ship.
- `System.Security.Cryptography.Pkcs` (NuGet.Packaging's signing dependency, not in the shared framework) is pinned to
  10.0.12 so the host's copy satisfies any SDK NuGet built against 8.0–10.0.
- `NuGet.Indexing` is dropped: 7.x is .NET Framework-only (Lucene.Net 3.0.3). Multi-source search results are
  interleaved in source order, one result per package id, instead of Lucene relevance merging.
- Minimum SDK: the one whose NuGet is at least the compile version (7.9, SDK 10.0.4xx). Newer SDKs bring a newer NuGet
  7.x, which MonoDevelop then uses, like their MSBuild; a NuGet 8 would be a deliberate upgrade of this ADR.

### API changes (NuGet 5.x → 7.9) handled in the port

- `BuildIntegratedNuGetProject`: new abstract `GetPackageSpecsAndAdditionalMessagesAsync` and
  `UninstallPackageAsync (string, BuildIntegratedInstallationContext, CancellationToken)`.
- `BuildIntegratedInstallationContext`: settable properties; `SuccessfulFrameworks` / `UnsuccessfulFrameworks` are the
  project's target framework aliases (`List<string>`), `OriginalFrameworks` is gone (conditional package references use
  the alias directly).
- `DependencyGraphRestoreUtility.RestoreProjectAsync` is gone: a single-project restore runs `RestoreAsync` on the
  solution graph with that project as the only restore root; the result comes from the `RestoreSummary`.
- `IPackageRestoreManager`: logger-taking restore overloads only (the add-in's extension methods pass a
  `LoggerAdapter` over the project context), `AssetsFileMissingStatusChanged`, `RaiseAssetsFileMissingEventForProjectAsync`;
  `IsCurrentSolutionEnabledForRestore` / `EnableCurrentSolutionForRestore` removed.
- `IPackageSourceProvider`: `LoadAuditSources` / `SaveAuditSources`; `DisablePackageSource` and
  `IsPackageSourceEnabled` take the source name.
- `IProjectSystemReferencesReader.GetItemsAsync`; `INuGetProjectServices.BuildProperties` (`IProjectBuildProperties`)
  removed.
- `IPluginFactory` is internal and `PluginDiscoverer` reads `NUGET_PLUGIN_PATHS` itself: the add-in uses NuGet's
  `PluginFactory`; `MonoDevelopPluginFactory` (ran .NET Framework plug-in `.exe` files with Mono) is not compiled.
- `StsAuthenticationHandler` (WS-Trust) and `NuGetEventTrigger` (Visual Studio ETW events) no longer exist.
- `Repository.ProviderFactory.GetCoreV3` grows with NuGet (e.g. the NuGetAudit vulnerability resource): the add-in takes
  NuGet's list and only swaps in its HTTP handler and plug-in manager providers instead of copying the NuGet 5 list.
- With a global `TargetFramework` (the IDE runs targets of multi-target projects with the active framework), NuGet 6+'s
  `GenerateRestoreGraphFile` writes a graph with that framework only: `MSBuildPackageSpecCreator` restores the outer
  project (`TargetFramework` set to empty for multi-target projects).
- NuGet 6+ names the no-op cache `obj/project.nuget.cache`; `PackageSource.Source` rejects null.
- `PackageSourceCredential.FromUserInput (…, storePasswordInClearText: false)` throws outside Windows on .NET (no
  `ProtectedData`): passwords are stored in clear text on Linux, as NuGet's own tools do.
- Roslyn 5.9's `IPackageInstallerService` / `ISymbolSearchService` (MonoDevelop.Refactoring's `PackageInstaller`
  services, re-included): `TryInstallPackageAsync`, `TryGetPackageSources`, `FindPackagesAsync (TypeQuery,
  NamespaceQuery)`, `FindReferenceAssembliesAsync`, `ValueTask<ImmutableArray<…>>` results.

### Consequences

- Good: the `NuGetSdkResolver` loads in the IDE (the warning is gone), MSBuild and the IDE share one NuGet, and the IDE
  follows the SDK's NuGet fixes like it follows its MSBuild.
- Good: no NuGet assembly in `main/build/bin`; the add-in ships 3 assemblies instead of 16.
- Bad: the add-in's `NuGet.PackageManagement` 7.9 runs against the SDK's NuGet 7.x, which may be newer than 7.9; a
  breaking NuGet change inside 7.x would surface at run time. The FR-010 end-to-end test (add, update, restore, remove
  with a local feed) runs against the CI SDK.
- Bad: every project under `main/src` and `main/tests` has 11 extra compile-only package references (lock files).
