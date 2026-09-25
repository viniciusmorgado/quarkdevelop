#!/usr/bin/env bash
# Run the Linux solution's tests with coverage. Idempotent (results are overwritten).
# Usage: ./scripts/pm ./scripts/test.sh [--all] [--no-build] [--parallel] [extra dotnet test args...]
#   default     excludes tests in the Quarantine category
#   --all       runs quarantined tests too (informational; failures are expected there)
#   --parallel  two lanes at once (T145): Core, DotNetCore and PackageManagement in one, the GUI and other
#               suites in the other; one assembly at a time per lane, each lane with its own profile
# Outputs: out/tests/*.trx, out/coverage/Summary.txt (+ Cobertura XML)
# The coverage ratchet (minimum line coverage per assembly) runs in the ci workflow, not here.
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

filter='Category!=Quarantine'
parallel=false
build_args=()
extra=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--all) filter=''; shift ;;
		--no-build) build_args+=(--no-build); shift ;;
		--parallel) parallel=true; shift ;;
		*) extra+=("$1"); shift ;;
	esac
done

rm -rf "$MD_OUT/tests" "$MD_OUT/coverage" "$MD_OUT/test-profile" "$MD_OUT/test-settings"
mkdir -p "$MD_OUT/tests" "$MD_OUT/coverage"
# out/test-profile: the tests' MonoDevelop profile (the runsettings of main/msbuild/Linux/Test.targets point
# XDG_* there, per checkout); removed above so that every run starts with a fresh add-in registry.

# -m:1: one test assembly at a time. Run in parallel, the test hosts interfered with each other (mdtool
# children of Core.Tests intermittently could not load the C# project type, file watcher tests timed out);
# root cause still open (T135). Costs about 3.5 minutes (519 s instead of about 300 s).
args=("$MD_SLN" -m:1 --logger "trx" --results-directory "$MD_OUT/tests" --collect "XPlat Code Coverage")
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
if [[ "$parallel" != true ]]; then
	"${runner[@]}" dotnet test "${args[@]}" "${build_args[@]}" "${extra[@]}" || status=$?
else
	# The test projects of the solution, split in two lanes of similar length (about 5 minutes each with
	# coverage). Each project runs with a copy of its runsettings whose MonoDevelop profile (XDG_*) belongs
	# to its lane, so that the two lanes never share a profile or an add-in registry cache.
	mapfile -t projects < <(dotnet sln "$MD_SLN" list | sed 's|\\|/|g' | grep -E '\.Tests\.csproj$' | sort)
	lane_a=() lane_b=()
	for project in "${projects[@]}"; do
		case "$project" in
			*/MonoDevelop.Core.Tests.csproj|*/MonoDevelop.DotNetCore.Tests.csproj|*/MonoDevelop.PackageManagement.Tests.csproj)
				lane_a+=("$project") ;;
			*) lane_b+=("$project") ;;
		esac
	done
	run_lane() {
		local lane="$1"; shift
		local lane_status=0 project settings
		mkdir -p "$MD_OUT/test-settings/$lane"
		for project in "$@"; do
			settings="$MD_OUT/test-settings/$lane/$(basename "$project" .csproj).runsettings"
			sed "s|out/test-profile/|out/test-profile/$lane/|" "$(dirname "$MD_ROOT/main/$project")/obj/monodevelop.runsettings" > "$settings"
			"${runner[@]}" dotnet test "$MD_ROOT/main/$project" --settings "$settings" \
				"${args[@]:1}" "${build_args[@]}" "${extra[@]}" || lane_status=1
		done
		return "$lane_status"
	}
	md_log "lane a: ${lane_a[*]##*/}; lane b: ${lane_b[*]##*/}"
	run_lane a "${lane_a[@]}" > "$MD_OUT/tests/lane-a.log" 2>&1 &
	pid_a=$!
	run_lane b "${lane_b[@]}" > "$MD_OUT/tests/lane-b.log" 2>&1 &
	pid_b=$!
	wait "$pid_a" || status=1
	wait "$pid_b" || status=1
	cat "$MD_OUT/tests/lane-a.log" "$MD_OUT/tests/lane-b.log"
fi

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
