<p align="center">
  <img src="Assets/quark_banner.png" alt="QuarkDevelop" width="760">
</p>

<p align="center">
  <a href="#history-and-license"><img src="https://img.shields.io/badge/license-MIT%20%2F%20LGPL--2.1-blue" alt="license: MIT / LGPL-2.1"></a>
</p>

---

**An IDE for C# and .NET on Linux, running on .NET 10 LTS and GTK 3**

QuarkDevelop is an integrated development environment for C# and .NET. It is a Linux-only continuation of
MonoDevelop 8.6: it runs on **.NET 10 LTS** (CoreCLR, no Mono) with a **GTK 3** interface on X11 and Wayland, and it
builds and debugs SDK-style .NET projects with the installed .NET SDK and netcoredbg. macOS and Windows are not
supported.

- What the first release contains, and its known issues: [`docs/release-notes/v0.1.0-linux.md`](docs/release-notes/v0.1.0-linux.md).
- Removed and changed features: [`docs/BREAKING-CHANGES.md`](docs/BREAKING-CHANGES.md).
- Project rules: [`docs/constitution.md`](docs/constitution.md); decisions: [`docs/adr/`](docs/adr/); the migration
  specification, plan and tasks: [`specs/001-linux-dotnet10-migration/`](specs/001-linux-dotnet10-migration/).

Build requirements on the host: **podman** and **git** only. Everything runs in the dev container:

```bash
./scripts/pm ./scripts/setup.sh
./scripts/pm ./scripts/build.sh
./scripts/pm ./scripts/test.sh
```

Full guide: [`docs/linux/setup.md`](docs/linux/setup.md). The Flatpak bundle is built with
`PM_PROFILE=flatpak ./scripts/pm ./scripts/package-flatpak.sh`.

Directory organization
----------------------

 * `main`: the QuarkDevelop assemblies and add-ins. `main/MonoDevelop.Linux.sln` is the solution;
   `main/vendor` holds the forked dependencies (Xwt, vs-editor-api, …).
 * `scripts`: build, test, run and CI scripts, run inside the dev container with `./scripts/pm`.
 * `docs`, `specs`: documentation ([architecture](docs/architecture.md)), architecture decisions, the
   migration plan and [future work](docs/future-work.md).

Building, running and debugging
-------------------------------

```bash
./scripts/pm ./scripts/build.sh                 # main/build/bin and main/build/AddIns
./scripts/pm ./scripts/test.sh                  # unit tests and coverage
./scripts/pm ./scripts/run.sh                   # the IDE (see setup.md to show it on your desktop)
./scripts/pm ./scripts/debug.sh ide             # the IDE under netcoredbg
```

`./scripts/pm dotnet main/build/bin/mdtool.dll build <solution or project>` builds from the command line.
Problems and their fixes: [`docs/linux/troubleshooting.md`](docs/linux/troubleshooting.md).

History and license
-------------------

QuarkDevelop continues MonoDevelop, which the Mono project, Xamarin and Microsoft developed until 2020, when the
upstream repository (`mono/monodevelop`) was archived. This repository starts from MonoDevelop's last version, 8.6, and
keeps the upstream history. The code keeps the MonoDevelop names for now: assemblies, namespaces, the solution
`main/MonoDevelop.Linux.sln` and the Flatpak id.

Source files carry their own license headers (MIT X11 for the MonoDevelop sources). `main/COPYING` is the LGPL 2.1
text shipped with the upstream sources, and vendored components record theirs in `main/vendor/*/UPSTREAM.md`. Authors
are listed in `main/AUTHORS`.
