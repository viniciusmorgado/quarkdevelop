# Contract: Flatpak bundle

- App id: `io.github.viniciusmorgado.MonoDevelop` (Flathub requires a publisher-controlled domain; ADR 0022).
- Runtime: `org.gnome.Platform` (GTK 3.24 included) + .NET 10 SDK extension.
- Exposes: desktop entry (from `main/monodevelop.desktop`), AppStream metadata
  (`main/monodevelop.appdata.xml`), MIME types (`main/monodevelop.xml`), icons.
- Commands: `flatpak run io.github.viniciusmorgado.MonoDevelop` (IDE), `--command=mdtool` (CLI),
  `--version` prints IDE version and runtime.
- Permissions: `--share=network`, `--socket=wayland`, `--socket=fallback-x11`, `--device=dri`,
  `--filesystem=home`, and SDK access per packaging ADR.
