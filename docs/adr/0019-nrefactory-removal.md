# 0019 — NRefactory 5 is removed from the Linux build (not vendored)

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

`MonoDevelop.Ide`, `CSharpBinding` and `MonoDevelop.Refactoring` reference the NRefactory 5 libraries
(`ICSharpCode.NRefactory`, `.CSharp`, `.Cecil`) from the `main/external/nrefactory` submodule, which
has been archived since 2016 (net40/net45, its own copy of the mcs parser, Mono.Cecil 0.9). ADR 0005
listed it as "remove usages from the Ide, vendor a minimal subset if that fails" (task T069).

State of use in the code (2026-09-23, `grep -rlE 'ICSharpCode\.NRefactory[^6]'`; `NRefactory6` is
in-repo Roslyn-based code, not this library):

- `MonoDevelop.Ide`: 15 files. 13 only have a leftover `using`, or use names that also exist in
  MonoDevelop or Roslyn (`ISymbol`, `TextSegment`, `ISegment`, `DomRegion` …).
  `Ambience.GetCref` is the only real use, and it is dead code (no callers).
- Add-ins: 24 files, concentrated in the code-issue pad (`MonoDevelop.Refactoring/MonoDevelop.CodeIssues`),
  the legacy mcs parser (`CSharpBinding/MonoDevelop.CSharp.Parser/McsParser.cs`, `parse.cs`) and
  ASP.NET support. ASP.NET is already excluded by ADR 0017.

## Considered Options

1. Remove NRefactory 5 from the Linux build. Delete the dead code and the leftover `using`s, and
   replace the remaining uses with Roslyn or MonoDevelop types, one project at a time as each
   project is ported (chosen).
2. Vendor a minimal subset under `main/vendor/nrefactory/`. That is ~300 kLOC of unmaintained code,
   including a second C# parser, retargeted to net10.0 only to supply a handful of types.
3. Keep it as a NuGet package. The only NuGet release (5.5.1) is net40-only.

## Decision Outcome

Option 1:

- `MonoDevelop.Ide` (T069, validated when the SDK-style Ide builds in T070): leftover `using`s
  and `Ambience.GetCref` are removed. The Ide project has no NRefactory reference.
- `CSharpBinding` and `MonoDevelop.Refactoring` (M5c): each file that still needs an NRefactory type
  is ported to Roslyn when its project is converted. Code that only served the NRefactory-based
  code-issue pipeline or the mcs parser is excluded from the Linux compile list and recorded in
  `docs/BREAKING-CHANGES.md`.
- The `main/external/nrefactory` submodule is removed when no Linux project references it.

### Consequences

- Good: one less archived dependency and one C# parser instead of two. No vendored code to maintain.
- Bad: features that only existed on top of NRefactory (the old code-issue batch runner) need a
  Roslyn reimplementation or are dropped. Each drop is listed in BREAKING-CHANGES.
