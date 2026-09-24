#!/usr/bin/env bash
# Lists the C# files compiled by the projects of the Linux solution, one absolute path per line
# (the security sweep of T127 greps these instead of the whole tree, which still holds legacy code).
# Usage: ./scripts/pm ./scripts/tools/compiled-files.sh > out/compiled-files.txt
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../../main"
dotnet sln MonoDevelop.Linux.sln list | tail -n +3 | sed 's|\\|/|g' | while read -r project; do
	dotnet msbuild "$project" -getItem:Compile -p:Configuration=Release 2>/dev/null \
		| python3 -c 'import json, os, sys; [print(os.path.normpath(i["FullPath"])) for i in json.load(sys.stdin)["Items"]["Compile"]]' \
		|| echo "compiled-files: could not evaluate $project" >&2
done | grep -v '/obj/' | sort -u
