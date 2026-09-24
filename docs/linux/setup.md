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

## 5. Editors

`.devcontainer/devcontainer.json` uses the same `Containerfile` (VS Code Dev Containers with
`"dev.containers.dockerPath": "podman"`, or JetBrains Rider).

See also [troubleshooting.md](troubleshooting.md).
