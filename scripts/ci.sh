#!/usr/bin/env bash
# The whole Linux CI gate in one script, run the same way locally and in GitHub Actions (T115).
# Steps (each timed; the first failure stops the run):
#   setup, lint, build --check (Release), duplicate-assembly check, tests + coverage ratchet,
#   vulnerability audit, mdtool smoke (build linux-smoke/Hello and linux-smoke/Modern and run them), GUI smoke
#   (the IDE's --smoke-test under Xvfb and Wayland: Smoke.sln, the Broken project, the C# 8 to 14 Modern.sln).
# Writes out/ci/summary.txt (step, status, seconds) and fails when the total exceeds the SC-007
# budget of 15 minutes (MD_CI_BUDGET_SECONDS overrides).
# Usage: ./scripts/pm ./scripts/ci.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

cd "$MD_ROOT"
ci_out="$MD_OUT/ci"
rm -rf "$ci_out"
mkdir -p "$ci_out"
summary="$ci_out/summary.txt"
budget="${MD_CI_BUDGET_SECONDS:-900}"
start_all=$SECONDS

step() {
	local name="$1"
	shift
	local start=$SECONDS
	md_log "ci: $name"
	if "$@" > "$ci_out/$name.log" 2>&1; then
		printf '%-18s ok   %4ds\n' "$name" $((SECONDS - start)) | tee -a "$summary"
	else
		printf '%-18s FAIL %4ds\n' "$name" $((SECONDS - start)) | tee -a "$summary"
		tail -40 "$ci_out/$name.log" >&2
		md_die "ci step '$name' failed (log: out/ci/$name.log)"
	fi
}

mdtool_smoke() {
	local dir
	dir="$(mktemp -d)"
	cp -r main/tests/linux-smoke/. "$dir/"
	# step() runs this inside an if (no set -e): chain the checks and keep their status
	local status=0
	MONODEVELOP_PROFILE="$dir/.profile" MONO_ADDINS_REGISTRY="$dir/.profile" XDG_CONFIG_HOME="$dir/.profile" \
		dotnet main/build/bin/mdtool.dll build "$dir/Hello/Hello.csproj" \
		&& dotnet "$dir/Hello/bin/Debug/net10.0/Hello.dll" | grep -q "Hello, MonoDevelop!" \
		|| status=1
	# T138: the C# 8 to 14 sample builds with no warnings
	MONODEVELOP_PROFILE="$dir/.profile" MONO_ADDINS_REGISTRY="$dir/.profile" XDG_CONFIG_HOME="$dir/.profile" \
		dotnet main/build/bin/mdtool.dll build "$dir/Modern/Modern.csproj" > "$ci_out/mdtool-modern.log" 2>&1 \
		&& grep -q " 0 Warning(s)" "$ci_out/mdtool-modern.log" \
		&& dotnet "$dir/Modern/bin/Debug/net10.0/Modern.dll" | grep -q "Modern C#: OK" \
		|| status=1
	rm -rf "$dir"
	return "$status"
}

gui_smoke() {
	# The IDE's --smoke-test (contracts/smoke-test.md, T103): start under Xvfb, open a copy of the smoke
	# solution, build it; exit 0 only when it builds with no errors and no unhandled exception was logged.
	local dir
	dir="$(mktemp -d)"
	cp -r main/tests/linux-smoke/. "$dir/"
	# step() runs this function inside an if, where set -e does not apply: keep the exit status explicitly
	local status=0
	XDG_CONFIG_HOME="$dir/.profile/config" XDG_DATA_HOME="$dir/.profile/data" XDG_CACHE_HOME="$dir/.profile/cache" \
		MD_SMOKE_OUT="$ci_out/gui-smoke" \
		xvfb-run -a -s "-screen 0 1600x1000x24" dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect "$dir/Smoke.sln" \
		|| status=$?
	rm -rf "$dir"
	test -s "$ci_out/gui-smoke/screenshot.png" || return 1
	return "$status"
}

gui_smoke_errors() {
	# T105: build the Broken project in the IDE; the smoke test activates the first row of the Errors pad and
	# checks that the editor opens Program.cs on the error line. Expected exit status: 1 (build errors).
	local dir
	dir="$(mktemp -d)"
	cp -r main/tests/linux-smoke/. "$dir/"
	local status=0
	XDG_CONFIG_HOME="$dir/.profile/config" XDG_DATA_HOME="$dir/.profile/data" XDG_CACHE_HOME="$dir/.profile/cache" \
		MD_SMOKE_OUT="$ci_out/gui-smoke-errors" \
		xvfb-run -a -s "-screen 0 1600x1000x24" dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect "$dir/Broken/Broken.csproj" \
		|| status=$?
	rm -rf "$dir"
	grep -q "error list navigation opened Program.cs at line 2" "$ci_out/gui-smoke-errors/ide.log" || return 1
	[[ $status -eq 1 ]]
}

gui_smoke_modern() {
	# T138: build the C# 8 to 14 sample in the IDE with no errors or warnings, open Patterns.cs (MD_SMOKE_OPEN) and
	# check that the IDE's workspace parses it as C# 14 with no syntax errors; the screenshot shows its highlighting.
	local dir
	dir="$(mktemp -d)"
	cp -r main/tests/linux-smoke/. "$dir/"
	local status=0
	XDG_CONFIG_HOME="$dir/.profile/config" XDG_DATA_HOME="$dir/.profile/data" XDG_CACHE_HOME="$dir/.profile/cache" \
		MD_SMOKE_OUT="$ci_out/gui-smoke-modern" MD_SMOKE_OPEN=Modern/Patterns.cs \
		xvfb-run -a -s "-screen 0 1600x1000x24" dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect "$dir/Modern.sln" \
		|| status=$?
	rm -rf "$dir"
	grep -q "build finished with 0 errors, 0 warnings" "$ci_out/gui-smoke-modern/ide.log" || return 1
	grep -q "Patterns.cs parses as C# 14.0 with 0 syntax errors" "$ci_out/gui-smoke-modern/ide.log" || return 1
	return "$status"
}

wayland_smoke() {
	# The same smoke test on Wayland (T104): a headless Weston compositor (no input devices: GDK logs
	# criticals for the missing seat, which the smoke tolerates), GDK_BACKEND=wayland, no X display.
	local dir runtime wpid
	dir="$(mktemp -d)"
	runtime="$(mktemp -d)"
	chmod 700 "$runtime"
	cp -r main/tests/linux-smoke/. "$dir/"
	XDG_RUNTIME_DIR="$runtime" weston --backend=headless --socket=wayland-md --width=1600 --height=1000 --idle-time=0 \
		> "$ci_out/weston.log" 2>&1 &
	wpid=$!
	for _ in $(seq 50); do [[ -S "$runtime/wayland-md" ]] && break; sleep 0.1; done
	local status=0
	env -u DISPLAY XDG_RUNTIME_DIR="$runtime" WAYLAND_DISPLAY=wayland-md GDK_BACKEND=wayland \
		XDG_CONFIG_HOME="$dir/.profile/config" XDG_DATA_HOME="$dir/.profile/data" XDG_CACHE_HOME="$dir/.profile/cache" \
		MD_SMOKE_OUT="$ci_out/wayland-smoke" \
		dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect "$dir/Smoke.sln" || status=$?
	kill "$wpid"
	rm -rf "$dir" "$runtime"
	grep -q "GDK display wayland-md" "$ci_out/wayland-smoke/ide.log" || return 1
	return "$status"
}

step setup ./scripts/setup.sh
step lint ./scripts/lint.sh
step build ./scripts/build.sh -c Release --check
step assemblies ./scripts/check-assemblies.sh
step test ./scripts/test.sh --no-build
step audit ./scripts/audit.sh
step mdtool-smoke mdtool_smoke
step gui-smoke gui_smoke
step gui-smoke-errors gui_smoke_errors
step gui-smoke-modern gui_smoke_modern
step wayland-smoke wayland_smoke

total=$((SECONDS - start_all))
printf '%-18s      %4ds (budget %ds)\n' total "$total" "$budget" | tee -a "$summary"
if (( total > budget )); then
	md_die "CI took ${total}s, over the ${budget}s budget (SC-007)"
fi
