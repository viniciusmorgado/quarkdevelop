# Linux setup guide

MonoDevelop runs on .NET 10 LTS and GTK3 on Linux. It is built, tested and run on the host. The developer scripts are
C# file-based apps (`dotnet scripts/<name>.cs`, [ADR 0028](../adr/0028-csharp-developer-scripts.md)), so the .NET SDK
is the only toolchain they need.

## 1. Requirements

| Needed for | Packages (Debian/Ubuntu names) |
|---|---|
| Building, running and debugging | the .NET 10 SDK, 10.0.400 or later (the IDE loads the SDK's NuGet, [ADR 0020](../adr/0020-nuget-client-version.md); `global.json` accepts any 10.0 SDK, which builds but runs without working package management), `git`, `gettext` (the build compiles the translations with `msgfmt`), GTK 3 with its SVG loader (`libgtk-3-0 librsvg2-common`; a desktop has them) |
| The headless tests and smoke tests | `xvfb xauth` (`xvfb-run`) |
| The Wayland smoke test | `weston` |
| The screenshot checks of the smoke tests | `imagemagick` |
| The Flatpak bundle and its test | `flatpak flatpak-builder desktop-file-utils appstream dbus` |

Mono is not needed; an installed Mono is never used.

## 2. Get the sources and check the toolchain

```bash
git clone <this repository> monodevelop
cd monodevelop
dotnet scripts/setup.cs
```

`setup.cs` checks the .NET SDK and GTK, restores the local dotnet tools (`.config/dotnet-tools.json`) and installs
the .NET Framework reference assemblies that the legacy test fixtures build against
(`~/.cache/monodevelop/netfx-refasm`). It also installs pinned, checksum-verified releases of netcoredbg and
actionlint in `~/.cache/monodevelop/tools`, linked from `~/.local/bin` (x64 builds; on another architecture, install
them yourself). Finally, it lists the system packages from section 1 that are missing. It is idempotent: run it again
after a pull. Add `~/.local/bin` to `PATH` so that the IDE finds netcoredbg; the scripts look there anyway.

## 3. Build, test, run

`dotnet scripts/<name>.cs` builds the script on its first run (a few seconds; later runs use the cached build) and
works from any directory. `dotnet` reads options such as `-c` itself, so arguments for the script go after `--`:
`dotnet scripts/build.cs -- -c Release`.

| Task | Command |
|---|---|
| Restore | `dotnet scripts/restore.cs` (`-- --locked`: locked mode, as in CI) |
| Build | `dotnet scripts/build.cs` (`-- -c Release`; `--check` also verifies the formatting of the C# files this fork added) |
| Format the C# files this fork added | `dotnet scripts/format.cs` |
| Test + coverage | `dotnet scripts/test.cs` → `out/tests`, `out/coverage/Summary.txt` (`-- --parallel`, `--no-build`, `--all`) |
| Lint the scripts and workflows | `dotnet scripts/lint.cs` |
| Duplicate-assembly check | `dotnet scripts/check-assemblies.cs` |
| Vulnerable packages | `dotnet scripts/audit.cs` |
| Warning baseline of a project | `dotnet scripts/warnings-baseline.cs -- <project>` ([ADR 0018](../adr/0018-warning-policy.md)) |
| The whole CI gate | `dotnet scripts/ci.cs` → `out/ci/summary.txt` |
| Run the IDE | `dotnet scripts/run.cs` (append a solution path to open it) |
| Run the IDE headless | `dotnet scripts/run.cs -- --headless` (Xvfb; add `--smoke-test` for the smoke test) |
| Debug | `dotnet scripts/debug.cs -- ide\|mdtool` (netcoredbg) |

The solution for Linux is `main/MonoDevelop.Linux.sln`; build output goes to `main/build/bin` and
`main/build/AddIns`. `dotnet main/build/bin/mdtool.dll build <solution or project>` builds from the command line.

When `xvfb-run` is installed, the GTK tests (as in CI) and the GUI smoke tests run under Xvfb, also on a desktop: no
test window opens there. The IDE started by `run.cs` (without `--headless`) or `debug.cs` opens on your desktop, on
Wayland or X11.

## 4. Debugging

MonoDevelop debugs .NET programs, and is debugged itself, with
[netcoredbg](https://github.com/Samsung/netcoredbg) (Samsung, MIT; [ADR 0016](../adr/0016-netcoredbg-debugging.md)).
`dotnet scripts/setup.cs` installs a pinned release in `~/.local/bin`. Microsoft's vsdbg may only be used in
Microsoft products, so VS Code OSS / VSCodium use netcoredbg as well.

**Inside the IDE.** *Run > Start Debugging* on a .NET project uses the `.NET Debugger (netcoredbg)` engine.
It finds netcoredbg on `PATH`, or at the path in the `MonoDevelop.Debugger.NetCoreDbg.Path` property.

**Command line.** `debug.cs` starts the IDE or mdtool under netcoredbg's command-line interface
(`break`, `run`, `bt`, `print`, `next`; `help` lists the commands):

```bash
dotnet scripts/build.cs
dotnet scripts/debug.cs -- mdtool build main/tests/linux-smoke/Smoke.sln
dotnet scripts/debug.cs -- ide
```

**VS Code OSS / VSCodium.** A `coreclr` launch configuration of `main/build/bin/MonoDevelop.dll` (or `mdtool.dll`,
runtime `dotnet`) can start `~/.local/bin/netcoredbg --interpreter=vscode` through `pipeTransport`. This needs the
C# extension or one of its open-source forks that supports `pipeTransport`. Build first.

**Rider.** Rider uses its own .NET debugger. Add a *.NET Executable* run configuration (executable
`main/build/bin/MonoDevelop.dll`, runtime `dotnet`), or attach to a running `dotnet MonoDevelop.dll`
process. To use netcoredbg from Rider, run `dotnet scripts/debug.cs` in its terminal.

## 5. Flatpak

The Flatpak bundle ([ADR 0022](../adr/0022-flatpak.md)) is built with flatpak-builder from the Flatpak files
(manifest, desktop entry, AppStream metadata, MIME types, launcher). The "Flatpak files" step of
`.github/workflows/release.yml` writes them to `out/flatpak-files`; to build the bundle locally, run that step's
script from the checkout root first. The Flathub runtimes (GNOME 51 and the .NET 10 SDK extension, about 3 GB on
first use), the build cache and the test installation live in `~/.cache/monodevelop/flatpak` (`MD_FLATPAK_STORE`
overrides it). Your own flatpak installation is not touched.

```bash
dotnet scripts/package-flatpak.cs   # → out/monodevelop.flatpak (+ .sha256, SBOM)
dotnet scripts/test-flatpak.cs      # install test in a clean installation
```

`package-flatpak.cs` builds the IDE itself (Release, `ContinuousIntegrationBuild=true` so that no file carries the path
of your checkout) when `main/build/bin` is missing; `-- --rebuild-ide` forces it, and is needed after an ordinary
`dotnet scripts/build.cs`. Outputs: `out/monodevelop.flatpak`, `out/monodevelop.flatpak.sha256`,
`out/monodevelop.cdx.json` (CycloneDX SBOM), logs in `out/flatpak/`. `test-flatpak.cs` installs the bundle into a
fresh installation, with a D-Bus session and a home directory of its own, so the data of an installed IDE is never
used. It checks the desktop entry, icons and MIME types, and runs `--version`, `mdtool build` of
`main/tests/linux-smoke/Hello` and the IDE smoke test under Xvfb (`out/flatpak-test/`).

On a desktop with Flatpak, install and run the bundle (the GNOME 51 runtime is fetched from Flathub):

```bash
flatpak install --user out/monodevelop.flatpak
flatpak run io.github.viniciusmorgado.MonoDevelop            # or from the application menu
flatpak run --command=mdtool io.github.viniciusmorgado.MonoDevelop build path/to/Project.csproj
flatpak run --command=dotnet io.github.viniciusmorgado.MonoDevelop --info
```

The bundle carries its own .NET 10 SDK (10.0.401), which runs the IDE and builds your projects. It
can read and write your home directory (projects outside it are not visible), uses the network for
NuGet, and keeps its settings in `~/.var/app/io.github.viniciusmorgado.MonoDevelop/`. Workloads
(`dotnet workload install`) and global tools go to `~/.dotnet`.

## 6. Editors

Any editor with .NET support works on the checkout: open `main/MonoDevelop.Linux.sln` in Rider, in VS Code OSS /
VSCodium with a C# extension, or in QuarkDevelop itself.

See also [troubleshooting.md](troubleshooting.md).
