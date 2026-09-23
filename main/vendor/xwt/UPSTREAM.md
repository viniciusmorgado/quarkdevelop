# Vendored: Xwt

| | |
|---|---|
| Upstream | https://github.com/mono/xwt |
| Source commit | `9a22465b52c7dbaae321a514b046edebd6a0efb8` (submodule main/external/xwt, branch d16-1) |
| License | MIT (LICENSE.txt) |
| Vendored on | 2026-09-23 (task T066, ADR 0005) |

Contents: `Xwt` (toolkit core), `Xwt.Gtk` (GTK backend sources; only the GTK3 build is kept),
`TestApps/Gtk3Test` and `TestApps/Samples`. Mac, WPF and GTK2-only projects were not copied.

## Local patches

Listed per commit in `git log -- main/vendor/xwt`; summary:

- SDK-style `net10.0` projects built with the repository props (ADR 0003). `Xwt/Directory.Build.props`
  imports the repository props instead of upstream's `net461;net40`. Analyzer findings are baselined
  in `main/msbuild/Linux/warning-baselines/Xwt*.props`; the sample apps build to `main/build/samples/xwt`.
- `Compat/XamlCompat.cs`: internal stand-ins for the System.Xaml attributes (`ContentProperty`,
  `ValueSerializer`); `Xwt.Design/DesignerSurface.cs` (XamlServices) and the designer sample are not built.
- `TransferDataSource.SerializeValue/DeserializeValue`: an in-process token registry replaces
  BinaryFormatter (removed from .NET 9+; constitution VII). Object transfers only work inside one process.
- GTK3 backend compiled against the GtkSharp 3.24.24 NuGet packages instead of gtk-sharp 3 from the GAC:
  - `NativeLibraryResolver.cs` replaces the `<dllmap>` of `Xwt.Gtk3.dll.config` (ADR 0013); both
    `.dll.config` files are removed.
  - Deprecated `GtkStyle` / `Gtk.Rc` / `ModifyBase` / `Requisition` uses moved to `StyleContext`,
    `PangoContext.FontDescription`, `gtk-font-name`, `OverrideBackgroundColor` and `GetPreferredHeight`
    (borders use the style border colour where GTK2 used `Style.Dark`).
  - `IScrollable.GetBorder` implemented (returns no border) in `GtkViewPort` and `WebView`.
  - The GTK2 container-leak workaround (needs `gtksharpglue`) is compiled only for GTK2.
- `TestApps/Samples/upstream-resources/`: images the upstream sample project linked from `Testing/`
  and `Xwt.XamMac/` (not vendored).

## Known gaps

- `WebView` (`GtkWebKitMini.cs`) binds WebKitGTK 1/3.0 (`libwebkitgtk-3.0`), which current distributions
  no longer ship; creating a WebView fails at runtime until it is ported to WebKit2GTK.
