# the consistency analysis: revision 3 (T016)

- **Date**: 2026-09-23
- **Inputs**: `docs/constitution.md` 1.1.0, `specs/001-linux-dotnet10-migration/{spec,plan,research,data-model,quickstart,tasks}.md`,
  `contracts/*.md`, `docs/adr/0001–0018`, `docs/BREAKING-CHANGES.md`, and revision 2 of this file.
- **Repository state**: HEAD `0ecd61be05` ("M3/M4: .NET 10 test harness for the core; analyze revision-2 fixes").
  The working tree also contains **uncommitted T059 work** (`MonoDevelop.MSBuildBuilder.csproj`, `Main.cs`,
  `BuildEngine.Shared.cs`, `ILogWriter.cs`, and the Linux sln). That work was not analysed as content. It is
  mentioned only under H1 as a process observation.
- **Method**: a read-only reviewer. Every command ran in the dev container (`./scripts/pm bash -lc '…'`).
  `check-prerequisites.sh --json --require-spec --require-tasks --include-tasks` returned
  FEATURE_DIR = `specs/001-linux-dotnet10-migration`. `specs/extensions.yml` does not exist, so no hooks ran.
- **Container checks for this report**:
  - `./scripts/lint.sh` exited with **0** (shellcheck on 13 scripts, plus actionlint).
  - Every commit since `f3064b6182` uses the no-reply identity.
  - `weston --version` returned 13.0.0.
  - The quickstart Wayland recipe works. `weston --backend=headless --socket=wayland-md` with `XDG_RUNTIME_DIR=$(mktemp -d)`
    created the socket. The `spikes/gtk3-hello` binary, run with `GDK_BACKEND=wayland`, printed `gtk3-hello: OK` and
    exited 0.
  - A grep for `CA2100|CA23xx|CA3xxx|CA5xxx` over all 8 files in `main/msbuild/Linux/warning-baselines/` found **no
    match**.
  - `./scripts/build.sh --check` exited 0: 0 errors, and `dotnet format whitespace` passed on the 7 new files.
  - `./scripts/inventory.sh --linux-sln /tmp/inv.md` covered 640 files and found 0 in each of the 4 GTK2-only rows.
  - Fresh clone inside the container (the T031 shape): `git clone . ~/fc-review && ./scripts/setup.sh && ./scripts/build.sh --check`
    produced 0 errors. The format step was **skipped** there, because `md_new_cs_files` returned 0 files with the
    default `MD_BASE_REF=main` and 7 files with `MD_BASE_REF=origin/main`.
  - `dotnet test main/tests/MonoDevelop.Core.Tests --list-tests` listed **860** cases.

**CRITICAL issues: 2**

## Resolution of revision-2 findings

| Rev-2 | Status in HEAD | Evidence / remainder |
|---|---|---|
| C1: T034–T036 had no tests | **Partial** | T132 was added and a Complexity Tracking row was recorded. The tests are not written yet, and a new untested behaviour change landed (see C2) |
| C2: `dotnet format` red | **Resolved (rule changed)** | Constitution 1.1.0 and ADR 0018 § Formatting now apply the rule to new files. `build.sh --check` exits 0. However, the gate can pass without checking anything (H2), and no task schedules the legacy reformatting (M2) |
| C3: lint exit 1 | **Resolved** | `lint.sh` exits 0 |
| C4: BREAKING-CHANGES incomplete | **Resolved** | Every user-facing ADR 0017 exclusion is listed, plus MonoDoc, WS-Trust, `CreateExternalProcessObject` and the binary instrumentation files |
| C5: unrecorded process deviations | **Partial** | 4 rows added to Complexity Tracking. Plan IV now reads "PASS with deviations". Deviation (c) was resolved by constitution V 1.1.0. Deviations (a) and (b) **recur in HEAD** (C1, H1) |
| H1: spike IDs | **Resolved** | research.md, plan R2/R3 and ADR 0006/0010 now use T001/T007/T008/T009/T010. One minor version slip remains (L3) |
| H2: security IDs in baseline | **Resolved** | The forbidden regex covers CA2100, CA23xx, CA3xxx and CA5xxx. The CA5369 sites are fixed in `Properties.cs`. None of the 8 baselines contains a security ID. ADR 0018 is updated |
| H3: Wayland in container | **Resolved** | weston is in the `Containerfile`. The quickstart and T104 commands were verified (GTK3 on Wayland exits 0) |
| H4: `inventory.sh --linux-sln` | **Mostly resolved** | The mode works (640 files; T082 and the quickstart use it). ADR 0011 is not updated, and globbed projects are not handled (M4) |
| H5: T031 command | **Resolved (feasible)** | Clone, setup and build were verified in the container with 0 errors. The host image-build path is not exercised, and nothing writes the log (M5) |
| M1–M11 | **Open** | Carried over as M7–M17 below |
| M12 (scripts contract) | Superseded | Replaced by M1, which covers a wider gap |
| L1–L3, L5–L8 | **Open** | Carried over as L6–L12 |
| L4 (543 sources) | Resolved | 542 `<Compile Include>` items. Adding the generated items makes 545 plausible |

## Specification Analysis Report

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Constitution (IV) | CRITICAL | constitution.md:44-46; plan.md:207 (Complexity Tracking row 3); commit `0ecd61be05` | HEAD mixes T017, T038, T039, T040 and T133, the T016 review fixes, and a production-code fix (CA5369 in `Properties.cs`) in **one commit**. The Complexity Tracking row added in that same commit says "later tasks follow one-task-per-commit". The deviation is therefore unrecorded, and the recorded justification is false. | Choose one. (a) Amend row 3 to list the multi-task commits explicitly (`a64d4a9899`, `f04d07795e`, `10a54b7c27`, `0ecd61be05`) and add a containment that can be enforced: `scripts/git-commit` rejects a message without exactly one `Task: Tnnn` trailer, except for an explicit `Review-fix:` trailer on doc-only commits. (b) Amend constitution IV (PATCH + ADR) to allow "one task plus the documentation fixes it triggers". Either way, commit the in-flight T059 work alone. |
| C2 | Constitution (V) | CRITICAL | constitution.md:54; `main/src/core/MonoDevelop.Core/MonoDevelop.Core/Properties.cs:361-366` (`LazyXmlDeserializer.Deserialize`); plan.md:210 | The CA5369 fix changes behaviour: stored property XML with a DTD or an external resolver is now rejected (the method returns `null` and logs a warning). No test in HEAD or earlier covers the rejection path. `PropertyTests` only round-trips valid values. This is not covered by the Complexity Tracking row, which is limited to T034–T036. | Add `PropertyTests.DeserializeRejectsDtd`: a serialized value containing `<!DOCTYPE …>` deserializes to `null` or default and does not resolve entities. Put the test in T132, extend Complexity Tracking row 6 to "T034–T036 and the CA5369 fix in `0ecd61be05`", and land T132 before any other code commit. |
| H1 | Constitution (Workflow) / ordering | HIGH | constitution.md:99-101; plan.md:208 (row 4); tasks.md:37 (T016 open), 236-237 (Dependencies) | T038–T040 and T133 were implemented while revision 2 (5 CRITICAL) was the current analysis. Uncommitted T059 builder work is in the tree now, although T041 and T132 are open. The Dependencies section says "T059–T061 … once T041 is done". Row 4 justifies this with "pausing would not have changed the findings" and has **no containment**, so in practice it waives the gate indefinitely. | Reword row 4 to say "no implementation commit beyond T041/T132 until an analyze revision reports 0 CRITICAL", and follow it: fix C1 and C2, re-run analyze, then do T132 → T041 → T059. Otherwise, amend the Development Workflow rule explicitly. |
| H2 | Constitution (V) / quality gate | HIGH | `scripts/lib.sh:217-226` (`md_new_cs_files`); `scripts/build.sh:25-30`; `.github/workflows/ci.yml:23-25`; constitution.md:55 | The formatting gate is fail-open. `git merge-base HEAD "${MD_BASE_REF:-main}" … \|\| true` returns nothing when the ref is missing; a verified fresh clone gave 0 files, so the check was skipped silently. After this branch merges, `merge-base(HEAD, origin/main)` = HEAD on `main` pushes, and later branches only check their *own* additions. That does not implement "every C# file added by this fork". | Pin the upstream fork point, `ba01d2d6d3` (the last upstream commit, 2021-10-04, and today's merge-base): `MD_UPSTREAM_BASE=ba01d2d6d3c84e92a6b5f360dac76ee821547529` in `lib.sh`. Use `git diff --diff-filter=AR --name-only "$MD_UPSTREAM_BASE" HEAD -- '*.cs'` plus untracked files. Call `md_die` if `git cat-file -e "$MD_UPSTREAM_BASE"` fails (CI already uses `fetch-depth: 0`). Drop `MD_BASE_REF` from `ci.yml`. |
| H3 | Coverage / constitution V containment | HIGH | tasks.md:82 (T132 open); plan.md:210 | Row 6 promises that T132 comes "before any further behaviour change". Since then, C2 happened and the T059 behaviour work was started. The T035 proof still points to T046, and T046 also lists "`AsyncLocal` delayed initialization", so ownership of the retroactive tests is split. | Make T132 the next task. Change T035's proof to "→ T132" and remove the `AsyncLocal` item from T046. |
| M1 | Inconsistency (contracts) | MEDIUM | contracts/scripts.md | The contract still says `--check` "adds `dotnet format --verify-no-changes`" (now: whitespace check on fork-added files). Several scripts are missing from it: `format.sh`, `warnings-baseline.sh`, `audit.sh` (T052), `inventory.sh --linux-sln`, `convert-legacy-csproj.py` and `tools/{linux-sln-sources,mdedit}.py`. | Update the `build.sh` row and add the missing rows. State that Python helpers are exempt from shellcheck. |
| M2 | Coverage gap (constitution V 1.1.0) | MEDIUM | constitution.md:55-56; tasks.md (no task) | The constitution now says "legacy files are reformatted one project at a time in dedicated, behaviour-free commits", but no task schedules any such commit. Conversion proofs (T033, T038–T040, T059…) still do not include `build.sh --check`. | Add T134: "format-only commit per converted project (Core, UnitTests, Core.Tests, Core.Tests.Addin, MSBuildBuilder) → `dotnet format whitespace <proj> --verify-no-changes` exit 0". Append `build.sh --check` to each conversion proof. |
| M3 | Governance | MEDIUM | constitution.md:112-118; ADR 0018 § Formatting; tasks.md:37 | Narrowing two gates of a NON-NEGOTIABLE principle (formatting scope; "new warnings are errors" → per-ID baseline) was versioned MINOR (1.1.0). The governance rule says "MAJOR for removing or redefining a principle". The amendment's ADR is a subsection of the warning-policy ADR. T016 still cites "constitution 1.0.1". | Either bump to 2.0.0, or add a sentence to ADR 0018 arguing that this is scope clarification (MINOR). Update the T016 text. |
| M4 | Untestable metric (rev-2 H4 remainder) | MEDIUM | `scripts/tools/linux-sln-sources.py:17-22`; ADR 0011:29 | The metric regex-parses only explicit `<Compile Include="….cs">`. It ignores default globs, `Remove`, `Condition` and wildcard includes. Complexity Tracking row 1 plans to switch to globs, and the metric would then undercount silently. ADR 0011 still defines the metric as a bare `grep -rlE` over the Linux solution. | Use `dotnet msbuild <proj> -getItem:Compile` (JSON) for each sln project. Point ADR 0011:29 to `inventory.sh --linux-sln`. |
| M5 | Underspecification (SC-001) | MEDIUM | tasks.md:57 (T031); spec.md:241-242 | T031 is feasible (verified), but it runs inside an already-built image, so "only podman and git from a fresh clone" (the image build) is never exercised. The command also never writes `docs/evidence/M2/fresh-clone.log`. | Run on the host (git and podman are allowed): `d=$(mktemp -d) && git clone -q . "$d/fc" && (cd "$d/fc" && PM_REBUILD=1 ./scripts/pm ./scripts/setup.sh && ./scripts/pm ./scripts/build.sh) 2>&1 \| tee docs/evidence/M2/fresh-clone.log`. |
| M6 | Constitution (VII) / hidden runtime failures | MEDIUM | `warning-baselines/MonoDevelop.Core.Tests.props` (SYSLIB0006); `MonoDevelop.Core.props` (CA1416 ×84); `DedicatedThreadSchedulerTests.cs:129` | The baselines accept `Thread.Abort` (SYSLIB0006), which always throws `PlatformNotSupportedException` on .NET 10. They also accept 84 CA1416 platform-compatibility sites (Windows-only APIs reachable on Linux). These are guaranteed or likely runtime failures, not cosmetic legacy noise. | Treat SYSLIB0006 as forbidden for new baselines. In T041, fix or quarantine the `Thread.Abort` test with a reason. In T049, triage the CA1416 sites and track the count in the M3 evidence. |
| M7 | Inconsistency (rev-2 M1) | MEDIUM | plan.md:173-174 | T038–T041 appear in both the M3 and M4 milestone rows. T132 and T133 are in neither. | Keep T038–T041, T132 and T133 in M3. Make M4 = T055–T057 (+T107). |
| M8 | Inconsistency (rev-2 M2) | MEDIUM | plan.md:154 | W2 is stale: SDK discovery lives in Core, Mono.Debugging belongs to M5, and MSBuildResolver has no task. | Update W2. |
| M9 | Inconsistency (rev-2 M3) | MEDIUM | plan.md:158-164 | The "Excluded" list is from revision 1. | Replace it with a link to ADR 0017. |
| M10 | Inconsistency (rev-2 M4) | MEDIUM | plan.md:112; ADR 0003 | `Addin.props` and `Test.props` do not exist. The real file is `Test.targets` (added in HEAD), next to `OutputLayout.targets`. | Match the real files. |
| M11 | Inconsistency (rev-2 M5) | MEDIUM | tasks.md:34; quickstart.md:26; plan.md:92, 128-146, 171 | The ADR thresholds are ≥ 17 / ≥ 18. The plan tree says 0001..0017, and ADR 0018 is missing from the plan table. | Use "≥ 19 (0001–0018 + README)" everywhere and add the 0018 row. |
| M12 | Inconsistency (rev-2 M6) | MEDIUM | tasks.md:119 (T058) | linux-smoke was committed in `10a54b7c27`, but T058 is still unchecked. | Run the T058 proof and check the task. |
| M13 | Coverage (rev-2 M7) | MEDIUM | spec.md:174-175 | No test covers opening a legacy non-SDK project. | Add it to T051 or T064. |
| M14 | Ambiguity (rev-2 M8) | MEDIUM | spec.md:183; T061 | "Stops promptly" has no bound. | Use ≤ 5 s and assert it in T061. |
| M15 | Underspecification (rev-2 M9) | MEDIUM | T124; quickstart.md:78 | The install check runs in the builder image, not in a clean environment. | Add a clean-install profile. |
| M16 | Proof missing (rev-2 M10) | MEDIUM | tasks.md:32 (T011), 213 (T120) | Neither task has a "→" proof. | Add proofs. |
| M17 | Ambiguity (rev-2 M11) | MEDIUM | constitution.md:62 | "The quarantine list shrinks over time" conflicts with the per-suite ratchet. | Clarify it in a constitution PATCH. |
| L1 | Hygiene | LOW | `scripts/tools/__pycache__/mdedit.cpython-312.pyc` (committed) | A bytecode cache is committed and not ignored. | `git rm` it and add `__pycache__/` to `.gitignore`. |
| L2 | Duplication / traceability | LOW | tasks.md:79, 83, 236 | T133 was created and checked in the same commit that implements it, and it overlaps T038 (both describe `TestHost`). Dependencies do not mention T132 or T133. | Merge T133 into T038, or state the split. Add T132 and T133 to Dependencies. |
| L3 | Inconsistency | LOW | ADR 0010:16; research.md:30; `spikes/roslyn-publicizer/*.csproj` | The ADR says the spike used "Krafs.Publicizer 2.3.2". The spike used 2.3.0, and 2.3.2 is the product pin. | Write "2.3.0 (spike); 2.3.2 pinned". |
| L4 | Inconsistency | LOW | plan.md:75-76 | The text says "the two justified deviations"; Complexity Tracking has 6 rows. | Say "the deviations". |
| L5 | Underspecification | LOW | contracts/smoke-test.md; T104 | Weston headless has no seat, so GTK logs `Gdk-CRITICAL gdk_seat_get_keyboard` on Wayland. A smoke contract that treats CRITICAL log lines as failure would break. | State that the pass criterion is the exit code, or filter this message. |
| L6 | MADR (rev-2 L1) | LOW | ADR 0012, 0017 | Both lack "Considered Options". | Add the section. |
| L7 | Inconsistency (rev-2 L2) | LOW | research.md:89; ADR 0008:17,26; plan.md:190 | "B35" is undefined. | Define it or remove it. |
| L8 | Stale IDs (rev-2 L3) | LOW | `MonoDevelop.Core.csproj:3` ("T028-T040"); `scripts/debug.sh:20` ("T024") | These IDs are out of date. | Use T033–T037 and T025. |
| L9 | Constitution I wording (rev-2 L5) | LOW | quickstart.md:24-26, 54 | These commands run without `./scripts/pm`. | Wrap them in `./scripts/pm bash -lc`. |
| L10 | Reproducibility (rev-2 L6) | LOW | ci.yml:32,38 | Actions are pinned by tag. | Pin them by SHA. |
| L11 | Duplication (rev-2 L7) | LOW | FR-016/SC-009; T109/T129 | Both pairs duplicate each other. | Keep SC-009 and make T129 the gate. |
| L12 | Traceability (rev-2 L8) | LOW | tasks.md:75 (T037 → T046) | The proof points to the wrong task. | Change it to T041. |

**Overflow**: none. There are 34 findings, under the limit of 50.

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 clean-clone-build-in-container | Yes | T001, T018–T024, T031 | Fresh clone builds with 0 errors (verified); host path M5 |
| FR-002 core-on-net10 | Yes | T033–T037, T132, T042–T046, T049, T059, T062 | T132 open (C2, H3) |
| FR-003 detect-dotnet-sdks | Yes | T042, T044 | |
| FR-004 addin-discovery | Yes | T037, T038, T133, T050, T062 | CecilReflector in place |
| FR-005 mdtool-build-clean-select | Yes | T058–T064 | T059 in progress, uncommitted (H1) |
| FR-006 gtk3-x11-wayland | Yes | T066, T070–T086, T103, T104 | Wayland unblocked (verified) |
| FR-007 edit-build-navigate-csharp | Yes | T085, T088, T089, T103, T105 | |
| FR-008 debug-net10 | Yes | T110–T114 | |
| FR-009 git-status-diff-history | Yes | T102 | |
| FR-010 nuget-manage | Yes | T100 | |
| FR-011 unit-test-runner | Yes | T101 | |
| FR-012 exclusions-listed | Yes | T013, T014, T108 | Complete |
| FR-013 flatpak-desktop-entry | Yes | T120–T124 | |
| FR-014 structured-logs | Yes | T125 | |
| FR-015 idempotent-scripts | Yes | T024, T114, T115, T123 | Lint green |
| FR-016 no-vulnerable-deps-no-dead-feeds | Yes | T019, T020, T052, T127 | |
| FR-017 vendored-with-origin | Yes | T066–T068, T110 | |
| FR-018 localizable | Yes | T047, T048 | |
| SC-001 fresh-clone-zero-manual-steps | Yes | T027, T031 | M5 |
| SC-002 tests-run-quarantine-≤15% | Yes | T040, T041, T057, T107 | 860 cases discovered; M6, M17 |
| SC-003 coverage-60/40-ratchet | Yes | T055, T056 | |
| SC-004 mdtool-samples-in-pipeline | Yes | T064, T065, T115, T116 | |
| SC-005 smoke-x11-wayland-≤10s | Yes | T103, T104, T109, T129 | L5 |
| SC-006 debug-scenario | Yes | T113 | |
| SC-007 pipeline-≤15min | Yes | T115, T119 | |
| SC-008 flatpak-clean-install | Yes | T124 | M15 |
| SC-009 zero-high-critical-vulns | Yes | T052, T127 | |

**Edge cases**: all six are covered, **except** opening a legacy project (M13).

## Constitution Alignment Issues

- **IV**: a multi-task commit is not covered by Complexity Tracking, and the recorded justification is contradicted by HEAD (C1).
- **V (NON-NEGOTIABLE)**: a behaviour change landed without a test (C2). The formatting gate is fail-open (H2). No task schedules the legacy
  reformatting (M2).
- **Development Workflow**: implementation continues while analyze reports CRITICAL issues, and the waiver has no containment (H1).
- **Governance**: the amendment's SemVer category is debatable (M3).

## Unmapped Tasks

These tasks map to constitution or documentation standards rather than to an FR or SC, and are justified:
T030, T069, T083, T126, T128 and T133 (test infrastructure; it overlaps T038, see L2).

## Metrics

- Total Requirements: **27** (18 FR + 9 SC)
- Total Tasks: **133** (37 checked: T001–T015, T017–T029, T033–T040, T133)
- Coverage: **100%** (27/27 requirements have at least one task)
- Ambiguity Count: **2** (M14, M17)
- Duplication Count: **2** (L2, L11)
- Findings: 2 CRITICAL, 3 HIGH, 17 MEDIUM, 12 LOW
- **Critical Issues Count: 2**

## Next Actions

- **CRITICAL issues exist.** T016 stays open. Before any further implementation commit, including the in-flight T059 work:
  1. C2: add `PropertyTests.DeserializeRejectsDtd` (inside T132) and extend Complexity Tracking row 6.
  2. C1: amend Complexity Tracking row 3, or constitution IV, and enforce one task per commit with a
     `Task:` trailer in `scripts/git-commit`.
  3. H1: add containment to row 4. H2: pin `MD_UPSTREAM_BASE` in `lib.sh` and make the check fail if the base is missing. H3: T132 next.
- MEDIUM and LOW: edit tasks.md, plan.md, contracts/scripts.md, ADR 0010/0011 and `.gitignore` directly. M3 and M17 need a
  constitution PATCH, or MAJOR for M3, with an ADR note.
- After the fixes, re-run the consistency analysis (revision 4) and overwrite this file.

Would you like me to suggest concrete remediation edits for the top 5 issues? (None were applied; this analysis is read-only.)
