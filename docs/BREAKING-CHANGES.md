# Breaking changes — MonoDevelop on Linux / .NET 10

This fork of MonoDevelop 8.6 targets Linux only, runs on .NET 10 LTS (no Mono) and uses GTK3.
Compared with MonoDevelop 8.6:

## Platforms and runtime

- macOS and Windows builds are no longer supported; their code is not part of the Linux build
  ([ADR 0017](adr/0017-linux-exclusions.md)).
- The IDE runs on .NET 10 (CoreCLR), not Mono. Mono-specific options (`MONO_OPTIONS`,
  `mdtool build -r:<mono prefix>`) are ignored.
- The UI toolkit is GTK 3.24 (was GTK 2.24); GTK2 themes (`gtkrc`) no longer apply
  ([ADR 0011](adr/0011-gtk3-port-strategy.md)).
- The build system is `dotnet build` on `main/MonoDevelop.Linux.sln`, which replaces `main/Main.sln`
  ([ADR 0002](adr/0002-linux-solution.md)). The legacy build files are removed: `configure`, the
  `Makefile`s and `Makefile.am`s, `configure.ac`, `autogen.sh`, `profiles/`, `version-checks`,
  `winbuild*.bat`, `main/mdtool.in`/`main/monodevelop.in` launchers (use `scripts/run.sh`), the macOS
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
| F#, VB.NET, IL assembler, T4 text templating | deferred |
| Building .NET Framework-only projects | not supported (no Mono/.NET Framework on Linux) |
| `mdtool run-md-tests` | replaced by `dotnet test` |
| mdhost, mdmonitor, performance diagnostics | removed |
| UI automation (AutoTest) tests | removed |
| Add-in browser tool (ExtensionTools) | removed |
| Property editor (Xamarin.PropertyEditing) | removed (Mac-only UI) |
| New Cocoa/WPF text editor (`MonoDevelop.TextEditor`) | removed; the GTK source editor remains |
| Windows installer (`setup/WixSetup`) | removed |
| `./configure`, `scripts/configure.*`, `winbuild*.bat`, autotools `make` targets | removed (use `scripts/*.sh`) |
| `mdtool` tools `run-md-tests`, `update-perf-baseline`, `generate-makefiles`, `gsetup` | removed |
| ASP.NET Core project support (`MonoDevelop.AspNetCore`) | deferred |
| NuGet package authoring projects (`MonoDevelop.Packaging`) | deferred |
| Deployment / packaging add-in (`Deployment`, `Deployment.Linux`) | deferred |
| Connected Services (`MonoDevelop.ConnectedServices`) | removed |
| NUnit 2/3 in-IDE runners (`MonoDevelop.UnitTesting.NUnit`: Mono runner processes, .NET Framework NUnit project templates, NUnit test class file template) | replaced by VSTest-based test running: NUnit, xUnit and MSTest projects run through their VSTest adapters and the `vstest.console` of the .NET SDK (T101) |
| Test runs with the `vstest.console.exe` of the `Microsoft.TestPlatform` package (run with Mono) | replaced by the `vstest.console.dll` of the .NET SDK the IDE uses |
| Test adapters taken from a test project's package folders (`TestAdaptersPaths`) | not passed; the test host loads the adapters next to the test assembly, as `dotnet test` does |
| Debug Test / Debug All Tests | deferred: the test host is started as a native `dotnet exec` command until the .NET execution command of `MonoDevelop.DotNetCore` (T099) is back |
| Add-in development tooling (`MonoDeveloperExtensions`) | deferred |
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
| .NET Core 1.x / 2.0 project templates shipped with the IDE (2017 packages) and the .NET Core SDKs bundled with Mono's MSBuild | removed; the `.NET` templates come from the installed SDK (.NET 10 console, library and test templates; older SDKs keep their 2.1–3.1 entries) |
| .NET Core test adapter of the DotNetCore add-in (`DotNetCoreTestPlatformAdapter`) | deferred to the `MonoDevelop.UnitTesting` port (T101) |

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
- `Mono.Addins.Gui` (GTK2) is replaced by `Mono.Addins.GuiGtk3`, which has the same classes in the
  `Mono.Addins.GuiGtk3` namespace.
- The VS editor API assemblies keep their names. WPF-only members are not available: presenter
  styles, `ITextFormattable`, `WpfHelper`, and the TextUIWpf contracts. Clipboard access in
  `EditorOperations` goes through `Microsoft.VisualStudio.Text.Operations.EditorClipboard`
  (see `main/vendor/vs-editor-api/UPSTREAM.md`).
