# 0005 — Third-party dependencies: NuGet or vendored in `main/vendor/`

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

15 git submodules point to archived mono/xamarin/microsoft repositories. The maintainer decided that
forks live in this same repository (no separate repos) and that the DotDevelop fork is used only for
cherry-picks.

## Considered Options

1. Maintained NuGet package, else vendored copy in `main/vendor/` (chosen)
2. Forks in separate GitHub repositories consumed as submodules (rejected by the maintainer)
3. Keep upstream submodules unmodified (they do not build on .NET 10)

## Decision Outcome

Order of preference per dependency: maintained NuGet package → vendored copy in
`main/vendor/<name>/` → drop. Each vendored directory has an `UPSTREAM.md` with origin URL, source
commit, license and a list of local patches. The corresponding submodule is removed from
`.gitmodules` in the same commit that introduces its replacement.

| Submodule | Destination |
|---|---|
| mono-addins | NuGet Mono.Addins, .Setup, .CecilReflector 1.4.1; `Mono.Addins.Gui` vendored + GTK3 |
| xwt | vendored (`Xwt`, `Xwt.Gtk3`), ported to GtkSharp 3 NuGet (Xwt NuGet is net472-only) |
| debugger-libs | vendored `Mono.Debugging` only (Soft debugger excluded) |
| guiunit | replaced by NUnit + in-repo UI-thread helper |
| nrefactory | usages removed; vendored minimal subset only if unavoidable |
| vs-editor-api | vendored text subset (no FPF/WindowsBase clones) |
| libgit2sharp, libgit2, libgit-binary | NuGet LibGit2Sharp 0.32.0 |
| nuget-binary | removed (SDK restore) |
| sharpsvn-binary, macdoc, mono-tools, mdtestharness, Xamarin.PropertyEditing | removed from the Linux build |

Update 2026-09-24 (T148): the last eight submodules (guiunit, nrefactory, nuget-binary, sharpsvn-binary, macdoc,
mono-tools, mdtestharness, Xamarin.PropertyEditing) are removed together with `.gitmodules`; the repository has no
submodules. Projects outside the Linux build that still point into `main/external/` (legacy test projects, Subversion,
Mac/Windows platform tests, the legacy `DownloadNupkg` restore in `main/msbuild/MDBuildTasks.targets`) cannot be built
from this repository any more.

Update 2026-09-25: `main/external` is removed. Its last content, the F# binding, moved to
`main/src/addins/FSharpBinding` (ADR 0027).

Code cherry-picked from DotDevelop (github.com/dotdevelop/dotdevelop) records the source commit in
the commit message and in `specs/001-linux-dotnet10-migration/research.md`.

### Consequences

- Good: reproducible, reviewable patches; no external repositories to maintain.
- Bad: vendored code must be kept buildable by us; licenses (MIT/LGPL) must be preserved in-tree.
