# 0015 — Test framework: NUnit 3.14 + coverlet

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

Tests (~3.8k) use GuiUnit (NUnitLite-based, submodule) or NUnit 2.7 and run through
`mdtool run-md-tests`. Coverage used Mono's log profiler. None of this works on .NET 10.

## Decision Outcome

- NUnit 3.14 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk; run with `dotnet test`.
- Mechanical changes: `TestFixtureSetUp/TearDown` → `OneTimeSetUp/TearDown` (18 files),
  `ExpectedException` → `Assert.Throws`.
- GuiUnit's main-thread loop replaced by a `[SetUpFixture]` that installs the IDE synchronization
  context (GTK main loop under Xvfb for UI tests).
- Coverage: coverlet collector (Cobertura) + ReportGenerator; thresholds per constitution V.
- NUnit 4 later, with NUnit.Analyzers code fixes.
- Failing tests are quarantined with `[Category("Quarantine")]` and a reason in
  `docs/evidence/M4/quarantine.md`.

### Consequences

- Good: standard tooling, IDE/CI integration, coverage.
- Bad: the `mdtool run-md-tests` runner becomes obsolete (removed from the Linux build).
