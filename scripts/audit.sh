#!/usr/bin/env bash
# Vulnerability gate: fails when any package of the Linux solution,
# including transitive ones, has a High or Critical advisory. `dotnet list package --vulnerable`
# itself always exits 0, so its report is parsed here.
# Usage: ./scripts/pm ./scripts/audit.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

mkdir -p "$MD_OUT"
report="$MD_OUT/audit.txt"

md_log "dotnet list package --vulnerable --include-transitive"
dotnet list "$MD_SLN" package --vulnerable --include-transitive > "$report" 2>&1

high="$(grep -cE '\b(High|Critical)\b' "$report" || true)"
moderate="$(grep -cE '\b(Moderate|Low)\b' "$report" || true)"
echo "High/Critical findings: $high; Moderate/Low findings: $moderate (report: ${report#"$MD_ROOT"/})"

if [[ "$high" -gt 0 ]]; then
	grep -E '\b(High|Critical)\b' "$report" >&2
	md_die "vulnerable packages with High/Critical severity"
fi
md_log "audit OK"
