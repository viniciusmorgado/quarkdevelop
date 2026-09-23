# 0006 — Mono.Addins 1.4.1 on CoreCLR

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The whole IDE is built on Mono.Addins (390 files, 70 manifests). The submodule version targets
.NET Framework. It was unknown whether the add-in registry scan (historically able to spawn a
`mono` setup process) and assembly loading work on CoreCLR.

## Decision Outcome

Use Mono.Addins, Mono.Addins.Setup and Mono.Addins.CecilReflector 1.4.1 from NuGet
(netstandard2.0 assets). All add-in assemblies load into the single default
`AssemblyLoadContext`; the registry update runs in-process. Tests use an isolated registry directory
per run (the test host is the entry assembly, so start-up directory heuristics differ).
Vendor `mono-addins` only if a CoreCLR bug requires a patch.

Evidence: spike T005 (`spikes/addins-host`, `spikes/addins-plugin`): root add-in declared by an
embedded manifest, `.addins` file pointing to `AddIns/`, plugin built separately; XML extension
nodes and `TypeExtensionPoint` objects loaded on .NET 10.0.12; scan in-process in ~1 ms.

### Consequences

- Good: no rewrite of the extension model; manifests and extension paths are preserved.
- Bad: no add-in unloading (same as before); per-add-in isolation is not possible (MEF/Roslyn need
  single type identity anyway).
