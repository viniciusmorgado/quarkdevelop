# Future work

These items are decided but belong after the Linux / .NET 10 migration. They are not scheduled in
`specs/001-linux-dotnet10-migration/tasks.md`, and each one starts only when its precondition holds.

## UI framework study: coupling of the GTK UI and a possible move to Avalonia

- **Recorded:** 2026-09-24, maintainer's request.
- **Precondition:** the migration is complete (M9 accepted) and the current editor works normally on .NET 10 LTS.
- **Goal:** decide whether the IDE's user interface can move from GTK 3 (GtkSharp + Xwt) to a modern
  cross-platform framework such as [Avalonia UI](https://avaloniaui.net/), and at what cost.

**Scope of the study.** Map how tightly the UI code is coupled to the rest of the IDE:
- Which assemblies and namespaces reference `Gtk`, `Gdk`, `Pango`, `Cairo`, `GLib`, `Atk` or Xwt directly, and how
  much code that is. The inventory in `scripts/inventory.sh` and `scripts/tools/compiled-files.sh` gives a starting
  count.
- Which IDE services and add-in extension points expose GTK or Xwt types in their public API. Some pass
  `Gtk.Widget`, `Control`, `Xwt.Widget` or GTK events through interfaces used by add-ins, for example pads,
  document views, option panels, commands and the text editor extension points.
- Which layers are already toolkit-neutral: Core (project model, build, MSBuild integration, runtime), the Roslyn
  type system, the VS editor text model subset, the debugger session layer.
- The text editor: Mono.TextEditor draws with Cairo on GTK. The editor would be the largest single piece to
  replace. Candidates are AvaloniaEdit, or a new view over the existing text model.
- Docking, the workbench shell, the pads and the dialogs.

**Expected outputs.**
- A coupling report with numbers per assembly and per extension point.
- A list of the seams where an abstraction would let GTK and Avalonia coexist.
- A rough effort and risk estimate.
- A proof of concept of one pad or dialog in Avalonia hosted next to the GTK workbench, or a clear reason why
  that is not practical.
- An ADR with the decision.
