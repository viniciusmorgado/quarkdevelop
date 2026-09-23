# T007 — DotDevelop as prior art (cherry-pick evaluation)

Clone: `/home/dev/prior-art/dotdevelop` (podman volume), cloned 2026-09-23. Also cloned the forks
`dd-vs-editor-api`, `dd-monodevelop.netcoredbg`, `dd-xwt` and `dd-mono-addins`, and fetched PR heads `pr10`, `pr130`, `pr149` and `pr150`.
Our base is `ba01d2d6d3`, the mono/monodevelop master tip.

## 1. Where it diverges

- **Merge-base with our HEAD:** `fb12ccb7f4` (2020-01-29, "Merge PR #9588"). The README cites `96b42aa074`, which is an ancestor of that commit.
- **Commits we have that DotDevelop does not:** 4 (`ca84d50373`, `65407e073f`, `61d6e00c3c`, `ba01d2d6d3`). None of them touch code.
- **`origin/main` (`7dc1f75236`, 2023-11-26):** 180 commits ahead of our base. Diff: 450 files, +10.7k/−11.4k lines. Of that, 18.6 % is `.nugetfallback/` and 6 % is `main/external/nuget-binary/`.
- **Branches:** `lytico/netcoredebug_build_fix` (2024-01) is main plus 1 doc commit. `develop`, `dotdevelop_oe_8.6`, `7.8.4` and `lytico/try_net48` add nothing beyond main. `dd/issues/40` (2021-01) has 18 unmerged commits (NuGet 5.8–5.9, `Microsoft.Build.Runtime`, a `net48` moniker). `origin/net5` and the other ~390 branches are copies of mono's.
- **GTK3 work exists only in unmerged PRs:**
  - PR #10: Jo Shields' 2018 "hackweek2" GTK# 3 port of mono/monodevelop (`b08b7c532f`, merge-base `6e3695ee72`, 22 commits, 729 files, −65k lines).
  - PR #149 (closed) and PR #150 (open draft, head `e6a6152f95`, 2025-07, 382 commits) by MakiWolf.
  - PR #154 (Windows GTK3 attempt, which does not start).
- **Repository state:** 336 stars, 72 open issues, last push 2024-06. The GitHub API returns 0 Actions runs.

## 2. Findings by topic

| Topic | What DotDevelop did | Evidence | Cherry-pick value |
|---|---|---|---|
| (a) SDK-style / net5+ | **No.** 5 SDK-style projects (same as upstream). Every IDE project still targets `$(MDFrameworkVersion)` / v4.7.2. | `git grep TargetFramework` on main and pr150 | none |
| (a') New .NET projects *as targets* | The IDE can create and evaluate net5.0/6.0 projects. It sets `MSBuildVersion=16.0` for net6+ SDKs. `IntrinsicFunctions` is resolved by reflection from `Microsoft.Build.dll` instead of the 513-line local copy. | `18270c51c1`, `12bba68fd5`, `21031632fd` (`TargetFramework.cs`, `DotNetCoreVersion.cs`), `4518519b5e`, `7045264a30`; net7/8 in draft PR #130 (`0958a61439`) | **Medium** for `7045264a30` and `21031632fd`. Low for the template and SDK lists, which are version churn we will redo. |
| (b) GTK3 | On main it is only a switch: `main/msbuild/ReferencesGtk.props` has `HaveGtkSharp=false` and 47 `#if GTK3/GTK_SHARP` guards; builds use `gtk-sharp2`. PR #150 turns on `GtkSharp 3.24.24.95` + `WebkitGtkSharp` and has ~22 `ExposeEvent`s left. The author says it "displays the main ide window" but nothing works. PR #10 uses system `gtk-sharp-3.0`, removes Stetic, has 5 `ExposeEvent`s left, and started porting OnExposeEvent→OnDrawn (`c5374d0332`) and GetPreferred* (`85160e10e0`). | `c7a790741e`, `ea73e5e872`, `dbf7afbbbb`, `92f12b0bce`, `a99df6fd68` (Gio→GtkSharp in GnomePlatform); lytico's Expose→Draw in PR #150: `dcd1055dbb`, `91ee4676aa`, `a609d7883f`, `fe39d4d649` | **Medium as a reference**, low as a direct pick: the work is incomplete, mixes in unrelated reverts, and depends on forked Xwt and Mono.Addins. PR #10's Stetic removal and Drawn ports are the most coherent diffs. |
| (c) Mono.Addins / Remoting / Mono.Posix | **Not replaced.** Mono.Addins is a fork (`dotdevelop/mono-addins@dotdevelop_gtksharp`, `cddcc80`) that adds a GuiGtk3 build on GtkSharp (`ec8fa46ac3`, `44e4039e34`). Remoting (39 files) and `Mono.Unix` (229 files) are unchanged. `Mono.Posix` is still referenced from `ReferencesGtk.props`. | `.gitmodules` | Low (Mono.Addins.GuiGtk3 only) |
| (d) .NET runtime / MSBuildLocator | The IDE still runs on **Mono**. `Microsoft.Build.Locator` went from 1.1.2 to 1.6.10. There is no DotNetCore host runtime. | `369281bf83`; `MonoDevelop.Core.csproj:88` | Low |
| (e) netcoredbg | Added submodules `main/external/Monodevelop.Netcoredbg`: `DotNetCoreDebuggerSession : VSCodeDebuggerSession`, MIT, © LeXtudio 2019 (Lex Li). They also added `Samsung.Netcoredbg`, built by hand with `build.sh`. Users set the debugger path in Preferences. | `379883b7c5`, fork commits `c585b74`…`0a52b6c` | **High**: small, MIT, uses our existing VsCodeDebugProtocol layer. Re-vendor it as a first-party add-in and use upstream Samsung/netcoredbg release binaries. |
| (f) Linux build / CI | `.github/workflows/monodevelop.yml` runs in an `ubuntu:20.04` container with mono-complete (preview repo, msbuild 16.10), `gtk-sharp2` and dotnet-sdk-6.0, then `./configure --profile=gnome && make`. It builds only, with no tests. `Building.md` needs `MSBuildEnableWorkloadResolver=false` when .NET 6+ is present. | `601031202c`, `89baf76ee1`, `c37bbe61a0` | Low (Mono-based; we already have the podman container) |
| (g1) md-addins | Removed the `VSEditorCoreDirectory` reference (commented out) and moved Mac-only code behind `DD_Mac_TODO`, and into `*.Mac.csproj` for Debugger and DesignerSupport. | `cab7d39538` (PR #138), `9f9ddad48f`, `231c194549`, `90831c65dd`, `72b5371629`, `c3595c09eb` | **Medium**: good map of the Mac-only seams. |
| (g2) VS editor 16.1.28 | They **build the editor from source**. `main/msbuild/ReferencesVSEditor.Gtk.props` is used on UNIX and imports `OpenSource.Def/Impl.projitems` from the fork `dotdevelop/vs-editor-api@dotdevelop_oe_8.1.5` (`71b2d6b`, 33 commits on top of `5ce6368`, MIT). That fork adds TextUIGtk, TextUIGtkUtil, TextUICommon and Gtk FPF shims. `CompositionManager` logs MEF errors and continues (`25b9e676f7`, marked `DD_VS_API_TODO`). The new editor is not shown to work on Linux; the legacy `SourceEditor2` is what runs. | `54982ad47f`, `87bb143136`, `704af9c73c` | **Medium**: this is the only known route that avoids the dead 16.1.28 packages. Evaluate it against dropping the VS editor for the legacy editor. |
| (g3) Roslyn 3.4.0-beta4 | Still pinned to `3.4.0-beta4-19568-04`. The 20 MB of `.nupkg` files for it are committed to `.nugetfallback/` (a local feed in `NuGet.config`). Their Roslyn 3.8 attempt was reverted (`684e65b133`). | `432497f732`, `4c0597eb27`, `a909ec7809` | **High (stop-gap)**: the vendored nupkgs unblock restore today. Roslyn is MIT, so redistribution is fine. |
| (g4) Other dead dependencies | LibGit2Sharp: the source submodules (libgit2, libgit-binary, libgit2sharp) are replaced by package `0.27.0-preview-g1da3cfaa68`. NuGet: the `nuget-binary` submodule is replaced by vendored NuGet 5.8 DLLs. myget.org and dead feeds are removed. The mono-addins package was bumped. | `216f01c79f`, `2356bb926d`, `c0e4657526`, `e3d7608c40`, `d61f45f44a`, `b03e3d8db3`, `a909ec7809`, `f769ce7b8c` | **High** for the LibGit2Sharp switch; low for the vendored NuGet DLLs (use PackageReference instead). |
| Misc | `CairoExtensions` loads native libs through a `LibraryTools` function-loader instead of hard-coded DllImport names, with a test. | `5586c55790`, `f0653f507a` | Medium (native-library resolution on .NET) |

## 3. Licensing

- **DotDevelop:** no top-level LICENSE; the GitHub API reports `license=null`. `main/COPYING` is LGPL 2.1 and was not changed after the fork. Contributions carry the per-file MIT/LGPL headers inherited from MonoDevelop, so they are compatible with our base.
- **Forks:** vs-editor-api (MIT, Microsoft), Monodevelop.Netcoredbg (MIT, LeXtudio), Samsung netcoredbg (MIT), GtkSharp (LGPL).
- **Attribution:** keep the original author and SHA in the trailers of any cherry-picked commit.

## 4. Does their Linux build work?

- **Partially, and only on Mono + GTK2.** Launch is `mono main/build/bin/MonoDevelop.exe` (Building.md). The build needs Ubuntu 20.04, mono-complete 6.12, and Mono's msbuild 16.x; distro xbuild fails with `MSBUILD0004`.
- **Newer distros fail:** #158 (Ubuntu 24.04), #156, #147 (Debian 11), #139 (Fedora), #122 (SDK 3.1/5.0 gone). PR #160 (2025-04) removes another dead feed "to fix the build". No Actions runs are retrievable.
- **Status:** a GTK2 build was restored in 2022–23; the project is now effectively dormant (#163, 2025-12), with GTK4/GirCore only discussed (#162). Neither GTK3 nor .NET ships on any branch.

## Recommendation

Use DotDevelop as a **source of targeted patches, not a base**. It never left .NET Framework, Mono or GTK2 on main.

- **Pick first:**
  - the `.nugetfallback` Roslyn feed (`432497f732`, `4c0597eb27`)
  - the LibGit2Sharp package switch (`216f01c79f`, `2356bb926d`)
  - the netcoredbg add-in (`379883b7c5` plus fork `0a52b6c`)
  - the reflection-based MSBuild `IntrinsicFunctions` (`7045264a30`)
  - the `MSBuildVersion` fix (`4518519b5e`)
  - the netstandard/.NET 5+ target-framework checks (`21031632fd`)
- **Study:**
  - the Mac-code isolation commits
  - `ReferencesVSEditor.Gtk.props` together with the vs-editor-api fork
  - PR #10 and PR #150 as GTK3 porting cookbooks
