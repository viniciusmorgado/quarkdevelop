# M8 — security sweep (T127, 2026-09-24)

This sweep covers the 5,334 C# files compiled by `main/MonoDevelop.Linux.sln`, listed by
`./scripts/pm ./scripts/tools/compiled-files.sh`. Legacy code outside the Linux build (ADR 0017) is not
in scope.

## Summary

| Check | Result |
|---|---|
| `BinaryFormatter`, `SoapFormatter`, `NetDataContractSerializer`, `LosFormatter`, `ObjectStateFormatter` | **0 uses** (the only matches are comments that record the removals, ADR 0009) |
| .NET Remoting (`RemotingServices`, `ChannelServices`, channels, `ObjRef` marshalling) | **0 uses** |
| `MarshalByRefObject` as a base class | 19 files, inert (see below) |
| Json.NET `TypeNameHandling` other than `None` | 2 attributes in vendored vs-editor-api CodeLens data models, never deserialized by MonoDevelop |
| `UseShellExecute = true` | 1 (`HelpOperations.SearchHelpFor`, `monodoc` with quoted arguments) |
| Shell command lines (`bash -c`) | the external-terminal runners of GnomePlatform (see below) |
| XML with DTD processing | 2 sites, both without network entity resolution (see below) |
| `Assembly.LoadFrom`/`LoadFile` | 7 sites, all loading trusted local assemblies or the user's own project assemblies (see below) |
| Vulnerable packages (`./scripts/pm ./scripts/audit.sh`: `dotnet list package --vulnerable --include-transitive`) | **0** High/Critical, **0** Moderate/Low |
| `NuGetAudit` during restore | enabled (ADR 0004); a High or Critical advisory fails the restore in CI |

## Details

- **`MarshalByRefObject`.** It remains the base class of old cross-domain types:
  - `RemoteProcessObject`, `RemoteLogger`, `ProgressStatusMonitor`, `Counter`, the `InstrumentationServiceBackend`;
  - the AutoTest classes compiled into the Ide;
  - the DesignerSupport toolbox loader.

  On .NET the class exists but nothing marshals it: there are no application domains and no remoting
  channels. The AutoTest client entry points that used Remoting throw `PlatformNotSupportedException`,
  and the service logs a warning instead of publishing a remote session (ADR 0009, ADR 0017). Removing the base classes is cosmetic and changes public API, so it is left out.
- **DTD processing.**
  - `WelcomePageNewsFeed.GetSafeReaderSettings` parses DTDs with `XmlResolver = null`, so no external entity
    or DTD is fetched, and entity expansion is capped by .NET's default `MaxCharactersFromEntities`.
  - `XmlEditorService` validates the user's own XML files with a `LocalOnlyXmlResolver`, which refuses
    non-file URIs.
- **Shell.** `GnomePlatform` starts the user's program in an external terminal through
  `gnome-terminal … -x bash -c "'<program>' <args> ; <pause>"` (and the KDE, xfce and xterm variants). Program,
  arguments and directory come from the user's own run configuration, and the IDE runs that code anyway,
  so this is not a trust boundary. Paths are quoted with `EscapeDir`.
- **Assembly loading.**
  - `Runtime.cs` and `IdeStartup.cs` resolve application assemblies from the installation directory.
  - `SdkResolution.cs` loads MSBuild SDK resolvers from the installed .NET SDK.
  - Xwt's `GtkEngine` loads its own backend.
  - The DesignerSupport toolbox scans assemblies the user adds to the toolbox.
  - `NativeToolkitHelper` (Mac) is never reached on Linux.
- **Process creation.** There are 85 `Process.Start`, `new Process` and `ProcessStartInfo` sites. On .NET,
  `UseShellExecute` is false by default, and only the `monodoc` call above sets it.

## Commands

```bash
./scripts/pm bash -lc './scripts/tools/compiled-files.sh > out/compiled-files.txt'
./scripts/pm bash -lc 'xargs -d "\n" grep -nE "BinaryFormatter|SoapFormatter|NetDataContractSerializer|LosFormatter|ObjectStateFormatter" < out/compiled-files.txt'
./scripts/pm bash -lc 'xargs -d "\n" grep -nE "RemotingServices|ChannelServices|MarshalByRefObject" < out/compiled-files.txt'
./scripts/pm bash -lc 'xargs -d "\n" grep -nE "UseShellExecute *= *true|DtdProcessing\.Parse|TypeNameHandling\.(All|Auto|Objects|Arrays)|Assembly\.(LoadFrom|LoadFile)" < out/compiled-files.txt'
./scripts/pm ./scripts/audit.sh
```
