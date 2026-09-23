# 0001 — Spec-driven development and MADR decision records

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The migration touches ~100 projects over many milestones and will be executed incrementally, partly
with interruptions. Decisions and progress must be traceable and resumable.

## Decision Drivers

- Traceability from requirement → task → evidence.
- Resume work after interruptions (context loss) from files in the repository.

## Considered Options

1. A spec-driven flow (constitution → spec → plan → tasks → implementation) + MADR ADRs.
2. Ad-hoc issues and README notes.

## Decision Outcome

Option 1. The constitution is `docs/constitution.md`; the specification, plan and tasks live in
`specs/001-linux-dotnet10-migration/`; ADRs in `docs/adr/`; evidence in `docs/evidence/Mx/`.
Artifacts are reviewed by independent reviewers before implementation.

### Consequences

- Good: every milestone has checkable acceptance commands; `tasks.md` checkboxes are the resume point.
- Bad: documentation overhead per change.

## Amendments to the constitution

The constitution (`docs/constitution.md`) is amended only together with an entry here
(Governance).

| Version | Change | Motivation and impact |
|---|---|---|
| 1.1.0 (MINOR) | Principle V: `dotnet format` is enforced on C# files added by the fork. Legacy files are reformatted one project at a time in dedicated commits. | Reformatting 5,600 legacy files in behaviour commits would hide every real change. `scripts/build.sh --check` enforces the rule (analyze revision 2, C2). |
| 1.2.0 (MINOR) | Principle IV: every commit carries a `Tasks:` trailer. One task per commit is the default, and several tasks are allowed only when they cannot build independently. | Makes task→commit traceability checkable (analyze revision 3, C1). It relaxes the earlier "one task's scope per commit" wording for interdependent tasks. The relaxation is bounded by the next row. |
| 1.3.0 (MINOR) | Enforcement of IV, V and Governance in `scripts/git-commit` (new obligation for authors): several tasks need a `Coupled: <reason>` line; legacy files may not have their line endings rewritten outside `Format-only:` commits; task commits other than T016 are refused while the committed `docs/evidence/M1/analyze.md` reports CRITICAL issues (commits that fix findings say so in an `Analyze-fix:` line). | Analyze revisions 4 and 5 (C1, C3, H1, L17): the written rules were not enforced; adding the `Coupled:` obligation is a MINOR change. |
