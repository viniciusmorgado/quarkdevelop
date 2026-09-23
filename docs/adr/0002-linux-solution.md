# 0002 — Primary `MonoDevelop.Linux.sln` growing by waves

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

`main/Main.sln` has 218 entries (164 projects incl. 68 in submodules, 8 configurations for
Gnome/Mac/Win32). None of it builds on .NET 10, and many projects never will on Linux. We need a
green build from the first migrated project on.

## Considered Options

1. New classic `main/MonoDevelop.Linux.sln` that grows per conversion wave.
2. `.slnx` solution.
3. Solution filter (`.slnf`) over `Main.sln`.
4. Edit `Main.sln` in place.

## Decision Outcome

Option 1. The IDE's own solution parser (`MonoDevelop.Projects.MSBuild/SlnFile.cs`) cannot read
`.slnx`, so the IDE could not open its own source (option 2). A filter still loads every legacy
entry (3). Editing in place gives no green baseline (4). Configurations: `Debug`/`Release` only.
When milestone M5 completes, `Main.sln` is replaced by the Linux solution (Linux-only focus).

### Consequences

- Good: `dotnet build main/MonoDevelop.Linux.sln` is the single, always-green entry point.
- Bad: two solutions coexist until M5; `Main.sln` is not maintained meanwhile.
