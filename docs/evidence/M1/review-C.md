# Review C — traceability and completeness (condensed)

| Area | Finding | Resolution |
|---|---|---|
| Principle IV | 71/96 tasks without a proof | all tasks carry "→" (revision 2) |
| FR-003/004 | no tests for SDK detection / no-SDK / add-in failure | T042, T050 |
| FR-005 | `-c:` not exercised | T064 contract test |
| FR-006 | Wayland untested | T104 |
| FR-007 | completion and error navigation untested | T089, T105 |
| FR-008 | debugger test path wrong | T113 path fixed |
| FR-009/010/011 | no tests | T102, T100, T101 |
| FR-012 | exclusion list vs solution not checked | ADR 0017 extended; T108 |
| FR-013 | desktop entry/icon/MIME not checked | FR-013 reworded; T122 validators |
| FR-016 | `dotnet list --vulnerable` exits 0 | `scripts/audit.sh` gate (T052) |
| FR-017 | UPSTREAM.md not checked | vendoring tasks name UPSTREAM.md |
| FR-018 | no `.po` → `.mo` compilation after autotools removal | T048 |
| SC-002 | denominator undefined | spec SC-002 defines it |
| SC-003 | coverage timing conflict | "By M4" + T056 |
| SC-007 | CI time never measured | T115 |
| Delivery | release without Flatpak/SBOM; CI least privilege | T117, T123; `permissions: contents: read` |
| Docs | setup.md needed before M8 | T027 (M2) |
| ADRs | missing: logging, versioning, NRefactory, NuGet client, warning policy | ADR 0018–0023 tasks |
| Evidence | M8 without evidence; stale checkboxes; ID drift | T129 evidence; checkboxes synced; files renamed |
| M6 | acceptance impossible without push | local `scripts/ci.sh` + actionlint; hosted run after authorization (T119) |
