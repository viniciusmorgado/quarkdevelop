# 0011 — GTK2 → GTK3 port strategy

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

The UI uses gtk-sharp 2.12 from the Mono GAC; 1,167 files reference the GTK stack. GTK2-only APIs:
`ExposeEvent` (90 files), `SizeRequested` (67), `Gdk.GC`/`Drawable` (45), `Gtk.Rc` (9); 187 files
of Stetic-generated code. Spike T006 proved GtkSharp 3.24.24.95 (NuGet) renders a window with Cairo
drawing on .NET 10 under Xvfb.

## Considered Options

1. Port each project/area directly to GtkSharp 3 APIs (no shim).
2. Write a GTK2-compat shim over GtkSharp 3.
3. Rewrite the UI in another toolkit (Avalonia, GTK4).

## Decision Outcome

Option 1: `ExposeEvent` → `Drawn` (Cairo), `SizeRequested/Allocated` → `GetPreferred*` overrides,
`Gdk.GC/Drawable` → Cairo, `Gtk.Rc`/`Style` → `StyleContext` + CSS providers, `HBox/VBox` → `Box`
(deprecated classes are allowed temporarily where they still exist in GTK3). A shim cannot bridge
the semantic changes of drawing and size negotiation (2); a new toolkit is a rewrite (3).

- Areas not yet ported are excluded via a tracked `Gtk3PortPending.props` so each commit compiles.
- Stetic-generated `Build()` code is frozen as hand-maintained source (Stetic is GTK2-only) and the
  `.stetic` files are deleted.
- Progress metric: `grep -rlE 'ExposeEvent|Gdk\.GC|SizeRequested'` over the Linux solution → 0.

**Amendment (2026-09-23, analyze revision 4, H3).** Thin, stateless port helpers are allowed while
M5b/M5c run: `MonoDevelop.Components/Gtk3Compat.cs` and the output of `scripts/tools/gtk3-codemod.py`.
They keep GTK2-era coordinates and sizes working on top of GTK3 APIs (`Gtk3ExposeEvent`,
`Gtk3SizeRequest`, `CellRenderer` size helpers). They are not a GTK2 API emulation layer: nothing
re-creates `Gdk.GC`, `Gtk.Rc`, expose events or GTK2 size negotiation. They are tested in
`MonoDevelop.Ide.Gtk3.Tests` (T136). Their use is a second progress metric, reported in T109, and
must reach 0 by the end of M5c:
`grep -rlE 'Gtk3ExposeEvent|Gtk3SizeRequest|Gtk3BaseSizeRequest|Gtk3BaseGetSize|Gtk3CompatExtensions' main/src`.

### Consequences

- Good: native GTK3 behavior (Wayland, HiDPI, CSS theming).
- Bad: largest effort of the migration; visual regressions need screenshot checks.
