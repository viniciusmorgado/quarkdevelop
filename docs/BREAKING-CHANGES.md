# Breaking changes — MonoDevelop on Linux / .NET 10

This fork of MonoDevelop 8.6 targets Linux only, runs on .NET 10 LTS (no Mono) and uses GTK3.
Compared with MonoDevelop 8.6:

## Platforms and runtime

- macOS and Windows builds are no longer supported; their code is removed from the repository
  ([ADR 0017](adr/0017-linux-exclusions.md)).
- The IDE runs on .NET 10 (CoreCLR), not Mono. Mono-specific options (`MONO_OPTIONS`,
  `mdtool build -r:<mono prefix>`) are ignored.
- The UI toolkit is GTK 3.24 (was GTK 2.24); GTK2 themes (`gtkrc`) no longer apply
  ([ADR 0011](adr/0011-gtk3-port-strategy.md)).
- The repository has no git submodules (`.gitmodules` is gone): dependencies come from NuGet or from
  `main/vendor/` ([ADR 0005](adr/0005-third-party-dependencies.md)). guiunit, nrefactory, nuget-binary,
  sharpsvn-binary, macdoc, mono-tools, mdtestharness and Xamarin.PropertyEditing were dropped, and so were the
  legacy projects that referenced them.
- The build system is `dotnet build` on `main/MonoDevelop.Linux.sln`, which replaces `main/Main.sln`
  ([ADR 0002](adr/0002-linux-solution.md)). The legacy build files are removed: `configure`, the
  `Makefile`s and `Makefile.am`s, `configure.ac`, `autogen.sh`, `profiles/`, `version-checks`,
  `winbuild*.bat`, `main/mdtool.in`/`main/monodevelop.in` launchers (use `dotnet scripts/run.cs`), the macOS
  app bundle files (`main/build/MacOSX`) and `setup/` (Windows installer, Mono libraries).
- Translations: `main/po/MonoDevelop.Translations.csproj` compiles the `.po` catalogs into
  `main/build/locale` (T048). It replaces the autotools `po/Makefile.am`, including its `gettext-update`
  rule that regenerated the catalogs from `Main.sln`.

## Flatpak package

The Flatpak `io.github.viniciusmorgado.MonoDevelop` ([ADR 0022](adr/0022-flatpak.md)) replaces the
distribution packages of MonoDevelop 8.6:

- It ships its own .NET 10 SDK (10.0.401), which builds and runs projects; SDKs installed on the host
  are not used inside the sandbox.
- Only the home directory is visible to the IDE; projects elsewhere (e.g. `/opt`, other disks outside
  `~`) cannot be opened.
- Settings are kept in `~/.var/app/io.github.viniciusmorgado.MonoDevelop/`, separate from a
  MonoDevelop profile in `~/.config/MonoDevelop`.
- File associations: C# files, `.sln` and `.csproj`. The legacy MonoDevelop (`.mds`, `.mdp`),
  SharpDevelop (`.prjx`, `.cmbx`), VB.NET and ASP.NET Web Forms MIME types are no longer registered.
- No external terminal for "Run in external console", no netcoredbg debugger yet (T112).

## Removed or deferred features

| Feature | Status |
|---|---|
| ASP.NET WebForms / MVC 5 projects, Web References (WCF) | removed |
| GTK# visual designer (Stetic) | removed |
| Autotools/Makefile integration | removed |
| Subversion support | removed (Git remains) |
| Mono soft debugger, GDB debugger | replaced by netcoredbg for .NET programs |
| F# | partial: the F# binding runs on FSharp.Compiler.Service 31 (F# 4.7). SDK projects load and build, with highlighting but no type checking in the editor; scripts are fully checked; the F# Interactive pad is hidden ([ADR 0027](adr/0027-fsharp-binding.md), [future work](future-work.md)) |
| VB.NET and IL assembler (`ilasm`) projects | removed: not supported |
| T4 text templating (`TextTemplating`) | deferred: to be ported ([future work](future-work.md)) |
| Building .NET Framework-only projects | not supported (no Mono/.NET Framework on Linux) |
| Portable Class Library (PCL), Xamarin and netstandard1.x projects | not supported: these target frameworks are retired; such projects are not tested, and their test fixtures are quarantined (`legacy-fixture`, T134). Retarget to `netstandard2.0` or `net10.0` |
| `mdtool run-md-tests` | replaced by `dotnet test` |
| mdhost, mdmonitor, performance diagnostics | removed |
| UI automation (AutoTest) tests | removed |
| Add-in browser tool (ExtensionTools) | removed |
| Property editor (Xamarin.PropertyEditing) | removed (Mac-only UI) |
| New Cocoa/WPF text editor (`MonoDevelop.TextEditor`) | removed; the GTK source editor remains |
| Windows installer (`setup/WixSetup`) | removed |
| `./configure`, `scripts/configure.*`, `winbuild*.bat`, autotools `make` targets | removed (use `dotnet scripts/<name>.cs`) |
| `mdtool` tools `run-md-tests`, `update-perf-baseline`, `generate-makefiles`, `gsetup` | removed |
| ASP.NET Core project support (`MonoDevelop.AspNetCore`: launch profiles, development certificate, publish, scaffolding) | deferred: to be ported |
| NuGet Package options of SDK projects (`MonoDevelop.Packaging`) | deferred: to be ported; the Xamarin `.nuproj` packaging projects are removed |
| Deployment / packaging add-in (`Deployment`, `Deployment.Linux`) | removed; use `dotnet publish` |
| Connected Services (`MonoDevelop.ConnectedServices`) | removed |
| NUnit 2/3 in-IDE runners (`MonoDevelop.UnitTesting.NUnit`: Mono runner processes, .NET Framework NUnit project templates, NUnit test class file template) | replaced by VSTest-based test running: NUnit, xUnit and MSTest projects run through their VSTest adapters and the `vstest.console` of the .NET SDK (T101) |
| Test runs with the `vstest.console.exe` of the `Microsoft.TestPlatform` package (run with Mono) | replaced by the `vstest.console.dll` of the .NET SDK the IDE uses |
| Test adapters taken from a test project's package folders (`TestAdaptersPaths`) | not passed; the test host loads the adapters next to the test assembly, as `dotnet test` does |
| Debug Test / Debug All Tests | deferred: the test host is started as a native `dotnet exec` command until the .NET execution command of `MonoDevelop.DotNetCore` (T099) is back |
| Add-in development tooling (`MonoDeveloperExtensions`) | removed |
| MonoDoc documentation browser and help tree | removed (no MonoDoc on .NET 10) |
| WS-Trust (STS) authentication for package feeds | removed (no WCF/WIF on .NET 10) |
| Remote external-process objects (`ProcessService.CreateExternalProcessObject`) | throws `NotSupportedException` |
| Binary instrumentation data files (mdmonitor) | removed; auto-save uses JSON |
| C# Code Style options page (Text Editor > Source Analysis > C# > Code Style) | removed (Roslyn 4+ options API); use `.editorconfig` |
| C# completion extras: delegate/lambda creation, event sender cast, cast, string format items, Apple protocol members (MonoDevelop's own Roslyn completion providers) | removed; Roslyn's C# completion providers remain |
| C# format on return | removed (not in Roslyn 4+); the new line is indented by the editor |
| C# project formatting policies as Roslyn document options (code fixes, generated code) | not applied; explicit formatting uses the policy, Roslyn features use `.editorconfig` |
| Roslyn "install package" code fixes and package symbol search | back with the NuGet add-in (T100); symbol search answers namespace queries with no result |
| NuGet multi-source search relevance ranking (`NuGet.Indexing`, Lucene) | replaced: results of several sources are interleaved in source order, one per package id ([ADR 0020](adr/0020-nuget-client-version.md)) |
| NuGet credential provider plug-ins that are .NET Framework `.exe` files (run with Mono) | not supported; .NET plug-ins run through NuGet's own plug-in support |
| Encrypted package source passwords in `NuGet.Config` | Linux stores them in clear text (NuGet encrypts only on Windows); the Mono key store check is removed |
| C# NUnit test markers in the editor and source locations of tests | deferred to the `MonoDevelop.UnitTesting` port |
| Gettext: Makefile rules for translation projects (Autotools) and `.mo` files as deploy files (Deployment) | removed with those add-ins (ADR 0017) |
| Gettext: GtkSpell spell checking in the catalog editor | removed (it was already disabled upstream; GtkSpell 2 is GTK 2 only) |
| Assembly browser: MonoDoc documentation of the browsed members | removed (no MonoDoc) |
| Assembly browser decompiler | ICSharpCode.Decompiler 11 (was 5): the decompiled C# and IL follow the newer ILSpy output |
| C# test markers in the editor and source locations of tests | back with the UnitTesting add-in (T101), for NUnit, xUnit and MSTest |
| .NET Core 1.x / 2.0 project templates shipped with the IDE (2017 packages) and the .NET Core SDKs bundled with Mono's MSBuild | removed; the templates come from `dotnet new` (T152, below) |
| .NET Core test adapter of the DotNetCore add-in (`DotNetCoreTestPlatformAdapter`) | deferred to the `MonoDevelop.UnitTesting` port (T101) |

## Project and file templates (T152)

The New Project and New File dialogs list the templates of `dotnet new` for the installed .NET SDK, its workloads and
the packages added with `dotnet new install`. The IDE creates projects, solutions and files by running the CLI
([ADR 0026](adr/0026-dotnet-new-templates.md)):

- Only C# and F# are offered (C# by default). Visual Basic variants and VB-only templates are hidden. F# projects load
  with the F# binding, within the limits of [ADR 0027](adr/0027-fsharp-binding.md).
- Windows-only templates are hidden: Windows Forms and WPF (by their tags) and `webconfig` (IIS).
- Categories are the first segment of the template tags: .NET → Common, Web, Test, Solution in New Project; Common,
  Web, Test, Config, MSBuild in New File. The old categories (Multiplatform, Other → .NET/Miscellaneous, .NET Core →
  App/Library/Tests) are gone.
- New solutions are written by `dotnet new sln --format sln` and `dotnet sln add`. "Blank Solution" is the `sln`
  template. The Workspace (`.mdw`) and Generic Project templates are removed without replacement, and so is the
  solution filter (`slnf`), which needs an existing solution.
- Template options other than the language (target framework, top-level statements, authentication…) are not offered;
  the defaults of the template apply. The target framework page of the .NET Core wizard is removed.
- New File: the files come from item templates. Empty Class/Interface/Enum/Struct become the SDK's `class`,
  `interface`, `enum`, `struct` and `record` items, offered for C# projects only (the command Add > New Class creates
  `class`). The Empty C# file, Empty HTML/XML/text file, resource file, `App.config`, application manifest and
  `AssemblyInfo` templates are removed without replacement. Files are written to disk; without a project, the dialog
  asks for a folder instead of opening an unsaved file.
- The Gettext add-in no longer has a "Translation project" template (existing translation projects still load).

Removed from the repository (339 files):

| Where | Removed |
|---|---|
| `MonoDevelop.Ide` | 3 `*.xpt.xml` and 10 `*.xft.xml` (Blank Solution, Workspace, Generic Project; Empty Class/Enum/Interface/Struct, HTML, XML, text, resource file, App.config, AppManifest), their registrations and the static template categories; `MicrosoftTemplateEngine*` (7 files: in-process instantiation, add-in template registrations), `TemplateExtensionNode`, `ItemTemplateExtensionNode`, `ItemTemplate`, `NewItemConfiguration`, `ItemTemplatePackageInstaller`, `ProjectTemplatingProvider` |
| `CSharpBinding` | 6 `*.xpt.xml` (console, library, empty, GTK# 2, portable library, shared project) and 2 `*.xft.xml` (empty file, AssemblyInfo) |
| `MonoDevelop.DotNetCore` | the template registrations of SDKs 1.x–3.1 and 10.0, the template categories and wizard (`MonoDevelop.DotNetCore.Templating`, `GtkDotNetCoreProjectTemplateWizardPageWidget`) and the 84 template images |
| `MonoDevelop.Gettext` | `TranslationProject.xpt.xml` and its 16 images |
| `MonoDevelop.PackageManagement` | `ItemTemplateNuGetPackageInstaller` and its tests |
| `AspNet` (removed, ADR 0017) | 3 `*.xpt.xml`, 34 `*.xft.xml` and their Razor, ASPX, C#, TypeScript, CSS/LESS/SCSS, JSON, T4 and image assets (75 files) |
| `MonoDevelop.AspNetCore` (to be ported) | 13 `*.xft.xml` with their Razor, C# and JSON assets (27 files), the template registrations of SDKs 2.1–3.1 and the 2017 template packages (`DownloadNupkg`) |
| `Deployment`, `Deployment.Linux`, `MonoDevelop.GtkCore`, `MonoDevelop.Packaging`, `MonoDevelop.UnitTesting.NUnit`, `TextTemplating`, `VBNetBinding`, `ILAsmBinding` | their `*.xpt.xml` / `*.xft.xml` templates and template images (64 files) |
| `external/fsharpbinding` (now `src/addins/FSharpBinding`) | 7 `*.xpt.xml`, 6 `*.xft.xml`, `FSharp-templates.xml` (the F# code snippets, restored with [ADR 0027](adr/0027-fsharp-binding.md)) and `templates.targets` |
| Tests | `MicrosoftTemplateEngineTests`, `ProjectTemplateTests`, `ProjectTemplateTest` (IdeUnitTests), the DotNetCore template tests and the `DotNetCoreTemplating` / `FileFormatExclude` fixtures |

Since 2026-09-25 the add-ins excluded from the Linux build are removed from the repository, except `MonoDevelop.AspNetCore`,
`TextTemplating` and `MonoDevelop.Packaging`, which are to be ported, and the F# binding, back in the Linux build
([ADR 0027](adr/0027-fsharp-binding.md)). Their templates stay removed (the templates come from `dotnet new`).
No add-in existed only for templates.

## Add-in authors

- Add-ins must target `net10.0` (or `netstandard2.0`) and load in the default
  `AssemblyLoadContext`; extension points and manifest paths are unchanged
  ([ADR 0006](adr/0006-mono-addins-on-coreclr.md)).
- UI add-ins must use GTK3 (GtkSharp 3.24) or Xwt.
- .NET Remoting and BinaryFormatter-based APIs were removed ([ADR 0009](adr/0009-remove-remoting-binaryformatter.md)).
- NRefactory 5 is gone ([ADR 0019](adr/0019-nrefactory-removal.md)):
  - `CodeGenerator.AddLocalNamespaceImport` takes a `MonoDevelop.Ide.Editor.DocumentLocation`
    instead of an NRefactory `TextLocation`.
  - `CodeGenerator.CreateGenerator (ITextDocument, ICompilation)` is removed. Use
    `CreateGenerator (Document)`.
  - `FoldingUtilities.FlagIfInsideMembers` is removed (it was already marked obsolete).
  - `MemberReference.EntityOrVariable` holds Roslyn `ISymbol`s.
  - `MonoDevelop.CSharp.CSharpEnhancedCodeProvider` (NRefactory CodeDOM) is gone: the C# CodeDOM provider is
    System.CodeDom's `CSharpCodeProvider` (registered by CSharpBinding.Core).
- Roslyn 5 (ADR 0010): `MonoDevelopWorkspaceDiagnosticAnalyzerProviderService` no longer implements a Roslyn interface
  (import it by its own type); the MEF export `IStreamingFindUsagesPresenter` and the TextEditor command mappings of
  the Refactoring and C# add-ins are removed with the Cocoa/WPF editor.
- NuGet (ADR 0020): the NuGet add-in uses the NuGet 7.9 client and the IDE loads NuGet from the .NET SDK (SDK 10.0.4xx
  or later). Add-ins that use NuGet types compile against the `NuGet.*` 7.9 packages with `ExcludeAssets="runtime"`
  (done for every project by `main/msbuild/Linux/Common.targets`) and must not ship NuGet assemblies. In
  `MonoDevelop.PackageManagement`, `IPackageRestoreManager` restores without a logger are extension methods,
  `MonoDevelopPluginFactory` is gone and `BuildIntegratedInstallationContext` lists target framework aliases.
- Templates (T152, [ADR 0026](adr/0026-dotnet-new-templates.md)): the extension points `/MonoDevelop/Ide/Templates`,
  `/MonoDevelop/Ide/ItemTemplates` and `/MonoDevelop/Ide/ItemTemplatePackageInstallers` are removed, and so are
  `MicrosoftTemplateEngineSolutionTemplate`, `ItemTemplate`, `NewItemConfiguration`, `ItemTemplatePackageInstaller`,
  `TemplatingService.GetItemTemplates` and `TemplatingService.ProcessTemplate (ItemTemplate, …)`. The only project
  templating provider is `DotNetNewProjectTemplatingProvider`. `/MonoDevelop/Ide/ProjectTemplates` and
  `/MonoDevelop/Ide/FileTemplates` still exist, but the dialogs no longer list what is registered there. To add
  templates, install a template package with `dotnet new install`.
- `Mono.Addins.Gui` (GTK2) is replaced by `Mono.Addins.GuiGtk3`, which has the same classes in the
  `Mono.Addins.GuiGtk3` namespace.
- The VS editor API assemblies keep their names. WPF-only members are not available: presenter
  styles, `ITextFormattable`, `WpfHelper`, and the TextUIWpf contracts. Clipboard access in
  `EditorOperations` goes through `Microsoft.VisualStudio.Text.Operations.EditorClipboard`
  (see `main/vendor/vs-editor-api/UPSTREAM.md`).
