# Final consistency analysis (T131)

- **Date**: 2026-09-25
- **Inputs**: `docs/constitution.md` 1.4.0 and the amendment log in ADR 0001;
  `specs/001-linux-dotnet10-migration/{spec,plan,research,data-model,quickstart,tasks}.md` and `contracts/*.md`;
  `docs/adr/0001–0026` and their index; `docs/BREAKING-CHANGES.md`, `docs/architecture.md`, `docs/future-work.md`,
  `docs/linux/*`, `README.md`, `CONTRIBUTING.md`; `docs/evidence/M0–M9`, including revision 5 of
  `docs/evidence/M1/analyze.md` (0 CRITICAL); `scripts/*`, `.github/workflows/*`, `main/msbuild/Linux/*`; and the
  118 commits of `git log refs/heads/main..HEAD`.
- **Repository state**: HEAD `ccac779958` ("Tick T130 and T016"), the tip of `dotnet-linux-migration`, with a clean
  working tree. The fork point is `ba01d2d6d3`. The last full `scripts/ci.sh` run passed at `f7f2366f43` in 569 s
  (commit `901f45e2a1`, `docs/evidence/M9/acceptance.md`).
- **Method**: read-only review. Every command ran in the dev container (`./scripts/pm bash -lc '…'`) or was a
  read-only `ls`/`cat`/`grep`/`find` on the checkout. `dotnet build` and `dotnet test` were **not** run. Test,
  coverage and timing figures come from the evidence files and commit messages and are **not re-verified**.
- **Container checks for this report**:
  - `./scripts/lint.sh` exited with **0** (shellcheck on 20 scripts, plus actionlint).
  - Commit identity, trailers, warning baselines, line endings and repository hygiene: see sections 3 and 5.

**CRITICAL issues: 0**

## Resolution of revision-5 findings

| Rev-5 | Status at `ccac779958` | Evidence / remainder |
|---|---|---|
| H1: gate integrity | **Resolved** | `scripts/git-commit:11-13, 64-69` reads `HEAD:docs/evidence/M1/analyze.md` and accepts `Analyze-fix:`. The Complexity Tracking row names the commits, but with pre-rewrite hashes (new H1) |
| H4: SC-003 evidence | **Resolved** | T056 is checked; `coverage-baseline.txt` has Core, Ide and `total` lines (66.5 / 21.0 / 29.0); SC-003 amended by 1.4.0. The plan row still quotes 52.4% (M3) |
| M1: scripts contract | **Open** | Now M5 |
| M3: compiled-file count | **Resolved** | `scripts/tools/compiled-files.sh` exists; T150 uses it |
| M5: SYSLIB0006, CA1416 | **Resolved** | No SYSLIB0006 in any baseline; CA1416 is left in 2 projects |
| M6–M9: plan staleness | **Open** | Now M3 |
| M13: clean-install profile | **Resolved** | T124 installs into a fresh Flatpak installation (`docs/evidence/M7/README.md`) |
| M17: PCL decision | **Open** | Now M10 |
| M18: ratchet overwrite | **Open** | Now M11 |
| M19: M4 evidence | **Resolved** | `docs/evidence/M4/README.md` exists |
| M20: "15 submodules" | **Withdrawn** | `refs/heads/main` has 15 gitlinks and 15 `.gitmodules` entries; T004 was right |
| M21: artifact name | **Resolved** | `ci.yml:65` names `monodevelop-ci-<version.config>+<sha>`; T115 and T116 are checked |
| L4, L5, L8, L14, L16 | **Open** | Now L6, L7, L9, L1 |
| Others | Superseded | Folded into the findings below where still relevant |

## 1. Traceability

| Requirement | Tasks | Evidence | Status |
|---|---|---|---|
| FR-001 clean-clone build in the container | T001, T018–T032, T130 | `M2/fresh-clone.log`, `M9/acceptance.md` (SC-001) | Met (warm image; cold run on a hosted runner waits for T119) |
| FR-002 core on .NET 10 | T033–T054, T059, T062, T132, T133 | `M3/README.md`, `M4/README.md` | Met; builder/evaluator parity gaps T140, T141 open |
| FR-003 detect SDKs | T042, T044 | `M3/README.md` (`DotNetCoreTargetRuntimeTests`) | Met; T141 (SDK resolver, `global.json` rules) open |
| FR-004 add-in discovery | T037, T038, T050, T133 | `M3/README.md` | Met |
| FR-005 mdtool build/clean/select | T058–T065 | `M3/mdtool.md`, `ci.sh` step `mdtool-smoke`, `MdtoolContractTests` | Met |
| FR-006 GTK3 on X11 and Wayland | T066–T084, T103, T104, T109, T136, T137 | `M5/README.md`, `T103`/`T104`/`T109` screenshots | Met; T150 (344 port-helper uses) and T144 open |
| FR-007 edit, build, navigate C# | T085, T088, T089, T105, T138, T146, T147 | `M5/README.md` (T088, T089, T105, T138, T146, T147) | Met; T149 open |
| FR-008 debug .NET 10 | T110–T114, T153 | `M5/README.md` (T112–T114, T153), SC-006 | Met |
| FR-009 Git status/diff/history | T102 | Commit `c4b6abe5f1`; `VersionControl.Git.Tests` in the gate | Met; **no stored evidence** (M9) |
| FR-010 NuGet | T100 | `M5/README.md` § T100, `T100-nuget.png` | Met |
| FR-011 NUnit/xUnit/MSTest | T101 | `M5/README.md` § T101, two screenshots | Met |
| FR-012 exclusions listed | T013, T014, T108, T148, T152 | `BREAKING-CHANGES.md`, ADR 0017 | Met; PCL/netstandard1.x not listed (M10) |
| FR-013 Flatpak with desktop entry | T120–T124 | `M7/README.md` | Met in a clean container; not tried on a desktop session |
| FR-014 structured logs | T125, T126 | ADR 0023; commits `330e5cda75`, `167e7d6398` | Met; **no stored output** of the T125 proof (M9) |
| FR-015 idempotent scripts | T024, T115, T123, T145 | `M2/README.md`, `M6/README.md`, lint exit 0 | Met |
| FR-016 no vulnerable deps, no dead feeds | T019, T020, T052, T127 | `M8/security.md`; `NuGet.config` has one source | Met |
| FR-017 vendored with origin | T066–T068, T110, T148 | `main/vendor/*/UPSTREAM.md` (4 of 4) | Met |
| FR-018 localizable | T047, T048 | `M3/README.md` | Met |
| SC-001 | T027, T031, T130 | `M2/fresh-clone.log`, `M9/acceptance.md` | Met (warm cache) |
| SC-002 | T040, T041, T107, T134, T135 | `M4/quarantine.md`: 52 cases, max 4.4% per suite | Met; counts in acceptance stale (M2) |
| SC-003 (amended) | T055, T056, T129 | `M4/coverage-baseline.txt`, `M8/README.md` (Core 66.83%) | Met as amended |
| SC-004 | T064, T065, T115 | `ci.sh` `mdtool-smoke` | Met |
| SC-005 | T103, T104, T109, T129 | `M5/README.md`, `M8/startup.md` (2.2 s cold) | Met |
| SC-006 | T113, T153 | `NetCoreDbgTests`, `gui-smoke-debug` | Met |
| SC-007 | T115, T145, T119 | `M6/README.md` (536 s), 569 s at `f7f2366f43` | Met locally; hosted run is T119 |
| SC-008 | T124 | `M7/README.md` | Met |
| SC-009 | T052, T127 | `M8/security.md`, `audit.sh` | Met |

No requirement is without a task. FR-009 and FR-014 have no stored evidence file (M9).

## 2. Tasks

`tasks.md` has 153 tasks (T001–T154; T139 was never defined): **141 checked, 12 open**.

**Checked tasks.** Every checked task from T040 on has at least one commit with its ID in a `Tasks:` line,
except T133, which only a commit body names. T001–T039 come from the 10 commits made before the trailer rule
(constitution 1.2.0). Those commits name their milestone, and some their task range, in the subject or body. Spot check with
`git log refs/heads/main..HEAD -E --grep "Tasks: .*Tnnn"`:

| Task | Commit(s) | Task | Commit(s) | Task | Commit(s) |
|---|---|---|---|---|---|
| T045 | `73d811bfd1` | T085 | `e08fa72a89` | T129 | `3b00191cee`, `4ffe530bdf`, `e5157bc80e` |
| T052 | `1aba3b7592` | T100 | `9e27ab8280` | T138 | `f12dbdd739` |
| T056 | `9b98f7cde3`, `14a4984704` | T102 | `c4b6abe5f1` | T145 | `9f8090bf40` |
| T061 | `076f03f33c`, `89b5cda2ca` | T108 | `c697bdea21`, `94ea5f5192` | T148 | `310d8aa661` |
| T066 | `e8b1fdbaa5` | T113 | `7e1cc35027` | T152 | `f7f2366f43` |
| T069 | `750709a231`, `9d75bd1e2b` | T124 | `898d0d0e29` | T153 | `ff147327a9` |
| T076 | `fb3165a409`, `ac361dcbe2` | T125 | `330e5cda75` | T033 | `64ef825574` (subject "T033-T037") |

All 21 match, and each has the evidence its proof names. T109 is checked although its proof text still asks for
0 port helpers; ADR 0011 (amendment of 2026-09-24) moved that metric to T150 (L3).

**Open tasks.**

| Task | What is left | Documented | Release blocker? |
|---|---|---|---|
| T119 | Hosted CI run and its evidence | Yes (needs push authorization) | No, but it must be green before the tag (M7) |
| T131 | This analysis and the release notes | This file | Closes when this file is committed |
| T134 | net4x and PCL/Xamarin/netstandard1.x fixtures (27 quarantined cases) | Quarantine record, acceptance "Known gaps" | No (test debt; product support decision is M10) |
| T140 | Builder ignores add-in MSBuild import search paths (6 cases) | Yes | No; affects third-party add-ins, should be a known issue (M6) |
| T141 | .NET SDK resolver not loaded by the IDE evaluator | Yes | No; known issue (M6) |
| T142 | Mono-only and excluded-add-in Core tests | Yes | No |
| T143 | Core fixtures that need NuGet packages | Yes | No |
| T144 | Intermittent GTK test-host crash (about 1 in 20) | Yes; T153 found a probable cause | No (test host only) |
| T149 | Source generators from project references | Yes, ADR 0025 | No; known issue (M6) |
| T150 | ADR 0011 port helpers to 0 | Yes, ADR 0011 amendment | No |
| T151 | Ide.Tests multi-target fixture | Yes | No |
| T154 | F# language binding | Yes, `BREAKING-CHANGES.md` | No (F# deferred by the spec's assumptions) |

No open task is an undocumented gap, and none blocks the release.

## 3. Constitution compliance

- **Identity**: all 118 commits have author and committer `Vinicius Donatto Morgado` with the GitHub no-reply
  address (one line from `git log --format='%an|%ae|%cn|%ce' | sort | uniq -c`).
- **Trailers (IV)**: 108 commits have a `Tasks:` line (6 of them `Tasks: none`); the 10 without are the first 10
  commits, made before 1.2.0. 32 commits name several tasks: the 21 made after the `Coupled:` rule
  (`9ca4038e63`) all have a `Coupled:` line; the 11 before it do not and are covered by Complexity Tracking row 3,
  whose hashes are stale (H1). 4 commits put `Tasks:` outside the final trailer block (L1). One behaviour commit
  has `Tasks: none` (M8).
- **Warning baselines (V, VII)**: none of the files in `main/msbuild/Linux/warning-baselines/` contains CA2100, CA23xx,
  CA3xxx, CA5xxx, SYSLIB0011, SYSLIB0050 or SYSLIB0051. The SYSLIB IDs present are 0001, 0003, 0004, 0005, 0014, 0018
  and 0037, none of them a serialization or security rule. `scripts/warnings-baseline.sh:26` refuses the forbidden set.
- **Line endings (V)**: 1,426 fork-modified pre-fork files outside `main/vendor`. Comparing
  `git diff --numstat refs/heads/main HEAD` with `--ignore-cr-at-eol` gives equal counts for 1,405. The other 21
  differ by 2–34 lines, for example `MonoDevelop.Core/Gettext.cs` (62 vs 28: 17 LF lines of a mixed-ending file are
  now CRLF, `edb27fd4fa`, before the guard), `FileServiceTests.cs` (41 vs 25) and `DocumentSwitcher.cs` (74 vs 66).
  No file has a whole-file rewrite (L2).
- **Lint**: `./scripts/lint.sh` exit 0.
- **Coverage (V, SC-003 as amended)**: the ratchet file holds Core 66.5, Ide 21.0, total 29.0 against measured
  66.83 / 21.29 / 29.25 (`M8/README.md`). Core ≥ 60% holds, and there is no global target any more. `test.sh`
  fails on a drop, but `--update-baseline` still accepts a lower value (M11).
- **Quarantine (SC-002)**: 52 rows in `M4/quarantine.md` (Core 31, DotNetCore 12, Ide 8, PackageManagement 1),
  about 1.3% of the ~4,030 discovered cases. The largest suite share is DotNetCore at 4.4%, well under 15%. Every
  row has a reason, owner, date and task.
- **Tests never deleted (V)**: `f7f2366f43` removed 2 quarantined Ide.Tests cases and the template test classes
  listed in `BREAKING-CHANGES.md:130` together with the XML templates they tested. The new
  `DotNetNewTemplateTests` (19) and `DotNetNewTemplatingTests` (5) replace them. This is justified, but there is no
  Complexity Tracking row (M12).
- **Lock files (III)**: 80 `packages.lock.json`, `RestoreLockedMode` in CI, cache key on lock files.

## 4. Documentation consistency

- **ADR index**: 26 files, 26 rows, no broken link. ADR 0012 and 0017 still lack "Considered Options" (L6).
- **BREAKING-CHANGES**: covers `Main.sln` and the autotools files (lines 18–25, 48, 60), the submodules (14–17),
  the old templates (92–133), VB.NET (39, 51, 98), and macOS/Windows (8, 57, 59, 101). The move of the constitution
  to `docs/` is a development-process change, recorded in ADR 0001 (1.3.1) and `CONTRIBUTING.md`, and rightly
  not listed there. Known limitations of the release are missing (M6).
- **README and setup**: every `scripts/…` command in `README.md`, `CONTRIBUTING.md`, `docs/linux/*` and
  `quickstart.md` exists, and the documented options (`run.sh --headless`, `debug.sh ide|mdtool`,
  `build.sh -c/--check`, `test.sh --all`) match the scripts. The README opens with the upstream "archived"
  notice (M1).
- **Smoke-test contract**: `SmokeTest.cs` reads 8 `MD_SMOKE_*` variables; the contract documents 3 (`OPEN`,
  `GOTO`, `DEBUG`) (M4).
- **quickstart.md**: the commands match the scripts and paths (spikes, `linux-smoke`, `inventory.sh --linux-sln`,
  the Wayland recipe). The § M5a command differs from the verified one (L9).
- **Stale statements**: see M2, M3 and L3–L8. The largest: 37 commit hashes in `plan.md` and the evidence files
  point to commits that are not in the branch history (H1).

## 5. Repository hygiene

- `git grep -iE` over HEAD for the tool and vendor names that must not appear in the repository (the list is
  kept outside it) returns **nothing**, with `main/external` excluded. The same pattern finds nothing in the 118
  commit messages or in the paths they touch.
- No committed file contains a host home path or a personal e-mail. `docs/evidence/M0/T009-legacy-baseline.md` and
  `T010-dotdevelop.md` mention `/home/dev/…`, the home of the container user; this is not personal data.
- No `.pdb` file is tracked outside `main/external`.

## 6. Findings

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| H1 | Traceability (Governance) | HIGH | `plan.md` Complexity Tracking rows 3–6 (23 hashes); `docs/evidence/M1/analyze.md` (15); `M0/README.md`, `M0/inventory.md` (`f3064b6182`); `M2/fresh-clone.log`; `M3/mdtool.md`; `M4/quarantine.md`; `M5/README.md`, `M5/T106-main-loop-stall.md`, `M5/inventory-linux-sln.md`; `M7/README.md` | 37 commit hashes predate the history rewrite of 2026-09-24. None is an ancestor of HEAD. They resolve only as unreachable objects in the local store, so a clone will not have them. The deviation record that the constitution requires cannot be checked. (The 50 DotDevelop hashes and 1 Xwt upstream hash are external and fine.) | Before the push, and before the objects are pruned, map each hash to its rewritten commit by subject (`git log -1 --format=%s <old>`, then match it in `git log --format='%h %s' refs/heads/main..HEAD`) and update `plan.md` and the evidence. Add a note to M1 revision 5 that its hashes predate the rewrite. |
| M1 | Documentation (release) | MEDIUM | `README.md:1-8, 18, 36-42, 76-84` | The README opens with "has not been built nor maintained since January 2020 and has been archived", sends bug reports to Visual Studio for Mac, and lists Gitter and ximian mailing lists. It also says the constitution is under `specs/`. | Replace the notice with a short fork status and release line; drop or mark the upstream contact lists; link `docs/constitution.md`. |
| M2 | Stale evidence | MEDIUM | `M9/acceptance.md` SC-002 and SC-007; `M4/quarantine.md:5-7` | Written before `f7f2366f43` and `ff147327a9`: it says 54 quarantined cases (Ide 10) and "4 GUI smokes". The record now has 52 rows (Ide 8), and `ci.sh` runs 5 GUI smokes (`gui-smoke-debug` added). | Update both files: 52 / Ide 8, 5 GUI smokes. |
| M3 | Inconsistency (plan) | MEDIUM | `plan.md:75-76, 92, 112, 168-181, 211` | "The two justified deviations" (the table has 9 rows); `0001..0017`; `Addin.props, Test.props` (the files are `Common.props`, `Common.targets`, `OutputLayout.targets`, `Test.targets`, `BuildVariables.targets`); the milestone table stops at T131 and omits T132–T154; the M4/M5 row says "product total 52.4%", which contradicts 29.25% and the amended SC-003. | Refresh these lines together with H1. |
| M4 | Contract drift | MEDIUM | `contracts/smoke-test.md`; `SmokeTest.cs:42-104`; `scripts/ci.sh:76, 96, 112, 131` | `MD_SMOKE_NEW_PROJECT`, `MD_SMOKE_NEW_FILE` (T152, used by `gui-smoke`), `MD_SMOKE_OUT` (used by every CI smoke), `MD_SMOKE_NO_BUILD` and `MD_SMOKE_TIMEOUT` (watchdog, default 600 s) are not in the contract. | Add items 11–13 for them. |
| M5 | Contract drift | MEDIUM | `contracts/scripts.md` | Missing `audit.sh`, `check-assemblies.sh`, `format.sh`, `test-flatpak.sh`, `warnings-baseline.sh` and `netfx-refasm.sh`; the `test.sh` options `--no-build`, `--parallel` and `--update-baseline`; the `git-commit` rules (`Coupled:`, `Format-only:`, `Analyze-fix:`). | Add the rows. |
| M6 | Release notes | MEDIUM | `docs/BREAKING-CHANGES.md` (the release notes, `release.yml:76-89`) | Nothing tells users about the limitations of this release: add-in MSBuild import paths ignored by the builder (T140); the IDE evaluator skips the .NET SDK resolver (T141); false editor errors for generators from project references (T149); PCL/Xamarin/netstandard1.x and .NET Framework fixtures (T134); Flatpak not tried on a desktop session. | Add a "Known issues" section before tagging. |
| M7 | Release process | MEDIUM | `.github/workflows/{ci,release,codeql}.yml`; T119 | No workflow has run on a hosted runner yet. The first run of `release.yml` would be the tag itself. It re-runs `ci.sh` as a gate, so a failure publishes nothing, but the tag would already be pushed. | Push the branch first, get a green `ci.yml` run, record it (T119), then tag. |
| M8 | Traceability (IV) | MEDIUM | `3656ebf5c7` (`Tasks: none`) | The GtkSharp toggle-reference workaround (ADR 0024, 11 files, `ToplevelReferenceTests`) is a behaviour change without a task. `Tasks: none` is meant for chores. | Add a checked task (the next free ID) that names the commit, or a Complexity Tracking row. |
| M9 | Evidence (DoD) | MEDIUM | FR-009 (T102), FR-014 (T125) | No stored output. T102's proof is tests in the gate. T125's proof command (`MD_LOG_FORMAT=json … \| jq`) has no recorded output. | Store a short run of each under `docs/evidence/M8/`. |
| M10 | Coverage / compatibility (VI) | MEDIUM | `data-model.md:35-36`; `M4/quarantine.md`; `BREAKING-CHANGES.md` | The quarantine uses `legacy-fixture` (17), `network` (15), `excluded` and `SDK-change`, which the data model does not define (it lists `GTK2`, which is unused). Whether PCL/Xamarin/netstandard1.x projects are supported is still undecided and not in BREAKING-CHANGES. | Extend the data model; record the retirement (or the plan) in BREAKING-CHANGES. |
| M11 | Constitution V (ratchet) | MEDIUM | `scripts/test.sh:110-112` | `--update-baseline` copies the current values even when they are lower. | Refuse a lower value unless an explicit override is given. |
| M12 | Constitution V (tests kept) | MEDIUM | `f7f2366f43`; `BREAKING-CHANGES.md:130`; `M4/quarantine.md:140-142` | The template tests were deleted together with the template feature. They are replaced by new tests, but no Complexity Tracking row records this. T142 plans the same for `MakefileTests`. | Add one Complexity Tracking row: tests of removed features go with the feature, and the replacement tests are named. |
| L1 | Tooling | LOW | `d5dbc51358`, `af8781f969`, `531aba2910`, `7e1cc35027` | `Tasks:` is not in the final trailer block, so `%(trailers:key=Tasks)` is empty. `7e1cc35027` was made after `git-commit:14` documented the placement, which is still not enforced. | Check the placement with `git interpret-trailers --parse`. |
| L2 | Line endings | LOW | 21 files (section 3) | Partial CR-only changes of 2–34 lines. The largest are mixed-ending files that were normalised before the guard. | Optionally restore them in one `Format-only:` commit. |
| L3 | tasks.md | LOW | `tasks.md:25, 32, 34, 199, 237` | T109's proof still asks for 0 port helpers (moved to T150); T013 says "0001–0017"; T011 and T120 have no "→" proof; T139 is unused. | Update the wording; note that T139 is unused. |
| L4 | Stale evidence | LOW | `M4/README.md:13-18` | "Product total 52.4% (target ≥ 40%)" was measured on 2 test assemblies and against a target that has since been dropped. | Add a pointer to `M8/README.md` and SC-003 (1.4.0). |
| L5 | Gate scope | LOW | `scripts/git-commit:64-69` | The analyze gate reads only `docs/evidence/M1/analyze.md`, not this file. There is no effect today, because both report 0 CRITICAL. | Say in CONTRIBUTING that M1 stays the gate input, or read the newest analysis. |
| L6 | ADRs | LOW | ADR 0021:12; ADR 0012, 0017; ADR 0008:17, 26 | ADR 0021 cites SC-004 for the first release (SC-004 is the CLI build); ADR 0012 and 0017 have no "Considered Options"; "backlog B35" is undefined. | Fix the reference, add the sections, define or drop B35. |
| L7 | Reproducibility | LOW | `.github/workflows/*.yml` | Actions are pinned by tag (`@v4`, `@v3`). | Pin them by SHA; Dependabot keeps them current. |
| L8 | Documentation | LOW | `docs/linux/setup.md:3, 35`; `scripts/debug.sh:20`; `CONTRIBUTING.md` | "Is being migrated", "(available from milestone M5)"; netcoredbg "added in M2, task T024" (it is T025); CONTRIBUTING does not describe `Coupled:`, `Format-only:` or `Analyze-fix:`. | Refresh the wording. |
| L9 | quickstart | LOW | `quickstart.md:26, 60` | § M5a uses `dotnet run --project …/Gtk3Test`, not the verified command; § M1 says "≥ 17" ADRs (there are 26). | Use the verified command; update the count. |
| L10 | Plan scale | LOW | `plan.md:57` | "164 in `Main.sln`" describes the pre-fork solution, which no longer exists. | Add "(pre-fork)". |

## Metrics

- Requirements: **27** (18 FR + 9 SC), all with tasks; 27/27 met (SC-003 as amended; SC-001 and SC-007 measured
  locally, with the hosted run pending).
- Tasks: **153** (141 checked, 12 open, none a release blocker).
- Commits reviewed: **118** (all with the no-reply identity).
- Findings: **0 CRITICAL, 1 HIGH, 12 MEDIUM, 10 LOW**.

## Verdict

**Ready for `v0.1.0-linux`**, pending the maintainer's authorization to push and tag, under these conditions:

1. **Before the push**: fix H1. It takes minutes while the old objects are still in the local store, and it cannot
   be done from a clone afterwards. It is best done together with M3.
2. **Between the push and the tag**: a green hosted `ci.yml` run, recorded as T119 (M7).
3. **Before the tag (recommended)**: M1 (README), M6 (known issues in the release notes) and M2 (stale counts).
   These are documentation-only commits.

All the other MEDIUM and LOW findings can follow the release. No open task blocks it. This analysis is read-only,
and it closes T131 once committed, together with the release-notes changes of condition 3.
