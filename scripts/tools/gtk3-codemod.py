#!/usr/bin/env python3
"""Mechanical GTK2 -> GTK3 (GtkSharp 3.24) rewrites for MonoDevelop sources (ADR 0011, T071-T082).

Idempotent; keeps BOM and line endings. Handles the patterns that can be rewritten without
understanding the code:

  1. `override bool OnExposeEvent (Gdk.EventExpose e)` -> `override bool OnDrawn (Cairo.Context gtk3cr)`;
     the body gets `var e = new MonoDevelop.Components.Gtk3ExposeEvent (this, gtk3cr);` so e.Window and
     e.Area keep working, `CairoHelper.Create (e.Window | GdkWindow)` becomes `e.CreateContext ()`
     and `base.OnExposeEvent (e)` becomes `base.OnDrawn (gtk3cr)`.
  2. `override void OnSizeRequested (ref Requisition r)` -> a `Gtk3SizeRequest ()` helper returning the
     requisition, called by generated OnGetPreferredWidth/Height overrides; `base.OnSizeRequested`
     maps to the base preferred sizes.
  3. `Gtk.TreeModel` (GTK2 class) -> `Gtk.ITreeModel` (GTK3 interface) in type positions.
  4. CellRenderer `public override void GetSize (Widget, ref Rectangle, out x, out y, out w, out h)` ->
     `protected override void OnGetSize (...)` plus OnGetPreferredWidth/Height(-For-Width/Height)
     overrides measuring through it (GtkCellRendererText no longer calls get_size); `base.GetSize`
     becomes a generated `Gtk3BaseGetSize` built on the base preferred sizes.
  5. CellRenderer `protected override void Render (Drawable window, Widget, bg, cell, expose, flags)` ->
     `protected override void OnRender (Cairo.Context gtk3cr, Widget, bg, cell, flags)`;
     `CairoHelper.Create (window)` becomes `gtk3cr.CreateSharedContext ()`,
     `window.DrawLayout (w.Style.TextGC (s), x, y, l)` becomes `gtk3cr.DrawLayout (w, s, x, y, l)`,
     `base.Render` maps to `base.OnRender` and a used expose area comes from the cairo clip.

Everything else is left to the port tasks. Usage:
    python3 scripts/tools/gtk3-codemod.py FILE... [--dry-run]
"""
import re
import sys


def load(path):
    raw = open(path, "rb").read()
    return raw.decode("utf-8-sig"), raw.startswith(b"\xef\xbb\xbf")


def save(path, text, bom):
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode())


def body_span(text, start):
    """(open, close) indices of the brace block starting at the first '{' after start."""
    i = text.index("{", start)
    depth = 0
    k = i
    in_str = None
    while k < len(text):
        c = text[k]
        if in_str:
            if c == "\\":
                k += 2
                continue
            if c == in_str:
                in_str = None
        elif c in "\"'":
            in_str = c
        elif c == "/" and text[k + 1] == "/":
            k = text.index("\n", k)
            continue
        elif c == "/" and text[k + 1] == "*":
            k = text.index("*/", k) + 2
            continue
        elif c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i, k
        k += 1
    raise ValueError("unbalanced braces")


def line_indent(text, pos):
    start = text.rfind("\n", 0, pos) + 1
    return re.match(r"[ \t]*", text[start:]).group(0)


EXPOSE = re.compile(r"(protected|public)\s+override\s+bool\s+OnExposeEvent\s*\(\s*(?:Gdk\.)?EventExpose\s+(\w+)\s*\)")


def port_expose(text, nl):
    changed = False
    while True:
        m = EXPOSE.search(text)
        if not m:
            return text, changed
        name = m.group(2)
        sig = f"{m.group(1)} override bool OnDrawn (Cairo.Context gtk3cr)"
        o, c = body_span(text, m.end())
        indent = line_indent(text, m.start()) + "\t"
        body = text[o + 1:c]
        body = re.sub(r"base\.OnExposeEvent\s*\(\s*" + name + r"\s*\)", "base.OnDrawn (gtk3cr)", body)
        body = re.sub(r"PropagateExpose\s*\(\s*([^,()]+?)\s*,\s*" + name + r"\s*\)", r"PropagateDraw (\1, gtk3cr)", body)
        body = re.sub(r"(?:Gdk\.)?CairoHelper\.Create\s*\(\s*(?:" + name + r"\.Window|(?:this\.)?GdkWindow)\s*\)",
                      name + ".CreateContext ()", body)
        decl = nl + indent + f"var {name} = new MonoDevelop.Components.Gtk3ExposeEvent (this, gtk3cr);"
        text = text[:m.start()] + sig + text[m.end():o + 1] + decl + body + text[c:]
        changed = True


SIZE = re.compile(r"(protected|public)\s+override\s+void\s+OnSizeRequested\s*\(\s*ref\s+(?:Gtk\.)?Requisition\s+(\w+)\s*\)")


def port_size_request(text, nl):
    m = SIZE.search(text)
    if not m:
        return text, False
    name = m.group(2)
    o, c = body_span(text, m.end())
    indent = line_indent(text, m.start())
    inner = indent + "\t"
    body = text[o + 1:c]
    uses_base = re.search(r"base\.OnSizeRequested\s*\(\s*ref\s+" + name + r"\s*\)", body) is not None
    body = re.sub(r"base\.OnSizeRequested\s*\(\s*ref\s+" + name + r"\s*\)", name + " = Gtk3BaseSizeRequest ()", body)
    body = re.sub(r"\breturn\s*;", f"return {name};", body)
    new_method = (
        f"Gtk.Requisition Gtk3SizeRequest ()" + text[m.end():o + 1] + nl + inner + f"var {name} = new Gtk.Requisition ();"
        + body.rstrip() + nl + inner + f"return {name};" + nl + indent + "}" + nl + nl
        + indent + "protected override void OnGetPreferredWidth (out int minimum_width, out int natural_width)" + nl
        + indent + "{" + nl + inner + "minimum_width = natural_width = Gtk3SizeRequest ().Width;" + nl + indent + "}" + nl + nl
        + indent + "protected override void OnGetPreferredHeight (out int minimum_height, out int natural_height)" + nl
        + indent + "{" + nl + inner + "minimum_height = natural_height = Gtk3SizeRequest ().Height;" + nl + indent + "}"
    )
    if uses_base:
        new_method += (nl + nl + indent + "Gtk.Requisition Gtk3BaseSizeRequest ()" + nl + indent + "{" + nl
                       + inner + "base.OnGetPreferredWidth (out _, out int width);" + nl
                       + inner + "base.OnGetPreferredHeight (out _, out int height);" + nl
                       + inner + "return new Gtk.Requisition { Width = width, Height = height };" + nl + indent + "}")
    text = text[:m.start()] + new_method + text[c + 1:]
    return text, True


TREEMODEL = [
    (re.compile(r"\bGtk\.TreeModel\b(?![A-Za-z])"), "Gtk.ITreeModel"),
    (re.compile(r"(?<![.\w])TreeModel(?=\s+[a-z_]\w*\s*[,;=)]|\s*\)\s*[\w(]|\s*>|\s*\[\])"), "ITreeModel"),
    # fields and members named in PascalCase: `TreeModel TModel;`, `TreeModel ModelFromWidget (`
    (re.compile(r"(?<![.\w\"])(?<!new )TreeModel(?=\s+[A-Z]\w*\s*[;=({])"), "ITreeModel"),
]


def port_treemodel(text):
    new = text
    for rx, repl in TREEMODEL:
        new = rx.sub(repl, new)
    return new, new != text


def line_ending_at(text, pos, default):
    """The line terminator of the line containing pos (files may mix CRLF and LF)."""
    end = text.find("\n", pos)
    if end <= 0:
        return default
    return "\r\n" if text[end - 1] == "\r" else "\n"


def enclosing_base_class(text, pos):
    """Base type name of the innermost class declared before pos (heuristic: last `class X : Base`)."""
    found = None
    for m in re.finditer(r"\bclass\s+\w+\s*:\s*([\w.]+)", text[:pos]):
        found = m.group(1)
    return found


GTK_CELL_RENDERERS = {
    "CellRendererText", "CellRendererPixbuf", "CellRendererToggle", "CellRendererCombo", "CellRendererSpin",
    "CellRendererProgress", "CellRendererAccel", "CellRendererSpinner",
}

CELL_GETSIZE = re.compile(
    r"public\s+override\s+void\s+GetSize\s*\(\s*(?:Gtk\.)?Widget\s+(\w+)\s*,\s*ref\s+(?:Gdk\.)?Rectangle\s+(\w+)\s*,"
    r"\s*out\s+int\s+(\w+)\s*,\s*out\s+int\s+(\w+)\s*,\s*out\s+int\s+(\w+)\s*,\s*out\s+int\s+(\w+)\s*\)")


def port_cell_get_size(text, nl):
    """GTK2 CellRenderer.GetSize override -> OnGetSize + GTK3 preferred-size vfuncs built on it."""
    m = CELL_GETSIZE.search(text)
    if not m:
        return text, False
    nl = line_ending_at(text, m.start(), nl)
    w, area, xo, yo, wd, ht = m.groups()
    o, c = body_span(text, m.end())
    indent = line_indent(text, m.start())
    inner = indent + "\t"
    body = text[o + 1:c]
    base_rx = re.compile(r"\bbase\.GetSize\s*\(")
    uses_base = base_rx.search(body) is not None
    body = base_rx.sub("Gtk3BaseGetSize (", body)
    sig = (f"protected override void OnGetSize (Gtk.Widget {w}, ref Gdk.Rectangle {area}, "
           f"out int {xo}, out int {yo}, out int {wd}, out int {ht})")

    def method(decl, lines):
        return nl + nl + indent + decl + nl + indent + "{" + nl + "".join(inner + l + nl for l in lines) + indent + "}"

    extra = method("protected override void OnGetPreferredWidth (Gtk.Widget widget, out int minimum_size, out int natural_size)", [
        "var area = Gdk.Rectangle.Zero;",
        "OnGetSize (widget, ref area, out _, out _, out natural_size, out _);",
        "minimum_size = natural_size;"])
    extra += method("protected override void OnGetPreferredHeight (Gtk.Widget widget, out int minimum_size, out int natural_size)", [
        "var area = Gdk.Rectangle.Zero;",
        "OnGetSize (widget, ref area, out _, out _, out _, out natural_size);",
        "minimum_size = natural_size;"])
    extra += method("protected override void OnGetPreferredHeightForWidth (Gtk.Widget widget, int width, out int minimum_height, out int natural_height)", [
        "OnGetPreferredHeight (widget, out minimum_height, out natural_height);"])
    extra += method("protected override void OnGetPreferredWidthForHeight (Gtk.Widget widget, int height, out int minimum_width, out int natural_width)", [
        "OnGetPreferredWidth (widget, out minimum_width, out natural_width);"])
    if uses_base:
        base_type = (enclosing_base_class(text, m.start()) or "").split(".")[-1]
        decl = ("void Gtk3BaseGetSize (Gtk.Widget widget, ref Gdk.Rectangle cell_area, "
                "out int x_offset, out int y_offset, out int width, out int height)")
        if base_type == "CellRenderer":
            # GtkCellRenderer's own get_size is abstract: GTK2's base call measured nothing.
            extra += method(decl, ["x_offset = y_offset = width = height = 0;"])
        elif base_type not in GTK_CELL_RENDERERS:
            # A managed renderer (ported by this rule): its OnGetSize is the GTK2 GetSize. Going
            # through the base preferred sizes would dispatch back to this class's OnGetSize.
            extra += method(decl, ["base.OnGetSize (widget, ref cell_area, out x_offset, out y_offset, out width, out height);"])
        else:
            extra += method(decl, [
                "base.OnGetPreferredWidth (widget, out _, out width);",
                "base.OnGetPreferredHeightForWidth (widget, width, out _, out height);",
                "MonoDevelop.Components.Gtk3CompatExtensions.Gtk3CalcOffset (this, widget, cell_area, width, height, out x_offset, out y_offset);"])
    text = text[:m.start()] + sig + text[m.end():o + 1] + body + text[c:c + 1] + extra + text[c + 1:]
    return text, True


CELL_RENDER = re.compile(
    r"protected\s+override\s+void\s+Render\s*\(\s*(?:Gdk\.)?Drawable\s+(\w+)\s*,\s*(?:Gtk\.)?Widget\s+(\w+)\s*,"
    r"\s*(?:Gdk\.)?Rectangle\s+(\w+)\s*,\s*(?:Gdk\.)?Rectangle\s+(\w+)\s*,\s*(?:Gdk\.)?Rectangle\s+(\w+)\s*,"
    r"\s*(?:Gtk\.)?CellRendererState\s+(\w+)\s*\)")


def port_cell_render(text, nl):
    """GTK2 CellRenderer.Render (Drawable, ...) override -> GTK3 OnRender (Cairo.Context, ...)."""
    m = CELL_RENDER.search(text)
    if not m:
        return text, False
    nl = line_ending_at(text, m.start(), nl)
    win, w, bg, cell, expose, flags = m.groups()
    o, c = body_span(text, m.end())
    inner = line_indent(text, m.start()) + "\t"
    body = text[o + 1:c]
    body = re.sub(r"\bbase\.Render\s*\(\s*" + win + r"\s*,\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*,\s*\w+\s*,\s*(\w+)\s*\)",
                  r"base.OnRender (gtk3cr, \1, \2, \3, \4)", body)
    body = re.sub(r"(?:Gdk\.)?CairoHelper\.Create\s*\(\s*" + win + r"\s*\)", "gtk3cr.CreateSharedContext ()", body)
    body = re.sub(r"\b" + win + r"\.DrawLayout\s*\(\s*(\w+)\.Style\.TextGC\s*\(([^()]*)\)\s*,",
                  r"gtk3cr.DrawLayout (\1, \2,", body)
    if re.search(r"\b" + expose + r"\b", body):
        body = (nl + inner + f"Gdk.CairoHelper.GetClipRectangle (gtk3cr, out Gdk.Rectangle {expose});") + body
    sig = (f"protected override void OnRender (Cairo.Context gtk3cr, Gtk.Widget {w}, Gdk.Rectangle {bg}, "
           f"Gdk.Rectangle {cell}, Gtk.CellRendererState {flags})")
    text = text[:m.start()] + sig + text[m.end():o + 1] + body + text[c:]
    return text, True


def main():
    files = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry = "--dry-run" in sys.argv
    total = 0
    for path in files:
        text, bom = load(path)
        nl = "\r\n" if "\r\n" in text else "\n"
        changes = []
        text, c = port_expose(text, nl)
        if c:
            changes.append("OnDrawn")
        while True:
            text, c = port_size_request(text, nl)
            if not c:
                break
            if "GetPreferred" not in changes:
                changes.append("GetPreferred")
        text, c = port_treemodel(text)
        if c:
            changes.append("ITreeModel")
        while True:
            text, c = port_cell_get_size(text, nl)
            if not c:
                break
            if "OnGetSize" not in changes:
                changes.append("OnGetSize")
        while True:
            text, c = port_cell_render(text, nl)
            if not c:
                break
            if "OnRender" not in changes:
                changes.append("OnRender")
        if changes:
            total += 1
            print(f"{path}: {', '.join(changes)}")
            if not dry:
                save(path, text, bom)
    print(f"{total} files changed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
