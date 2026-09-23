# Contract: developer scripts

All scripts: bash, `set -euo pipefail`, idempotent, `shellcheck`-clean, run inside the container
(`./scripts/pm ./scripts/<name>.sh`) except `scripts/pm` itself (host).

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/pm <cmd>` | run `<cmd>` in the dev container (builds image on first use) | exit code of `<cmd>` |
| `scripts/git-commit <args>` | `git commit` with the only allowed identity | commit created |
| `scripts/inventory.sh [out]` | static migration inventory | `docs/evidence/M0/inventory.md` |
| `scripts/setup.sh` | verify toolchain, restore tools/workloads | prints versions; exit 0 |
| `scripts/restore.sh` | `dotnet restore main/MonoDevelop.Linux.sln` (locked mode in CI) | exit 0 |
| `scripts/build.sh [-c Debug\|Release]` | restore + build the Linux solution | `main/build/bin`, `main/build/AddIns` |
| `scripts/test.sh [--all] [filter]` | tests (excl. Quarantine unless `--all`) with coverage | `out/tests/*.trx`, `out/coverage/Summary.txt` |
| `scripts/run.sh [args]` | start the IDE (X11/Wayland, or Xvfb with `--headless`) | IDE process |
| `scripts/debug.sh` | start the IDE waiting for a debugger (netcoredbg/VS Code attach) | pid printed |
| `scripts/package-flatpak.sh` | build the Flatpak bundle | `out/monodevelop.flatpak` |
