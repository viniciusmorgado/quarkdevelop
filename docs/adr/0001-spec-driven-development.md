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
