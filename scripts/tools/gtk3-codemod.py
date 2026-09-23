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
]


def port_treemodel(text):
    new = text
    for rx, repl in TREEMODEL:
        new = rx.sub(repl, new)
    return new, new != text


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
        if changes:
            total += 1
            print(f"{path}: {', '.join(changes)}")
            if not dry:
                save(path, text, bom)
    print(f"{total} files changed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
