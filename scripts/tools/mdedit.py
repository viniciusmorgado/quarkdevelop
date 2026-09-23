"""Small helper for scripted source edits (used from `./scripts/pm python3`).

Preserves the UTF-8 BOM and the line ending of every line (legacy files often mix CRLF and LF):
replacements are made on LF-normalized text, then unchanged lines get their original ending back
and new lines take the ending of the preceding original line. Fails loudly when a pattern is
missing.
"""
import difflib


def _strip(line):
    return line.rstrip("\r\n")


def restore_line_endings(original, edited):
    """Apply `original`'s per-line endings to `edited` (both full texts)."""
    old_lines = original.splitlines(keepends=True)
    new_lines = edited.splitlines(keepends=True)
    matcher = difflib.SequenceMatcher(None, [_strip(l) for l in old_lines], [_strip(l) for l in new_lines], autojunk=False)
    out, last_end = [], "\r\n" if "\r\n" in original else "\n"
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            for old, new in zip(old_lines[i1:i2], new_lines[j1:j2]):
                end = old[len(_strip(old)):]
                # keep "no newline at end of file" as edited
                out.append(_strip(new) + (end if new[len(_strip(new)):] else ""))
                last_end = end or last_end
        else:
            for new in new_lines[j1:j2]:
                out.append(_strip(new) + (last_end if new[len(_strip(new)):] else ""))
    return "".join(out)


def edit(path, pairs, count=None):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    original = raw.decode("utf-8-sig")
    text = original.replace("\r\n", "\n")
    for old, new in pairs:
        if old not in text:
            raise SystemExit(f"{path}: pattern not found: {old[:80]!r}")
        text = text.replace(old, new) if count is None else text.replace(old, new, count)
    text = restore_line_endings(original, text)
    with open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f:
        f.write(text)
