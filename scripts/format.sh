#!/usr/bin/env bash
# Apply dotnet-format whitespace fixes to C# files added by this fork (see md_new_cs_files).
# Usage: ./scripts/pm ./scripts/format.sh [files...]   (default: all new files)
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

if [[ $# -gt 0 ]]; then
	files=("$@")
else
	mapfile -t files < <(md_new_cs_files)
fi
[[ ${#files[@]} -gt 0 ]] || { md_log "no files to format"; exit 0; }
md_log "dotnet format whitespace (${#files[@]} files)"
cd "$MD_ROOT"
dotnet format whitespace . --folder --include "${files[@]}"
