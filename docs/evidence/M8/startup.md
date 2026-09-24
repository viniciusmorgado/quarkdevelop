# M8 — start-up time (T129, 2026-09-24)

NFR-001: the IDE shows its main window within 10 s on the reference machine.

Method:
- `--smoke-test` with `MD_SMOKE_NO_BUILD=1` opens `main/tests/linux-smoke/Smoke.sln` without building it.
- The time is the smoke test's log line `main window shown N s after the process started`, measured from the
  process start time (`Process.StartTime`) to the first visible frame of the workbench.
- Environment: the dev container on the reference machine (12 cores), under Xvfb, with a local build.
- **Cold** runs use a fresh profile each time: no add-in registry, no MEF composition cache, no settings.
- **Warm** runs reuse one profile, as a returning user would.

| Run | Load average when measured | Main window after process start |
|---|---|---|
| cold 1, 2 | ~3 (idle machine) | 2.3 s, 2.2 s |
| cold 3, 4, 5 | rising to 45 (two parallel builds and test runs on the machine) | 4.3 s, 5.1 s, 4.9 s |
| warm 1–5 | ~45 | 4.7 s, 2.7 s, 3.2 s, 2.3 s, 2.6 s |

The CI smoke runs of the same day, on an otherwise idle container, match the idle numbers:
- X11: 2.2 s;
- Wayland: 1.7 s;
- Broken: 2.4 s;
- Modern: 2.3 s.

All cold starts. See `docs/evidence/M5/README.md`, T109.

**Result:** 2.2 s cold on an idle machine, and at most 5.1 s with the machine fully loaded. Both are within the 10 s
target. Every run exited with code 0.

To reproduce:

```bash
./scripts/pm bash -lc 'd=$(mktemp -d); cp -r main/tests/linux-smoke/. $d/; p=$(mktemp -d); \
  XDG_CONFIG_HOME=$p/c XDG_DATA_HOME=$p/d XDG_CACHE_HOME=$p/k MD_SMOKE_OUT=out/startup MD_SMOKE_NO_BUILD=1 \
  xvfb-run -a -s "-screen 0 1600x1000x24" dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect $d/Smoke.sln; \
  grep "main window shown" out/startup/ide.log'
```
