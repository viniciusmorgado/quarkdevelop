# T106: main-loop stall while loading the Linux solution

Command (inside the dev container):

```bash
MD_SMOKE_NO_BUILD=1 xvfb-run -a -s "-screen 0 1600x1000x24" \
  dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect main/MonoDevelop.Linux.sln
```

The smoke test adds a 50 ms GLib timeout on the main loop while `main/MonoDevelop.Linux.sln` loads
and for 5 s afterwards, then logs the longest interval between two ticks (`Smoke test: longest main
loop stall ...`). Target: at most 1 s.

| Run (2026-09-23, commit after 3b990fa4ec) | Projects | Longest stall |
|---|---|---|
| 1 | 50 | 107 ms |
| 2 | 50 | 92 ms |
| 3 | 50 | 78 ms |

Limits of this measurement: the C# language binding GUI add-in (T089) is not ported yet, so the
Roslyn workspace does less work than it will; the probe is repeated for the M5c evidence (T109).
