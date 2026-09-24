# Quarantined tests

Tests excluded from the gate with `[Category ("Quarantine")]` (constitution V). Per suite, the count may not grow after the suite is first converted.

## MonoDevelop.Core.Tests

Quarantined 78 test cases (77 methods; 80 at first run) on 2026-09-23 (+1 on 2026-09-24); 47 released on 2026-09-24 (T135, below). 31 cases (30 methods) remain, 2.7% of the suite's 1158 cases: legacy-fixture: 13, Bug: 7, net4x-fixture: 6, network: 2, excluded: 1, Mono-only: 1, SDK-change: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.CheckAssemblyReferences` | net4x-fixture | legacy .NET Framework fixture project | Expected: 4 | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.CheckReferencesAreOk` | net4x-fixture | legacy .NET Framework fixture project | Expected: equivalent to < "mscorlib", "System.Core", "System" > | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.RequiresFacadeAssembliesAsync(False,"MonoDevelop.Core.dll")` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: False | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.RequiresFacadeAssembliesAsync(True,"System.Collections.Immutable.dll")` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: True | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.TestFrameworkVersion` | net4x-fixture | legacy .NET Framework fixture project | Expected string length 13 but was 7. Strings differ at index 0. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.SdkResolverTests.UnknownSdk_DotNetMSBuildSdkResolverDoesNotFatalReportError` | Bug | MonoDevelop's SDK resolution does not load the .NET SDK resolver (SDK 10 ships Microsoft.DotNet.SdkResolver.dll at the SDK root, not under SdkResolvers/) | Expected: String containing "Check that a recent enough .NET Core SDK is installed" | migration | 2026-09-23 | T141 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.AddFiles_NetStandardProjectWithXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.BuildMultiTargetProject` | network | restores netcoreapp1.1/netstandard1.0 packages from nuget.org (no network in tests) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T143 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.DependsOn_FilesInProjectSubDirectory_XamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.FSharpXamarinFormsProject_SaveProject_XamlFilesDependentUponUnchanged` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.MultiTargetProject_ExecutionTargets` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.ReevaluateXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.InvalidOperationException : Sequence contains no matching element | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.ReloadModifiedFile_XamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.SaveNetStandardProjectWithXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.LocalCopyTests.LocalCopyDefault` | net4x-fixture | legacy .NET Framework fixture project | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.InjectTarget` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | Expected: 1 | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.InjectTargetAfterLoadingProject` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | Expected: 1 | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.MultipleProjectsUsingSdk` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | <repo>/main/tests/tmp/ProjectUsing | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingMultipleSdk` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | Expected: 1 | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingSdk` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | <repo>/main/tests/tmp/ProjectUsing | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingSdkImport` | Bug | the out-of-process .NET builder ignores the MSBuild import search paths and SDK folders registered by add-ins (the Mono builder got them from its patched MSBuild.exe.config toolset; ADR 0008) | Expected: 1 | migration | 2026-09-23 | T140 |
| `MonoDevelop.Projects.MakefileTests.MakefileSynchronization` | excluded | needs the MonoDevelop.Autotools add-in, excluded from the Linux build (ADR 0017) | Contains Program.cs | migration | 2026-09-23 | T142 |
| `MonoDevelop.Projects.MultiTargetProjectTests.TargetFrameworkMonikers_DifferentShortNameFormats` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/main/tests/tmp/short-name-formats.csproj-33/short-na | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.NetStandardProjectTests.NetStandardProjectReferenceIncludesFacades` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: True | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.AddingRemovingAndThenAddingReferenceToPortableLibrarySavesReferenceToFile` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.BuildPortableLibrary` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: null | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.LoadPortableLibrary` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: instance of <MonoDevelop.Projects.DotNetProject> | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.PortableLibraryImplicitReferences` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.ProjectTests.RefreshReferences` | Mono-only | expects a missing local gtk-sharp.dll to fall back to the Mono GAC package (no GAC on .NET, ADR 0007) | Expected: not null | migration | 2026-09-23 | T142 |
| `MonoDevelop.Projects.ProjectTests.Resources` | SDK-change | MSBuild on .NET needs System.Resources.Extensions and GenerateResourceUsePreserializedResources for the non-string .resx resources of a .NET Framework project (MSB3822/MSB3823) | Expected: 0 | migration | 2026-09-23 | T143 |
| `MonoDevelop.Projects.ProjectTests.UnknownNuGetPackageReferenceId_DesignTimeBuilds` | network | runs `nuget restore` against nuget.org (no nuget executable, no network in tests) | System.ComponentModel.Win32Exception : An error occurred trying to start process 'nuget' with working director | migration | 2026-09-23 | T143 |

### Released

- 2026-09-23: the 5 `SynchronizationContext may not be used as a TaskScheduler` cases pass after
  `WorkspaceObject.Dispose` switched to `Runtime.MainTaskScheduler` (the failure appeared when NUnit's
  timeout wrapper ran a test on a thread-pool thread) — T135.
- 2026-09-24 (T135): 47 cases, by root cause.
  - 4: Mono.Addins 1.4 gives extension nodes created from custom attributes (`[ProjectModelDataItem]`,
    `[ExportProjectType]`) an assembly-qualified type name. `DataContext` registered the serializable classes
    under a wrong name (`GenericProject` and the `.mdw` `Workspace` were unknown) and `GetTypeGuidForItem`
    wrote the generic GUID for shared projects. Both now strip the assembly part (`DataContext.GetTypeFullName`):
    `GenericProjectTests` (2), `SharedAssetsProjectTests.SaveSharedProject`,
    `FileWatcherTests.AddSolutionToWorkspace_ChangeFileInAddedSolution`.
  - 8: the tests run on NUnit worker threads, and the project and the file service raise their events on the main
    loop (GuiUnit ran the tests on it). The tests now wait for the main loop (`await Runtime.RunInMainThread`) or
    run the user's steps on it, and `TestTimeTracking` uses a handler longer than one `TimeSpan` tick:
    `ImportWithCoreCompileDependsOn*` (2), `FastBuildCheckWithLibrary`, `SkipBuildingUnmodifiedProjects`,
    `ThawAfterGeneratingFileChangeEvents_*`, `TestTimeTracking`,
    `FileWrittenButAlreadyExistsInFilesCollection_DuplicateFileNotAdded`,
    `FileRenamedInSolutionPad_FileWatcherRenameEventIsIgnored`.
  - 5: inotify watches a new directory only after reporting its creation, so a file written into it right away
    raised no event and was not added to the project. `FileWatcherWrapper` now reports the files that a new
    directory already has: `DotNetCoreFileWatcherTests` `AddRenameRemoveSingleFile`, `RenameDirectory`,
    `MoveDirectoryOutsideProjectDirectory`, `MoveDirectoryUpToProjectRootDirectory_FileServiceEventsFired`,
    `AddFileExternally_FileGlobHasMSBuildMetadata_FileAddedToProject`.
  - 2 (timing): `FileWatcherTestBase` started waiting after the files were written and missed events that had
    already arrived (it now checks the captured events first, under a lock);
    `BuilderManagerTests.AtLeastOneBuilderPersolution` waits for the builder disposal instead of a fixed 500 ms,
    and releases the blocked build in `finally`: `SaveProjectFileExternally_TwoSolutionsOpen_*`,
    `AtLeastOneBuilderPersolution`.
  - 1: `BasicServiceProvider` completed the initialization task before removing it, and `SetResult` ran the
    `GetService` continuation inline, so `PeekService` still saw the service as initializing:
    `ServiceProviderTests.PeekService`.
  - 2: glob matches came in directory order (sorted on macOS, hash order on ext4/overlayfs), which decides the
    order of the Remove items and Exclude lists the project writes. `DefaultMSBuildEngine` sorts them (ordinal):
    `MSBuildGlobTests` (2).
  - 4: the .NET SDK's common targets give every configuration a default `OutputPath` (`bin\$(Configuration)\`).
    Linking a copied configuration to its evaluated project replaced the copied values with it, so copying and
    renaming configurations wrote the wrong output path. `MSBuildPropertyGroup.CopyFrom` marks the copies as
    modified: `RenameProjectConfiguration`, `CopyConfiguration`, `RenameConfiguration`. A new configuration whose
    `OutputPath` equals that default no longer writes it (SDK 10 difference; fixture
    `ConsoleProject.csproj.config-props-added` updated): `AddProjectConfigurationWithProperties`.
  - 3: test premises from Mono: `LoadReferenceWithSpaces_bug43510` used gtk-sharp 2.12 from the Mono GAC (now a
    framework assembly); `MSBuildRuntimeVersionProperty`: like `dotnet msbuild`, the evaluator leaves
    `MSBuildRuntimeVersion` empty on .NET; `BuildWithCustomProps3`: the IDE sets `BuildingInsideVisualStudio=true`
    only for solution builds, so the test saves its wrapping solution.
  - 1: an in-memory project with a relative file name was evaluated under `main/` and imported the repository's
    `Directory.Build.props` (`TargetFramework=net10.0`). `Util.GetTmpProjectFileName` places it in the tests' tmp
    folder, which now also stops `Directory.Packages.props` (central package management) from applying to
    fixtures: `WriteProject_ProjectDefinesMultipleTargetFrameworksAndTargetFrameworkVersionChanged_*`.
  - 4: `project-with-wildcard-links` was a PCL project (retired project type, loaded as an unknown item); the tests
    are about wildcard links, so the fixture is now a plain C# library: `LoadProjectWithWildcardLinks*` (4).
  - 13: already fixed by earlier commits and passing 3 runs in a row. `ProxyCache` dereferenced the null that .NET's
    `GetProxy` returns for an unproxied URI (23da988aa3): `HttpSourceAuthenticationHandlerTests` (7). One add-in
    registry per test host (763695990b); a shared registry made C# projects load as unknown items:
    `ProjectLoadSaveTests` `CreateConsoleProject`, `FrameworkAssemblyVersionNotStored`,
    `LoadSaveBuildConsoleProject`, `SetCustomPropertiesInNewProject`, `LoadSaveConsoleProjectWithEmptyGroup`,
    `ProjectTests.AddReference`.
  - Every released case passed 3 runs in a row. The timing ones also passed a run of their classes with 13 busy
    processes in parallel (load average 28).

## MonoDevelop.Xml.Tests

No quarantined tests since 2026-09-24 (T107). The two XSLT validation tests quarantined on 2026-09-23 (IDE-host: the GUI
add-ins were not loaded by the headless test host) pass in the IDE test host (IdeUnitTests.GuiTestHost), which also brings
back ExpandSelectionTests and XmlCodeCompletionTests.

## MonoDevelop.TextEditor.Tests

Quarantined 16 test cases (16 methods) on 2026-09-24. Bug: 16.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `Mono.TextEditor.Tests.Actions.NavigationExtensionTests.TestBug294858` | Bug | fails on .NET 10; root cause to be analysed | System.InvalidOperationException : Service MonoDevelop.Ide.Gui.Documents.DocumentManager not initialized | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.Test55462` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.Test55670` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestBinaryLiteral` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestBug56747` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestBug57033` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestBug603` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestCDATASection` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestChunkValidity` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.InvalidOperationException : Operation not supported in background thread | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestDoubleDigit` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestDoubleVerbatimStringEscapes` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestFSharpLineBug` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestHexDigit` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestSpans` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestStringEscapes` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |
| `Mono.TextEditor.Tests.SyntaxHighlightingTests.TestVerbatimStringEscapes` | Bug | test main loop: runs off the thread the runtime treats as main (GuiUnit ran the suite on the GTK loop) | System.AggregateException : One or more errors occurred. (Operation not supported in background thread) | migration | 2026-09-24 | T135 |

## MonoDevelop.VersionControl.Git.Tests

No quarantined tests since 2026-09-24 (T107). The three blame tests quarantined earlier that day (IDE-host: they need the
VS editor MEF composition) pass: the test host now has the editor platform implementation assemblies (through
IdeUnitTests).

## MonoDevelop.Ide.Tests

Quarantined 30 test cases (26 methods) on 2026-09-24. Bug: 16, legacy-fixture: 5, net4x-fixture: 5, Mono-only: 3, Flaky: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.Ide.Composition.CompositionManagerCachingTests.TestCacheControlDataIntegrity` | net4x-fixture | expects System.Console in mscorlib (.NET Framework); on .NET it is a separate assembly | Expected: some item equal to "System.Console, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Composition.CompositionManagerCachingTests.TestCacheWithDynamicAssembly` | Mono-only | CodeDOM compilation (CSharpCodeProvider.CompileAssemblyFromSource) is not supported on .NET | System.PlatformNotSupportedException : Operation is not supported on this platform. | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Gui.GLibLoggingTests.GLibLoggingHaveFullStacktracesInLog` | Mono-only | expects Mono's mixed-mode stack trace in the legacy mdtool test host (MonoDevelopProcessHost.Main, native GLib frames) | Expected: String containing "at MonoDevelopProcessHost.Main" | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Gui.GLibLoggingTests.ValidateCrashIsSentForGLibExceptions` | Mono-only | expects Mono's mixed-mode stack trace in the legacy mdtool test host (MonoDevelopProcessHost.Main, native GLib frames) | Expected: String containing "at MonoDevelopProcessHost.Main" | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.ProjectTemplateTests.Bug57840` | legacy-fixture | project template of an add-in outside the Linux build (shared project, portable library) | System.InvalidOperationException : Sequence contains no matching element | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.ProjectTemplateTests.NewSharedProjectAddedToExistingSolutionUsesCorrectBuildAction` | legacy-fixture | project template of an add-in outside the Linux build (shared project, portable library) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.Projects.PclToProjectJsonConversionTests.MigrateXamarinFormsPclProjectToProjectJson` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.InvalidCastException : Unable to cast object of type 'MonoDevelop.Projects.UnknownSolutionItem' to type | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.RoslynSearchCategoryTests.TestConsoleProjectWorks` | Bug | fails on .NET 10; root cause to be analysed | Expected: 2 | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.RoslynServices.MonoDevelopFrameworkAssemblyPathResolverFactoryTests.TestSimpleCase` | Bug | fails on .NET 10; root cause to be analysed | Expected: not null | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestBatchedBehaviourWorks(False)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestBatchedBehaviourWorks(True)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestCachedContentsAreReleasedIfNotQueried(False)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestCachedContentsAreReleasedIfNotQueried(True)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestFileChangeTriggersNotification` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsAreReported(False)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsAreReported(True)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsOnFileAdded` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsOnWorkspaceReopen(False)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsOnWorkspaceReopen(True)` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.Tasks.CommentTasksProviderTests.TestToDoCommentsTagsChanged` | Bug | TODO comment tasks: the notifications the test waits for never arrive (no Roslyn solution crawler in Roslyn 4+; MonoDevelopTaskListProvider) | System.TimeoutException : The test did not complete within 120000 ms (IdeUnitTests message loop) | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.TypeSystem.TypeSystemServiceTests.EditorConfigFile_ModifiedInTextEditor` | Flaky | timing-dependent (file watcher / event timing) | Timed out waiting for analyzer config file changed event | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.TypeSystem.TypeSystemServiceTests.MultiTargetFramework` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/… | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.TypeSystem.TypeSystemServiceTests.MultiTargetFramework_ProjectReferences` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/… | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.TypeSystem.TypeSystemServiceTests.MultiTargetFramework_ReloadProject_TargetFrameworksChanged` | Bug | fails on .NET 10; root cause to be analysed | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/… | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.TypeSystem.TypeSystemServiceTests.MultiTargetFramework_RemoveProject` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/… | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.TypeSystem.WorkspaceFilesCacheTests.TestWorkspaceFilesCacheCreation_MultiTargetFramework` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo>/… | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.TypeSystemServiceTests.ProjectModifiedWhilstBeingAddedToSolution` | Bug | fails on .NET 10; root cause to be analysed | System.Xml.Linq reference missing from type system information | migration | 2026-09-24 | T135 |
| `MonoDevelop.Ide.TypeSystemServiceTests.ProjectReferencingOutputTrackedReference` | legacy-fixture | needs a language binding that is not in the Linux build (IL assembler, F#) | System.ArgumentNullException : Value cannot be null. (Parameter 'project') | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.TypeSystemServiceTests.TestOuptutTracking_LanguageName` | legacy-fixture | needs a language binding that is not in the Linux build (IL assembler, F#) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-24 | T134 |
| `MonoDevelop.Ide.UserPreferencesTests.LoadUserPreferences` | Bug | fails on .NET 10; root cause to be analysed | MonoDevelop.Core.UserException : Could not load workspace item: <repo>/… | migration | 2026-09-24 | T135 |

## MonoDevelop.CSharpBinding.Tests

Quarantined 9 test cases (9 methods) on 2026-09-24. Bug: 8, Flaky: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.CSharpBinding.CSharpCompletionTextEditorTests.TestImportCompletionExtensionMethods` | Bug | fails on .NET 10; root cause to be analysed | Expected: True | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.CSharpCompletionTextEditorTests.TestImportCompletionTypes` | Bug | fails on .NET 10; root cause to be analysed | Expected: True | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.CSharpCompletionTextEditorTests.TestVSTSBug568065` | Bug | fails on .NET 10; root cause to be analysed | Expected: 1 | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.ExpandSelectionHandlerTests.TestExpandSelection` | Flaky | timing-dependent (selection expanded before the document is parsed); failed in 1 of 2 runs | Expected: 74 | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.Refactoring.CSharpCodeActionEditorExtensionTests.FixesAreReportedByExtension` | Bug | diagnostics / code fixes do not reach the editor extension in the test host (pulled diagnostics, T090) | Expected: 1 | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.Refactoring.CSharpCodeActionEditorExtensionTests.FixesAreReportedForCompilerErrors` | Bug | diagnostics / code fixes do not reach the editor extension in the test host (pulled diagnostics, T090) | System.Threading.Tasks.TaskCanceledException : A task was canceled. | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.Refactoring.CSharpResultsEditorExtensionTests.DiagnosticEnableSourceAnalysisChanged` | Bug | diagnostics / code fixes do not reach the editor extension in the test host (pulled diagnostics, T090) | System.Threading.Tasks.TaskCanceledException : A task was canceled. | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.Refactoring.CSharpResultsEditorExtensionTests.DiagnosticsAreReportedByExtension` | Bug | diagnostics / code fixes do not reach the editor extension in the test host (pulled diagnostics, T090) | System.Threading.Tasks.TaskCanceledException : A task was canceled. | migration | 2026-09-24 | T135 |
| `MonoDevelop.CSharpBinding.Tests.CustomProjectRuleSetTests.CustomCodeAnalysisRuleSetFile` | Bug | fails on .NET 10; root cause to be analysed | System.Collections.Generic.KeyNotFoundException : The given key 'SA1003' was not present in the dictionary. | migration | 2026-09-24 | T135 |

## MonoDevelop.PackageManagement.Tests

Quarantined 1 test cases (1 methods) on 2026-09-24. network: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.PackageManagement.Tests.NuGetSdkResolverTests.ProjectUsingMSBuildSdkFromNuGet` | network | restores an MSBuild SDK package from nuget.org (no network in tests); PackageOperationsEndToEndTests resolves one from a local feed | MonoDevelop.Core.UserException : Unable to find SDK 'Xam.Test.MSBuild.Sdk/0.1.0' | migration | 2026-09-24 | T100 |

## MonoDevelop.DotNetCore.Tests

Quarantined 13 test cases (13 methods) on 2026-09-24. network: 11, legacy-fixture: 1, SDK-change: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.DotNetCore.Tests.DependencyNodeTests.MultiTarget_NetStandardAndNetCoreApp_NewtonsoftJsonNuGetPackageReference` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DependencyNodeTests.NetStandardLibrary_NewtonsoftJsonNuGetPackageReference` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DependencyNodeTests.NetStandardLibrary_OneIndirectNuGetDiagnosticWarningsForSystemNetHttp` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DependencyNodeTests.NetStandardLibrary_OneNuGetDiagnosticWarningsForSystemComponentModelEventBasedAsync` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DependencyNodeTests.NetStandardLibrary_TwoNuGetDiagnosticWarningsForSystemComponentModelEventBasedAsync` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.AspNetCoreProject_DefaultBuildActions` | SDK-change | the .NET 10 Web SDK defines no TypeScriptCompile build action for .ts files | Expected string length 17 but was 4. Strings differ at index 0. | migration | 2026-09-24 | T135 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.CanReference_PortableClassLibrary_FromNetStandardOrNetCoreAppProject` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-24 | T134 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.GetReferences_ThreeProjectReferencesAndReferenceOutputAssemblyIsFalse_ReferenceOutputAssemblyIsFalseProjectsNotReturned` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.GetReferences_ThreeProjectReferencesJsonNet_JsonNetReferenceAvailableToReferencingProjects` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.GetReferences_ThreeProjectReferences_TransitivelyReferencedProjectsIncluded` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.DotNetCoreProjectExtensionTests.NetStandard_EnsureGeneratedAssemblyInfoAvailableToTypeSystem` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.FrameworkReferenceTests.UnknownNuGetPackageReferenceId_DesignTimeBuilds` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'msbuild' with working direct | migration | 2026-09-24 | T099 |
| `MonoDevelop.DotNetCore.Tests.PackProjectTests.Should_pack_multi_target_project` | network | restores packages from nuget.org (msbuild /t:Restore, netstandard1.x packages; no network in tests); DependenciesNodeSdkProjectTests restores from a local feed | Expected: 0 | migration | 2026-09-24 | T099 |
