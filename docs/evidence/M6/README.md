# M6 evidence — CI gate

`scripts/ci.sh` runs the whole Linux gate in the dev container, in this order: setup, lint,
build `--check` (Release), duplicate-assembly check, tests with the coverage ratchet, vulnerability
audit, mdtool smoke, and GUI smoke. `.github/workflows/ci.yml` runs the same script
(`./scripts/pm ./scripts/ci.sh`), writes `out/ci/summary.txt` to the job summary, and uploads
`out/ci`, `out/coverage` and the TRX files as the artifact `monodevelop-ci-<version>+<sha>`.

## Local run (2026-09-23, M5c)

[ci-local-summary.txt](ci-local-summary.txt):

```text
setup              ok      1s
lint               ok      1s
build              ok     92s
assemblies         ok      1s
test               ok    263s
audit              ok     15s
mdtool-smoke       ok      5s
gui-smoke          ok      7s
wayland-smoke      ok      7s
total                    392s (budget 900s)
```

- **Tests:** MonoDevelop.Core.Tests 1068 passed, 0 failed, 9 skipped (Category!=Quarantine);
  MonoDevelop.Ide.Gtk3.Tests 36 passed. Coverage (Release, the CI configuration): MonoDevelop.Core 63.5%,
  MonoDevelop.Ide 0.45%, product total 16.4% (ratchet baseline 62.8 / 0.42 / 16.38).
- **Build timing:** incremental locally; a fresh runner also restores packages and compiles the
  whole solution. The SC-007 budget (15 min) is enforced by the script itself.
- **GUI smoke (X11):** the IDE's `--smoke-test` under Xvfb opens a copy of `linux-smoke/Smoke.sln`
  and builds it: 0 errors, exit 0 ([T103-smoke-x11.png](../M5/T103-smoke-x11.png)).
- **Wayland smoke:** the same on a headless Weston compositor with `GDK_BACKEND=wayland` and no X
  display; the log confirms the GDK display `wayland-md` ([T104-smoke-wayland.png](../M5/T104-smoke-wayland.png)).
  Weston's headless backend has no input devices, so GDK logs criticals for the missing seat.
- **Hosted run:** no hosted GitHub Actions run yet. Pushing needs the maintainer's authorization
  (T119).

## T118 — Dependabot and CodeQL (2026-09-24)

- `.github/dependabot.yml` sends weekly update PRs for:
  - NuGet (`/main`, central versions in `Directory.Packages.props`), with Roslyn, MSBuild, the NuGet client and the test
    packages each in their own group;
  - GitHub Actions.

  The `Containerfile` base images are not covered: they are pinned by digest through `ARG`s, which
  Dependabot's docker updater does not follow, so they are updated by hand with `global.json`.
- `.github/workflows/codeql.yml` runs CodeQL for C# (`build-mode: none`, no container needed) and for the
  workflows (`actions`). It runs on pushes and PRs to `main` and weekly, with `security-events: write` only
  on the analysis job. Code outside the Linux build (Mac/Windows platforms, externals, fixtures) is skipped.
- `./scripts/pm actionlint`: clean. No hosted run yet (it needs a push, T119).

## T117 — versioning and release workflow (2026-09-24)

[ADR 0021](../../adr/0021-versioning-and-release.md): `version.config` stays the product version (profile directory,
add-in compatibility), and releases take their version from a SemVer tag such as `v0.1.0-linux`.

`.github/workflows/release.yml` runs on `v*` tags:
- The `build` job has read-only access. It rejects tags that are not SemVer, runs `scripts/ci.sh`, packages the
  Flatpak once `scripts/package-flatpak.sh` exists (M7), writes release notes (the CI summary and
  `docs/BREAKING-CHANGES.md`) and uploads `monodevelop-<version>+<sha>`.
- The `publish` job is the only one with `contents: write`. It creates the GitHub release; a pre-release tag
  gives a GitHub pre-release.

Local checks: `./scripts/pm actionlint` is clean. The tag check accepts `v0.1.0-linux` and `v1.2.3`, and rejects
`v1.2`, `v01.2.3` and `v1.2.3+x`. No tag has been pushed: that needs the maintainer's authorization (M9).

## T145 — CI time: two test lanes (2026-09-24)

`scripts/test.sh --parallel` (used by `scripts/ci.sh`) runs the test projects in two lanes at the same time:
- lane a: Core, DotNetCore and PackageManagement;
- lane b: the GUI and other suites.

Each lane runs one assembly at a time and gives every project a copy of its runsettings whose MonoDevelop profile
(`XDG_*`) is `out/test-profile/<lane>/`. The two lanes therefore never share a profile or an add-in registry cache.
Coverage is merged and ratcheted as before. Without `--parallel`, the tests run sequentially as before.

| Run | test step | CI total (budget 900 s) |
|---|---|---|
| sequential, 2026-09-24 | 690 s | 830 s |
| two lanes, 2026-09-24 | 383 s | 536 s |

All 12 suites passed in the two-lane run: 3,871 passed, 0 failed. Coverage: Core 66.61%, Ide 20.83%, total 28.92%.

## T119 — hosted CI run (2026-09-25)

First green `ci.yml` run on a GitHub-hosted runner (ubuntu-24.04):
[run 36091142830](https://github.com/viniciusmorgado/quarkdevelop/actions/runs/36091142830), commit `b23a0a7f4b`.
- The run was cold: the dev image was built from the `Containerfile` and the NuGet cache started empty.
- Every step passed, and the NuGet cache was saved for later runs.
- The whole job took 13 min 25 s (SC-007 budget: 15 min).

The two earlier hosted runs found two problems that a warm local cache had hidden. Both are fixed:
- **Run 36088701543, failed in `setup`.** The runner's NuGet cache was mounted inside the container home, so
  podman created `/home/dev/.nuget` as root and `dotnet` could not read its `NuGet.Config`. The cache is now mounted
  at `/nuget-packages` (`35627c7c51`).
- **Run 36089278891, failed in `test`: 17 tests with NU1100.** Test helpers wrote a `NuGet.Config` whose only source
  key, "NuGet v3 Official", was excluded by the repository's package source mapping (`7b6018561d`).

`ci.sh` now prints the failing tests in the job log and the job summary (`a919cb79c5`).
