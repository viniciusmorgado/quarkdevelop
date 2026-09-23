#!/usr/bin/env bash
# Start the IDE (or mdtool) under netcoredbg for command-line debugging, or print the
# instructions to attach from VS Code / Rider.
# Usage: ./scripts/pm ./scripts/debug.sh [ide|mdtool] [arguments...]
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

target="${1:-ide}"
[[ $# -gt 0 ]] && shift
case "$target" in
	ide) dll="$MD_ROOT/main/build/bin/MonoDevelop.dll" ;;
	mdtool) dll="$MD_ROOT/main/build/bin/mdtool.dll" ;;
	*) md_die "unknown target '$target' (ide|mdtool)" ;;
esac
[[ -f "$dll" ]] || md_die "$dll not built yet; run ./scripts/build.sh"

if ! command -v netcoredbg >/dev/null 2>&1; then
	md_die "netcoredbg is not installed in this image (added in M2, task T024)"
fi

md_log "netcoredbg --interpreter=cli -- dotnet ${dll#"$MD_ROOT"/} $*"
exec netcoredbg --interpreter=cli -- dotnet "$dll" "$@"
