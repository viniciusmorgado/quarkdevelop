# 0013 — Native library resolution without dllmap

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

GTK/GLib/Pango P/Invokes use Windows DLL names (`libglib-2.0-0.dll`, `libgtk-win32-2.0-0.dll`, …)
and rely on Mono `<dllmap>` rules in 7 `.dll.config` files. CoreCLR ignores dllmap.

## Decision Outcome

A shared `NativeLibraryMap` helper in MonoDevelop.Core registers
`NativeLibrary.SetDllImportResolver` for each assembly that declares P/Invokes, driven by a table
that replaces the `.dll.config` files (e.g. `libglib-2.0-0.dll` → `libglib-2.0.so.0`,
`libgtk-win32-2.0-0.dll` → `libgtk-3.so.0`, `libc` → `libc.so.6`). GTK2-specific imports are
rewritten to GTK3 equivalents or removed when the calling code is ported. Hot paths may move to
`[LibraryImport]` later. Windows/macOS imports are left untouched (excluded projects).

### Consequences

- Good: one place to maintain library names; works under Flatpak.
- Bad: every P/Invoke-owning assembly must call the registration once at start-up (module
  initializer).
