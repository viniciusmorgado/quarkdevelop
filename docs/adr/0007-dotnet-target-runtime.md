# 0007 — .NET SDK target runtime

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

`SystemAssemblyService` expects a current `TargetRuntime`: `MonoTargetRuntime` (from
`MonoRuntimeInfo.FromCurrentRuntime`, null without Mono) or `MsNetTargetRuntime` (Windows only). On
.NET 10 on Linux neither exists → `NullReferenceException` at start-up
(`SystemAssemblyService.cs:74-77`).

## Decision Outcome

Add `DotNetCoreTargetRuntime` and `DotNetCoreTargetRuntimeFactory` in
`MonoDevelop.Core.Assemblies/`, registered under `/MonoDevelop/Core/Runtimes`:

- `IsRunning` when the host is CoreCLR; MSBuild bin path = the SDK directory located by
  Microsoft.Build.Locator (`dotnet --list-sdks` fallback).
- No GAC directories; framework assemblies resolved from `packs/Microsoft.NETCore.App.Ref`.
- When no SDK is found the runtime still exists but reports that it cannot build, with an
  actionable message (spec edge case).
- `SystemAssemblyService` is null-safe; Mono/MS.NET factories remain but yield nothing on Linux.

### Consequences

- Good: the project model initializes on .NET 10; user builds use the installed SDK.
- Bad: .NET Framework-only user projects cannot be built (documented in BREAKING-CHANGES).
