# Quarantined tests

Tests excluded from the gate with `[Category ("Quarantine")]` (constitution V). Per suite, the count may not grow after the suite is first converted.

## MonoDevelop.Core.Tests

Quarantined 77 test cases (76 methods; 80 at first run) on 2026-09-23. Bug: 47, legacy-fixture: 13, Flaky: 10, net4x-fixture: 6, Mono-only: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.CheckAssemblyReferences` | net4x-fixture | legacy .NET Framework fixture project | Expected: 4 | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.CheckReferencesAreOk` | net4x-fixture | legacy .NET Framework fixture project | Expected: equivalent to < "mscorlib", "System.Core", "System" > | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.RequiresFacadeAssembliesAsync(False,"MonoDevelop.Core.dll")` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: False | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.RequiresFacadeAssembliesAsync(True,"System.Collections.Immutable.dll")` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: True | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.Assemblies.SystemAssemblyServiceTests.TestFrameworkVersion` | net4x-fixture | legacy .NET Framework fixture project | Expected string length 13 but was 7. Strings differ at index 0. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Core.FileServiceEventQueueTests.TestTimeTracking` | Flaky | timing-dependent (file watcher / event timing) | Time it took to call event handler was not recorded | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.SdkResolverTests.UnknownSdk_DotNetMSBuildSdkResolverDoesNotFatalReportError` | Bug | project model / evaluator difference on SDK 10 | Expected: String containing "Check that a recent enough .NET Core SDK is installed" | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.ServiceProviderTests.PeekService` | Bug | fails on .NET 10; root cause to be analysed | Expected: same as <MonoDevelop.Core.TestService> | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WhenCredentialServiceThrows_Returns401` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Moq.MockException :  | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WhenOperationCanceledExceptionThrownDuringAcquiringCredentials_Throws` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Multiple failures or warnings in test: | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WhenTaskCanceledExceptionThrownDuringAcquiringCredentials_Throws` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Multiple failures or warnings in test: | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WithAcquiredCredentialsOn401_RetriesRequest` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Expected: OK | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WithAcquiredCredentialsOn403_RetriesRequest` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Expected: OK | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WithMissingCredentials_Returns401` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Moq.MockException :  | migration | 2026-09-23 | T135 |
| `MonoDevelop.Core.Web.HttpSourceAuthenticationHandlerTests.SendAsync_WithWrongCredentials_StopsRetryingAfter3Times` | Bug | mock expectations differ on the .NET 10 HttpClient pipeline | Expected: 5 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.AddRenameRemoveSingleFile` | Flaky | timing-dependent (file watcher / event timing) | System.ApplicationException : Timed out waiting. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.FileRenamedInSolutionPad_FileWatcherRenameEventIsIgnored` | Flaky | timing-dependent (file watcher / event timing) | Expected: not equal to <System.Threading.Tasks.Task`1[MonoDevelop.Projects.ProjectFile]> | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.FileWrittenButAlreadyExistsInFilesCollection_DuplicateFileNotAdded` | Flaky | timing-dependent (file watcher / event timing) | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.MoveDirectoryOutsideProjectDirectory` | Flaky | timing-dependent (file watcher / event timing) | System.ApplicationException : Timed out waiting. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.MoveDirectoryUpToProjectRootDirectory_FileServiceEventsFired` | Flaky | timing-dependent (file watcher / event timing) | System.ApplicationException : Timed out waiting. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreFileWatcherTests.RenameDirectory` | Flaky | timing-dependent (file watcher / event timing) | System.ApplicationException : Timed out waiting. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.AddFiles_NetStandardProjectWithXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.BuildMultiTargetProject` | Bug | fails on .NET 10; root cause to be analysed | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.DependsOn_FilesInProjectSubDirectory_XamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.FSharpXamarinFormsProject_SaveProject_XamlFilesDependentUponUnchanged` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.MultiTargetProject_ExecutionTargets` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.ReevaluateXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.InvalidOperationException : Sequence contains no matching element | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.ReloadModifiedFile_XamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.DotNetCoreProjectTests.SaveNetStandardProjectWithXamarinFormsVersion24PackageReference` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | msbuild /t:Restore "<repo>/main/te | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.FileServiceTests.ThawAfterGeneratingFileChangeEvents_File1ChangeFollowedByFile2ChangeThenFile2Change` | Bug | fails on .NET 10; root cause to be analysed | Expected: some item equal to /tmp/tmpmjsnC6.tmp.tmp | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.FileWatcherTests.AddSolutionToWorkspace_ChangeFileInAddedSolution` | Flaky | timing-dependent (file watcher / event timing) | MonoDevelop.Core.UserException : Could not load workspace item: <repo>/… | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.GenericProjectTests.LoadGenericProject` | Bug | project model / evaluator difference on SDK 10 | Expected: instance of <MonoDevelop.Projects.GenericProject> | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.GenericProjectTests.LoadGenericProjectWithImportBeforePropertyGroup` | Bug | project model / evaluator difference on SDK 10 | Expected: instance of <MonoDevelop.Projects.GenericProject> | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.GetAnalyzerFilesAsyncTests.ImportWithCoreCompileDependsOnAddedAfterAnalyzerFilesCached` | Bug | fails on .NET 10; root cause to be analysed | Expected: "CoreCompileFiles" | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.GetSourceFilesAsyncTests.ImportWithCoreCompileDependsOnAddedAfterSourceFilesCached` | Bug | fails on .NET 10; root cause to be analysed | Expected: "CoreCompileFiles" | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.LocalCopyTests.LocalCopyDefault` | net4x-fixture | legacy .NET Framework fixture project | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.MSBuildGlobTests.FileUpdateRemoveMetadataDefinedInGlob` | Bug | fails on .NET 10; root cause to be analysed | String lengths are both 1772. Strings differ at index 1473. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildGlobTests.RemoveAllFilesFromProject_NoFilesDeleted_RemoveItemAddedForFiles` | Bug | fails on .NET 10; root cause to be analysed | String lengths are both 1657. Strings differ at index 1495. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSdkProjectTests.WriteProject_ProjectDefinesMultipleTargetFrameworksAndTargetFrameworkVersionChanged_TargetFrameworksUpdated` | Bug | fails on .NET 10; root cause to be analysed | String lengths are both 19. Strings differ at index 12. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.InjectTarget` | Bug | project model / evaluator difference on SDK 10 | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.InjectTargetAfterLoadingProject` | Bug | project model / evaluator difference on SDK 10 | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.MultipleProjectsUsingSdk` | Bug | project model / evaluator difference on SDK 10 | <repo>/main/tests/tmp/ProjectUsing | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingMultipleSdk` | Bug | project model / evaluator difference on SDK 10 | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingSdk` | Bug | project model / evaluator difference on SDK 10 | <repo>/main/tests/tmp/ProjectUsing | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MSBuildSearchPathTests.ProjectUsingSdkImport` | Bug | project model / evaluator difference on SDK 10 | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MakefileTests.MakefileSynchronization` | Bug | project model / evaluator difference on SDK 10 | Contains Program.cs | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.MultiTargetProjectTests.TargetFrameworkMonikers_DifferentShortNameFormats` | net4x-fixture | legacy .NET Framework fixture project | msbuild /t:Restore /p:RestoreDisableParallel=true "<repo> | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.NetStandardProjectTests.NetStandardProjectReferenceIncludesFacades` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: True | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.AddingRemovingAndThenAddingReferenceToPortableLibrarySavesReferenceToFile` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.BuildPortableLibrary` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: null | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.LoadPortableLibrary` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | Expected: instance of <MonoDevelop.Projects.DotNetProject> | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.PortableLibraryTests.PortableLibraryImplicitReferences` | legacy-fixture | PCL / Xamarin / netstandard1.x fixture (retired target frameworks) | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T134 |
| `MonoDevelop.Projects.ProjectBuildTests.BuildWithCustomProps3` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 37 but was 49. Strings differ at index 0. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectBuildTests.FastBuildCheckWithLibrary` | Bug | fails on .NET 10; root cause to be analysed | Expected: True | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.AddProjectConfigurationWithProperties` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 2263 but was 2225. Strings differ at index 1571. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.CopyConfiguration` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 2497 but was 2496. Strings differ at index 1627. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.CreateConsoleProject` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 1954 but was 2155. Strings differ at index 1376. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.FrameworkAssemblyVersionNotStored` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 6 but was 73. Strings differ at index 6. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.LoadReferenceWithSpaces_bug43510` | Bug | fails on .NET 10; root cause to be analysed | Expected: True | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.LoadSaveBuildConsoleProject` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 73 but was 6. Strings differ at index 6. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.LoadSaveConsoleProjectWithEmptyGroup` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 73 but was 6. Strings differ at index 6. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.RenameConfiguration` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 1965 but was 1969. Strings differ at index 1205. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.RenameProjectConfiguration` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 1970 but was 1969. Strings differ at index 880. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectLoadSaveTests.SetCustomPropertiesInNewProject` | Bug | fails on .NET 10; root cause to be analysed | Expected string length 1998 but was 2199. Strings differ at index 1420. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectTests.AddReference` | Bug | fails on .NET 10; root cause to be analysed | System.NullReferenceException : Object reference not set to an instance of an object. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectTests.MSBuildRuntimeVersionProperty` | Mono-only | exercises Mono runtime behaviour | Expected: False | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectTests.RefreshReferences` | Bug | fails on .NET 10; root cause to be analysed | Expected: not null | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectTests.Resources` | Bug | fails on .NET 10; root cause to be analysed | Expected: 0 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectTests.UnknownNuGetPackageReferenceId_DesignTimeBuilds` | Bug | fails on .NET 10; root cause to be analysed | System.ComponentModel.Win32Exception : An error occurred trying to start process 'nuget' with working director | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectWithWildcardsTests.LoadProjectWithWildcardLinks` | Bug | project model / evaluator difference on SDK 10 | System.InvalidCastException : Unable to cast object of type 'MonoDevelop.Projects.UnknownSolutionItem' to type | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectWithWildcardsTests.LoadProjectWithWildcardLinks2` | Bug | project model / evaluator difference on SDK 10 | System.InvalidCastException : Unable to cast object of type 'MonoDevelop.Projects.UnknownSolutionItem' to type | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectWithWildcardsTests.LoadProjectWithWildcardLinks3` | Bug | project model / evaluator difference on SDK 10 | System.InvalidCastException : Unable to cast object of type 'MonoDevelop.Projects.UnknownSolutionItem' to type | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.ProjectWithWildcardsTests.LoadProjectWithWildcardLinks4` | Bug | project model / evaluator difference on SDK 10 | System.InvalidCastException : Unable to cast object of type 'MonoDevelop.Projects.UnknownSolutionItem' to type | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.SharedAssetsProjectTests.SaveSharedProject` | Bug | fails on .NET 10; root cause to be analysed | String lengths are both 942. Strings differ at index 236. | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.SolutionTests.SkipBuildingUnmodifiedProjects(True,1,2)` | Bug | fails on .NET 10; root cause to be analysed | Expected: 1 | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.BuilderManagerTests.AtLeastOneBuilderPersolution` | Flaky | builder counts are timing-dependent; a failed assertion leaves the sync build blocked and its builder hangs later tests | Test exceeded Timeout value of 120000ms | migration | 2026-09-23 | T135 |
| `MonoDevelop.Projects.FileWatcherTests.SaveProjectFileExternally_TwoSolutionsOpen_SolutionsHaveCommonDirectories` | Flaky | timing-dependent (file watcher / event timing); fails about 1 run in 3 | Expected: 1 | migration | 2026-09-23 | T135 |

### Released

- 2026-09-23: the 5 `SynchronizationContext may not be used as a TaskScheduler` cases pass after
  `WorkspaceObject.Dispose` switched to `Runtime.MainTaskScheduler` (the failure appeared when NUnit's
  timeout wrapper ran a test on a thread-pool thread) — T135.

## MonoDevelop.Xml.Tests

Quarantined 2 test cases (2 methods) on 2026-09-23. IDE-host: 2.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.Xml.Tests.Schema.SchemaValidationTests.ValidateXsltInvalid` | IDE-host | needs the IDE add-in host (GUI add-ins are not loaded by the headless test host) | System.InvalidOperationException : Add-in engine not initialized. | migration | 2026-09-23 | T107 |
| `MonoDevelop.Xml.Tests.Schema.SchemaValidationTests.ValidateXsltValid` | IDE-host | needs the IDE add-in host (GUI add-ins are not loaded by the headless test host) | Expected: 0 | migration | 2026-09-23 | T107 |

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

Quarantined 3 test cases (3 methods) on 2026-09-24. IDE-host: 3.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.VersionControl.Git.Tests.BaseGitUtilsTest.BlameDiffWithNotCommitedItem(True,True)` | IDE-host | needs the VS editor MEF composition of the IDE host (Mono.TextEditor text model) | Microsoft.VisualStudio.Composition.CompositionFailedException : Expected 1 export(s) with contract name "Micro | migration | 2026-09-24 | T107 |
| `MonoDevelop.VersionControl.Git.Tests.BaseGitUtilsTest.BlameIsCorrect` | IDE-host | needs the VS editor MEF composition of the IDE host (Mono.TextEditor text model) | Microsoft.VisualStudio.Composition.CompositionFailedException : Expected 1 export(s) with contract name "Micro | migration | 2026-09-24 | T107 |
| `MonoDevelop.VersionControl.Git.Tests.BaseGitUtilsTest.BlameWithWorkingChanges` | IDE-host | needs the VS editor MEF composition of the IDE host (Mono.TextEditor text model) | Microsoft.VisualStudio.Composition.CompositionFailedException : Expected 1 export(s) with contract name "Micro | migration | 2026-09-24 | T107 |

## MonoDevelop.PackageManagement.Tests

Quarantined 1 test cases (1 methods) on 2026-09-24. network: 1.

| Test | Reason | Note | First error line | Owner | Date | Task |
|---|---|---|---|---|---|---|
| `MonoDevelop.PackageManagement.Tests.NuGetSdkResolverTests.ProjectUsingMSBuildSdkFromNuGet` | network | restores an MSBuild SDK package from nuget.org (no network in tests); PackageOperationsEndToEndTests resolves one from a local feed | MonoDevelop.Core.UserException : Unable to find SDK 'Xam.Test.MSBuild.Sdk/0.1.0' | migration | 2026-09-24 | T100 |
