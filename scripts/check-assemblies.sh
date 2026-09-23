#!/usr/bin/env bash
# Duplicate-assembly check (risk R9, task T068). Fails when
#   - an assembly name exists both in main/build/bin and in an add-in directory (main/build/AddIns/**):
#     the add-in engine and the default load context would load two copies;
#   - an assembly under main/build/bin or main/build/AddIns has the name of an assembly of the .NET
#     shared framework (e.g. WindowsBase.dll): the framework copy wins and types are missing.
# Usage: ./scripts/pm ./scripts/check-assemblies.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

build="$MD_ROOT/main/build"
[[ -d "$build/bin" ]] || md_die "nothing built: $build/bin"

runtime_dir="$(dirname "$(dotnet --list-runtimes | awk '$1 == "Microsoft.NETCore.App" { v = $2; p = $3 } END { gsub(/[][]/, "", p); print p "/" v "/x" }')")"
[[ -d "$runtime_dir" ]] || md_die "cannot find the Microsoft.NETCore.App runtime"

status=0
declare -A in_bin
while IFS= read -r f; do in_bin["$(basename "$f")"]=1; done < <(find "$build/bin" -maxdepth 1 -name '*.dll')

if [[ -d "$build/AddIns" ]]; then
	while IFS= read -r f; do
		name="$(basename "$f")"
		if [[ -n "${in_bin[$name]:-}" ]]; then
			echo "duplicate: $name in bin and ${f#"$build/"}" >&2
			status=1
		fi
	done < <(find "$build/AddIns" -name '*.dll')
fi

while IFS= read -r f; do
	name="$(basename "$f")"
	if [[ -f "$runtime_dir/$name" ]]; then
		echo "shadows the shared framework: ${f#"$build/"}" >&2
		status=1
	fi
done < <(find "$build/bin" "$build/AddIns" -name '*.dll' 2>/dev/null)

if [[ $status -eq 0 ]]; then
	md_log "no duplicate or framework-shadowing assemblies ($(find "$build/bin" -maxdepth 1 -name '*.dll' | wc -l) in bin)"
fi
exit $status
