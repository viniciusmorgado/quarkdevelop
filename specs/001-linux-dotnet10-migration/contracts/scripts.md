# Contract: developer scripts

All scripts: bash, `set -euo pipefail`, idempotent, `shellcheck`-clean, run inside the container
(`./scripts/pm ./scripts/<name>.sh`) except `scripts/pm` itself (host).

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/pm <cmd>` | run `<cmd>` in the dev container (builds image on first use); `PM_PROFILE=flatpak` selects the Flatpak-builder image (M7) | exit code of `<cmd>` |
| `scripts/git-commit <args>` | `git commit` with the only allowed identity | commit created |
| `scripts/inventory.sh [out]` | static migration inventory | `docs/evidence/M0/inventory.md` |
| `scripts/setup.sh` | verify toolchain, restore tools/workloads | prints versions; exit 0 |
| `scripts/restore.sh` | `dotnet restore main/MonoDevelop.Linux.sln` (locked mode in CI) | exit 0 |
| `scripts/lib.sh` | shared helpers (sourced) | — |
| `scripts/lint.sh` | shellcheck of maintained scripts (+ actionlint for workflows) | exit 0 |
| `scripts/build.sh [-c Debug\|Release] [--check]` | restore + build the Linux solution; `--check` adds `dotnet format --verify-no-changes` | `main/build/bin`, `main/build/AddIns` |
| `scripts/test.sh [--all] [filter]` | tests (excl. Quarantine unless `--all`) with coverage | `out/tests/*.trx`, `out/coverage/Summary.txt` |
| `scripts/run.sh [--headless] [args]` | start the IDE (X11/Wayland, or Xvfb with `--headless`); `--smoke-test <sln>` is an IDE argument (contracts/smoke-test.md) | IDE process / smoke exit code |
| `scripts/debug.sh [ide\|mdtool] [args]` | run the IDE or mdtool under netcoredbg (CLI) | debugger session |
| `scripts/ci.sh` | run the CI pipeline steps locally in the container, timed (SC-007) | `out/ci/summary.txt` |
| `scripts/package-flatpak.sh` | build the Flatpak bundle (run with `PM_PROFILE=flatpak ./scripts/pm`) | `out/monodevelop.flatpak` |
