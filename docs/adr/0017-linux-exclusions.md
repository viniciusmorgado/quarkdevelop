# 0017 — Projects excluded from the Linux build

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The maintainer set a Linux-only focus; macOS/Windows may break. Some features depend on technologies
with no .NET 10/Linux equivalent.

## Considered Options

1. Port every project of `Main.sln` before the first Linux release.
2. Exclude the projects that serve only macOS or Windows, or depend on technologies with no .NET 10/Linux equivalent,
   list them for users, and let deferred ones return through an ADR.
3. Delete the excluded code from the repository.

## Decision Outcome

Chosen: option 2 (option 1 would block the release on dead technologies; option 3 loses code that a later ADR may
bring back). Not part of `main/MonoDevelop.Linux.sln` (code stays in the repository until removed deliberately):

| Group | Projects / directories | Reason |
|---|---|---|
| macOS | `addins/MacPlatform`, `MonoDevelop.TextEditor.Cocoa`, `AspNetCore.DevCertInstaller`, Xwt.XamMac, Xwt.Gtk.Mac, Xamarin.PropertyEditing(.Mac), macdoc, `tests/MacPlatform.Tests` | Cocoa/Xamarin.Mac |
| Windows | `addins/WindowsPlatform` (+ WindowsAPICodePack), `MonoDevelop.TextEditor.Wpf`, `MonoDevelop.Debugger.Win32` (+ CorApi, Mono.Debugging.Win32), Xwt.WPF, Xwt.Gtk.Windows, `Subversion.Win32`, `tests/WindowsPlatform.Tests`, `setup/WixSetup` | Win32/WPF |
| Legacy web | `addins/AspNet` (WebForms/MVC5), `MonoDevelop.WebReferences` | System.Web, WCF |
| Legacy tooling | `MonoDevelop.GtkCore` (Stetic designer), `MonoDevelop.Autotools`, `tools/mdhost`, `tools/mdmonitor`, `tools/ExtensionTools`, `PerformanceDiagnostics`, `tests/TestRunner` | GTK2-only designer, autotools, Remoting, Mono profiler |
| Mono runtime | `MonoDevelop.Debugger.Soft` (+ Mono.Debugger.Soft), `MonoDevelop.Debugger.Gdb` | Mono-only / unmaintained |
| Deferred languages | `external/fsharpbinding`, `VBNetBinding`, `ILAsmBinding`, `TextTemplating` | C# first |
| VCS | `VersionControl.Subversion*` | SharpSvn/libsvn P/Invoke |
| UI automation | `tests/UserInterfaceTests`, `tests/ui/*` | AutoTest over Remoting |
| Submodules | mono-tools, mdtestharness, nuget-binary, sharpsvn-binary | unused / replaced |
| Legacy build tooling | `msbuild/MDBuildTasks` (DownloadNupkg for the legacy build), `tools/AssemblyInfoWriter` | replaced by SDK restore / `GenerateAssemblyInfo` |
| Superseded test runners | `MonoDevelop.UnitTesting.NUnit` (+ `NUnitRunner`, `NUnit3Runner`, Remoting-based) | IDE test running goes through VSTest (`MonoDevelop.UnitTesting` + DotNetCore) |
| Mac/VS-specific services | `MonoDevelop.ConnectedServices` | VS for Mac Azure services |
| Editor tests of excluded editor | ~~`core/MonoDevelop.TextEditor.Tests`~~: back in the Linux solution since T085/T107 as the converted Mono.TextEditor suite (`docs/evidence/M5/README.md`) | — |
| Deferred (post-MVP, may return by ADR) | `Deployment` (+ `Deployment.Linux`), `MonoDevelop.AspNetCore`, `MonoDevelop.Packaging`, `MonoDeveloperExtensions` | not needed for the C# desktop/console MVP |

Deferred items may return through a new ADR. All exclusions are listed for users in
`docs/BREAKING-CHANGES.md`.

### Consequences

- Good: bounded scope; the Linux build has no dead weight.
- Bad: feature loss for users of these add-ins.

## Amendment 2026-09-25: excluded code removed

Decided by the maintainer after `v0.1.0-linux`. The scope rule is now parity with the .NET SDK on Linux: the IDE
supports every project that runs on Linux with the dotnet CLI or Rider. What goes is code for the .NET Framework,
Windows, macOS or Mono only, and VB.NET and Subversion, which are too little used to be worth maintaining.

- Removed from the repository: `MacPlatform`, `WindowsPlatform` (+ WindowsAPICodePack), `MonoDevelop.Debugger.Win32`,
  `MonoDevelop.Debugger.Soft`, `MonoDevelop.Debugger.Gdb`, `MonoDevelop.Debugger.PerfTests`, `AspNet`,
  `CSharpBinding/AspNet`, `CSharpBinding/Autotools`, `MonoDevelop.Autotools`, `Deployment` (+ `Deployment.Linux`),
  `ILAsmBinding`, `VBNetBinding`, `MonoDevelop.GtkCore`, `MonoDevelop.TextEditor` (Cocoa/WPF),
  `MonoDevelop.UnitTesting.NUnit` (+ runners), `MonoDevelop.WebReferences`, `MonoDevelop.ConnectedServices`,
  `MonoDeveloperExtensions`, `PerformanceDiagnostics`, `VersionControl.Subversion*` (+ Win32), `tools/mdhost`,
  `tools/mdmonitor`, `tools/ExtensionTools`, `tests/MacPlatform.Tests`, `tests/WindowsPlatform.Tests`, `tests/ui`,
  `tests/UserInterfaceTests`, `tests/performance`, `tests/StressTest`, `tests/TestRunner` and the `main/tests/*.dll.filter`
  files of that runner.
- Kept outside the solution, to be ported (`docs/future-work.md`): `MonoDevelop.AspNetCore`, `TextTemplating`,
  `MonoDevelop.Packaging`, `MonoDevelop.DesignerSupport.Tests` and the F# binding (`main/external/fsharpbinding`).
- From `AspNet`, the web MIME types (HTML, JavaScript, TypeScript, LESS/SASS/SCSS, Razor) moved into `MonoDevelop.Ide`
  and `xhtml1-strict.xsd` into the Xml add-in, whose tests use it. Its HTML editor is to be ported into the Xml add-in.
