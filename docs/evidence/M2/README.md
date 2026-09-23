# M2 evidence — Linux / .NET 10 toolchain

| Criterion (plan § Milestones, M2) | Evidence | Result |
|---|---|---|
| Fresh clone builds with podman + git only (SC-001) | [fresh-clone.log](fresh-clone.log) | setup + build exit 0 in a fresh clone **without submodules** |
| Build is idempotent (two consecutive runs) | [toolchain.log](toolchain.log) § build twice | both runs `0 Error(s)`, format check of new files clean |
| Pinned toolchain | [toolchain.log](toolchain.log) § versions | SDK 10.0.401 (global.json 10.0.100 + latestFeature), netcoredbg 3.2.0-1, actionlint 1.7.12; base images pinned by digest |
| Scripts lint clean, workflow valid | [toolchain.log](toolchain.log) § lint | shellcheck (13 scripts) + actionlint OK |
| Conventions | `CONTRIBUTING.md`, ADR 0018, `scripts/git-commit` | warnings-as-errors with baselines; `Tasks:` trailer enforced |

The CI workflow (`.github/workflows/ci.yml`) runs the same scripts in the same image; a hosted
run requires pushing, which needs the maintainer's authorization (risk R13).
