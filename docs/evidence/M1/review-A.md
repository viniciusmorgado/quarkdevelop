# Review A — consistency analysis of revision 1 (condensed)

| ID | Sev. | Finding | Resolution (revision 2) |
|---|---|---|---|
| C1 | CRITICAL | Global coverage ≥ 40% due at M4 by the constitution but placed at M8 (T095) | T056 puts Core 60% + global 40% in M4 |
| C2 | CRITICAL | M3 behaviour changes shipped before any test project is converted | test harness T038–T041 precedes T042–T052; each runtime task names its test |
| C3 | CRITICAL | No `AnalysisLevel`/warnings-as-errors policy; `-warnaserror` unachievable | `TreatWarningsAsErrors` + per-project baseline (T029, ADR 0018) |
| C4 | CRITICAL | Quarantine "must not grow after M4" vs new suites in M5 | per-suite ratchet (data-model, SC-002) |
| C5 | CRITICAL | Flatpak commands outside `./scripts/pm` | `PM_PROFILE=flatpak` profile of `scripts/pm` (constitution 1.0.1, T121) |
| C6 | CRITICAL | ~55 tasks without proof | every task has "→" |
| C7 | CRITICAL | multi-project UI tasks vs "one task per commit" | M5c split one project per task |
| C8 | CRITICAL | docs deferred to M8 while SC-001 needs them | README/setup/troubleshooting in M2 (T027), CONTRIBUTING T030 |
| C9 | CRITICAL | images pulled by mutable tags | digests pinned (T025) |
| C10 | HIGH | ADRs without "Considered Options" | added to 0004–0009, 0013–0015 |
| I1 | HIGH | spike IDs ≠ task IDs | evidence renamed T009/T010; research uses task IDs |
| I2 | HIGH | debugger/GUI test commands wrong | quickstart § M5 fixed |
| I3 | HIGH | linux-smoke inherits repo props | isolated `Directory.Build.*`/`Directory.Packages.props` (T058) |
| I4 | HIGH | `shellcheck scripts/*.sh` fails on legacy configure.sh | `scripts/lint.sh` with explicit list |
| I5 | HIGH | projects in no wave/exclusion | ADR 0017 extended |
| I6 | HIGH | "UI-thread monitor" is an excluded add-in | spec edge case reworded; T106 probe |
| I7 | HIGH | BREAKING-CHANGES incomplete | completed |
| U1 | HIGH | Wayland never validated | T104 |
| I8 | MEDIUM | Microsoft.Build 17.x vs SDK MSBuild 18.9 | 18.9.6 everywhere |
| I9 | MEDIUM | phase/milestone order inconsistencies | phases ordered by milestone |
| U2–U8, A1–A2, G1–G2, I10–I11, D1 | MEDIUM/LOW | paths, CI time, tests for FR-009/010/011/018, SBOM, UPSTREAM.md, owner field | tasks T047–T052, T100–T106, T115–T124; contracts/quickstart/data-model updated |
