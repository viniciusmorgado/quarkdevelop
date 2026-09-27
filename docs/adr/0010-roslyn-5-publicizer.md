# 0010 — Roslyn 5.9 with Publicizer for internal APIs

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

MonoDevelop uses Roslyn 3.4.0-beta4 (dead feed) and ~160 files use Roslyn *internal* namespaces
(`Shared.Extensions` 56, `Options` 48, `Host.Mef` 34, `CSharp.Extensions` 31, `Editor` 19, …). Access
relied on Roslyn granting `InternalsVisibleTo` to MonoDevelop assemblies signed with
`MonoDevelop-Public.snk`.

## Decision Drivers

Spike T008: Roslyn 5.9.0 assemblies carry 408 IVT grants and **none** for MonoDevelop, Xamarin or
VS for Mac, nor for MonoDevelop's public key. Spike T008 (b): Krafs.Publicizer 2.3.2 makes internal
members callable at compile time and emits `IgnoresAccessChecksToAttribute`, which CoreCLR honours at
run time (`SyntaxNodeExtensions.GetAncestor<T>`, `SyntaxTreeExtensions.IsInNonUserCode` verified).

## Considered Options

1. Roslyn 5.9 + Krafs.Publicizer, exact version pin.
2. Rewrite all C# services on public APIs.
3. Build a private Roslyn fork with IVT for MonoDevelop.

## Decision Outcome

Option 1. Pin `Microsoft.CodeAnalysis*` exactly (5.9.0) because internals change between releases;
`<Publicize Include="…"/>` only on the assemblies we actually need; prefer public API when a
replacement is cheap. EditorFeatures (not on nuget.org beyond 2.8.2) come from the dnceng
`dotnet-tools` feed only if required (ADR 0004 amendment).

### Consequences

- Good: C# features can be ported without a rewrite.
- Bad: every Roslyn upgrade may break internal call sites; upgrades are deliberate, tested changes.

**Amendment (2026-09-24, T089/T090): no Roslyn EditorFeatures.** The C# add-ins (CSharpBinding, Refactoring) are
ported without `Microsoft.CodeAnalysis.EditorFeatures*`, so the dnceng `dotnet-tools` feed is not added (ADR 0004
unchanged). EditorFeatures served the Cocoa/WPF editor (ADR 0012); what the GTK editor used from it is replaced by
Roslyn Workspaces/Features APIs: classification by a C# `ITaggerProvider` over `Classifier.GetClassifiedSpansAsync`
(plus the `CSharp` content type), formatting by `Formatter` and `ISyntaxFormattingService` (`RoslynFormattingService`),
debugger data tips by Features' `ILanguageDebugInfoService`, find references by the public `IFindReferencesProgress`,
diagnostics by pulling `IDiagnosticAnalyzerService` with the host analyzers as solution analyzer references (Roslyn 4+
removed `IDiagnosticService` and `IWorkspaceDiagnosticAnalyzerProviderService`). Features that only existed on top of
EditorFeatures or on removed Roslyn 3 services are excluded and listed in `docs/BREAKING-CHANGES.md`.
Publicizer caveats met while porting: internal types that exist in two assemblies (`ArrayBuilder<T>`,
`SpecializedCollections`, `ImmutableArrayExtensions`, `EnumerableExtensions`) make calls ambiguous and are avoided;
`Microsoft.CodeAnalysis.ParsedDocument` clashes with MonoDevelop's (`DoNotPublicize` in Refactoring, a `using` alias in
CSharpBinding); events whose backing field is publicized under the same name are subscribed through reflection; internal
abstract or sealed members of Roslyn base classes (`CommonCompletionProvider`) cannot be implemented outside Roslyn.

**Amendment (2026-09-26): smart indentation and formatting while typing.** Two services of EditorFeatures had no
replacement. The C# smart indent (the `ISmartIndentProvider` that EditorFeatures registered for the `CSharp` content
type): `CSharpIndentationTracker` asked `ISmartIndentationService` and got nothing, so a new line started at the
indentation of the empty line itself, column 0. The tracker now computes the indentation with Roslyn's
`IIndentationService` (Workspaces) on the document of the buffer, with the formatting policy of the document and the
indentation settings of the editor. And the check of the characters that format while typing
(`SupportsFormattingOnTypedCharacter`): `EditorFormattingServiceTextEditorExtension` formatted on every key, so a letter
or Tab reformatted the enclosing statement with the Roslyn options of the document and moved the caret, e.g. to the next
lines. It formats again only on `;{}#nte:)`, following the format-on-typing preferences, as EditorFeatures did. The C#
indentation tests that upstream disabled with the Cocoa editor (`CSharpTextEditorIndentationTests`) run again, and the
GUI smoke test types Return and Tab in the IDE (`MD_SMOKE_TYPING`).
