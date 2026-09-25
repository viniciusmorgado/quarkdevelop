#!/usr/bin/env bash
# Lint the maintained shell scripts and the workflows.
# Usage: ./scripts/pm ./scripts/lint.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cd "$MD_ROOT"
mapfile -t scripts < <(
	{
		printf '%s\n' scripts/pm
		find scripts -maxdepth 1 -name '*.sh'
	} | sort -u
)
md_log "shellcheck ${#scripts[@]} scripts"
shellcheck -x "${scripts[@]}"

if command -v actionlint >/dev/null 2>&1 && [[ -d .github/workflows ]]; then
	md_log "actionlint"
	actionlint
fi
md_log "lint OK"
