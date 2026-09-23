# 0017 — Projects excluded from the Linux build

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The maintainer set a Linux-only focus; macOS/Windows may break. Some features depend on technologies
with no .NET 10/Linux equivalent.

## Decision Outcome

Not part of `main/MonoDevelop.Linux.sln` (code stays in the repository until removed deliberately):

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
| Editor tests of excluded editor | `core/MonoDevelop.TextEditor.Tests` | tests the Cocoa/WPF editor |
| Deferred (post-MVP, may return by ADR) | `Deployment` (+ `Deployment.Linux`), `MonoDevelop.AspNetCore`, `MonoDevelop.Packaging`, `MonoDeveloperExtensions` | not needed for the C# desktop/console MVP |

Deferred items may return through a new ADR. All exclusions are listed for users in
`docs/BREAKING-CHANGES.md`.

### Consequences

- Good: bounded scope; the Linux build has no dead weight.
- Bad: feature loss for users of these add-ins.
