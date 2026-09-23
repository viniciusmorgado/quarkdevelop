# Contract: Flatpak bundle

- App id: `com.monodevelop.MonoDevelop` (provisional, see ADR on naming/packaging).
- Runtime: `org.gnome.Platform` (GTK 3.24 included) + .NET 10 SDK extension.
- Exposes: desktop entry (from `main/monodevelop.desktop`), AppStream metadata
  (`main/monodevelop.appdata.xml`), MIME types (`main/monodevelop.xml`), icons.
- Commands: `flatpak run com.monodevelop.MonoDevelop` (IDE), `--command=mdtool` (CLI),
  `--version` prints IDE version and runtime.
- Permissions: `--share=network`, `--socket=wayland`, `--socket=fallback-x11`, `--device=dri`,
  `--filesystem=home`, and SDK access per packaging ADR.
