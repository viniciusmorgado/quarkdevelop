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
- The build system is `dotnet build` on `main/MonoDevelop.Linux.sln`; `./configure && make`,
  `winbuild.bat` and profiles are obsolete.

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
| `./configure`, `scripts/configure.*`, `winbuild*.bat`, autotools `make` targets | obsolete (use `scripts/*.sh`) |
| `mdtool` tools `run-md-tests`, `update-perf-baseline`, `generate-makefiles`, `gsetup` | removed |
| ASP.NET Core project support (`MonoDevelop.AspNetCore`) | deferred |
| NuGet package authoring projects (`MonoDevelop.Packaging`) | deferred |
| Deployment / packaging add-in (`Deployment`, `Deployment.Linux`) | deferred |
| Connected Services (`MonoDevelop.ConnectedServices`) | removed |
| NUnit 2/3 in-IDE runners (`MonoDevelop.UnitTesting.NUnit`) | replaced by VSTest-based test running |
| Add-in development tooling (`MonoDeveloperExtensions`) | deferred |
| MonoDoc documentation browser and help tree | removed (no MonoDoc on .NET 10) |
| WS-Trust (STS) authentication for package feeds | removed (no WCF/WIF on .NET 10) |
| Remote external-process objects (`ProcessService.CreateExternalProcessObject`) | throws `NotSupportedException` |
| Binary instrumentation data files (mdmonitor) | removed; auto-save uses JSON |
| C# Code Style options page (Text Editor > Source Analysis > C# > Code Style) | removed (Roslyn 4+ options API); use `.editorconfig` |
| C# completion extras: delegate/lambda creation, event sender cast, cast, string format items, Apple protocol members (MonoDevelop's own Roslyn completion providers) | removed; Roslyn's C# completion providers remain |
| C# format on return | removed (not in Roslyn 4+); the new line is indented by the editor |
| C# project formatting policies as Roslyn document options (code fixes, generated code) | not applied; explicit formatting uses the policy, Roslyn features use `.editorconfig` |
| Roslyn "install package" code fixes and package symbol search | deferred to the NuGet add-in port (`MonoDevelop.PackageManagement`) |
| C# NUnit test markers in the editor and source locations of tests | deferred to the `MonoDevelop.UnitTesting` port |

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
- `Mono.Addins.Gui` (GTK2) is replaced by `Mono.Addins.GuiGtk3`, which has the same classes in the
  `Mono.Addins.GuiGtk3` namespace.
- The VS editor API assemblies keep their names. WPF-only members are not available: presenter
  styles, `ITextFormattable`, `WpfHelper`, and the TextUIWpf contracts. Clipboard access in
  `EditorOperations` goes through `Microsoft.VisualStudio.Text.Operations.EditorClipboard`
  (see `main/vendor/vs-editor-api/UPSTREAM.md`).
