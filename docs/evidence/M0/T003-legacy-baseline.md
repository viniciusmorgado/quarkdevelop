# T003: Legacy baseline build/test with Mono (spike)

Date: 2026-09-23. Time-boxed to about 45 minutes; about 17 minutes used. Code untouched: no tracked file was edited, and all build output was removed afterwards.

## Environment

- Throwaway image `localhost/md-mono-baseline`, built `FROM docker.io/library/mono:6.12` (Debian 10 "buster"). Its apt sources point at `archive.debian.org` because buster is EOL. Added packages: autoconf, automake, libtool, gettext, make, git, gtk-sharp2, libgtk2.0-cil-dev and related packages.
- Toolchain: Mono 6.12.0.182, MSBuild 16.10.1 (Mono). **No .NET Core SDK.**
- Everything ran through `PM_IMAGE=localhost/md-mono-baseline ./scripts/pm ...`, with `NUGET_PACKAGES=/home/dev/mono-baseline/nuget`.

## Documented path: `./configure --profile=gnome && make`

1. **`./configure --profile=gnome`: OK.** Autotools configure of `main/` succeeds. The only warning is `unrecognized options: --with-macarch`.
2. **`make` fails immediately.** The `print_config` target runs `dotnet --version`:
   `make: dotnet: Command not found` / `make: *** [Makefile:43: print_config] Error 127`.
   The legacy build expects a .NET Core SDK in `/usr/local/share/dotnet` next to Mono.
3. **`make -C main restore-packages` fails.** This step runs `mono external/nuget-binary/nuget.exe restore` on `Main.sln`, which auto-detected MSBuild "15.0". Errors:
   - `error MSB4236: The SDK 'Xamarin.Mac.Sdk' specified could not be found.` This hits vs-editor-api's BraceCompletionImpl, TextUICocoa, WindowsBase, PresentationCore and PresentationFramework. Those projects pin `Xamarin.Mac.Sdk`/`Xamarin.MSBuild.Sdk` 0.31.0 in `main/external/vs-editor-api/global.json`.
   - `Unable to find package Xamarin.Mac.Sdk. No packages exist with this id in source(s): local-build, mirepoix, nuget.org, Roslyn Nightlies`
   - `Unable to locate the .NET SDK`
   - `Unable to load the service index` for `dotnet.myget.org/F/roslyn` and `myget.org/F/mirepoix`

## Feed liveness (probed from the container)

| Feed | Result |
|---|---|
| dotnet.myget.org (roslyn, vstest, templating, msbuild, nuget-build) | dead (connection fails) |
| myget.org (vs-editor, mirepoix, azure-appservice) | dead |
| dotnetfeed.blob.core.windows.net | HTTP 403 |
| ci.appveyor.com/nuget/nugetizer3000 | 200 |
| nuget.org, Azure vside `vs-impl`, `vssdk` | live |

## Package resolvability

All `PackageReference` versions from `src/`, `tests/`, `msbuild/`, vs-editor-api and mono-addins (100 id/version pairs) were checked against the nuget.org, vs-impl and vssdk flat containers.

**None of the pinned versions below exists on any live feed:**

- **Roslyn `3.4.0-beta4-19568-04`**: CSharp, CSharp.Workspaces, EditorFeatures, EditorFeatures.Wpf, InteractiveHost, Workspaces.MSBuild.
  - nuget.org has `3.4.0` and `3.4.0-beta4-final` for CSharp/Workspaces, but EditorFeatures on nuget.org stops at 2.8.2.
  - vssdk only keeps EditorFeatures from 3.11.0-4.x onward.
- **VS editor `16.1.28-g2ad4df7366`**: CoreUtility, Text.Data/Logic/UI/UI.Wpf/Internal, Language, Language.Intellisense, Language.NavigateTo.Interfaces, Platform.VSEditor.
  - nuget.org has 16.1.89 for some of these.
  - vs-impl Platform.VSEditor starts at 17.4.
- **`Microsoft.VisualStudio.CodingConventions 1.1.20180503.2`**: the id is absent from all three feeds.
- **Others:** `Microsoft.VisualStudio.Imaging 16.0.27828`, `Microsoft.TemplateEngine.* 3.0.0-rc1.19464.2`, `DiagnosticMargin 1.0.1`, `Xamarin.Mac.Sdk`/`Xamarin.MSBuild.Sdk 0.31.0`, `Xamarin.TestReporting`.
- Everything else resolves on nuget.org: NUnit 2.7.0/3.9.0, Newtonsoft 12.0.2, NuGet 5.4.0, Mono.Cecil 0.10.1, StreamJsonRpc 1.5.43, the test-project packages, and so on.

## Partial attempt: MonoDevelop.Core

```
msbuild /t:Restore /p:RestoreConfigFile=<scratchpad>/NuGet.override.config \
  src/core/MonoDevelop.Core/MonoDevelop.Core.csproj
```

The override config lists only nuget.org, vssdk and vs-impl.

- **What restored:** the 34 vs-editor-api "Def/Impl" projects that use `Microsoft.NET.Sdk`.
- **Core itself failed:**
  - `NU1603`: EditorFeatures 3.4.0-beta4-19568-04 was not found, and NuGet fell back to 3.11.0-4.25056.4.
  - `Unable to find package Microsoft.CodeAnalysis.CSharp.Features with version (= 3.11.0-4.25056.4)`, and the same for other packages.
  - `Unable to find package Microsoft.VisualStudio.CodingConventions. No packages exist with this id`
  - A version conflict on Workspaces.Common.

MonoDevelop.Core, and therefore MonoDevelop.Core.Tests and everything downstream of it, **cannot be built without changing package versions and code**. Once that is done it is no longer a baseline.

The private `md-addins` repo (`MdAddinsDirectory`, `version-checks`) is not needed on this Linux path. vs-editor-api is consumed from source (`ReferencesVSEditor.Mac.props` → `OpenSource.*.projitems`), so md-addins was not the blocker.

## What could be baselined: mono-addins (submodule 7829340)

It has no dependency on Roslyn or the VS editor.

```
cd main/external/mono-addins
msbuild /restore /p:RestoreConfigFile=<override> Mono.Addins.sln   # 0 errors
mono NUnit.ConsoleRunner.3.9.0/tools/nunit3-console.exe Test/UnitTests/bin/Debug/UnitTests.dll
```

- **Result: 81 tests, 81 passed, 0 failed, 0 skipped** (about 10 s, net461 on Mono 6.12).
- Building only `UnitTests.csproj`, without the test add-in projects in the solution, gives 44/81 passed. That is a setup problem, not a regression. The full solution must be built first.

## Conclusion

- **The full legacy baseline, with MonoDevelop.Core.Tests and the other UnitTests, is not feasible without modifications.** The blockers:
  1. The Roslyn 3.4.0-beta4-19568-04 build is lost.
  2. The VS editor 16.1.28-g2ad4df7366 build and CodingConventions are lost.
  3. vs-editor-api needs the Xamarin.Mac.Sdk and a .NET Core SDK.
  4. The myget feeds are dead.
- **Recovered baseline:** mono-addins, 81/81 passing on Mono 6.12. Use this as the pre-migration reference for the add-in engine.
- **Before/after comparison for the rest of the codebase:** use the test inventory from the source (test names and attributes), not a pre-migration run.

## Cleanup

Removed everything the spike created:

- configure outputs: `config.make`, `local-config/`, `main/Makefile*`, `main/configure`, `autom4te.cache`, and so on
- the `obj/` directories in `MonoDevelop.Core` and `MDBuildTasks`
- vs-editor-api `obj/`
- mono-addins `bin/`, `obj/` and `Test/tmp`

The cached packages stay in `/home/dev/mono-baseline/`, and the image `localhost/md-mono-baseline` is kept for reuse.
