# Vendored: Mono.Addins.GuiGtk3

| | |
|---|---|
| Upstream | https://github.com/mono/mono-addins (`Mono.Addins.GuiGtk3/`) |
| Source commit | `78293401391922f6d06c4707a043e030b54e3732` (submodule main/external/mono-addins) |
| License | MIT (COPYING) |
| Vendored on | 2026-09-23 (task T067, ADR 0005) |

The rest of mono-addins comes from NuGet (`Mono.Addins`, `Mono.Addins.Setup`, `Mono.Addins.CecilReflector`
1.4.1), so the `main/external/mono-addins` submodule is removed. Upstream ships the GTK3 add-in
manager as a separate assembly and namespace (`Mono.Addins.GuiGtk3`), which replaces the GTK2
`Mono.Addins.Gui` used by the IDE.

## Local patches

Listed per commit in `git log -- main/vendor/mono-addins-gui`; summary:

- SDK-style `net10.0` project on GtkSharp 3.24.24 and Mono.Addins 1.4.1 (ADR 0003). Analyzer findings
  are baselined in `main/msbuild/Linux/warning-baselines/Mono.Addins.GuiGtk3.props`. The wildcard
  `AssemblyVersion` is fixed to `1.0.0.0` because the build is deterministic.
- `Mono.Addins.Gui/Catalog.cs`:
  - `Mono.Unix.Catalog` (native libintl) is replaced by an internal `Catalog` that forwards to
    `Localization.GetStringHandler` / `GetPluralStringHandler`. The host sets these (MonoDevelop:
    `GettextCatalog`). Untranslated text is returned when no handler is set.
  - `StyleColors` replaces the GtkStyle colour accessors (`Style.Background/Base`), which were removed
    from GtkSharp 3.24. `Style.PaintFocus` is replaced by `StyleContext.RenderFocus`.
