# M9 — acceptance checklist (T130, 2026-09-25)

Branch `dotnet-linux-migration`, measured at `e5157bc80e`, in the reference container on the maintainer's
workstation (x86-64, 12 cores).

| Criterion | Status | Evidence |
|---|---|---|
| **SC-001:** fresh clone to successful build with only podman and git | **Met** | Details below |
| **SC-002:** every test project runs; quarantine ≤ 15%, with reasons | **Met** | Details below |
| **SC-003:** coverage: core ≥ 60% and a ratchet; no global target since 2026-09-25 | **Met** as amended | Details below |
| **SC-004:** CLI build of the sample project and solution succeeds, the broken sample fails, in every pipeline run | **Met** | Details below |
| **SC-005:** GUI smoke on X11 and Wayland; start-up ≤ 10 s | **Met** | Details below |
| **SC-006:** automated debug scenario (breakpoint, locals, step, exit) | **Met** (DAP level); GUI debugging open (T153) | Details below |
| **SC-007:** full pipeline ≤ 15 min with a warm cache | **Met** | Details below |
| **SC-008:** Flatpak installs cleanly; version and headless build checks | **Met** | Details below |
| **SC-009:** zero High/Critical vulnerabilities | **Met** | Details below |

## Details

**SC-001.** The branch was cloned into a new directory inside the container, and the commands of
`docs/linux/setup.md` were run from that clone:
- `./scripts/pm ./scripts/setup.sh`: ok.
- `./scripts/pm ./scripts/build.sh`: ok in 76 s.
- `mdtool build` of Hello: exit 0, and the program prints "Hello, MonoDevelop!".

The container image and the NuGet cache volume already existed on the machine. A fully cold run (image build of
about 3 minutes plus NuGet downloads) is exercised by the hosted CI once pushing is authorized (T119).

**SC-002.** All 12 test projects run in the gate. The quarantine record is
[docs/evidence/M4/quarantine.md](../M4/quarantine.md) and the M8 summary is in [M8/README.md](../M8/README.md).
- There are 54 quarantined cases out of about 4,030 discovered, which is 1.3%. The largest suite share is DotNetCore
  at 4.4%.
- Every entry has a reason, and every Bug/Flaky entry is fixed or linked to a follow-up task (T135 is done).
- The full regression with the quarantined tests included (`scripts/test.sh --all`) fails exactly 48 of the 54
  quarantined cases: Core 26, DotNetCore 12, Ide 10. No test outside the quarantine fails. The other 6 (Core 5,
  PackageManagement 1) need nuget.org and pass when the container has network access.

**SC-003.** Coverage ratchet: [docs/evidence/M4/coverage-baseline.txt](../M4/coverage-baseline.txt).
- MonoDevelop.Core: 66.83% (target 60%).
- Whole solution (product assemblies): 29.25%. The original target was 40%; there is no fixed target since the amendment.

The total is held down by MonoDevelop.Ide at 21.3% of the largest assembly, and by add-ins with little or no GUI test
coverage: Debugger 0.8%, VersionControl 7.9%, RegexToolkit 0%, DesignerSupport 0%. The ratchet was raised to
66.5 / 21.0 / 29.0, so coverage cannot fall back.

Amendment of 2026-09-25 (constitution 1.4.0, ADR 0001), decided by the maintainer:
- The 40% global target is dropped.
- Tests focus first on the IDE running smoothly: the X11, Wayland, errors, modern-C# and debug smoke tests.
- After that, they cover critical methods.
- Existing tests are preserved, and the ratchet stays.

**SC-004.** The `mdtool-smoke` step of `scripts/ci.sh` runs in every pipeline run:
- it builds Hello, Smoke.sln (with `-p:` project selection) and Modern, and runs the programs;
- it cleans them;
- it requires Broken to fail.

**SC-005.** The `gui-smoke` (X11 under Xvfb) and `wayland-smoke` (headless Weston, `GDK_BACKEND=wayland`) steps pass,
with a check that the screenshot is not blank. Main window after process start:
- 1.7–2.4 s in CI (cold profile);
- 2.2 s for a cold start on an idle machine;
- at most 5.1 s under full load.

Sources: [M5 T109](../M5/README.md) and [M8/startup.md](../M8/startup.md).

**SC-006.** `NetCoreDbgTests` (MonoDevelop.Debugger.Tests, 6 tests) build a net10.0 console program and debug it
with netcoredbg through the IDE's `NetCoreDbgSession`. They cover a breakpoint hit, locals, step over, continue to
exit code 3, and an unhandled exception. They pass in every gate run.

GUI debugging through the Debug layout reaches the breakpoint, but the Locals/Watch pads hit a GtkSharp
toggle-reference crash in 2 of 3 runs. That is being fixed in T153, together with a `gui-smoke-debug` CI step.

**SC-007.** The full `scripts/ci.sh` run takes 515–645 s of wall-clock time with a warm cache (budget 900 s). It
covers setup, lint, the Release build with format check, the assembly check, tests with coverage in two lanes, the
audit, the mdtool smoke and 4 GUI smokes. The timings are in [M6/README.md](../M6/README.md). A hosted-runner
measurement needs push authorization (T119).

**SC-008.** [M7/README.md](../M7/README.md). The bundle installs into a fresh Flatpak installation. `--version`
works, `mdtool build` of Hello succeeds, and the IDE smoke test passes under Xvfb inside the sandbox. It has not
been tried on a real desktop session.

**SC-009.** `scripts/audit.sh` (`dotnet list package --vulnerable --include-transitive`) reports 0 High/Critical and
0 Moderate/Low findings. NuGetAudit is enabled during restore, and the T127 sweep is in
[M8/security.md](../M8/security.md).

## Known gaps

None of these is part of an SC.
- T144: an intermittent GTK test-host crash (about 1 in 20 runs).
- T147 remainder T149: source generators from project references, generated files under Dependencies.
- T150: the ADR 0011 port helpers, 344 uses, still to be removed.
- T152: New Project/New File dialogs driven by `dotnet new`, C# and F# only, Linux-only templates (in progress).
- T154: an F# language binding. F# projects can be created and built, but have no editor support yet.
- Test and infrastructure debt: T134, T140–T143, T151.
