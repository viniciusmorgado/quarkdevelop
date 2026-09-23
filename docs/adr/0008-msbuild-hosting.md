# 0008 — MSBuild hosting on .NET 10

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

MonoDevelop evaluates projects with its own in-process evaluator (`DefaultMSBuildEngine`) and runs
builds in an out-of-process builder (`MonoDevelop.MSBuildBuilder`, TCP + `BinaryMessage`), which was
launched with Mono and got MSBuild from Mono's installation (`$(MSBuildToolsPath)`), copying the
MSBuild bin directory and patching `exe.config`.

## Decision Outcome

- Reference `Microsoft.Build*` 17.x with `ExcludeAssets=runtime`; call
  `MSBuildLocator.RegisterInstance` (SDK instance) first thing in `mdtool`, `MonoDevelop.Startup` and
  the builder, before any MSBuild type is loaded.
- Evaluation stays in-process in the custom evaluator for now (backlog: switch to
  `ProjectInstance`, B35); an evaluation-diff test against `dotnet msbuild -getItem` guards drift.
- The builder targets `net10.0`, is launched with `dotnet exec`, receives SDK paths via environment
  (`MSBUILD_EXE_PATH`, `MSBuildExtensionsPath`, `MSBUILDADDITIONALSDKRESOLVERSFOLDER`), no longer
  copies MSBuild or patches config; cancellation uses `BuildManager.CancelAllSubmissions` instead of
  `Thread.Abort`.

### Consequences

- Good: builds use the same MSBuild as `dotnet build`; no Mono.
- Bad: the custom evaluator may diverge from SDK 10 semantics until replaced.
