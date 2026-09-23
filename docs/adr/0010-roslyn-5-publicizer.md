# 0010 — Roslyn 5.9 with Publicizer for internal APIs

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

MonoDevelop uses Roslyn 3.4.0-beta4 (dead feed) and ~160 files use Roslyn *internal* namespaces
(`Shared.Extensions` 56, `Options` 48, `Host.Mef` 34, `CSharp.Extensions` 31, `Editor` 19, …). Access
relied on Roslyn granting `InternalsVisibleTo` to MonoDevelop assemblies signed with
`MonoDevelop-Public.snk`.

## Decision Drivers

Spike T008: Roslyn 5.9.0 assemblies carry 408 IVT grants and **none** for MonoDevelop, Xamarin or
VS for Mac, nor for MonoDevelop's public key. Spike T008 (b): Krafs.Publicizer 2.3.2 makes internal
members callable at compile time and emits `IgnoresAccessChecksToAttribute`, which CoreCLR honours at
run time (`SyntaxNodeExtensions.GetAncestor<T>`, `SyntaxTreeExtensions.IsInNonUserCode` verified).

## Considered Options

1. Roslyn 5.9 + Krafs.Publicizer, exact version pin.
2. Rewrite all C# services on public APIs.
3. Build a private Roslyn fork with IVT for MonoDevelop.

## Decision Outcome

Option 1. Pin `Microsoft.CodeAnalysis*` exactly (5.9.0) because internals change between releases;
`<Publicize Include="…"/>` only on the assemblies we actually need; prefer public API when a
replacement is cheap. EditorFeatures (not on nuget.org beyond 2.8.2) come from the dnceng
`dotnet-tools` feed only if required (ADR 0004 amendment).

### Consequences

- Good: C# features can be ported without a rewrite.
- Bad: every Roslyn upgrade may break internal call sites; upgrades are deliberate, tested changes.
