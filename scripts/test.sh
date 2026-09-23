#!/usr/bin/env bash
# Run the Linux solution's tests with coverage. Idempotent (results are overwritten).
# Usage: ./scripts/pm ./scripts/test.sh [--all] [--no-build] [extra dotnet test args...]
#   default  excludes tests in the Quarantine category
#   --all    runs quarantined tests too (informational; failures are expected there)
# Outputs: out/tests/*.trx, out/coverage/Summary.txt (+ Cobertura XML)
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

filter='Category!=Quarantine'
build_args=()
extra=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--all) filter=''; shift ;;
		--no-build) build_args+=(--no-build); shift ;;
		*) extra+=("$1"); shift ;;
	esac
done

rm -rf "$MD_OUT/tests" "$MD_OUT/coverage"
mkdir -p "$MD_OUT/tests" "$MD_OUT/coverage"

args=("$MD_SLN" --logger "trx" --results-directory "$MD_OUT/tests" --collect "XPlat Code Coverage")
if [[ -n "$filter" ]]; then
	args+=(--filter "$filter")
fi

md_log "dotnet test ${filter:+--filter $filter}"
status=0
dotnet test "${args[@]}" "${build_args[@]}" "${extra[@]}" || status=$?

mapfile -t reports < <(find "$MD_OUT/tests" -name 'coverage.cobertura.xml')
if [[ ${#reports[@]} -gt 0 ]]; then
	md_log "coverage report"
	dotnet tool run reportgenerator -- \
		"-reports:$(IFS=';'; echo "${reports[*]}")" \
		"-targetdir:$MD_OUT/coverage" \
		"-reporttypes:TextSummary;Cobertura" >/dev/null
	cat "$MD_OUT/coverage/Summary.txt"
fi

exit "$status"
