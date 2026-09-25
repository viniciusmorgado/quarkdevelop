# 0004 — Central Package Management and nuget.org-only feeds

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

`NuGet.config` lists 13 feeds, most of them dead (dotnet.myget.org, myget vs-editor, appveyor,
dotnetfeed.blob). Versions are spread across `Directory.Build.props` properties and csproj files.
Several pinned packages have known vulnerabilities (Newtonsoft.Json 12.0.x, SharpZipLib 1.2.0).

## Considered Options

1. Central Package Management + nuget.org-only feeds + lock files (chosen)
2. Keep per-project versions and the old feed list (dead feeds break restore)
3. Mirror the old packages into a private feed (not reproducible for contributors; license unclear for vs-impl packages)

## Decision Outcome

- `main/Directory.Packages.props` with `ManagePackageVersionsCentrally=true` for all converted
  projects.
- `NuGet.config` restricted to nuget.org with package source mapping. The public dnceng
  `dotnet-tools` feed (Microsoft, MIT-licensed Roslyn builds) may be added **only** for
  `Microsoft.CodeAnalysis.EditorFeatures*` if the IDE's C# add-ins require it (amend this ADR).
- `RestorePackagesWithLockFile=true`; CI restores in locked mode; `NuGetAudit` enabled.
- Removed: myget/appveyor/dotnetfeed feeds, `external/nuget-binary/nuget.exe` restore, paket for
  fsharpbinding (F# deferred).

Update 2026-09-25: the packages of the F# binding are in `main/Directory.Packages.props`, and Paket is removed with the
binding's build files (ADR 0027).

### Consequences

- Good: reproducible restores, one-line upgrades, vulnerability audit.
- Bad: packages that only existed on dead feeds (Roslyn 3.4 beta, VS editor 16.1.28) must be
  replaced (ADR 0010, ADR 0012).
