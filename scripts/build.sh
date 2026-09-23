#!/usr/bin/env bash
# Restore and build the Linux solution. Idempotent.
# Usage: ./scripts/pm ./scripts/build.sh [-c Debug|Release] [--check]
#   --check  also verify formatting of files added by this fork (dotnet format whitespace)
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

configuration=Debug
check=0
while [[ $# -gt 0 ]]; do
	case "$1" in
		-c|--configuration) configuration="$2"; shift 2 ;;
		--check) check=1; shift ;;
		*) md_die "unknown argument: $1" ;;
	esac
done

"$MD_ROOT/scripts/restore.sh"

md_log "dotnet build ($configuration)"
dotnet build "$MD_SLN" -c "$configuration" --no-restore -nologo

if [[ "$check" == 1 ]]; then
	mapfile -t new_files < <(md_new_cs_files)
	if [[ ${#new_files[@]} -gt 0 ]]; then
		md_log "dotnet format whitespace --verify-no-changes (${#new_files[@]} new files)"
		(cd "$MD_ROOT" && dotnet format whitespace . --folder --verify-no-changes --include "${new_files[@]}")
	fi
fi

md_log "build OK"
