# M3 evidence — headless core on .NET 10 (WS-1)

All commands run in the dev container (`./scripts/pm`): SDK 10.0.401, MSBuild 18.9.11, runtime
10.0.12, no Mono (`! command -v mono`).

## Walking skeleton (US1/US2)

- `mdtool` builds, cleans and reports errors for the SDK-style `linux-smoke` projects without Mono. The
  outputs are in [mdtool.md](mdtool.md), and `MdtoolContractTests` (T064) checks the same things
  automatically.
- `dotnet build main/MonoDevelop.Linux.sln` finishes with 0 errors. It builds Core, the MSBuild
  builder, CSharpBinding.Core, mdtool, the translations, the vendored Xwt/Mono.Addins.GuiGtk3 and the
  tests.

## Task → proof

| Task | Proof |
|---|---|
| T045 NativeLibraryMap | `NativeLibraryMapTests`: Windows names map to Linux sonames, and libc and GLib P/Invokes resolve |
| T046 Mono.Unix, code pages | `LinuxRuntimeTests.MonoUnixNativeCallsWork`, `LegacyCodePagesDecode`, `TextFileUtilityTests` |
| T047 translations at runtime | `MdtoolContractTests.MessagesAreTranslatedForTheUserLocale` (LANG=de_DE prints "Projektmappe wird geladen") |
| T048 catalogs | `po/MonoDevelop.Translations.csproj` produces `build/locale/<lang>/LC_MESSAGES/monodevelop.mo` (24 catalogs) |
| T049 CoreCLR runtime | `LinuxRuntimeTests.RuntimeIsCoreClrNotMono`, `MSBuildComesFromTheSdkOfTheCurrentRuntime` |
| T050 add-in resilience | `AddinResilienceTests`: a missing dependency is reported and its extensions are skipped |
| T051 round trip | `SdkRoundTripTests`: Smoke.sln and the SDK csproj files are unchanged after load and save |
| T054 SDK helpers | The helpers live in Core (`DotNetCoreTargetRuntime`, `DotNetCoreSdkInfo`); no `MonoDevelop.DotNetCore.Core` was needed. The project graph builds. |
| T061 cancellation | `BuildCancellationTests`: a 300 s build stops within 60 s of cancellation |
| T063 evaluator | `SdkEvaluationTests`: properties, reserved properties and Compile items match `dotnet msbuild` (DotDevelop 7045264a30 and 4518519b5e adapted) |
| T064 mdtool contract | `MdtoolContractTests` (10 cases) |

## Problems found and fixed along the way

- mdtool's `dotnet restore` left an MSBuild node running that held mdtool's stdout. It now uses
  `--disable-build-servers`.
- The test startup hook leaked into child `dotnet msbuild` processes (exit 134). It is now cleared
  after start-up.
- `ProjectCapabilityTests` removed the shared add-in registry entry of the test add-in, which caused
  about 220 cascading failures.
- `TextFileUtility`: `GetBuffer` padding, UTF-32 detected as UTF-16, and an unrequested BOM on
  `WriteText`.

Test results and coverage are in [../M4/README.md](../M4/README.md).
