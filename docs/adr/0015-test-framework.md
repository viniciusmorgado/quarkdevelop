# 0015 — Test framework: NUnit 3.14 + coverlet

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

Tests (~3.8k) use GuiUnit (NUnitLite-based, submodule) or NUnit 2.7 and run through
`mdtool run-md-tests`. Coverage used Mono's log profiler. None of this works on .NET 10.

## Considered Options

1. NUnit 3.14 + Microsoft.NET.Test.Sdk + coverlet (chosen)
2. NUnit 4 directly (larger churn at once)
3. xUnit/MSTest (rewrite of ~3.8k tests)

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

**Amendment (2026-09-23): coverage ratchet during the port.** `scripts/test.sh` fails when an
assembly listed in `docs/evidence/M4/coverage-baseline.txt`, or the product `total`, drops. There is
one exception. A commit that moves legacy sources back into the build (shrinking
`Gtk3PortPending.props`) adds coverable lines that were never tested, which can lower the total. Such
a commit may update the baseline (`./scripts/test.sh --update-baseline`) if its message states the
old and new values. New code never lowers the ratchet.

**Amendment (2026-09-24): IDE test host (T107).** The IDE suites (`MonoDevelop.Ide.Tests`,
`MonoDevelop.CSharpBinding.Tests`, the editor tests of Refactoring and Xml) use `IdeUnitTests.GuiTestHost`
from a global `[SetUpFixture]`: GTK, the runtime (with the add-ins of the test's output folder), Xwt and a
mock shell are initialized on the NUnit test thread, which becomes the runtime's main thread
(`MDTestSingleThread`: no NUnit workers, no NUnit timeouts). NUnit 3 only keeps a message loop running
while it waits for an async test when the thread's synchronization context is the WinForms or WPF one,
and it finds them by type name. `IdeUnitTests/NUnitMessagePump.cs` therefore defines a
`System.Windows.Forms.WindowsFormsSynchronizationContext` and `Application.Run/Exit` that post to and run
the GLib main loop, as GuiUnit did. A test that does not complete fails after 2 minutes instead of
hanging the run.
