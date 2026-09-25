# M4 evidence — tests and coverage on .NET 10

All runs are in the dev container: `./scripts/pm ./scripts/test.sh`, excluding quarantined tests
(`Category!=Quarantine`), with GTK tests under Xvfb.

## Results (2026-09-23)

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| MonoDevelop.Core.Tests | 1066 | 0 | 9 |
| MonoDevelop.Ide.Gtk3.Tests | 7 | 0 | 0 |

- **Quarantine:** 77 cases, down from 80 at the first run (7.2% of 1075). Each case has a reason,
  owner, date and follow-up task in [quarantine.md](quarantine.md). Legacy fixtures are tracked in
  T134, and the Bug/Flaky entries in T135.
- **Line coverage** (coverlet + ReportGenerator, product assemblies only, SC-003):
  - `MonoDevelop.Core`: 62.8% (target ≥ 60%).
  - Product total: 52.4% (target ≥ 40% at the time). This total covered only the assemblies exercised by the two
    suites above. With all 12 suites (M8) it is 29.25%, and the 40% target was dropped on 2026-09-25 (SC-003,
    constitution 1.4.0). Current figures: [M8/README.md](../M8/README.md) and [coverage-baseline.txt](coverage-baseline.txt).

  The ratchet file is [coverage-baseline.txt](coverage-baseline.txt). `scripts/test.sh` fails when
  any listed value drops.
- **Two runs in a row:** the first was fully green. The second had one file-watcher timing failure,
  now quarantined as Flaky (T135).

## Bugs found while raising coverage (fixed with tests)

- `MSBuildErrorParser` dropped one-character messages.
- `LocalConsole`'s input reader never advanced, and `ReadLine` repeated earlier lines.
- `ProcessArgumentBuilder` could not parse the escaped backslashes that it produces itself.
- `TargetFrameworkMoniker` lost profile paths and threw on malformed input.
- `TextEncoding` never listed the last table entry.
- `TextFileUtility`:
  - `GetBuffer` returned a zero-padded buffer.
  - UTF-32 files were detected as UTF-16.
  - `WriteText` wrote a BOM that was not asked for.
- `WorkspaceObject.Dispose` threw on threads without a synchronization context.
- `ProjectCapabilityTests` damaged the shared add-in registry.
