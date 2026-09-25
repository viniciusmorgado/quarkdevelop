# Contract: GUI smoke test

Command: `xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test [-no-redirect] [<solution or project>]`
(implementation: `main/src/core/MonoDevelop.Ide/MonoDevelop.Ide/SmokeTest.cs`; CI steps in `scripts/ci.sh`).

## Steps

1. Start the IDE and wait for the main window (60 s at most; the log records the time since process start and the
   GDK display, e.g. `:99` or `wayland-0`).
2. Open `<solution or project>` (a `.sln` or `.csproj`; default `main/tests/linux-smoke/Smoke.sln`; a project is
   wrapped in a solution of its own) and wait for the load; the log records the longest main-loop stall (T106).
3. If `MD_SMOKE_NEW_PROJECT` or `MD_SMOKE_NEW_FILE` is set, show those dialogs (below).
4. Unless `MD_SMOKE_NO_BUILD=1`, build the solution and wait for completion; the log says
   `build finished with <n> errors, <m> warnings` and lists each error.
5. On build errors, activate the first row of the Errors pad (as a double-click does) and check that the editor
   opens that file with the caret on the error line (T105); the log says
   `error list navigation opened <file> at line <n>`.
6. If `MD_SMOKE_DEBUG=1` and the build had 0 errors, debug the startup project (below).
7. If `MD_SMOKE_OPEN` is set, open that file (below).
8. Save `screenshot.png` (main window) and exit; the log's last line is
   `Smoke test: exit code <code> (<reason>) after <s> s`.

## Outputs

`<out>/ide.log` and `<out>/screenshot.png`, plus `new-project.png` and `new-file.png` when those dialogs are shown.
`<out>` is `MD_SMOKE_OUT`, or `out/smoke` relative to the working directory.

## Environment variables

All are off by default.

| Variable | Effect |
|---|---|
| `MD_SMOKE_OUT=<dir>` | Output directory instead of `out/smoke` (every `scripts/ci.sh` smoke sets it). |
| `MD_SMOKE_TIMEOUT=<s>` | Watchdog for the whole run, in seconds (default 600). When it fires, the run exits with 2 (`timeout after <s> s`). |
| `MD_SMOKE_NO_BUILD=1` | Stop after the load (and `MD_SMOKE_OPEN`): no build, no navigation, no debugging. Exit code 0 unless a check below fails. |
| `MD_SMOKE_NEW_PROJECT=1` | After the load, show the New Project dialog with the C# console template selected; the log says `New Project dialog categories: <list>; selected <template> (<languages>)`; save `new-project.png`. No categories, no selected template, or a console template with one language exit with 2 (T152). |
| `MD_SMOKE_NEW_FILE=1` | After the load, show the New File dialog of the first project on the `class` item template; the log says `New File dialog shown for <project>`; save `new-file.png` (T152). |
| `MD_SMOKE_OPEN=<file>` | Relative to the solution's directory. Before the screenshot, open that file in the editor. A C# file is parsed with the parse options of its project in the IDE's Roslyn workspace; the log says `<file> parses as C# <version> with <n> syntax errors`. Its project is then compiled in the workspace, source generators included; the log says `<project> compiles in the workspace with <n> errors, ... (<m> source-generated documents, first compilation <s> s)`. Syntax or compilation errors exit with 2 (T138, T146, T147). |
| `MD_SMOKE_GOTO=<method>` | With `MD_SMOKE_OPEN`: go to the definition of that partial method of the project, which a source generator implements. The editor must open the read-only generated document; the log says `go to definition of <method> opened <file> at line <n>, read-only: <bool>`. A failure exits with 2 (T147). |
| `MD_SMOKE_DEBUG=1` | Needs a build with 0 errors. Set a breakpoint on the first line of the startup project's `Program.cs`, start debugging (netcoredbg) and wait until the debugger stops there; the log says `the debugger stopped at the breakpoint Program.cs:1`. The Locals pad is brought to the front, and the screenshot is taken while stopped. A failure exits with 2 (T112, T153). |

## Exit codes

- `0`: the solution loaded and built with 0 errors, every requested check passed, no unhandled exception and no
  GLib-GObject critical was logged.
- `1`: the build had errors (and error navigation, if run, worked). `scripts/ci.sh` expects this for `Broken`.
- `2`: any other failure, checked in this order after the build: failed error navigation, failed `MD_SMOKE_OPEN`
  (or `MD_SMOKE_GOTO`), failed debugging, GLib-GObject criticals (then build errors give 1), unhandled exceptions.
  Also: main window not shown within 60 s, the solution could not be opened or did not load as a solution, a
  failed template dialog check, the watchdog, or an exception in the smoke test itself.
- **GLib-GObject rule (T153):** a critical or error of the `GLib-GObject` log domain (e.g. `g_object_remove_toggle_ref`
  or `g_object_unref` on a freed instance) gives exit code `2` even when everything else passed; the log says
  `<n> GLib-GObject criticals were logged`. Criticals of other domains (the Wayland run logs `Gdk` criticals for its
  missing seat) do not change the exit code.

## Uses in CI (`scripts/ci.sh`)

| Step | Input | Variables | Expected |
|---|---|---|---|
| `gui-smoke` | `Smoke.sln` (X11) | `MD_SMOKE_OUT`, `MD_SMOKE_NEW_PROJECT`, `MD_SMOKE_NEW_FILE` | exit 0, non-blank screenshots, New Project categories line |
| `gui-smoke-debug` | `Smoke.sln` (X11) | `MD_SMOKE_OUT`, `MD_SMOKE_DEBUG` | exit 0, breakpoint line |
| `gui-smoke-errors` | `Broken/Broken.csproj` (X11) | `MD_SMOKE_OUT` | exit 1, `error list navigation opened Program.cs at line 2` |
| `gui-smoke-modern` | `Modern.sln` (X11) | `MD_SMOKE_OUT`, `MD_SMOKE_OPEN`, `MD_SMOKE_GOTO` | exit 0, C# 14, 0 workspace errors, generated document opened |
| `wayland-smoke` | `Smoke.sln` (headless Weston, `GDK_BACKEND=wayland`) | `MD_SMOKE_OUT` | exit 0, Wayland display, non-blank screenshot |
