#!/usr/bin/env python3
"""Print the C# files compiled by the projects of main/MonoDevelop.Linux.sln (one per line,
relative to main/). Used by `scripts/inventory.sh --linux-sln`. Explicit <Compile Include> items
are resolved relative to each project (ADR 0003: SDK-style projects list their sources)."""
import os
import re
import sys

main_dir = sys.argv[1] if len(sys.argv) > 1 else "main"
sln = os.path.join(main_dir, "MonoDevelop.Linux.sln")
projects = re.findall(r'^Project\([^)]*\) = "[^"]*", "([^"]+\.csproj)"', open(sln).read(), re.M)
seen = set()
for proj in projects:
    proj_path = os.path.normpath(os.path.join(main_dir, proj.replace("\\", "/")))
    proj_dir = os.path.dirname(proj_path)
    text = open(proj_path).read()
    for inc in re.findall(r'<Compile Include="([^"]+\.cs)"', text):
        f = os.path.normpath(os.path.join(proj_dir, inc.replace("\\", "/")))
        rel = os.path.relpath(f, main_dir)
        if rel not in seen and os.path.exists(f):
            seen.add(rel)
            print(rel)
