# 0016 — Debugging user programs with netcoredbg

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The debugger engines target Mono (Soft debugger), Win32 (CorDebug) and GDB. The
`MonoDevelop.Debugger.VSCodeDebugProtocol` add-in provides an abstract `VSCodeDebuggerSession`
(DAP client) with no concrete engine for CoreCLR.

## Considered Options

1. netcoredbg (Samsung, MIT) via DAP.
2. vsdbg (Microsoft) — license restricts use to Microsoft products.
3. Mono soft debugger — Mono only.

## Decision Outcome

Option 1: implement `NetCoreDbgSession` + engine registration; adapter path from the container /
Flatpak (`netcoredbg --interpreter=vscode`); pinned release verified by SHA-256; protocol package
`Microsoft.VisualStudio.Shared.VsCodeDebugProtocol` 17.x. An automated DAP scenario test (breakpoint,
locals, step, exit code) guards it.

### Consequences

- Good: debugging of .NET 10 programs on Linux with an OSS adapter.
- Bad: feature parity (e.g. some expression evaluation cases) depends on netcoredbg.

## Amendment (2026-09-24, T112–T114): placement and implementation

**Placement: a new add-in, `main/src/addins/MonoDevelop.Debugger.NetCoreDbg`** (add-in id
`MonoDevelop.Debugger.NetCoreDbg`, engine id `MonoDevelop.Debugger.NetCoreDbg`), depending on Core, Ide,
Debugger and Debugger.VsCodeDebugProtocol only. Considered:

- *Inside `MonoDevelop.Debugger.VsCodeDebugProtocol`*: that add-in is the adapter-neutral DAP client; an engine
  for .NET projects there would need the DotNetCore add-in, which already depends on it (a cycle).
- *Inside `MonoDevelop.DotNetCore`*: no new project, but it mixes the project system with a debugger that is
  optional (netcoredbg may be missing, e.g. Flatpak without it) and could not be disabled on its own.
- *A separate add-in* (chosen, as DotDevelop did): four small source files, one manifest; it is the least
  invasive for the existing add-ins (no dependency added to any of them).

The engine recognizes execution commands that run a managed assembly with the dotnet host
(`dotnet program.dll args`, i.e. `ProcessExecutionCommand`, which `DotNetCoreExecutionCommand` is), so the
add-in does not reference DotNetCore; it is the default engine for them (the Mono soft debugger is excluded,
ADR 0017). netcoredbg is taken from the `MonoDevelop.Debugger.NetCoreDbg.Path` property when set, else from
`PATH` (the dev container installs the pinned release in `/usr/local/bin`). Engine and session are based on the
DotDevelop netcoredbg add-in (https://github.com/dotdevelop/monodevelop.netcoredbg, branch `dotdevelop`,
`0a52b6c4c6de32a66d6549d3c6abca8152bf310d`, MIT); its GTK2 options panel and bundled-netcoredbg path lookup
are not taken.

Changes to the DAP client needed for netcoredbg (`VSCodeDebuggerSession`): breakpoints are sent after
`launch` and before `configurationDone` (the DAP order; netcoredbg starts the program on `configurationDone`, so
breakpoints inserted on the later `process` event could miss the first lines), the `process` event is recorded
on the protocol thread and resets the session's cached process list (new protected
`DebuggerSession.ResetProcesses` in the vendored Mono.Debugging), `entry` stops are handled, and the add-in
imports `Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.dll` so that dependent add-ins can load it.

Tests: `MonoDevelop.Debugger.Tests` (`NetCoreDbgTests`: breakpoint, locals, step over, continue with exit
code, unhandled exception, over a net10.0 console program built by the test) and
`MonoDevelop.DotNetCore.Tests` (`NetCoreDbgEngineTests`: a .NET SDK project's execution command is debugged
with this engine). Debugging MonoDevelop itself: `scripts/debug.sh` and `.vscode/launch.json` (netcoredbg
through `pipeTransport`), see `docs/linux/setup.md`.
