# 0012 — Text editor: Mono.TextEditor over a vendored vs-editor-api subset

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

Two editors exist: the GTK `Mono.TextEditor` + `MonoDevelop.SourceEditor2` (with a `VSEditor/`
bridge to the VS text model) and the newer `MonoDevelop.TextEditor` built on the VS editor for
Cocoa/WPF. The VS editor packages (16.1.28) came from a dead feed; vs-editor-api also ships "FPF"
clones named `WindowsBase`/`PresentationCore`, which collide with .NET's own `WindowsBase` facade.

## Considered Options

1. Mono.TextEditor + SourceEditor2, ported to GTK3/Cairo, over a vendored text-only subset of vs-editor-api.
2. The VS editor for Cocoa/WPF (`MonoDevelop.TextEditor`) with the VS editor 17.x packages.
3. AvalonEdit.
4. GtkSourceView.

## Decision Outcome

Chosen: option 1. Keep Mono.TextEditor + SourceEditor2, ported to GTK3/Cairo, over a vendored **text-only** subset of
vs-editor-api (`main/vendor/vs-editor-api/`: Text.Data, Text.Logic, implementation pieces required by
SourceEditor2 and Roslyn EditorFeatures), without FPF assemblies. The Cocoa/WPF editor is excluded.
Alternatives rejected: AvalonEdit (WPF), GtkSourceView (full rewrite of editor integrations),
VS editor 17.x packages (Windows/VS-specific dependencies, licensing of vs-impl feed).

### Consequences

- Good: preserves existing editor features and extensions.
- Bad: we maintain the vendored text model subset.
