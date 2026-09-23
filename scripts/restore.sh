#!/usr/bin/env bash
# Restore NuGet packages for the Linux solution. Idempotent.
# Usage: ./scripts/pm ./scripts/restore.sh [--locked]
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

args=()
if [[ "${1:-}" == "--locked" || "${CI:-}" == "true" ]]; then
	args+=(--locked-mode)
fi

md_log "dotnet restore ${MD_SLN#"$MD_ROOT"/} ${args[*]:-}"
dotnet restore "$MD_SLN" "${args[@]}"
