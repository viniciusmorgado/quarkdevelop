#!/usr/bin/env bash
# Run the Linux solution's tests with coverage. Idempotent (results are overwritten).
# Usage: ./scripts/pm ./scripts/test.sh [--all] [--no-build] [extra dotnet test args...]
#   default  excludes tests in the Quarantine category
#   --all    runs quarantined tests too (informational; failures are expected there)
#   --update-baseline  rewrite the coverage ratchet file after an intentional change
# Outputs: out/tests/*.trx, out/coverage/Summary.txt (+ Cobertura XML)
# Coverage ratchet (task T055): each assembly listed in docs/evidence/M4/coverage-baseline.txt must
# keep at least its recorded line coverage; the run fails otherwise.
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

filter='Category!=Quarantine'
update_baseline=false
baseline="$MD_ROOT/docs/evidence/M4/coverage-baseline.txt"
build_args=()
extra=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--all) filter=''; shift ;;
		--no-build) build_args+=(--no-build); shift ;;
		--update-baseline) update_baseline=true; shift ;;
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
# GTK tests (MonoDevelop.Ide.Gtk3.Tests) need a display: use Xvfb when there is none.
runner=()
if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" ]] && command -v xvfb-run >/dev/null; then
	runner=(xvfb-run -a -s "-screen 0 1280x1024x24")
fi
"${runner[@]}" dotnet test "${args[@]}" "${build_args[@]}" "${extra[@]}" || status=$?

mapfile -t reports < <(find "$MD_OUT/tests" -name 'coverage.cobertura.xml')
if [[ ${#reports[@]} -gt 0 ]]; then
	md_log "coverage report"
	dotnet tool run reportgenerator -- \
		"-reports:$(IFS=';'; echo "${reports[*]}")" \
		"-targetdir:$MD_OUT/coverage" \
		"-reporttypes:TextSummary;Cobertura" >/dev/null
	cat "$MD_OUT/coverage/Summary.txt"

	# Per-assembly line coverage plus "total" = overall line coverage of the product assemblies
	# (SC-003), with two decimals from the merged Cobertura report: Summary.txt truncates to one
	# decimal, which made the 0.05 tolerance below useless against run-to-run noise (timing paths).
	current="$MD_OUT/coverage/line-coverage.txt"
	python3 - "$MD_OUT/coverage/Cobertura.xml" <<-'PY' | sort > "$current"
		import sys, xml.etree.ElementTree as ET
		root = ET.parse(sys.argv[1]).getroot()
		for package in root.iter("package"):
		    print(package.get("name"), "%.2f" % (100 * float(package.get("line-rate"))))
		print("total", "%.2f" % (100 * float(root.get("line-rate"))))
	PY
	if [[ "$update_baseline" == true ]]; then
		cp "$current" "$baseline"
		md_log "coverage baseline updated: $baseline"
	elif [[ -f "$baseline" && -n "$filter" ]]; then
		md_log "coverage ratchet ($baseline)"
		while read -r assembly minimum; do
			actual="$(awk -v a="$assembly" '$1 == a { print $2 }' "$current")"
			if [[ -z "$actual" ]] || awk -v a="$actual" -v m="$minimum" 'BEGIN { exit !(a + 0.05 < m) }'; then
				echo "coverage of $assembly dropped: ${actual:-missing}% < $minimum%" >&2
				status=1
			else
				echo "$assembly $actual% (baseline $minimum%)"
			fi
		done < "$baseline"
	fi
fi

exit "$status"
