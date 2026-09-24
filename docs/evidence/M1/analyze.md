# Consistency analysis: revision 5 (T016)

- **Date**: 2026-09-23
- **Inputs**: the constitution 1.2.1 (now `docs/constitution.md`), `specs/001-linux-dotnet10-migration/{spec,plan,research,data-model,quickstart,tasks}.md`,
  `contracts/*.md`, `docs/adr/0001–0019`, `docs/BREAKING-CHANGES.md`, `docs/evidence/M0–M6`, and revision 4 of this file.
- **Repository state**: HEAD `d61af9e5b2` ("MonoDevelop.Ide.Gtk3.Tests: tests for the M5b behaviour changes, run under Xvfb").
  Since revision 4 (`82da01c728`) there are 6 commits:
  - `dc4ece0a56`: CI gate, T115 and T116.
  - `e0dbd904a2`: `WorkspaceObject.Dispose`, T135.
  - `534e664f05`: format-only change to new tests, `Tasks: none`.
  - `e289c26bc2`: restores the line endings of 5 legacy files, `Format-only`.
  - `1f70505bf3`: the revision-4 process fixes, T016.
  - `d61af9e5b2`: the Ide port tests, T016 and T136.

  The working tree holds uncommitted work from other sessions: Core coverage tests, GTK3 ports of Ide widget files,
  `scripts/tools/gtk3-codemod.py`, and **revision 4 of this file, which was never committed** (see H1). That work is out of
  scope. Every file with local changes was read from HEAD (`git show HEAD:<path>`). The specs, scripts, workflows and warning
  baselines have no local changes.
- **Method**: read-only review. Every command ran in the dev container (`./scripts/pm bash -lc '…'`), or was a read-only git
  or grep command; `dotnet build` and `dotnet test` were **not** run.
- **Container checks for this report**:
  - `./scripts/lint.sh` exited with **0** (shellcheck on 17 scripts, including `ci.sh` and `git-commit`, plus actionlint).
  - The 6 new commits use the no-reply identity. Every one has a `Tasks:` line, and the multi-task ones have a `Coupled:` line.
  - A grep for `CA2100|CA23xx|CA3xxx|CA5xxx` over the 83 files in `main/msbuild/Linux/warning-baselines/` found **no match**.
  - Line endings: for every fork-modified pre-fork `.cs` file outside `main/vendor`, I compared
    `git diff --numstat ba01d2d6d3 HEAD` with `--ignore-cr-at-eol`. **None** has a line-ending rewrite. The 5 repaired files
    now differ from upstream by 4–30 lines, and each count is the same with or without whitespace.
  - `main/external/{xwt,mono-addins,vs-editor-api}` no longer exist. `docs/evidence/M4/quarantine.md` at HEAD has 75 rows.
  - The test and coverage figures (850 passed, 0 failed, 9 skipped; Core 57.8%; the 7 T136 tests reported green) come from
    `docs/evidence/M6/README.md` and the commit messages. They are **not re-verified**.

**CRITICAL issues: 0**

## Resolution of revision-4 findings

IDs are kept from revision 4. Resolved IDs are retired, and new findings get new numbers.

| Rev-4 | Status in HEAD | Evidence / remainder |
|---|---|---|
| C1: multi-task commits not recorded | **Resolved** | Complexity Tracking row 3 lists the 7 multi-task commits made under 1.2.0 (`d9c227a5c0` … `826fdc40a4`). Constitution 1.2.1 IV and `scripts/git-commit:38-41` require a `Coupled:` line. The 4 commits that were plausibly coupled (`37b97d3037`, `d7eafe962b`, `33b3edceb6`, `c379e20b7f`) are still not named (L15) |
| C2: M5b without tests | **Resolved** | T136 adds `main/tests/MonoDevelop.Ide.Gtk3.Tests` to the Linux sln. It has 7 tests: 2 for `SyncContext.AsyncDispatch` (the handler runs on another thread and does not block the caller) and 5 for `Gtk3ExposeEvent`/`SizeRequest` (with and without a window, context restore, natural size). `test.sh` uses `xvfb-run` when there is no display. The M5b preamble in tasks.md:147-148 requires every port task to extend these tests. Complexity Tracking records that `d7eafe962b` shipped behaviour before its tests |
| C3: line-ending rewrites | **Resolved** | `e289c26bc2` restored all 5 files, and the whole-tree scan above finds none left. `mdedit.py` keeps each line's ending (`restore_line_endings`). `git-commit:43-53` rejects such rewrites outside `Format-only:` commits. There is a Complexity Tracking row |
| H1: implementing during CRITICAL | **Partial** | Row 4 now covers everything up to `82da01c728`. `git-commit:55-62` refuses task commits other than T016 while analyze reports CRITICAL. The remainder is now H1 (below) |
| H2: governance amendments | **Resolved** | ADR 0001 § "Amendments to the constitution" covers 1.1.0, 1.2.0 and 1.2.1, with the motivation, the impact and why each is MINOR or PATCH. T016's text now says 1.2.0. The PATCH category of 1.2.1 is debatable (L17) |
| H3: ADR 0011 vs shim | **Resolved** | The ADR 0011 amendment allows thin, stateless helpers, names them, requires tests (T136), and sets the removal metric `grep -rlE 'Gtk3ExposeEvent\|…' main/src` → 0 by the end of M5c. T109 reports it |
| H4: SC-003 measure and M4/M5 overlap | **Partial** | SC-003 (spec.md:247-252) now defines "whole Linux solution". The coverlet `<Exclude>` in `Test.targets` drops vendored code and tests, and `test.sh` writes a `total` line. There is a Complexity Tracking row for the overlap. The remainder is now H4 (below) |
| M1–M18 | **Open** | None of the files involved changed. M6 has grown (T136 is in no milestone row), and so have M1 (the `git-commit` rules and `ci.sh` behaviour are missing from the contract) and M16 (see H1 on `d61af9e5b2`) |
| M19 (T041 gate evidence) | **Partial** | `docs/evidence/M6/README.md` now stores the gate result (850/0/9, Core 57.8%). `docs/evidence/M4/README.md` still does not exist, and `M3/README.md:42` still links to it |
| M20 | **Open** | |
| L1–L12, L14 | **Open** | L1 has grown: T136 was also created and checked in the commit that implements it. L3 has grown: Complexity Tracking now has 10 rows |
| L13 (leftover checkouts) | **Resolved** | The directories are gone |

## Specification Analysis Report

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| H1 | Constitution (Workflow, IV) / gate integrity | HIGH | constitution.md:44-48, 101-103; plan.md Complexity Tracking row 4 ("up to 82da01c728"); `scripts/git-commit:55-62`; commits `dc4ece0a56`, `e0dbd904a2`, `d61af9e5b2`; `git show HEAD:docs/evidence/M1/analyze.md` = revision 3 | (a) `dc4ece0a56` (T115, T116) and `e0dbd904a2` (T135) were committed at 18:21, after revision 4 had reported 3 CRITICAL (18:17) and before the gate existed. Row 4 stops at `82da01c728`. (b) The gate passes any commit whose trailer contains T016. `d61af9e5b2` lists "T016, T136", with `Coupled: T136 is the remediation of … C2 (T016)`. That is a traceability reason, not the "cannot build independently" condition of IV; the two tasks build separately. (c) The gate reads the working-tree `analyze.md`, but revision 4 was never committed: HEAD still holds revision 3 ("CRITICAL issues: 2"). A fresh clone therefore gates on stale data, and T016's evidence for revision 4 is lost. | Extend row 4 to "… up to `e0dbd904a2`". In `git-commit`, accept `Analyze-fix: <finding IDs>` (checked against the IDs in `analyze.md`) for remediation commits, instead of adding T016 to the trailer. Make the gate read `git show HEAD:docs/evidence/M1/analyze.md`. Commit this revision alone as T016. |
| H4 | Inconsistency / coverage (SC-003, M4 gate) | HIGH | plan.md Complexity Tracking row "M5a/M5b started while M4 was open"; `docs/evidence/M4/coverage-baseline.txt` (HEAD: `MonoDevelop.Core 57.6` only); `docs/evidence/M6/README.md` (Core 57.8%); tasks.md:108 (T056 open) | The deviation row justifies the M4/M5 overlap with "T056 is met (Core 62.8%, product total 52.4%)". HEAD does not support this: T056 is unchecked, the committed evidence says 57.6% and 57.8%, and the tests behind 62.8% are uncommitted. The ratchet file also has no `total` line, so the solution-wide half of SC-003 is still not enforced, even though `test.sh` now computes it. | Commit the coverage work together with `./scripts/test.sh --update-baseline` output (Core and `total`) and check T056 with that evidence. Otherwise, reword the row to "T056 open (57.6%); in progress". |
| M1 | Inconsistency (contracts) | MEDIUM | contracts/scripts.md | Unchanged. It is also missing the `git-commit` rules (`Coupled:`, `Format-only:`, the analyze gate, `Analyze-override:`), the `test.sh` Xvfb fallback and `total` line, and the steps of `ci.sh`. | Update the rows (see revision 4). |
| M2 | Coverage gap (constitution V) | MEDIUM | constitution.md:57-58; tasks.md | No task schedules the per-project legacy reformatting, although the `Format-only:` mechanism now exists. | Add the task: one format-only commit per converted project. |
| M3 | Untestable metric | MEDIUM | `scripts/tools/linux-sln-sources.py:17-22`; ADR 0011:29 | The script still ignores `<Compile Remove>`, so it counts 926 excluded Ide files as compiled. | Use `dotnet msbuild -getItem:Compile`. |
| M4 | Underspecification (SC-001) | MEDIUM | tasks.md:57 (T031) | The image build is still not exercised. | Re-run from the host with `PM_REBUILD=1`. |
| M5 | Constitution (VII) | MEDIUM | `warning-baselines/MonoDevelop.Core.Tests.props` (SYSLIB0006); Core CA1416 ×84 | Unchanged. | Regenerate the baseline; triage CA1416. |
| M6 | Inconsistency | MEDIUM | plan.md:173-176 | T038–T041 are in both the M3 and M4 rows. T132–T136 are in no row. | M3 += T132, T133; M4 = T055–T057, T134, T135; M5b += T136. |
| M7 | Inconsistency | MEDIUM | plan.md:154 | W2 is stale. | Update it. |
| M8 | Inconsistency | MEDIUM | plan.md:158-164 | The "Excluded" list is stale. | Link to ADR 0017. |
| M9 | Inconsistency | MEDIUM | plan.md:112 | `Addin.props` and `Test.props` do not exist. | Match the real files. |
| M10 | Inconsistency | MEDIUM | tasks.md:34; quickstart.md:26; plan.md:92, 128-146 | The ADR thresholds and the table stop at 0017. There are 20 files. | "≥ 20"; add the 0018 and 0019 rows. |
| M11 | Coverage (edge case) | MEDIUM | spec.md:174-175 | No test covers "report clearly when it cannot be built". | Add it to T134. |
| M12 | Ambiguity | MEDIUM | spec.md:183 | "Stops promptly" has no bound, and the test uses 60 s. | Write the bound into the spec. |
| M13 | Underspecification | MEDIUM | T124; quickstart.md:78 | There is no clean-install environment. | Add a clean-install profile. |
| M14 | Proof missing | MEDIUM | tasks.md:32 (T011), 220 (T120) | Neither task has a "→" proof. | Add proofs. |
| M15 | Ambiguity | MEDIUM | constitution.md:64 | "Shrinks over time" conflicts with "never increases per suite". | PATCH, recorded in ADR 0001. |
| M16 | Traceability | MEDIUM | `cab48e1cc3`, `82da01c728` (T071); `d5b2fe8b70` | Cross-area codemod commits are tagged with one area's task. | Add a cross-cutting M5b codemod task. |
| M17 | Coverage / terminology (VI) | MEDIUM | quarantine.md (13 `legacy-fixture` → T134); data-model.md:35-36 | PCL, Xamarin and netstandard1.x fixtures have no owning decision and are not in BREAKING-CHANGES. | Decide on support or retirement; add the category to data-model. |
| M18 | Constitution (V ratchet) | MEDIUM | `scripts/test.sh` (`--update-baseline`) | It still overwrites the baseline even when the value drops. | Refuse decreases without an explicit override. |
| M19 | Evidence (DoD) | MEDIUM | tasks.md:109 (T057); `docs/evidence/M3/README.md:42` | Partial: the gate result is stored in M6. `M4/README.md` is missing, and the link to it is broken. | Finish T057. |
| M20 | Validation drift | MEDIUM | tasks.md:25 (T004); quickstart.md:15 | "15 submodules"; `.gitmodules` has 12. | Update the proof. |
| M21 | Inconsistency (US6, T116) | MEDIUM | spec.md:168 (US6 scenario 1); tasks.md:211 (T116: `<version>+<sha>`); ci.yml (`ci-${{ github.run_number }}+${{ github.sha }}`); tasks.md:210-211 (T115, T116 unchecked) | The artifact is named after the run number, not the product version, so it does not meet "artifacts named with version and commit". T115 and T116 have committed code and M6 evidence but are unchecked, and no text says what is still missing (the hosted run belongs to T119). | Derive the version from `version.config` into the artifact name (for example `monodevelop-<version>+<sha>`). Then check T115, and check T116 after `actionlint` (or state why they stay open). |
| L1 | Duplication / traceability | LOW | tasks.md:79, 83, 150, 242-244 | T133 overlaps T038. T134, T135 and T136 were created in the commits that implement them. Dependencies do not mention T132–T136. | Merge or state the split, and extend Dependencies. |
| L2 | Inconsistency | LOW | ADR 0010:16; research.md:30 | The ADR says "2.3.2"; the spike used 2.3.0. | Write "2.3.0 (spike); 2.3.2 pinned". |
| L3 | Inconsistency | LOW | plan.md:75-76 | "The two justified deviations", but there are 10 rows. | Say "the deviations". |
| L4 | MADR | LOW | ADR 0012, 0017 | Neither has a "Considered Options" section. | Add it. |
| L5 | Inconsistency | LOW | research.md:89; ADR 0008; plan.md:190 | "B35" is undefined. | Define it or remove it. |
| L6 | Stale IDs | LOW | `MonoDevelop.Core.csproj:3`; `scripts/debug.sh:20` | Revision-1 IDs. | T033–T037; T025. |
| L7 | Constitution I wording | LOW | quickstart.md:24-26, 54 | No `./scripts/pm`. | Wrap them. |
| L8 | Reproducibility | LOW | ci.yml (`checkout@v4`, `cache@v4`, `upload-artifact@v4`) | Pinned by tag. | Pin by SHA. |
| L9 | Duplication | LOW | FR-016/SC-009; T109/T129 | Both pairs repeat each other. | Keep SC-009; make T129 the gate. |
| L10 | Traceability | LOW | tasks.md:75 | T037 → "(T046)". | Change it to T041. |
| L11 | Traceability | LOW | tasks.md:92 (T046) | The proof still lists "`AsyncLocal` delayed initialization". | Remove it. |
| L12 | Quality gate precision | LOW | `scripts/lib.sh:30-39` | `--diff-filter=A`; duplicate `local base`. | Use `AR`; drop the duplicate. |
| L14 | Inconsistency | LOW | quickstart.md:60 | This M5a command differs from the verified one, which `ci.sh` `gui_smoke` also uses (`dotnet main/build/samples/xwt/Gtk3Test.dll`). | Use the verified command. |
| L15 | Traceability (C1 residual) | LOW | plan.md Complexity Tracking row 3 | `37b97d3037`, `d7eafe962b`, `33b3edceb6` and `c379e20b7f` list several tasks and predate the `Coupled:` rule. They are not named in the row. | Add them, with "coupled: builder/runtime, walking skeleton, contract tests + translations, fixtures + quarantine". |
| L16 | Tooling | LOW | `scripts/git-commit:8-10, 44`; commits `dc4ece0a56`, `e289c26bc2`, `d61af9e5b2` | (a) The header says a `Format-only:` commit "changes nothing else", but nothing enforces it. (b) `Coupled:` and `Format-only:` are written in a paragraph after `Tasks:`, so git sees only the last block as trailers. `git log --format='%(trailers:key=Tasks)'` returns nothing for those 3 commits. | (a) With `Format-only:`, require every staged file to have 0 changes under `-w`. (b) Require `Tasks:`, `Coupled:` and `Format-only:` to sit in one final trailer block (check with `git interpret-trailers --parse`). |
| L17 | Governance (SemVer) | LOW | ADR 0001 amendments table (1.2.1 PATCH); constitution.md:47-48 | 1.2.1 adds a new obligation (a `Coupled:` line). By the Governance rule, that is closer to "materially expanding guidance" (MINOR) than to a clarification. | Record 1.2.1 as MINOR (1.3.0), or give the reason it counts as PATCH in the ADR row. |

**Overflow**: none. There are 39 findings, under the limit of 50.

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 clean-clone-build-in-container | Yes | T001, T018–T024, T031 | M4 |
| FR-002 core-on-net10 | Yes | T033–T037, T132, T042–T046, T049, T059, T062 | All checked |
| FR-003 detect-dotnet-sdks | Yes | T042, T044 | |
| FR-004 addin-discovery | Yes | T037, T038, T133, T050, T062 | |
| FR-005 mdtool-build-clean-select | Yes | T058–T064 | `MdtoolContractTests`; the `ci.sh` mdtool smoke |
| FR-006 gtk3-x11-wayland | Yes | T066, T070–T086, T136, T103, T104 | Port tests in place (T136); ADR 0011 removal metric |
| FR-007 edit-build-navigate-csharp | Yes | T085, T088, T089, T103, T105 | |
| FR-008 debug-net10 | Yes | T110–T114 | |
| FR-009 git-status-diff-history | Yes | T102 | |
| FR-010 nuget-manage | Yes | T100 | |
| FR-011 unit-test-runner | Yes | T101 | |
| FR-012 exclusions-listed | Yes | T013, T014, T108 | PCL decision missing (M17) |
| FR-013 flatpak-desktop-entry | Yes | T120–T124 | |
| FR-014 structured-logs | Yes | T125 | |
| FR-015 idempotent-scripts | Yes | T024, T114, T115, T123 | `ci.sh` committed; lint green |
| FR-016 no-vulnerable-deps-no-dead-feeds | Yes | T019, T020, T052, T127 | `audit.sh` is a `ci.sh` step |
| FR-017 vendored-with-origin | Yes | T066–T068, T110 | Leftover checkouts removed |
| FR-018 localizable | Yes | T047, T048 | |
| SC-001 fresh-clone-zero-manual-steps | Yes | T027, T031 | M4 |
| SC-002 tests-run-quarantine-≤15% | Yes | T040, T041, T057, T107, T134, T135 | 75 quarantined, 850 passed, 9 skipped (≈ 8.0%) |
| SC-003 coverage-60/40-ratchet | Yes | T055, T056 | Scope is now defined; `total` is not in the ratchet file, and the "met" claim is unsupported (H4) |
| SC-004 mdtool-samples-in-pipeline | Yes | T064, T065, T115, T116 | `ci.sh` runs the contract tests and an mdtool smoke; no hosted run yet |
| SC-005 smoke-x11-wayland-≤10s | Yes | T103, T104, T109, T129 | |
| SC-006 debug-scenario | Yes | T113 | |
| SC-007 pipeline-≤15min | Yes | T115, T119 | Local run of 301 s with an incremental build; the budget is enforced by `ci.sh` |
| SC-008 flatpak-clean-install | Yes | T124 | M13 |
| SC-009 zero-high-critical-vulns | Yes | T052, T127 | |

**Edge cases**: 5 of 7 are covered: add-in resilience (T050), no SDK (T042), a foreign working directory (T133), Wayland (T104)
and a large solution (T106). Two are partial: build cancellation (M12) and a legacy project (M11).

## Constitution Alignment Issues

- **IV**: multi-task commits are enforced and recorded. One `Coupled:` reason (`d61af9e5b2`) does not meet the "cannot build
  independently" condition, because the analyze gate pushes remediation work under T016 (H1). Trailer placement defeats
  git's trailer parsing (L16).
- **V (NON-NEGOTIABLE)**: no open violation. The M5b test harness exists and is mandatory for every port task. Line endings
  are repaired and guarded. The remaining items are the legacy-reformatting task (M2), ratchet hardening (M18) and the
  quarantine wording (M15).
- **Development Workflow**: 2 implementation commits landed after revision 4 without a record, the gate can be bypassed by
  tagging T016, and the gate's input (revision 4) is not committed (H1).
- **Governance**: amendments are now logged in ADR 0001. The SemVer category of 1.2.1 is debatable (L17).

## Unmapped Tasks

These tasks map to constitution or documentation standards rather than to an FR or SC, and are justified: T030, T069,
T083, T126, T128 and T133. T134 and T135 map to SC-002. T136 maps to FR-006 and constitution V.

## Metrics

- Total Requirements: **27** (18 FR + 9 SC)
- Total Tasks: **136** (70 checked: T001–T015, T017–T055, T058–T070, T132, T133, T136; 66 open)
- Coverage: **100%** (27/27 requirements have at least one task)
- Ambiguity Count: **2** (M12, M15)
- Duplication Count: **2** (L1, L9)
- Findings: 0 CRITICAL, 2 HIGH, 21 MEDIUM, 16 LOW
- **Critical Issues Count: 0**

## Next Actions

- **No CRITICAL issues.** M1's acceptance criterion ("consistency analysis 0 CRITICAL") is met by this revision, but **only once
  this file is committed**. HEAD still holds revision 3, and `scripts/git-commit` reads this file to decide whether task
  commits are allowed.
  1. Commit this file alone: `Tasks: T016`. Then check T016 in tasks.md.
  2. H1: extend Complexity Tracking row 4 to `e0dbd904a2`. Add `Analyze-fix: <IDs>` to `git-commit`, and make the gate read
     `HEAD:docs/evidence/M1/analyze.md`.
  3. H4: commit the coverage work with the updated ratchet file (Core and `total`) and check T056, or reword the Complexity
     Tracking row.
- MEDIUM and LOW: implementation may proceed. Fix these in small documentation commits: tasks.md, plan.md, quickstart.md,
  contracts/scripts.md, data-model.md, ADRs 0001, 0010 and 0012, `ci.yml`, and `scripts/{test.sh,lib.sh,git-commit}`.
  M15 and L17 need an entry in ADR 0001.
- Re-run the consistency analysis at the end of M5b, or as T131.

Would you like me to suggest concrete remediation edits for H1, H4 and M21? None were applied; this analysis is read-only.
