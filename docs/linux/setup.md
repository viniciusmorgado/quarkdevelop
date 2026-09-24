# Linux setup guide

MonoDevelop is being migrated to .NET 10 LTS and GTK3 on Linux
(see `specs/001-linux-dotnet10-migration/`). All commands run inside a development container; the
host needs only **podman** (rootless) and **git**.

## 1. Get the sources

```bash
git clone <this repository> monodevelop
cd monodevelop
```

Submodules are only needed while legacy code is still being migrated:

```bash
./scripts/pm git submodule update --init --jobs 8
```

## 2. Enter the toolchain

`./scripts/pm <command>` runs `<command>` in the dev container. The first call builds the image from
`Containerfile` (≈ 3 minutes); later calls reuse it until `Containerfile` changes.

```bash
./scripts/pm dotnet --info        # .NET SDK 10.0.x, no Mono
./scripts/pm ./scripts/setup.sh   # verifies the toolchain, restores local tools
```

The container runs as your user (`--userns=keep-id`), so files it creates belong to you. Caches
(NuGet, uv) live in the podman volume `md-home`.

## 3. Build, test, run

| Task | Command |
|---|---|
| Restore | `./scripts/pm ./scripts/restore.sh` |
| Build | `./scripts/pm ./scripts/build.sh` (`-c Release`, `--check` for formatting) |
| Test + coverage | `./scripts/pm ./scripts/test.sh` → `out/tests`, `out/coverage/Summary.txt` |
| Lint scripts/workflows | `./scripts/pm ./scripts/lint.sh` |
| Run the IDE headless | `./scripts/pm ./scripts/run.sh --headless` (available from milestone M5) |
| Debug | `./scripts/pm ./scripts/debug.sh ide\|mdtool` (netcoredbg) |
| Commit (fixed identity) | `./scripts/pm ./scripts/git-commit -m "…"` |

The solution for Linux is `main/MonoDevelop.Linux.sln`; build output goes to `main/build/bin` and
`main/build/AddIns`.

## 4. Showing the IDE on your desktop

Forward your display into the container:

```bash
# X11
PM_PODMAN_ARGS="-e DISPLAY -v /tmp/.X11-unix:/tmp/.X11-unix" ./scripts/pm ./scripts/run.sh
# Wayland
PM_PODMAN_ARGS="-e WAYLAND_DISPLAY -e XDG_RUNTIME_DIR=/tmp/xdg -v $XDG_RUNTIME_DIR/$WAYLAND_DISPLAY:/tmp/xdg/$WAYLAND_DISPLAY" ./scripts/pm ./scripts/run.sh
```

Wayland forwarding was checked on a GNOME Wayland desktop (2026-09-23). Rootless podman usually cannot
connect to an X server without extra authorization, so prefer Wayland. The commands above use paths
relative to the checkout; from any other directory, give `scripts/pm` and `scripts/run.sh` as absolute
paths (the repository is mounted at the same path inside the container). Append a solution path to
open it at start-up.

## 5. Debugging

MonoDevelop debugs .NET programs, and is debugged itself, with
[netcoredbg](https://github.com/Samsung/netcoredbg) (Samsung, MIT; [ADR 0016](../adr/0016-netcoredbg-debugging.md)).
The dev container installs a pinned release on `PATH`. Microsoft's vsdbg may only be used in Microsoft
products, so VS Code OSS / VSCodium use netcoredbg as well.

**Inside the IDE.** *Run > Start Debugging* on a .NET project uses the `.NET Debugger (netcoredbg)` engine.
It finds netcoredbg on `PATH`, or at the path in the `MonoDevelop.Debugger.NetCoreDbg.Path` property.

**Command line.** `scripts/debug.sh` starts the IDE or mdtool under netcoredbg's command-line interface
(`break`, `run`, `bt`, `print`, `next`; `help` lists the commands):

```bash
./scripts/pm ./scripts/build.sh
./scripts/pm ./scripts/debug.sh mdtool build main/tests/linux-smoke/Smoke.sln
PM_PODMAN_ARGS="…display forwarding, see section 4…" ./scripts/pm ./scripts/debug.sh ide
```

**VS Code OSS / VSCodium.** `.vscode/launch.json` has `coreclr` configurations that start netcoredbg
through `pipeTransport`. This needs the C# extension or one of its open-source forks that supports
`pipeTransport`.

| Configuration | Use |
|---|---|
| `IDE (dev container)`, `mdtool build (dev container)` | launch `main/build/bin/MonoDevelop.dll` / `mdtool.dll` when VS Code is attached to `.devcontainer/` |
| `Attach (dev container)` | attach to a running .NET process in the container (process picker) |
| `IDE (host, scripts/pm)`, `mdtool build (host, scripts/pm)` | VS Code on the host: `scripts/pm` runs netcoredbg and the program in the dev container. The repository has the same path there, so breakpoints bind as is. The IDE window is shown through Wayland. |

Build first (`./scripts/pm ./scripts/build.sh`); the configurations use `${workspaceFolder}` paths.

**Rider.** Rider uses its own .NET debugger. Add a *.NET Executable* run configuration (executable
`main/build/bin/MonoDevelop.dll`, runtime `dotnet`), or attach to a running `dotnet MonoDevelop.dll`
process. To use netcoredbg from Rider, run `scripts/debug.sh` in its terminal.

## 6. Flatpak

The Flatpak bundle is built in a second container profile, `PM_PROFILE=flatpak`, which adds flatpak,
flatpak-builder and the AppStream/desktop validators to the same .NET SDK image
([ADR 0022](../adr/0022-flatpak.md)). The Flathub runtimes (GNOME 51, .NET 10 SDK extension, about
3 GB on first use) and the build cache live in the podman volume `md-flatpak`; your own flatpak
installation is not touched.

```bash
PM_PROFILE=flatpak ./scripts/pm ./scripts/package-flatpak.sh   # → out/monodevelop.flatpak (+ .sha256, SBOM)
PM_PROFILE=flatpak ./scripts/pm ./scripts/test-flatpak.sh      # install test in a clean installation
```

`package-flatpak.sh` builds the IDE itself (Release, `ContinuousIntegrationBuild=true` so that no
file carries the path of your checkout) when `main/build/bin` is missing; `--rebuild-ide` forces it,
and is needed after an ordinary `./scripts/build.sh`. Outputs: `out/monodevelop.flatpak`, `out/monodevelop.flatpak.sha256`,
`out/monodevelop.cdx.json` (CycloneDX SBOM), logs in `out/flatpak/`. `test-flatpak.sh` installs the
bundle into a fresh installation in the volume, checks the desktop entry, icons and MIME types, and
runs `--version`, `mdtool build` of `main/tests/linux-smoke/Hello` and the IDE smoke test under Xvfb
(`out/flatpak-test/`).

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

## 7. Editors

`.devcontainer/devcontainer.json` uses the same `Containerfile` (VS Code Dev Containers with
`"dev.containers.dockerPath": "podman"`, or JetBrains Rider).

See also [troubleshooting.md](troubleshooting.md).
