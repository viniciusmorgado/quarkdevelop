# the consistency analysis — revision 2 (T016)

- **Date**: 2026-09-23
- **Inputs**: `docs/constitution.md` 1.0.1, `specs/001-linux-dotnet10-migration/{spec,plan,research,data-model,quickstart,tasks}.md`,
  `contracts/*.md`, `docs/adr/0001–0018`, `docs/BREAKING-CHANGES.md`, `docs/evidence/M1/review-{A,B,C}.md`.
- **Repository state**: HEAD `e9d170a3a3` (the uncommitted T038 work in `main/tests/UnitTests` was not analysed).
- **Method**: a read-only reviewer. Every command ran in the dev container (`./scripts/pm bash -lc '…'`).
  `check-prerequisites.sh --json --require-spec --require-tasks --include-tasks` returned
  FEATURE_DIR = `specs/001-linux-dotnet10-migration`. `specs/extensions.yml` does not exist, so no hooks ran.
- **Container checks run for this report**: `dotnet --version` returned 10.0.401. `dotnet msbuild -version` returned
  18.9.11. `./scripts/lint.sh` exited with **1**. `actionlint` was clean. The command
  `git log --format='%ae %ce'` showed only the no-reply identity since `f3064b6182`.
  `git submodule status | grep -c '^ '` returned 15. `grep -c '<add key' NuGet.config` returned 1.
  `specify --version` returned 1.0.6. `netcoredbg --version` returned 3.2.0-1.
  `dotnet format whitespace main/src/core/MonoDevelop.Core --folder --verify-no-changes` exited with **2**:
  it reported 24,773 WHITESPACE and 148 CHARSET errors in 474 files. `dotnet msbuild -getProperty` on Core returned
  OutputPath=`main/build/bin/`, TreatWarningsAsErrors=true, AnalysisLevel=latest-recommended and
  RestorePackagesWithLockFile=true. `weston`, `flatpak`, `appstreamcli`, `desktop-file-validate` and `pkg-config`
  are absent. `XDG_RUNTIME_DIR` is unset.

**CRITICAL issues: 5**

## Specification Analysis Report

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Constitution (V) | CRITICAL | constitution.md:54; tasks.md:72-74 (T034–T036, checked); commit `10a54b7c27` | Several behaviour changes landed with no automated test in the same commit or earlier: instrumentation autosave changed from BinaryFormatter to JSON, remote instrumentation now throws `PlatformNotSupportedException`, `CreateExternalProcessObject` now throws `NotSupportedException`, and `CallContext` became `AsyncLocal`. The test harness (T038) is scheduled after them. T035 even points to tests in a later task (T046). Review A C2 fixed this ordering only for T042+. | Add a task right after T038–T040, before any other M3 task: tests for (a) the JSON autosave round-trip in `InstrumentationService`, (b) `CreateExternalProcessObject` throwing `NotSupportedException`, (c) `ItemInitializationContext` flowing through `AsyncLocal`. Record the gap as a deviation in the plan's Complexity Tracking. |
| C2 | Constitution (V) / Coverage gap | CRITICAL | constitution.md:55-56; tasks.md:55 (T026, checked), 71 (T033, checked); `.github/workflows/ci.yml` step "Build (+ format check)"; `scripts/build.sh --check` | `dotnet format --verify-no-changes` fails on the migrated `MonoDevelop.Core`: 24,773 whitespace and 148 charset violations in 474 files, measured with whitespace/folder mode alone. The style and analyzer passes run by the full `dotnet format` could add more. The CI build job, which runs `build.sh -c Release --check`, is therefore red. No task formats converted projects or reconciles `.editorconfig` with the legacy style. T026's proof (`actionlint` clean) does not show that the job passes. | Add an M2/M3 task: "align `.editorconfig` with the MonoDevelop code style (tabs, space before parentheses, charset), then apply `dotnet format` to each converted project in a formatting-only commit → `./scripts/pm ./scripts/build.sh --check` exits 0". Make `build.sh --check` part of the proof of every conversion task, including T033. |
| C3 | Constitution (Delivery Standards) | CRITICAL | constitution.md:93-94; tasks.md:50 (T024, checked); `scripts/warnings-baseline.sh:37` | `./scripts/lint.sh` exits 1 on HEAD. shellcheck reports SC2016 on `tr -d '^$()'` in `warnings-baseline.sh`, a script added after T024. T024's proof and the CI lint step both fail. | Fix line 37, for example `pattern="${forbidden//[\^\$()]/}"`, or add `# shellcheck disable=SC2016` with a reason. Re-run `./scripts/pm ./scripts/lint.sh`. |
| C4 | Constitution (I, VI) / FR-012 | CRITICAL | constitution.md:16-18, 67-68; spec.md:211-213; ADR 0017 rows "Superseded test runners", "Mac/VS-specific services", "Deferred (post-MVP)"; BREAKING-CHANGES.md:17-36; tasks.md:35 (T014, checked) | `BREAKING-CHANGES.md` leaves out several user-facing exclusions from ADR 0017: `MonoDevelop.AspNetCore` (ASP.NET Core project support), `MonoDevelop.Packaging` (NuGet package projects), `Deployment` (+ `Deployment.Linux`), `MonoDevelop.ConnectedServices`, `MonoDevelop.UnitTesting.NUnit` (in-IDE NUnit 2/3 runner, replaced by VSTest) and `MonoDeveloperExtensions`. T014's proof, "lists every ADR 0017 exclusion", is false. Review A I7 is only partly done. | Add these rows to the "Removed or deferred features" table, marking which ones are deferred. Add a T108 check that compares the ADR 0017 table against BREAKING-CHANGES. |
| C5 | Constitution (IV, Workflow, Governance) | CRITICAL | constitution.md:44-46, 98-100, 115; plan.md:69 ("one task per commit" PASS), 201-206 (Complexity Tracking) | Three deviations are not recorded in Complexity Tracking. (a) Commits span several tasks: `a64d4a9899` covers T018–T027, `f04d07795e` covers T021/T028/T029, and `10a54b7c27` covers T033–T037 plus part of T058 (`linux-smoke`). (b) M2/M3 implementation (`a64d4a9899`, `f04d07795e`, `10a54b7c27`) landed before the consistency analysis reported 0 CRITICAL. Revision 1 had 9 CRITICAL, and T016 is still open. (c) ADR 0018's per-ID baseline lets new *instances* of baselined IDs through, which differs from "new warnings in migrated projects are errors". ADR 0018 admits this under Consequences. The plan still reports the Constitution Check as PASS. | Add three rows to Complexity Tracking with the justification and a containment rule: one task per commit from now on; no further implementation until this report shows 0 CRITICAL; per-ID counts must not grow, checked by a script against `*.counts.txt`. Alternatively, amend the constitution (PATCH/MINOR + ADR) to define commit granularity and what counts as a "new warning". Change plan.md:69 from PASS to "PASS with deviations". |
| H1 | Inconsistency (review A I1 not done) | HIGH | research.md:28-33, 97, 140; plan.md:188-189 (R2 "T004b", R3 "T005"); ADR 0006 "spike T005"; ADR 0010 "Spike T004 / T004b" | Spike IDs from revision 1 are still in use, and they collide with task IDs. Research calls Mono.Addins "T005" (tasks: T007; T005 is the inventory), Roslyn "T004/T004b/T004c" (T008; T004 is submodules), the legacy baseline "T003" (T009; T003 is the specification init), DotDevelop "T007" (T010) and the container "T000" (T001). The evidence README uses the correct IDs, so the documents contradict each other. | Replace the IDs: T005→T007, T004/T004b/T004c→T008, T003→T009, T007 (DotDevelop)→T010, T000→T001. Apply this in research.md, plan.md R2/R3 and ADR 0006/0010. |
| H2 | Security / Inconsistency | HIGH | tasks.md:55 (T029 "security IDs never baselined"); ADR 0018 "IDs that may never be baselined"; `main/msbuild/Linux/warning-baselines/MonoDevelop.Core.props` | The Core baseline includes **CA5369**, a Security-category rule about insecure XML deserialization. It also includes CA2101 (P/Invoke marshalling) and SYSLIB0014. The forbidden list in `warnings-baseline.sh` covers only SYSLIB0011/0050/0051, NU1901–1904, CS8032 and CS0006, so T029's claim is false. | Extend the forbidden regex to the `CA5\d{3}`, `CA3\d{3}` and `CA23\d{2}` families and CA2100. Fix the 2 CA5369 sites in Core, or suppress them at each site with a justification. Regenerate the baseline and update ADR 0018. |
| H3 | Underspecification / constitution I | HIGH | spec.md:251-252 (SC-005 Wayland); tasks.md:180 (T104); quickstart.md:108; Containerfile | The Wayland smoke test cannot run in the reference container. `weston` is not installed, `XDG_RUNTIME_DIR` is unset (weston refuses to start without it), and `WAYLAND_DISPLAY` is not exported. No task adds weston to the Containerfile. | Add the `weston` apt package to the Containerfile as part of T104. Rewrite the quickstart command: `export XDG_RUNTIME_DIR=$(mktemp -d); weston --backend=headless --socket=wayland-md & …; WAYLAND_DISPLAY=wayland-md GDK_BACKEND=wayland dotnet … --smoke-test …`. |
| H4 | Untestable acceptance | HIGH | plan.md:176 (M5b criterion "GTK2-API count 0 in Linux solution"); tasks.md:155 (T082); quickstart.md:109; ADR 0011 "Progress metric" | `scripts/inventory.sh` greps all of `main/src` and `main/tests`, including excluded projects such as GtkCore/Stetic and MacPlatform. The count can never reach 0, and no tool restricts the count to Linux-solution compile items. | Add a `--linux-sln` mode to `inventory.sh`. It should list `Compile` items for each project in `MonoDevelop.Linux.sln` (`dotnet msbuild -getItem:Compile`) and grep only those. Reference it in T082, the quickstart and ADR 0011. |
| H5 | Untestable acceptance / constitution I | HIGH | tasks.md:57 (T031); spec.md:241-242 (SC-001) | The fresh-clone command `git clone . /tmp/fc && cd /tmp/fc && ./scripts/setup.sh && ./scripts/build.sh` cannot work as written under the "prefix with `./scripts/pm`" convention. The `&&` chain crosses the pm boundary, and `/tmp/fc` exists only in an ephemeral `--rm` container. It also never exercises the host path (podman + git builds the image), which is what SC-001 requires. | Rewrite it as `d=$(mktemp -d) && git clone . "$d/fc" && cd "$d/fc" && ./scripts/pm ./scripts/setup.sh && ./scripts/pm ./scripts/build.sh 2>&1 \| tee <repo>/docs/evidence/M2/fresh-clone.log` (host git is an allowed prerequisite), and include `PM_REBUILD=1` once. |
| M1 | Inconsistency (milestones) | MEDIUM | plan.md:173-174; tasks.md:77-82 | T038–T041 are labelled "M3" in tasks.md and appear in both the M3 row (range T033–T054) and the M4 row of the milestone table. T041's evidence goes to `docs/evidence/M4/`. | Pick one. Suggested: keep them in M3, remove them from the M4 row, and make M4 = T055–T057 plus T107's quarantine update. |
| M2 | Inconsistency (plan vs tasks) | MEDIUM | plan.md:154 (W2) | W2 says "DotNetCore.Core (SDK discovery)", but T054 and review B #2 keep SDK discovery in Core. W2 puts "Mono.Debugging (vendored)" in M3, but T110 is in M5. "MSBuildResolver" has no task. | Update W2: SDK helpers live in Core, Mono.Debugging moves to W4/M5, and MSBuildResolver gets a task (probably inside T059/T060) or is dropped. |
| M3 | Inconsistency (plan vs ADR 0017) | MEDIUM | plan.md:158-164 | The plan's "Excluded (ADR 0017)" list is from revision 1. It lacks ConnectedServices, UnitTesting.NUnit (+ runners), Deployment, AspNetCore, Packaging, MonoDeveloperExtensions, MDBuildTasks, AssemblyInfoWriter, TextEditor.Tests, ExtensionTools, tests/TestRunner, Subversion.Win32, WixSetup and AspNetCore.DevCertInstaller. | Replace the list with a link to ADR 0017 as the single source of truth. |
| M4 | Inconsistency (paths) | MEDIUM | plan.md:112; ADR 0003 "`main/msbuild/Linux/{Common,Addin,Test}.props`" | The repository has `Common.props`, `Common.targets`, `OutputLayout.targets`, `BuildVariables.targets` and `warning-baselines/`. `Addin.props` and `Test.props` do not exist and no task creates them; the add-in and test layout lives in `OutputLayout.targets`. | Update the plan structure and ADR 0003 to match the real files, or add the props to T038/T039 if they are still wanted. |
| M5 | Inconsistency (status/counts) | MEDIUM | tasks.md:34, 38 (T013, T017); plan.md:92, 128-146, 171; quickstart.md:70 | ADR 0018 landed in `80be2b3d33` and T029 uses it, yet T017 is still unchecked. The ADR thresholds disagree: quickstart M1 says "≥ 17", while T013 and the plan M1 say "≥ 18". The plan's documentation tree says "0001..0017", and the plan's ADR table has no 0018 row. | Check T017. Make the threshold "≥ 19 files (0001–0018 + README)" everywhere. Add ADR 0018 to the plan tree and table. |
| M6 | Inconsistency (status) | MEDIUM | tasks.md:117 (T058 unchecked); commit `10a54b7c27` | Most of `main/tests/linux-smoke/{Hello,Greeter,Broken,Smoke.sln,Directory.*}` is already committed under the T033–T037 commit, but T058 is open. | Either check T058 after running its proof, or note in tasks.md that T058 is partly done (see C5a). |
| M7 | Coverage gap (edge case) | MEDIUM | spec.md:174-175 | No task or test covers opening a legacy (non-SDK, .NET Framework) project and reporting clearly that it cannot be built. | Add it to T051 or T064: load a net472 fixture, expect a clear message and no crash. |
| M8 | Ambiguity | MEDIUM | spec.md:183; tasks.md:121 (T061) | "The build stops promptly" has no bound. | State a bound, for example ≤ 5 s after cancellation, and assert it in T061. |
| M9 | Underspecification | MEDIUM | tasks.md:215 (T124); quickstart.md:122; spec.md:259-260 (SC-008) | The quickstart runs the install check inside the `PM_PROFILE=flatpak` builder image, not the "clean container/environment" that T124 and SC-008 require. | Define a clean-install profile (a separate image with only flatpak and the GNOME runtime) in T121/T124, and use it in the quickstart. |
| M10 | Constitution (IV) / review A C6 incomplete | MEDIUM | tasks.md:32 (T011), 211 (T120) | T011 and T120 have no "→" proof. The deliverable is implied, not named as a proof. | T011 → `docs/evidence/M0/README.md` table complete. T120 → `docs/adr/0022-flatpak.md` exists and is indexed in `docs/adr/README.md`. |
| M11 | Ambiguity (constitution vs spec) | MEDIUM | constitution.md:60-61 ("the quarantine list shrinks over time"); spec.md:246; data-model.md:38-39 | The per-suite ratchet that resolved review A C4 lets the global list grow when new suites convert (M5c T107). The constitution text was not clarified. | Clarify it in a constitution PATCH (1.0.2): "per suite, the quarantine count never increases after the suite is first converted". |
| M12 | Inconsistency (contracts) | MEDIUM | contracts/scripts.md | `scripts/audit.sh` (T052) and `scripts/warnings-baseline.sh` (T029, already committed) are missing from the scripts contract. | Add both rows. |
| L1 | Constitution (MADR) / review A C10 incomplete | LOW | ADR 0012, ADR 0017 | Neither has a "Considered Options" section. ADR 0012 lists its alternatives inline. | Add the section. |
| L2 | Inconsistency | LOW | research.md:89, 132; ADR 0008; plan.md:190 (R4) | "backlog B35" is not defined anywhere. D16 calls packaging an "ADR 0017 companion", but it is ADR 0022 (T120). | Define B35, or replace it with "future task (post-M9)". Change the D16 reference to ADR 0022. |
| L3 | Inconsistency (stale IDs in code) | LOW | `MonoDevelop.Core.csproj` header comment ("tasks T028-T040"); `scripts/debug.sh:20` ("task T024") | These are revision-1 IDs. The correct ones are T033–T037, and T025 for netcoredbg. | Fix the comments. |
| L4 | Proof accuracy | LOW | tasks.md:71 (T033 "543 explicit sources") | The HEAD csproj has 541 `<Compile Include>` items. | Correct the number or drop it. |
| L5 | Constitution I (wording) | LOW | quickstart.md:68-70, 98 | `test -f`, `! grep`, `ls` and `cat` are documented without `./scripts/pm`. `./scripts/pm ! grep …` does not work; it needs `bash -lc`. | Wrap each in `./scripts/pm bash -lc '…'`. |
| L6 | Reproducibility | LOW | `.github/workflows/ci.yml` | `actions/checkout@v4` and `actions/cache@v4` are pinned by tag, while the images are pinned by digest. | Pin actions by commit SHA. Dependabot (T118) can keep them current. |
| L7 | Duplication | LOW | spec.md:220-221 (FR-016), 261 (SC-009); tasks.md:185, 225 (T109, T129) | SC-009 repeats FR-016. Start-up time is measured twice (T109 and T129). | Keep SC-009 as the measurable form. In T109, record the M5 measurement as informational and make T129 the gate. |
| L8 | Inconsistency | LOW | tasks.md:75 (T037 → "(T046)") | Loading the Core add-in through the registry is exercised by T038/T041 (`Runtime.Initialize`), not by T046. | Change the reference to T041. |

**Overflow**: none. There are 30 findings, under the limit of 50.

## Verification of revision-1 review resolutions

| Review item | Claimed resolution | Status |
|---|---|---|
| A-C1 coverage by M4 | T056 | Landed |
| A-C2 tests before behaviour | T038–T041 before T042–T052 | **Partial**: T034–T036 were implemented without tests (C1) |
| A-C3 warnings policy | T029 + ADR 0018 | Landed. Security-ID gap (H2); instance-level deviation not recorded (C5c) |
| A-C4 quarantine ratchet | data-model, SC-002 | Landed. Constitution wording not clarified (M11) |
| A-C5 Flatpak via pm | constitution 1.0.1 + T121 | Landed (T121 not implemented yet) |
| A-C6 proof per task | "→" on every task | **Partial**: T011 and T120 have no proof (M10) |
| A-C7 one project per UI task | M5c split | Landed |
| A-C8 docs early | T027, T030 | Landed (T027 files exist and are linked from README) |
| A-C9 pinned images | digests + spec tooling commit | Landed (Containerfile). CI actions are still tag-pinned (L6) |
| A-C10 Considered Options | ADRs 0004–0009, 0013–0015 | **Partial**: ADR 0012 and 0017 lack the section (L1) |
| A-I1 spike IDs | evidence renamed; research uses task IDs | **Not landed** in research.md, plan.md risks or ADR 0006/0010 (H1) |
| A-I2 debugger/GUI commands | quickstart § M5 | Landed (the debugger test path exists) |
| A-I3 linux-smoke isolation | own `Directory.*` | Landed (`main/tests/linux-smoke/Directory.{Build.props,Build.targets,Packages.props}`) |
| A-I4 lint list | `scripts/lint.sh` | Landed, but lint is currently failing (C3) |
| A-I5 / I7 exclusions, BREAKING-CHANGES | ADR 0017 extended; BREAKING-CHANGES completed | **Partial**: BREAKING-CHANGES is missing 6 user-facing items (C4); plan list is stale (M3) |
| A-U1 Wayland | T104 | Task exists, but it cannot run in the container (H3) |
| A-I8 Microsoft.Build 18.9.6 | CPM | Landed (the SDK's MSBuild is 18.9.11 ≥ 18.9.6) |
| B-1 IVT | T028 | Landed: the generated `obj/Debug/MonoDevelop.Core.AssemblyInfo.cs` has the same 4 IVT entries as the legacy csproj |
| B-2 no DotNetCore.Core cycle | T054 | Landed in tasks; the plan's W2 is stale (M2) |
| B-3 warnings | T029/ADR 0018 | Landed (see H2 and C5c) |
| B-4 TestHostSetup | T038 | Planned (work in progress, uncommitted) |
| C (traceability) FR-003…FR-018, SC-002/003/007 | tasks T042, T050, T064, T089, T100–T105, T108, T115, T122 | Landed. Every FR and SC is mapped (see below) |
| C Delivery (release/SBOM, least privilege) | T117, T123; `permissions: contents: read` | Landed |

## Plausibility of implemented tasks (HEAD `e9d170a3a3`)

| Task | Claimed proof | Observed |
|---|---|---|
| T001 | SDK 10.0.401, no Mono | OK: `dotnet --version` returned 10.0.401; `command -v mono` found nothing |
| T002 | `git log -1 --format=%ae` | OK: every commit since `f3064b6182` uses the no-reply address |
| T003 | `specify --version` = 1.0.6 | OK |
| T004 | 15 submodules | OK (15) |
| T005–T010 | evidence files and spikes | OK: `inventory.md`, `T006-gtk3-hello.png`, `T009-…`, `T010-…` exist; the M0 README uses the correct task IDs |
| T011 | research + M0 README | Present, but research uses stale IDs (H1) |
| T012–T015 | artifacts, ADRs, BREAKING-CHANGES, reviews | Present. T014's proof is false (C4) |
| T018 | global.json 10.0.100/latestFeature | OK |
| T019 | 1 `<add key>` | OK (1, with source mapping) |
| T020 | `main/Directory.Packages.props`, MSBuild 18.9.6 | OK (CPM and transitive pinning enabled; Roslyn pinned exactly with `[5.9.0]`) |
| T021 | `main/msbuild/Linux/{Common.props,Common.targets,OutputLayout.targets}` | OK. Core OutputPath is `main/build/bin/` |
| T022 | `grep 8.6 …/obj/Debug/BuildVariables.cs` | OK (Version = "8.6") |
| T023 | `dotnet sln … list` | OK at HEAD (Core; the working tree also adds UnitTests, uncommitted) |
| T024 | `lint.sh` + build ×2 | **Fails**: `lint.sh` exits 1 (C3) |
| T025 | netcoredbg, actionlint | OK (netcoredbg 3.2.0-1; actionlint clean; images pinned by digest) |
| T026 | `actionlint` clean | Clean, but the build job's `--check` step would fail (C2) |
| T027 | README Linux section, `docs/linux/*` | OK |
| T028 | IVT lines in generated AssemblyInfo | OK |
| T029 | `warning-baselines/MonoDevelop.Core.props`, TreatWarningsAsErrors | OK. It contains a security ID (H2) |
| T033 | SDK-style Core, CPM, listed packages | OK. The csproj is SDK-style with the listed packages and a lock file; there are 541 sources, not 543 (L4) |
| T034 | no Remoting/BinaryFormatter in Core compile items | OK: over the 541 compile items, the only matches are comments; `ProcessHostConsole.cs` exists |
| T035 | `ItemInitializationContext.cs` | OK (the file exists). No test yet (C1) |
| T036 | build clean of these IDs | Plausible: the listed IDs do not appear in `MonoDevelop.Core.counts.txt` |
| T037 | addin.xml imports | OK: only `Microsoft.Extensions.ObjectPool.dll` and `Newtonsoft.Json.dll` |

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 clean-clone-build-in-container | Yes | T001, T018–T024, T031 | T031's command needs fixing (H5) |
| FR-002 core-on-net10 | Yes | T033–T037, T042–T046, T049, T059, T062 | |
| FR-003 detect-dotnet-sdks | Yes | T042, T044 | |
| FR-004 addin-discovery | Yes | T037, T038, T050, T062 | |
| FR-005 mdtool-build-clean-select | Yes | T058–T064 | |
| FR-006 gtk3-x11-wayland | Yes | T066, T070–T086, T103, T104 | Wayland blocked (H3) |
| FR-007 edit-build-navigate-csharp | Yes | T085, T088, T089, T103, T105 | |
| FR-008 debug-net10 | Yes | T110–T114 | |
| FR-009 git-status-diff-history | Yes | T102 | |
| FR-010 nuget-manage | Yes | T100 | |
| FR-011 unit-test-runner | Yes | T101 | |
| FR-012 exclusions-listed | Yes | T013, T014, T108 | BREAKING-CHANGES incomplete (C4) |
| FR-013 flatpak-desktop-entry | Yes | T120–T124 | |
| FR-014 structured-logs | Yes | T125 | |
| FR-015 idempotent-scripts | Yes | T024, T114, T115, T123 | Lint failing (C3) |
| FR-016 no-vulnerable-deps-no-dead-feeds | Yes | T019, T020, T052, T127 | |
| FR-017 vendored-with-origin | Yes | T066–T068, T110 | UPSTREAM.md named in each task |
| FR-018 localizable | Yes | T047, T048 | |
| SC-001 fresh-clone-zero-manual-steps | Yes | T027, T031 | H5 |
| SC-002 tests-run-quarantine-≤15% | Yes | T040, T041, T057, T107 | M11 |
| SC-003 coverage-60/40-ratchet | Yes | T055, T056 | |
| SC-004 mdtool-samples-in-pipeline | Yes | T064, T065, T115, T116 | |
| SC-005 smoke-x11-wayland-≤10s | Yes | T103, T104, T109, T129 | H3 |
| SC-006 debug-scenario | Yes | T113 | |
| SC-007 pipeline-≤15min | Yes | T115, T119 | |
| SC-008 flatpak-clean-install | Yes | T124 | M9 |
| SC-009 zero-high-critical-vulns | Yes | T052, T127 | |

**Edge cases**: all six are covered by T042, T050, T038, T104, T061 and T106, **except** opening a legacy project (M7).

## Constitution Alignment Issues

- **V (NON-NEGOTIABLE)**: behaviour changes shipped without tests (C1). `dotnet format --verify-no-changes` fails on a migrated project (C2). New warning instances are not errors under the per-ID baseline, and this is not recorded (C5c).
- **Delivery Standards**: the maintained scripts fail shellcheck (C3).
- **I / VI**: removed or excluded add-ins are missing from `BREAKING-CHANGES.md` (C4).
- **IV / Development Workflow / Governance**: commits span several tasks, and implementation started before analyze reported 0 CRITICAL. Neither deviation is justified in Complexity Tracking (C5a, C5b).
- **IV (proof per task)**: T011 and T120 have no "→" proof (M10).

## Unmapped Tasks

These tasks map to the constitution, ADRs or Delivery Standards rather than to an FR or SC. They are justified:
T030 (CONTRIBUTING, Documentation standard), T069 (ADR 0019 NRefactory), T083 (GTK3 CSS themes, ADR 0011),
T126 (metrics and tracing, constitution VIII) and T128 (architecture docs, Documentation standard).

## Metrics

- Total Requirements: **27** (18 FR + 9 SC)
- Total Tasks: **131** (32 checked: T001–T015, T018–T029, T033–T037)
- Coverage: **100%** (27/27 requirements have at least one task)
- Ambiguity Count: **2** (M8, M11)
- Duplication Count: **1** (L7)
- Findings: 5 CRITICAL, 5 HIGH, 12 MEDIUM, 8 LOW
- **Critical Issues Count: 5**

## Next Actions

- **CRITICAL issues exist.** T016 is not done: its proof requires 0 CRITICAL. Resolve these before any further
  implementation work, including T038+:
  1. C3: one-line shellcheck fix in `scripts/warnings-baseline.sh:37`.
  2. C4: add 6 rows to `docs/BREAKING-CHANGES.md`.
  3. C5: add 3 rows to the plan's Complexity Tracking and change the Constitution Check line for IV.
  4. C2: edit tasks.md to add a formatting task (`.editorconfig` alignment plus a formatting-only commit for Core) and
     make `build.sh --check` part of the conversion proofs. Run it before CI is ever pushed.
  5. C1: edit tasks.md to add a regression-test task for T034–T036 immediately after T040.
- HIGH: edit research.md, plan.md and ADR 0006/0010 for the IDs (H1). Update `warnings-baseline.sh`, the Core
  baseline and ADR 0018 (H2). Update T104, the Containerfile and the quickstart for Wayland (H3). Add
  `inventory.sh --linux-sln` (H4). Rewrite the T031 command (H5).
- MEDIUM and LOW: edit tasks.md, plan.md and the contracts directly. No the specify step or the plan step re-run
  is needed. M11 needs a constitution PATCH (1.0.2) with an ADR note.
- After the fixes, re-run the consistency analysis and overwrite this file.

Would you like me to suggest concrete remediation edits for the top 10 issues? (None were applied; this analysis is
read-only.)
