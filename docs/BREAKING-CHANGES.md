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

## Add-in authors

- Add-ins must target `net10.0` (or `netstandard2.0`) and load in the default
  `AssemblyLoadContext`; extension points and manifest paths are unchanged
  ([ADR 0006](adr/0006-mono-addins-on-coreclr.md)).
- UI add-ins must use GTK3 (GtkSharp 3.24) or Xwt.
- .NET Remoting and BinaryFormatter-based APIs were removed ([ADR 0009](adr/0009-remove-remoting-binaryformatter.md)).
