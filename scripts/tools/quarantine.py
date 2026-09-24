#!/usr/bin/env python3
"""Quarantine failing legacy tests (constitution V, task T041).

Reads a TRX file, classifies each failed test with the rules below, adds
[Category ("Quarantine")] to the test method in the source (once per method), and rewrites the
suite's section of docs/evidence/M4/quarantine.md (reason, owner, date, linked task).

Usage (inside the dev container):
    python3 scripts/tools/quarantine.py <results.trx> <tests-project-dir> <suite-name> [--dry-run]
"""
import collections
import datetime
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
OWNER = "migration"

# (regex, reason category, note, follow-up task). Rules are tried in order on "test name + message";
# rules marked stack=True also look at the stack trace (fixture paths). Timeouts are only read from the
# message: with NUnit's DefaultTimeout every stack contains TimeoutCommand frames.
RULES = [
    (r"Timed out|exceeded Timeout|TimeoutException|was not recorded|FileWatcher", False,
     "Flaky", "timing-dependent (file watcher / event timing)", "T135"),
    (r"Moq\.|Mock|HttpSourceAuthenticationHandler", False,
     "Bug", "mock expectations differ on the .NET 10 HttpClient pipeline", "T135"),
    (r"Portable|Xamarin|Profile\d+|NetStandardProject|Facade", False,
     "legacy-fixture", "PCL / Xamarin / netstandard1.x fixture (retired target frameworks)", "T134"),
    (r"test-projects|\.NETFramework|v4\.[0-9]|mscorlib|LocalCopy|AssemblyReferences|ReferencesAreOk", True,
     "net4x-fixture", "legacy .NET Framework fixture project", "T134"),
    (r"SynchronizationContext may not be used as a TaskScheduler", False,
     "Bug", "test main loop: TaskScheduler.FromCurrentSynchronizationContext on the emulated main loop", "T135"),
    (r"Mono\.Runtime|mono_|MonoRuntime|MonoTargetRuntime|\.mdb\b|MSBuildRuntimeVersion", False,
     "Mono-only", "exercises Mono runtime behaviour", "T135"),
    (r"SdkResolv|Unable to find SDK|MSBuildSearchPath|UnknownSolutionItem|GenericProject|Makefile", False,
     "Bug", "project model / evaluator difference on SDK 10", "T135"),
    (r"Add-in engine not initialized|SchemaValidationTests\.", False,
     "IDE-host", "needs the IDE add-in host (GUI add-ins are not loaded by the headless test host)", "T107"),
]
DEFAULT = ("Bug", "fails on .NET 10; root cause to be analysed", "T135")


def classify(name, msg, stack):
    for pattern, use_stack, category, note, task in RULES:
        text = name + "\n" + msg + ("\n" + stack if use_stack else "")
        if re.search(pattern, text):
            return category, note, task
    return DEFAULT


def failed_tests(trx):
    root = ET.parse(trx).getroot()
    defs = {}
    for ut in root.iterfind(".//t:UnitTest", NS):
        tm = ut.find("t:TestMethod", NS)
        defs[ut.get("id")] = (tm.get("className"), tm.get("name"))
    for r in root.iterfind(".//t:UnitTestResult", NS):
        if r.get("outcome") != "Failed":
            continue
        cls, method = defs.get(r.get("testId"), (None, r.get("testName")))
        method = method.split("(")[0]  # parameterized cases: "Name(args)" -> method "Name"
        msg = r.findtext("t:Output/t:ErrorInfo/t:Message", default="", namespaces=NS)
        stack = r.findtext("t:Output/t:ErrorInfo/t:StackTrace", default="", namespaces=NS)
        yield cls, method, r.get("testName"), msg, stack


def add_category(project_dir, cls, method):
    """Adds [Category ("Quarantine")] before the method declaration. Returns the file or None."""
    short_cls = cls.split(".")[-1]
    for path in glob.glob(os.path.join(project_dir, "**", "*.cs"), recursive=True):
        # newline="" keeps CRLF/LF exactly as they are (many legacy files mix them).
        text = open(path, encoding="utf-8-sig", newline="").read()
        if not re.search(r"\bclass\s+" + re.escape(short_cls) + r"\b", text):
            continue
        pat = re.compile(r"(?m)^(?P<indent>[ \t]*)(?P<decl>(public|internal)\s+(async\s+)?[\w<>\[\], ]+\s+" + re.escape(method) + r"\s*\()")
        m = pat.search(text)
        if not m:
            continue
        # Walk back over the attribute block to see whether it is already quarantined.
        head = text[:m.start()]
        attrs = re.search(r"(?:(?:^[ \t]*\[.*\][ \t]*\r?\n))*\Z", head, re.M)
        if attrs and 'Category ("Quarantine")' in attrs.group(0):
            return path
        # Use the ending of the method's own line.
        eol = text.find("\n", m.start())
        newline = "\r\n" if eol > 0 and text[eol - 1] == "\r" else "\n"
        insert = m.group("indent") + '[Category ("Quarantine")]' + newline
        text = text[:m.start()] + insert + text[m.start():]
        bom = open(path, "rb").read(3) == b"\xef\xbb\xbf"
        with open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f:
            f.write(text)
        return path
    return None


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry = "--dry-run" in sys.argv
    if len(args) != 3:
        print(__doc__)
        return 2
    trx, project_dir, suite = args
    today = datetime.date.today().isoformat()
    rows, methods, missing = [], set(), []
    for cls, method, test_name, msg, stack in failed_tests(trx):
        category, note, task = classify(f"{cls}.{test_name}", msg, stack)
        first = (msg.strip().splitlines() or [""])[0][:110].replace("|", "\\|")
        rows.append((f"{cls}.{test_name}", category, note, first, task))
        if (cls, method) in methods:
            continue
        methods.add((cls, method))
        if not dry and add_category(project_dir, cls, method) is None:
            missing.append(f"{cls}.{method}")
    counts = collections.Counter(r[1] for r in rows)

    section = [f"## {suite}", "",
               f"Quarantined {len(rows)} test cases ({len(methods)} methods) on {today}. "
               + ", ".join(f"{k}: {v}" for k, v in counts.most_common()) + ".", "",
               "| Test | Reason | Note | First error line | Owner | Date | Task |",
               "|---|---|---|---|---|---|---|"]
    section += [f"| `{t}` | {c} | {n} | {e} | {OWNER} | {today} | {task} |" for t, c, n, e, task in sorted(rows)]
    section.append("")
    out = "docs/evidence/M4/quarantine.md"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    header = ("# Quarantined tests\n\nTests excluded from the gate with `[Category (\"Quarantine\")]` "
              "(constitution V). Per suite, the count may not grow after the suite is first converted.\n\n")
    existing = open(out).read() if os.path.exists(out) else header
    existing = re.sub(r"(?ms)^## " + re.escape(suite) + r"\n.*?(?=^## |\Z)", "", existing)
    if not dry:
        with open(out, "w") as f:
            f.write(existing.rstrip() + "\n\n" + "\n".join(section))
    print(f"{suite}: {len(rows)} cases, {len(methods)} methods; categories {dict(counts)}")
    if missing:
        print("could not locate (add the category by hand):", *missing, sep="\n  ")
    return 0


if __name__ == "__main__":
    sys.exit(main())
