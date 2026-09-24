# Architecture Decision Records

Format: [MADR](https://adr.github.io/madr/). New ADRs take the next number; superseded ADRs keep
their file and link to the replacement. Context for all of them: `specs/001-linux-dotnet10-migration/`.

| # | Title | Status |
|---|---|---|
| [0001](0001-spec-driven-development.md) | Spec-driven development and MADR decision records | Accepted |
| [0002](0002-linux-solution.md) | Primary `MonoDevelop.Linux.sln` growing by waves | Accepted |
| [0003](0003-sdk-style-in-place-conversion.md) | In-place SDK-style conversion to net10.0 | Accepted |
| [0004](0004-central-package-management-and-feeds.md) | Central Package Management and nuget.org-only feeds | Accepted |
| [0005](0005-third-party-dependencies.md) | Third-party dependencies: NuGet or vendored in `main/vendor/` | Accepted |
| [0006](0006-mono-addins-on-coreclr.md) | Mono.Addins 1.4.1 on CoreCLR | Accepted |
| [0007](0007-dotnet-target-runtime.md) | .NET SDK target runtime | Accepted |
| [0008](0008-msbuild-hosting.md) | MSBuild hosting on .NET 10 | Accepted |
| [0009](0009-remove-remoting-binaryformatter.md) | Remove .NET Remoting and BinaryFormatter | Accepted |
| [0010](0010-roslyn-5-publicizer.md) | Roslyn 5.9 with Publicizer for internal APIs | Accepted |
| [0011](0011-gtk3-port-strategy.md) | GTK2 → GTK3 port strategy | Accepted |
| [0012](0012-text-editor.md) | Text editor: Mono.TextEditor over vendored vs-editor-api subset | Accepted |
| [0013](0013-native-library-resolution.md) | Native library resolution without dllmap | Accepted |
| [0014](0014-localization-ngettext.md) | Localization with a managed gettext reader | Accepted |
| [0015](0015-test-framework.md) | Test framework: NUnit 3.14 + coverlet | Accepted |
| [0016](0016-netcoredbg-debugging.md) | Debugging user programs with netcoredbg | Accepted |
| [0017](0017-linux-exclusions.md) | Projects excluded from the Linux build | Accepted |
| [0018](0018-warning-policy.md) | Warnings as errors with per-project legacy baselines | Accepted |
| [0019](0019-nrefactory-removal.md) | NRefactory 5 removed from the Linux build | Accepted |
| [0020](0020-nuget-client-version.md) | NuGet client 7.9, loaded from the .NET SDK | Accepted |
| [0021](0021-versioning-and-release.md) | Versioning and releases | Accepted |
| [0022](0022-flatpak.md) | Flatpak packaging with the bundled .NET 10 SDK | Accepted |
| [0023](0023-logging-and-observability.md) | Logging and observability | Accepted |
| [0024](0024-gtksharp-toplevel-references.md) | Toplevels and GDK windows created from C# kept alive (GtkSharp toggle-reference workaround) | Accepted |
