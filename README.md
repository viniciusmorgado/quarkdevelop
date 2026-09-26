<p align="center">
  <img src="assets/quark_banner.png" alt="QuarkDevelop" width="760">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="license: MIT"></a>
</p>

---

**An IDE for C# and .NET on Linux, running on .NET 10 LTS and GTK 3**

QuarkDevelop is an integrated development environment for C# and .NET. It is a Linux-only continuation of
MonoDevelop 8.6: it runs on **.NET 10 LTS** (CoreCLR, no Mono) with a **GTK 3** interface on X11 and Wayland, and it
builds and debugs SDK-style .NET projects with the installed .NET SDK and netcoredbg. macOS and Windows are not
supported.

- Removed and changed features: [`docs/BREAKING-CHANGES.md`](docs/BREAKING-CHANGES.md).
- Decisions: [`docs/adr/`](docs/adr/); what is left to do: [`docs/future-work.md`](docs/future-work.md).

Build requirements: the **.NET 10 SDK** (10.0.400 or later), **git**, gettext and GTK 3. The developer scripts are C# files that the .NET
SDK runs directly:

```bash
dotnet scripts/setup.cs
dotnet scripts/build.cs
dotnet scripts/test.cs
```

Full guide: [`docs/linux/setup.md`](docs/linux/setup.md). The Flatpak bundle is built with
`dotnet scripts/package-flatpak.cs`.

Directory organization
----------------------

 * `main`: the QuarkDevelop assemblies and add-ins. `main/MonoDevelop.Linux.sln` is the solution;
   `main/vendor` holds the forked dependencies (Xwt, vs-editor-api, …).
 * `scripts`: build, test, run and CI scripts, in C# (`dotnet scripts/<name>.cs`).
 * `docs`, `specs`: documentation ([architecture](docs/architecture.md)), architecture decisions, the
   migration plan and [future work](docs/future-work.md).

Building, running and debugging
-------------------------------

```bash
dotnet scripts/build.cs                 # main/build/bin and main/build/AddIns
dotnet scripts/test.cs                  # unit tests and coverage
dotnet scripts/run.cs                   # the IDE
dotnet scripts/debug.cs -- ide          # the IDE under netcoredbg
```

Arguments for a script go after `--` (`dotnet scripts/build.cs -- -c Release`).
`dotnet main/build/bin/mdtool.dll build <solution or project>` builds from the command line.
Problems and their fixes: [`docs/linux/troubleshooting.md`](docs/linux/troubleshooting.md).

History and license
-------------------

QuarkDevelop continues MonoDevelop, which the Mono project, Xamarin and Microsoft developed until 2020, when the
upstream repository (`mono/monodevelop`) was archived. This repository starts from MonoDevelop's last version, 8.6, and
keeps the upstream history. The code keeps the MonoDevelop names for now: assemblies, namespaces, the solution
`main/MonoDevelop.Linux.sln` and the Flatpak id.

QuarkDevelop is licensed under the [MIT license](LICENSE), like almost all of the MonoDevelop sources. Each source file
carries its own license header, and a few keep another license, whose text is in [`LICENSES/`](LICENSES/):
[LGPL 2.1](LICENSES/LGPL-2.1.txt) for some workbench shell files, [GPL 2.0](LICENSES/GPL-2.0.txt) for two XML schemas of
the Xml add-in (`appconfig.xsd`, `manifest.xsd`) and [Apache 2.0](LICENSES/Apache-2.0.txt) for code taken from NuGet and
other .NET projects. Each of these files points to its license text. Vendored components record their licenses in
`main/vendor/*/UPSTREAM.md`.
Authors are listed in [`docs/monodevelop/AUTHORS`](docs/monodevelop/AUTHORS).
