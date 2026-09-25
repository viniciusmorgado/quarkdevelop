# FR-009 — Git status, diff and history (T102, 2026-09-25)

Run in the dev container at `b2520b31b8`, after `./scripts/pm ./scripts/build.sh`:

```bash
./scripts/pm bash -lc 'xvfb-run -a dotnet test main/src/addins/VersionControl/MonoDevelop.VersionControl.Git.Tests/MonoDevelop.VersionControl.Git.Tests.csproj \
  --no-build --filter "Category!=Quarantine" --logger "trx;LogFileName=git.trx" --results-directory out/git-tests'
```

Result:

```text
Passed!  - Failed:     0, Passed:    62, Skipped:    10, Total:    72, Duration: 3 s - MonoDevelop.VersionControl.Git.Tests.dll (net10.0)
```

The suite creates temporary Git repositories with LibGit2Sharp 0.32 and drives them through the IDE's
`GitRepository` (the version control layer behind the Version Control menu, the status icons and the diff, blame
and log views). None of its cases is quarantined (`docs/evidence/M4/quarantine.md`).

| Class (from the TRX) | Passed | Skipped |
|---|---|---|
| `BaseGitUtilsTest` (the Git repository tests, including the shared version-control tests) | 52 | 9 |
| `GitIntegrityTests` | 7 | 0 |
| `EditorCompareWidgetBaseTest` (diff view) | 3 | 1 |

Coverage of the three FR-009 features, by passing test name:
- **Status**: `TestGitStagedModifiedStatus` (2 cases), `TestGitStagedNewFileStatus(False)`, `FileIsCommitted`.
- **Diff**: `TestPushDiff`, `BlameDiffWithNotCommitedItem` (3 cases), the 3 passing `EditorCompareWidgetBaseTest`
  cases.
- **History**: `GetCommitChangesFullHistory`, the `RevisionFormatMessage*` cases and `BlameWithWorkingChanges`, plus
  revert, branch, stash and merge cases.

The 10 skipped cases carry an upstream `[Ignore]` with a reason, and none is quarantined: `LogSinceWorksAsync` (3
cases, "failing on Wrench (Windows) ... symlinks"), `LocksEntities` and `UnlocksEntities` (not implemented in
`GitRepository`), `RevertsRevision`, `TestGitBranchCreation(True)`, `TestGitStagedNewFileStatus(True)`,
`TestGitRecursiveCloneFailsAndDoesntCrash` and `TestDarkColorsAreDarker`. The same suite runs in every
`scripts/ci.sh` gate (lane b of `scripts/test.sh --parallel`).
