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
