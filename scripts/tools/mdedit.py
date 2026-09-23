"""Small helper for scripted source edits (used from `./scripts/pm python3`).

Preserves UTF-8 BOM and CRLF line endings, and fails loudly when a pattern is missing.
"""


def edit(path, pairs, count=None):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    crlf = "\r\n" in text
    if crlf:
        text = text.replace("\r\n", "\n")
    for old, new in pairs:
        if old not in text:
            raise SystemExit(f"{path}: pattern not found: {old[:80]!r}")
        text = text.replace(old, new) if count is None else text.replace(old, new, count)
    if crlf:
        text = text.replace("\n", "\r\n")
    with open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f:
        f.write(text)
