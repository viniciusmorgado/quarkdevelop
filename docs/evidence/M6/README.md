# M6 evidence — CI gate

`scripts/ci.sh` runs the whole Linux gate in the dev container, in this order: setup, lint,
build `--check` (Release), duplicate-assembly check, tests with the coverage ratchet, vulnerability
audit, mdtool smoke, and GUI smoke. `.github/workflows/ci.yml` runs the same script
(`./scripts/pm ./scripts/ci.sh`), writes `out/ci/summary.txt` to the job summary, and uploads
`out/ci`, `out/coverage` and the TRX files as the artifact `monodevelop-ci-<version>+<sha>`.

## Local run (2026-09-23)

[ci-local-summary.txt](ci-local-summary.txt):

```text
setup              ok      1s
lint               ok      0s
build              ok     22s
assemblies         ok      1s
test               ok    244s
audit              ok     18s
mdtool-smoke       ok      5s
gui-smoke          ok     10s
total                    301s (budget 900s)
```

- **Tests:** 850 passed, 0 failed, 9 skipped (Category!=Quarantine). MonoDevelop.Core line
  coverage is 57.8%, against the ratchet baseline of 57.6%.
- **Build timing:** the local build was incremental. A fresh runner also restores packages and
  compiles the whole solution. The SC-007 budget (15 min) is enforced by the script itself.
- **GUI smoke:** until the IDE starts (M5c), this step shows the Xwt sample gallery on GTK3 under
  Xvfb ([gui-smoke.png](gui-smoke.png)).
- **Hosted run:** no hosted GitHub Actions run yet. Pushing needs the maintainer's authorization
  (T119).
