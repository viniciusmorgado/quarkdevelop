#!/usr/bin/env bash
# The whole Linux CI gate in one script, run the same way locally and in GitHub Actions (T115).
# Steps (each timed; the first failure stops the run):
#   setup, lint, build --check (Release), duplicate-assembly check, tests + coverage ratchet,
#   vulnerability audit, mdtool smoke (build linux-smoke/Hello and run it), GUI smoke (Xwt/GTK3
#   sample window under Xvfb, screenshot).
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
	MONODEVELOP_PROFILE="$dir/.profile" MONO_ADDINS_REGISTRY="$dir/.profile" XDG_CONFIG_HOME="$dir/.profile" \
		dotnet main/build/bin/mdtool.dll build "$dir/Hello/Hello.csproj"
	dotnet "$dir/Hello/bin/Debug/net10.0/Hello.dll" | grep -q "Hello, MonoDevelop!"
	rm -rf "$dir"
}

gui_smoke() {
	# The IDE's --smoke-test (contracts/smoke-test.md, T103): start under Xvfb, open a copy of the smoke
	# solution, build it; exit 0 only when it builds with no errors and no unhandled exception was logged.
	local dir
	dir="$(mktemp -d)"
	cp -r main/tests/linux-smoke/. "$dir/"
	XDG_CONFIG_HOME="$dir/.profile/config" XDG_DATA_HOME="$dir/.profile/data" XDG_CACHE_HOME="$dir/.profile/cache" \
		MD_SMOKE_OUT="$ci_out/gui-smoke" \
		xvfb-run -a -s "-screen 0 1600x1000x24" dotnet main/build/bin/MonoDevelop.dll --smoke-test -no-redirect "$dir/Smoke.sln"
	rm -rf "$dir"
	test -s "$ci_out/gui-smoke/screenshot.png"
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
	grep -q "GDK display wayland-md" "$ci_out/wayland-smoke/ide.log"
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
step wayland-smoke wayland_smoke

total=$((SECONDS - start_all))
printf '%-18s      %4ds (budget %ds)\n' total "$total" "$budget" | tee -a "$summary"
if (( total > budget )); then
	md_die "CI took ${total}s, over the ${budget}s budget (SC-007)"
fi
