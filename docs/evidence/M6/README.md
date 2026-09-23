# M6 evidence — CI gate

`scripts/ci.sh` runs the whole Linux gate in the dev container, in this order: setup, lint,
build `--check` (Release), duplicate-assembly check, tests with the coverage ratchet, vulnerability
audit, mdtool smoke, and GUI smoke. `.github/workflows/ci.yml` runs the same script
(`./scripts/pm ./scripts/ci.sh`), writes `out/ci/summary.txt` to the job summary, and uploads
`out/ci`, `out/coverage` and the TRX files as the artifact `monodevelop-ci-<version>+<sha>`.

## Local run (2026-09-23, M5c)

[ci-local-summary.txt](ci-local-summary.txt):

```text
setup              ok      1s
lint               ok      1s
build              ok     92s
assemblies         ok      1s
test               ok    263s
audit              ok     15s
mdtool-smoke       ok      5s
gui-smoke          ok      7s
wayland-smoke      ok      7s
total                    392s (budget 900s)
```

- **Tests:** MonoDevelop.Core.Tests 1068 passed, 0 failed, 9 skipped (Category!=Quarantine);
  MonoDevelop.Ide.Gtk3.Tests 36 passed. Coverage (Release, the CI configuration): MonoDevelop.Core 63.5%,
  MonoDevelop.Ide 0.45%, product total 16.4% (ratchet baseline 62.8 / 0.42 / 16.38).
- **Build timing:** incremental locally; a fresh runner also restores packages and compiles the
  whole solution. The SC-007 budget (15 min) is enforced by the script itself.
- **GUI smoke (X11):** the IDE's `--smoke-test` under Xvfb opens a copy of `linux-smoke/Smoke.sln`
  and builds it: 0 errors, exit 0 ([T103-smoke-x11.png](../M5/T103-smoke-x11.png)).
- **Wayland smoke:** the same on a headless Weston compositor with `GDK_BACKEND=wayland` and no X
  display; the log confirms the GDK display `wayland-md` ([T104-smoke-wayland.png](../M5/T104-smoke-wayland.png)).
  Weston's headless backend has no input devices, so GDK logs criticals for the missing seat.
- **Hosted run:** no hosted GitHub Actions run yet. Pushing needs the maintainer's authorization
  (T119).
