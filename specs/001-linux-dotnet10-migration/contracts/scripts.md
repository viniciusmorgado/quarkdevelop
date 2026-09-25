# Contract: developer scripts

All scripts: bash (`set -euo pipefail`) or Python 3 without extra packages, idempotent, `shellcheck`-clean
(`scripts/lint.sh`), run inside the container (`./scripts/pm ./scripts/<name>`) except `scripts/pm` itself (host).
Scripts that source `scripts/lib.sh` refuse to run on the host.

## Environment and commits

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/pm <cmd>` | run `<cmd>` in the dev container (builds the image on first use). Environment: `PM_PROFILE=dev\|flatpak` (the Flatpak-builder image, M7), `PM_PODMAN_ARGS` (extra `podman run` arguments, e.g. display forwarding), `PM_IMAGE` (use an existing image), `PM_REBUILD=1` (rebuild the image) | exit code of `<cmd>` |
| `scripts/git-commit <git commit args>` | `git commit` with the only allowed identity (GitHub no-reply address) and the process rules below | commit created |
| `scripts/lib.sh` | shared helpers (sourced): container check, logging, paths, `md_new_cs_files` (C# files added since the fork point) | — |

`scripts/git-commit` refuses a commit when:
- the message has no `Tasks: Tnnn[, Tnnn]` (or `Tasks: none`) line;
- `Tasks:`, `Coupled:`, `Format-only:`, `Analyze-fix:` or `Analyze-override:` is not in the last paragraph, where
  git reads trailers;
- several tasks are listed without a `Coupled: <reason>` line (constitution IV);
- a file that exists at the fork point has its line endings rewritten (its diff is much larger than the
  whitespace-insensitive diff), unless the message has `Format-only: <reason>`; a `Format-only:` commit may change
  whitespace and line endings only (constitution V);
- the committed `docs/evidence/M1/analyze.md` reports CRITICAL issues and the commit is neither an analyze fix
  (`Analyze-fix: <finding IDs>`, or task T016) nor `Analyze-override: <reason>` (Development Workflow).

## Build, test, run

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/setup.sh` | verify the toolchain, restore local tools and workloads | prints versions; exit 0 |
| `scripts/restore.sh [--locked]` | `dotnet restore main/MonoDevelop.Linux.sln` (`--locked`: lock-file mode, as CI) | exit 0 |
| `scripts/build.sh [-c Debug\|Release] [--check]` | restore and build the Linux solution; `--check` adds `dotnet format whitespace --verify-no-changes` on the C# files added by the fork | `main/build/bin`, `main/build/AddIns` |
| `scripts/format.sh [files...]` | apply `dotnet format whitespace` to the given files (default: every C# file added by the fork) | files rewritten |
| `scripts/test.sh [--all] [--no-build] [--parallel] [--update-baseline [--allow-lower]] [dotnet test args...]` | tests of the Linux solution (without the `Quarantine` category unless `--all`), with coverage and the ratchet on `docs/evidence/M4/coverage-baseline.txt`; GUI suites under Xvfb when there is no display. `--parallel`: two lanes (T145). `--update-baseline`: rewrite the ratchet file; it refuses any value lower than the recorded one unless `--allow-lower` is given (constitution V) | `out/tests/*.trx`, `out/coverage/Summary.txt`, `out/coverage/line-coverage.txt` |
| `scripts/run.sh [--headless] [IDE args...]` | start the IDE (display forwarded, or Xvfb with `--headless`); `--smoke-test <sln>` is an IDE argument (`contracts/smoke-test.md`) | IDE process / smoke exit code |
| `scripts/debug.sh [ide\|mdtool] [args...]` | run the IDE or mdtool under netcoredbg (CLI) | debugger session |

## Gates and checks

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/lint.sh` | shellcheck of the maintained scripts, actionlint of the workflows | exit 0 |
| `scripts/audit.sh` | fail on any High or Critical advisory in `dotnet list package --vulnerable --include-transitive` (SC-009) | exit 0 |
| `scripts/check-assemblies.sh` | fail when an assembly is both in `main/build/bin` and an add-in directory, or shadows a shared-framework assembly (risk R9) | exit 0 |
| `scripts/ci.sh` | the CI gate, the same locally and in GitHub Actions: setup, lint, Release build with `--check`, assembly check, tests in two lanes with the ratchet, audit, mdtool smoke, 5 GUI smokes (`gui-smoke`, `gui-smoke-debug`, `gui-smoke-errors`, `gui-smoke-modern`, `wayland-smoke`); each step timed, the first failure stops the run; fails over the SC-007 budget of 900 s (`MD_CI_BUDGET_SECONDS` overrides) | `out/ci/summary.txt` |

## Packaging

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/package-flatpak.sh [--rebuild-ide]` | build the Flatpak bundle (run with `PM_PROFILE=flatpak ./scripts/pm`); refuses a bundle whose files contain the checkout path | `out/monodevelop.flatpak`, `.sha256`, `out/monodevelop.cdx.json` (SBOM) |
| `scripts/test-flatpak.sh [bundle]` | install the bundle into a fresh Flatpak installation and run the checksum, desktop integration, `--version`, `mdtool build` and IDE smoke checks (SC-008; `PM_PROFILE=flatpak`) | `out/flatpak-test/summary.txt` |

## Migration tools

| Script | Purpose | Success output / artifact |
|---|---|---|
| `scripts/inventory.sh [out]` / `--linux-sln [out]` | static migration inventory of the tree, or of the files compiled by the Linux solution | `docs/evidence/M0/inventory.md` or the given path |
| `scripts/warnings-baseline.sh <project.csproj>` | regenerate a project's warning baseline; security IDs are never baselined (ADR 0018) | `main/msbuild/Linux/warning-baselines/<Project>.{props,counts.txt}` |
| `scripts/convert-legacy-csproj.py <project.csproj> [--write]` | convert a legacy project to SDK-style `net10.0` (ADR 0003) | converted project with `TODO(convert)` notes |
| `scripts/netfx-refasm.sh` | install the pinned, checksum-verified .NET Framework reference assemblies used by legacy test fixtures (T134) | `~/.cache/monodevelop/netfx-refasm/` |
| `scripts/tools/compiled-files.sh` | list the C# files compiled by the Linux solution (security sweep, ADR 0011 metric) | one path per line |
| `scripts/tools/{gtk3-codemod,linux-sln-sources,mdedit,quarantine}.py` | GTK2 → GTK3 rewrites, compiled-source listing, line-ending-preserving edits, quarantine of failing legacy tests | as documented in each file |
