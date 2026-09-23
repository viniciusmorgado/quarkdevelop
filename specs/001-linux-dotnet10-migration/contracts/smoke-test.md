# Contract: GUI smoke test

Command: `xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test <solution>`

1. Start the IDE, wait for the main window (timeout 60 s; start-up time is logged).
2. Open `<solution>` (default `main/tests/linux-smoke/Smoke.sln`), wait for load.
3. Build the solution; wait for completion.
4. Exit code: `0` if build succeeded with 0 errors and no unhandled exception was logged;
   `1` on build errors; `2` on start-up/load failure or timeout.
5. Writes `out/smoke/screenshot.png` (main window after build) and `out/smoke/ide.log`.
