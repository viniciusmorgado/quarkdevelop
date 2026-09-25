# M8 evidence

T129 has three parts: start-up time, quarantine and warning baselines.

- **Start-up time:** [startup.md](startup.md). The main window appears in 2.2 s on a cold start on an idle
  machine, and in at most 5.1 s with the machine fully loaded. The NFR-001 target is 10 s.
- **Quarantine:** 100 → 54 quarantined test cases, detailed in the section below.
- **Warning baselines:** 29,900 → 23,026 occurrences, detailed in the section below.
- Security sweep (T127): [security.md](security.md).

## Quarantine (T129, T135)

Date: 2026-09-24. Record: [docs/evidence/M4/quarantine.md](../M4/quarantine.md).

| Suite | Before | After |
|---|---|---|
| MonoDevelop.TextEditor.Tests | 16 | 0 |
| MonoDevelop.CSharpBinding.Tests | 9 | 0 |
| MonoDevelop.Ide.Tests | 30 | 10 |
| MonoDevelop.DotNetCore.Tests | 13 | 12 |
| MonoDevelop.Core.Tests | 31 | 31 |
| MonoDevelop.PackageManagement.Tests | 1 | 1 |
| **Total** | **100** | **54** |

The Core count is 31 after its own triage (78 → 31, earlier on 2026-09-24).

No Bug or Flaky entry is left without a follow-up task. What remains falls into three groups:
- legacy .NET Framework/PCL fixtures (T134);
- tests that need nuget.org (T099, T100, T143, T151; the tests run offline);
- Mono-only features (T142).

The gate run after the merge passed 3,958 tests with 0 failures in 544 s (`scripts/ci.sh`).

**Coverage ratchet raised.** Measured: Core 66.83%, Ide 21.29%, total 29.25%. The baseline in
`docs/evidence/M4/coverage-baseline.txt` goes from 62.8 / 19.0 / 22.0 to 66.5 / 21.0 / 29.0.

## Warning baselines (T129)

Date: 2026-09-24, base commit `6d1ed3888b`. Policy: [ADR 0018](../../adr/0018-warning-policy.md).

### Result

| | Before | After |
|---|---|---|
| Occurrences in `main/msbuild/Linux/warning-baselines/*.counts.txt` | 29,900 | 23,026 |
| Baselined IDs (sum over projects of `WarningsNotAsErrors` entries) | 1,125 | 834 |
| Distinct IDs across all baselines | 137 | 112 |
| Projects with a baseline | 71 | 70 (`MonoDevelop.Core.Tests.Addin` has no warnings left) |
| Distinct warnings in a full solution build (`dotnet build main/MonoDevelop.Linux.sln --no-incremental -p:TreatWarningsAsErrors=false`) | 14,910 | 11,522 |

The counts files count lines of the build log, where MSBuild prints each diagnostic twice; the last
row counts each diagnostic once. No (project, ID) pair gained a warning compared with a full build of
the base commit. Some per-ID totals of the old counts files differ from the base build for reasons
outside this change (the baselines were generated at different times, e.g. CA1861, CA1707, CS0612);
regenerating them brings those totals up to date.

### How

1. `dotnet format analyzers main/MonoDevelop.Linux.sln --diagnostics <IDs> --severity info` for
   CA1507, CA1510–CA1513, CA1514, CA1825, CA1827, CA1829, CA1830, CA1834, CA1837, CA1845, CA1846,
   CA1847, CA1854, CA1858, CA1860, CA1865, CA1874. `dotnet format` reports no diagnostic while the
   project has `WarningsNotAsErrors` or `TreatWarningsAsErrors=true`, so it ran with the environment
   variable `TreatWarningsAsErrors=false` and the baseline directory moved aside.
2. The fixers normalise line endings, drop BOMs and re-indent the lines they touch. Every touched
   file was compared with the base commit and its line endings, BOM, final newline and the
   indentation style of the replaced lines were restored; files that use `Foo(x)` style keep it.
3. Fixes without a fix-all provider (CA1805, CA1852, CA1840, CA1860, CA1866) were applied at the
   locations listed by the build log; CA1853, CA1868 and the compiler warnings (CS0105, CS0108,
   CS0109, CS0168, CS0169, CS0414) were edited by hand.
4. `scripts/warnings-baseline.sh` regenerated the 68 baselines whose counts changed. The script now
   removes the baseline of a project that has no warnings left (it failed on an empty list before).

### Rules applied

- Throw helpers (CA1510, CA1512): kept only where the exception type and parameter name stay the
  same. Rewrites that changed the parameter name (`"entity"` for `type`, `"widget"` for `self`,
  `nameof (textIndex)` for `curIndex`, `length` → `start` in `Span`), added a parameter name to a
  parameterless exception, or used `ThrowIfNotEqual` were reverted (18 CA1510 and 12 CA1512
  occurrences remain). CA1513 (`ObjectDisposedException.ThrowIf`) was not applied: it changes the
  object name in the message.
- CA1852 (seal): only internal/private types. Types with virtual members (CS0549) or protected members
  (CS0628) were left unsealed.
- CA1805 (default initializers): removals that produced a new CS0649 (field never assigned) were
  reverted; struct `new T ()` initializers were left.
- CA1854 (`TryGetValue`): two rewrites that did not compile (duplicate `out var value` in
  `DatePickerBackend` and `NewFileDialog`) were reverted.
- CS0169/CS0414: fields removed only when nothing else refers to them (no reflection, no `[UI]`
  binding); `DocumentView.hasFocus` and the test mock were left.
- Not touched: obsolete APIs (CS0612/CS0618), renames (VSTHRD200, CA1707, CA1716), `static` on
  members (CA1822), culture-sensitive calls (CA1305/CA1310/CA1309/CA1311), CA2249, CA1826 (no
  fix-all; exception type changes), CA1861, CA1859, CA2201, CA2208.

### Per ID (fixed IDs; counts-file occurrences, projects listing the ID)

| ID | Before | After | Projects before | Projects after |
|---|---|---|---|---|
| CA1510 | 1,968 | 18 | 44 | 5 |
| CA1852 | 1,258 | 84 | 26 | 11 |
| CA1507 | 900 | 0 | 12 | 0 |
| CA1805 | 886 | 56 | 38 | 8 |
| CA1834 | 518 | 0 | 17 | 0 |
| CA1825 | 470 | 0 | 29 | 0 |
| CA1860 | 180 | 0 | 15 | 0 |
| CA1512 | 100 | 12 | 10 | 3 |
| CA1845 | 96 | 0 | 14 | 0 |
| CA1865 | 92 | 0 | 10 | 0 |
| CA1854 | 88 | 8 | 13 | 2 |
| CA1866 | 80 | 0 | 9 | 0 |
| CA1829 | 48 | 0 | 9 | 0 |
| CS0108 | 42 | 0 | 6 | 0 |
| CA1847 | 40 | 0 | 7 | 0 |
| CA1846 | 40 | 2 | 7 | 1 |
| CA1830 | 34 | 0 | 8 | 0 |
| CS0169 | 28 | 2 | 6 | 1 |
| CA1514 | 26 | 0 | 6 | 0 |
| CA1868 | 22 | 0 | 6 | 0 |
| CS0105 | 16 | 0 | 4 | 0 |
| CA1840 | 16 | 0 | 5 | 0 |
| CA1837 | 12 | 0 | 2 | 0 |
| CA1827 | 10 | 0 | 3 | 0 |
| CA1858 | 10 | 0 | 2 | 0 |
| CS0414 | 10 | 4 | 3 | 2 |
| CA1874 | 8 | 0 | 2 | 0 |
| CS0109 | 6 | 0 | 2 | 0 |
| CA1853 | 4 | 0 | 2 | 0 |
| CS0168 | 2 | 0 | 1 | 0 |

Side effects of the fixes above (no direct fix): single-char `StartsWith`/`EndsWith`/`Contains`
calls also carried CA1310/CA1305, sealed types no longer need CA1816, and the CA1854/CA1829 rewrites
removed the `ContainsKey` guards and `Enumerable` calls that CA1864/CA1826 reported on the same lines.

| ID | Before | After | Projects before | Projects after |
|---|---|---|---|---|
| CA1310 | 406 | 326 | 17 | 16 |
| CA1305 | 1,766 | 1,750 | 35 | 35 |
| CA1864 | 20 | 16 | 5 | 4 |
| CA1816 | 234 | 232 | 18 | 17 |
| CA1826 | 12 | 10 | 3 | 3 |

### Projects that lost IDs

IDs that left each project's `WarningsNotAsErrors` list (occurrences and ID count before → after).
SYSLIB0006, CS8621 and CS8714 were already gone from the code at the base commit; the old baselines
still listed them.

| Project | Occurrences | IDs | Removed from the baseline |
|---|---|---|---|
| CSharpBinding | 966 → 520 | 43 → 33 | CA1507 CA1805 CA1825 CA1830 CA1834 CA1845 CA1854 CA1860 CA1865 CA1868 |
| ChangeLogAddIn | 64 → 60 | 10 → 9 | CA1825 |
| ClassificationAggregatorImpl | 30 → 18 | 4 → 3 | CA1510 |
| ClassificationTypeImpl | 26 → 18 | 5 → 4 | CA1510 |
| CommandingImpl | 20 → 18 | 6 → 5 | CA1510 |
| CoreUtility | 78 → 56 | 13 → 11 | CA1510 CA1805 |
| CoreUtilityImpl | 62 → 32 | 9 → 5 | CA1510 CA1805 CA1854 CA1865 |
| DifferenceAlgorithmImpl | 36 → 18 | 5 → 4 | CA1510 |
| EditorOperationsImpl | 80 → 34 | 7 → 5 | CA1510 CA1845 |
| EditorOptionsImpl | 20 → 14 | 4 → 3 | CA1510 |
| EditorPrimitivesImpl | 30 → 16 | 5 → 4 | CA1510 |
| GnomePlatform | 42 → 40 | 7 → 6 | CA1852 |
| IdeUnitTests | 74 → 70 | 13 → 11 | CA1825 CA1834 |
| IntellisenseDef | 46 → 16 | 6 → 3 | CA1510 CA1805 CA1852 |
| Internal | 24 → 8 | 5 → 2 | CA1510 CA1512 CA1805 |
| LanguageDef | 28 → 26 | 6 → 5 | CA1805 |
| LanguageImpl | 58 → 40 | 11 → 7 | CA1510 CA1805 CA1829 CA1860 |
| Mono.Addins.GuiGtk3 | 308 → 256 | 20 → 13 | CA1514 CA1805 CA1834 CA1846 CA1866 CS0105 CS0109 |
| Mono.Debugging | 364 → 174 | 25 → 17 | CA1510 CA1512 CA1805 CA1825 CA1830 CA1834 CA1852 CA1865 |
| MonoDevelop.AssemblyBrowser | 260 → 156 | 20 → 15 | CA1507 CA1510 CA1805 CA1825 CA1852 |
| MonoDevelop.CSharpBinding.Core | 48 → 42 | 12 → 10 | CA1830 CA1874 |
| MonoDevelop.CSharpBinding.Tests | 252 → 188 | 15 → 12 | CA1825 CA1845 CA1860 |
| MonoDevelop.Core | 2,640 → 1,986 | 74 → 56 | CA1507 CA1512 CA1514 CA1805 CA1825 CA1827 CA1830 CA1834 CA1837 CA1840 CA1847 CA1853 CA1854 CA1860 CA1865 CA1866 CA1868 CS0169 |
| MonoDevelop.Core.Tests | 830 → 714 | 20 → 14 | CA1510 CA1805 CA1825 CA1829 CA1860 SYSLIB0006 |
| MonoDevelop.Core.Tests.Addin | 2 → 0 | 1 → 0 | CA1852 |
| MonoDevelop.Debugger | 700 → 650 | 36 → 27 | CA1805 CA1825 CA1845 CA1847 CA1854 CA1860 CA1865 CS0169 CS0414 |
| MonoDevelop.Debugger.VsCodeDebugProtocol | 34 → 18 | 9 → 6 | CA1805 CA1825 CA1852 |
| MonoDevelop.DesignerSupport | 240 → 196 | 31 → 26 | CA1514 CA1805 CA1825 CA1840 CA1854 |
| MonoDevelop.DocFood | 276 → 196 | 23 → 16 | CA1507 CA1510 CA1830 CA1834 CA1845 CA1854 CA1866 |
| MonoDevelop.DotNetCore | 266 → 228 | 19 → 15 | CA1825 CA1829 CA1858 CA1860 |
| MonoDevelop.DotNetCore.Tests | 212 → 164 | 12 → 9 | CA1825 CA1847 CA1860 |
| MonoDevelop.Gettext | 456 → 272 | 27 → 18 | CA1805 CA1825 CA1845 CA1847 CA1852 CA1854 CA1866 CA1868 CS0108 |
| MonoDevelop.HexEditor | 184 → 82 | 20 → 14 | CA1507 CA1510 CA1816 CA1825 CA1834 CA1854 |
| MonoDevelop.Ide | 8,816 → 7,168 | 99 → 74 | CA1507 CA1514 CA1825 CA1827 CA1829 CA1830 CA1834 CA1837 CA1840 CA1845 CA1846 CA1853 CA1858 CA1860 CA1865 CA1866 CA1868 CA1874 CS0105 CS0108 CS0109 CS0168 CS0169 CS8621 CS8714 |
| MonoDevelop.Ide.Tests | 1,230 → 1,008 | 28 → 21 | CA1805 CA1825 CA1829 CA1847 CA1852 CA1866 CS0108 |
| MonoDevelop.MSBuildBuilder | 144 → 40 | 10 → 7 | CA1834 CA1846 CA1852 |
| MonoDevelop.PackageManagement | 894 → 748 | 31 → 25 | CA1510 CA1825 CA1827 CA1829 CA1834 CA1860 |
| MonoDevelop.PackageManagement.Tests | 2,286 → 2,160 | 22 → 17 | CA1510 CA1825 CA1847 CA1852 CA1860 |
| MonoDevelop.Refactoring | 436 → 372 | 29 → 22 | CA1507 CA1510 CA1825 CA1830 CA1834 CA1845 CA1860 |
| MonoDevelop.Refactoring.Tests | 36 → 34 | 6 → 5 | CA1852 |
| MonoDevelop.RegexToolkit | 64 → 56 | 11 → 10 | CS0169 |
| MonoDevelop.SourceEditor | 2,448 → 1,790 | 47 → 33 | CA1507 CA1512 CA1514 CA1825 CA1829 CA1830 CA1834 CA1845 CA1846 CA1854 CA1865 CA1868 CS0105 CS0108 |
| MonoDevelop.TextEditor.Tests | 192 → 112 | 12 → 8 | CA1825 CA1845 CA1852 CA1866 |
| MonoDevelop.UnitTesting | 216 → 196 | 23 → 17 | CA1510 CA1805 CA1825 CA1834 CA1845 CA1865 |
| MonoDevelop.UnitTesting.Tests | 36 → 24 | 4 → 3 | CA1852 |
| MonoDevelop.VersionControl | 620 → 548 | 40 → 28 | CA1507 CA1510 CA1514 CA1805 CA1825 CA1845 CA1846 CA1860 CA1865 CS0105 CS0108 CS0169 |
| MonoDevelop.VersionControl.Git | 378 → 352 | 25 → 20 | CA1805 CA1825 CA1840 CA1845 CA1860 |
| MonoDevelop.VersionControl.Git.Tests | 196 → 188 | 10 → 7 | CA1805 CA1829 CA1852 |
| MonoDevelop.Xml | 270 → 206 | 30 → 22 | CA1310 CA1507 CA1510 CA1825 CA1834 CA1845 CA1866 CA1868 |
| MonoDevelop.Xml.Tests | 396 → 392 | 8 → 7 | CA1829 |
| MultiCaretImpl | 18 → 14 | 5 → 3 | CA1510 CA1805 |
| NavigationImpl | 18 → 12 | 3 → 2 | CA1510 |
| Outlining | 42 → 28 | 7 → 6 | CA1510 |
| PatternMatchingImpl | 22 → 20 | 6 → 5 | CA1510 |
| StandaloneUndoImpl | 50 → 12 | 7 → 2 | CA1510 CA1512 CA1805 CA1854 CA1864 |
| TagAggregatorImpl | 38 → 26 | 6 → 4 | CA1510 CA1805 |
| TextBufferUndoManagerImpl | 30 → 16 | 6 → 4 | CA1510 CA1805 |
| TextData | 204 → 48 | 12 → 9 | CA1510 CA1834 CS0108 |
| TextDataUtil | 70 → 22 | 6 → 4 | CA1510 CA1805 |
| TextLogic | 76 → 16 | 5 → 3 | CA1510 CA1512 |
| TextModelImpl | 332 → 70 | 10 → 6 | CA1510 CA1512 CA1805 CA1834 |
| TextSearchImpl | 16 → 14 | 4 → 3 | CA1510 |
| TextUI | 120 → 14 | 4 → 3 | CA1510 |
| TextUIUtil | 22 → 14 | 5 → 3 | CA1510 CA1805 |
| UnitTests | 28 → 10 | 8 → 4 | CA1510 CA1805 CA1825 CA1834 |
| Xwt | 698 → 390 | 35 → 27 | CA1507 CA1825 CA1840 CA1846 CA1854 CA1860 CA1865 CA1866 |
| Xwt.Gtk3 | 666 → 548 | 29 → 24 | CA1507 CA1510 CA1805 CA1825 CA1847 |
| mdtool | 8 → 4 | 3 → 2 | CA1852 |

### Validation

| Command (inside `./scripts/pm`) | Result |
|---|---|
| `./scripts/build.sh --check` (Debug) | pass, warnings as errors with the new baselines |
| `./scripts/build.sh -c Release --check` | pass |
| `./scripts/ci.sh` (setup, lint, Release build, assemblies, `test.sh --no-build --parallel`, audit, mdtool, GUI, GUI errors, GUI modern and Wayland smokes) | all steps ok, 538 s (budget 900 s) |
| `./scripts/test.sh --no-build --parallel` (in `ci.sh`) | 12 test assemblies, 0 failed (3,913 passed, 177 skipped); coverage ratchet passed; `MonoDevelop.Ide.Gtk3.Tests` 117/117 on the first run |
