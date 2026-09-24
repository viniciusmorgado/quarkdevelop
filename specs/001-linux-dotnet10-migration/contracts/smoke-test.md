# Contract: GUI smoke test

Command: `xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test <solution>`

1. Start the IDE, wait for the main window (timeout 60 s; start-up time is logged).
2. Open `<solution>` (default `main/tests/linux-smoke/Smoke.sln`), wait for load.
3. Build the solution; wait for completion.
4. On build errors, activate the first row of the Errors pad (as a double-click does) and check that the
   editor opens that file with the caret on the error line (T105); the log says
   `error list navigation opened <file> at line <n>`.
5. Exit code: `0` if build succeeded with 0 errors and no unhandled exception was logged;
   `1` on build errors; `2` on start-up/load failure, timeout or failed error navigation.
6. Writes `out/smoke/screenshot.png` (main window after build) and `out/smoke/ide.log`.
7. `MD_SMOKE_OPEN=<file>` (relative to the solution's directory; off by default): before the screenshot, open
   that file in the editor. A C# file is also parsed with the parse options of its project in the IDE's Roslyn
   workspace; the log says `<file> parses as C# <version> with <n> syntax errors`, and syntax errors give exit
   code `2` (T138).
   The file's project is then compiled in the workspace, source generators included; the log says
   `<project> compiles in the workspace with <n> errors, ... (<m> source-generated documents, first compilation <s> s)`,
   and errors give exit code `2` (T146, T147).
8. `MD_SMOKE_GOTO=<method>` (with `MD_SMOKE_OPEN`; off by default): go to the definition of that partial method of
   the project, which a source generator implements. The editor must open the read-only copy of the generated
   document; the log says `go to definition of <method> opened <file> at line <n>, read-only: <bool>`, and a failure
   gives exit code `2` (T147).
