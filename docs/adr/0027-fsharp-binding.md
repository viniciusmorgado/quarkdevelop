# 0027 — F# binding on FSharp.Compiler.Service 31

- Status: Accepted
- Date: 2026-09-25

## Context and Problem Statement

The upstream F# binding (the add-in, its GTK settings widgets, the F# Interactive host and the tests) stayed in
`main/external/fsharpbinding` after the rest of the excluded code was removed (ADR 0017, amendment 2026-09-25). It
kept its Paket build for .NET Framework and Mono. The code was written for FSharp.Compiler.Service (FCS) 31 and
FSharp.Core 4.7 (F# 4.7). The maintainer asked for a minimal port that builds with the rest of the IDE and can be
merged without rewrites, even if F# support stays partial.

## Considered Options

1. Port the code as it is, on the package versions it was written for (FCS 31, FSharp.Core 4.7).
2. Port it to the current FCS (43.x) and FSharp.Core 10. The FCS API was reorganized after 31 (namespaces, ranges,
   tooltips, the tokenizer, symbols), so most of the binding would change.
3. Keep it out of the build until option 2 is done.

## Decision Outcome

Option 1. The binding moves to `main/src/addins/FSharpBinding`. Its five projects join `main/MonoDevelop.Linux.sln` in
compile order: `MonoDevelop.FSharp.Shared`, `MonoDevelop.FSharp.Gui` (C#), `MonoDevelop.FSharpBinding`
(`MonoDevelop.FSharp.fsproj`), `MonoDevelop.FSharpi.Service` and `MonoDevelop.FSharp.Tests`. `main/external` is gone.
The Paket files, build scripts, the add-in's own solution and the prebuilt binaries (`paket.bootstrapper.exe`,
`NuGet.exe`, `FSharp.Compiler.Interactive.Settings.dll`) are removed. The Apache 2.0 license stays with the code, and
the upstream README moves to `docs/monodevelop/FSharpBinding.README.md`.

- Packages (ADR 0004): FSharp.Compiler.Service 31.0.0, FSharp.Core 4.7.0 (`DisableImplicitFSharpCoreReference`),
  ExtCore 0.8.46, Fantomas 3.0.0-beta-002 and System.Reactive 4.1.5. ExtCore is a .NET Framework package and is
  referenced with `NoWarn` NU1701. The add-in copies these packages into its directory; the IDE's assemblies are not
  copied.
- Code changes are limited to what .NET 10 and the port require:
  - type annotations where overloads became ambiguous on .NET;
  - indentation of `type` declarations that F# 9 reads as nested types;
  - GTK 3: `ITreeModel`, and the Stetic widgets ported as in ADR 0011;
  - the members Roslyn 5.9 added to `ISymbol`;
  - MonoDoc compiled out, as in `MonoDocDocumentationProvider` (`MONODOC`);
  - no dependency on the Cocoa/WPF TextEditor add-in (ADR 0012).
- Scripts and stand-alone files are checked against the assemblies of the running runtime, as `dotnet fsi` does
  (`assumeDotNetFramework = false`, `--targetprofile:netcore`). With the .NET Framework default, FCS found no framework
  on Linux and aborted every check.
- F# Interactive: `MonoDevelop.FSharpInteractive.Service` is a .NET program started with `dotnet exec`.
  - It uses the settings object of FCS instead of `FSharp.Compiler.Interactive.Settings.dll`, a prebuilt .NET
    Framework library.
  - It loads the IDE's Newtonsoft.Json from `build/bin`, so an installation has one copy of each assembly.
  - The System.Drawing image printer is removed, because System.Drawing is Windows-only on .NET.
- The F# language binding has no CodeDOM provider. FSharp.Compiler.CodeDom is a .NET Framework library whose provider
  cannot load on .NET.
- The F# code snippets (`Templates/FSharp-templates.xml`) are restored. T152 removed the file with the project
  templates but kept its registration, which made the add-in fail to load.
- Tests run on NUnit 3 in the IDE test host of ADR 0015: 371 pass. Four tests (seven cases) of retired frameworks or of
  Mono are quarantined.

### Consequences

- Good: the binding builds and ships with the IDE. F# projects load as F# projects and build from the IDE. F# scripts
  are type-checked, and the editor features (completion, tooltips, signature help, navigation, highlighting of usages
  and unused opens) pass their tests. The F# Interactive process runs on .NET 10.
- Bad: FCS 31 cannot read the F# metadata of FSharp.Core 8 and later, which is compressed since F# 8. SDK projects
  reference these versions, so their `.fs` files get no type checking in the editor. There, only highlighting, the
  lexer-based features and the build work.
- Bad: the language service understands F# 4.7. Syntax added later (interpolated strings, `open type`, `task`…) is
  not understood in the editor. The build uses the SDK's compiler and is not affected.
- Bad: the F# Interactive pad and its commands stay hidden, because their conditions look for the Mono F# SDK targets.
  Scripts run in F# Interactive have no `fsi` object.
- Bad: FCS 31, FSharp.Core 4.7, ExtCore and the Fantomas 3 beta are unmaintained. The move to the current FCS is future
  work (T154).
