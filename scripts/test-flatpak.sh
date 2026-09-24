#!/usr/bin/env bash
# Install test of the Flatpak bundle in a clean flatpak installation (M7, T124, SC-008).
# Usage: PM_PROFILE=flatpak ./scripts/pm ./scripts/test-flatpak.sh [bundle]   (default out/monodevelop.flatpak)
# Steps (timed, first failure stops the run; results in out/flatpak-test/summary.txt):
#   checksum, install (fresh user installation; the GNOME runtime comes from Flathub), desktop
#   integration (exported desktop entry, icon, MIME), `monodevelop --version`, `mdtool build` of a copy
#   of main/tests/linux-smoke/Hello and running the result, IDE --smoke-test on a copy of Smoke.sln
#   under Xvfb (screenshot).
# The installation is /var/lib/md-flatpak/test-user in the md-flatpak volume; it is wiped first, and so
# is the app's data directory ~/.var/app/<app id>. The host's flatpak installation is never used.
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

app_id=io.github.viniciusmorgado.MonoDevelop
export FLATPAK_USER_DIR=/var/lib/md-flatpak/test-user

# `flatpak run` needs a session bus and a runtime directory, which a container does not have. It also
# connects to the system bus; point it at the session bus (the container has no system bus), for the
# services D-Bus activates too: the Flatpak portal runs the image loaders of GNOME 50+ (glycin,
# `flatpak-spawn --sandbox`), as on a desktop.
if [[ -z "${DBUS_SESSION_BUS_ADDRESS:-}" ]]; then
	XDG_RUNTIME_DIR="$(mktemp -d)"
	chmod 700 "$XDG_RUNTIME_DIR"
	export XDG_RUNTIME_DIR
	exec dbus-run-session -- "$0" "$@"
fi
export DBUS_SYSTEM_BUS_ADDRESS="$DBUS_SESSION_BUS_ADDRESS"
dbus-update-activation-environment DBUS_SYSTEM_BUS_ADDRESS FLATPAK_USER_DIR

bundle="$(realpath "${1:-$MD_OUT/monodevelop.flatpak}")"
[[ -f "$bundle" ]] || md_die "$bundle not found; run ./scripts/package-flatpak.sh"
command -v flatpak >/dev/null 2>&1 || md_die "flatpak not found; run with PM_PROFILE=flatpak ./scripts/pm $0"

out="$MD_OUT/flatpak-test"
rm -rf "$FLATPAK_USER_DIR" "$out" "$HOME/.var/app/$app_id"
mkdir -p "$FLATPAK_USER_DIR" "$out"
summary="$out/summary.txt"
start_all=$SECONDS

step() {
	local name="$1" start=$SECONDS status
	shift
	md_log "flatpak-test: $name"
	# errexit inside the step (a subshell outside any `||`/`if` context), status kept explicitly
	set +e
	(set -e; "$@") > "$out/$name.log" 2>&1
	status=$?
	set -e
	printf '%-14s exit %-3s %4ds\n' "$name" "$status" $((SECONDS - start)) | tee -a "$summary"
	if [[ $status != 0 ]]; then
		tail -40 "$out/$name.log" >&2
		md_die "step '$name' failed (log: out/flatpak-test/$name.log)"
	fi
}

checksum() {
	(cd "$(dirname "$bundle")" && sha256sum -c "$(basename "$bundle").sha256")
}

install_bundle() {
	flatpak install --user --noninteractive -y "$bundle"
	flatpak info --user "$app_id"
	flatpak list --user --columns=application,version,branch,runtime,installation
}

desktop_integration() {
	local exports="$FLATPAK_USER_DIR/exports/share"
	desktop-file-validate "$exports/applications/$app_id.desktop"
	grep -E '^(Exec|Icon|MimeType)=' "$exports/applications/$app_id.desktop"
	test -s "$exports/icons/hicolor/128x128/apps/$app_id.png"
	test -s "$exports/icons/hicolor/512x512/apps/$app_id.png"
	test -s "$exports/mime/packages/$app_id.xml"
	appstreamcli validate --no-net "$exports/metainfo/$app_id.metainfo.xml"
}

version() {
	flatpak run "$app_id" --version | tee "$out/version.txt"
	grep -q '^MonoDevelop ' "$out/version.txt"
	flatpak run --command=dotnet "$app_id" --list-sdks
}

# The sandbox sees the home directory (--filesystem=home), not the repository: work on copies there.
work="$(mktemp -d -p "$HOME" md-flatpak-test.XXXXXX)"
trap 'rm -rf "$work"' EXIT
cp -r "$MD_ROOT/main/tests/linux-smoke/." "$work/"

mdtool_build() {
	flatpak run --command=mdtool "$app_id" build "$work/Hello/Hello.csproj"
	flatpak run --command=dotnet "$app_id" "$work/Hello/bin/Debug/net10.0/Hello.dll" | tee "$out/hello.txt"
	grep -q "Hello, MonoDevelop!" "$out/hello.txt"
}

ide_smoke() {
	# The IDE's --smoke-test (contracts/smoke-test.md): open the solution, build it, take a screenshot and
	# exit 0 when it builds with no errors. X11 through Xvfb (no Wayland display: fallback-x11 applies).
	MD_SMOKE_OUT="$work/smoke" xvfb-run -a -s "-screen 0 1600x1000x24" \
		flatpak run "$app_id" --smoke-test -no-redirect "$work/Smoke.sln"
	cp "$work/smoke/ide.log" "$out/ide.log"
	cp "$work/smoke/screenshot.png" "$out/screenshot.png"
	# every image of the IDE loads (GNOME 50+ loads images in a sandboxed glycin process)
	! grep -q "Error loading icon" "$out/ide.log"
}

step checksum checksum
step install install_bundle
step desktop desktop_integration
step version version
step mdtool-build mdtool_build
step ide-smoke ide_smoke

printf '%-14s          %4ds\n' total $((SECONDS - start_all)) | tee -a "$summary"
{
	echo "bundle: $(du -h "$bundle" | cut -f1) ($(stat -c %s "$bundle") bytes)"
	flatpak info --user "$app_id" | grep -E 'Installed|Runtime|Version|Commit'
	echo "runtime: $(du -sh "$FLATPAK_USER_DIR/runtime" | cut -f1) in $FLATPAK_USER_DIR/runtime"
} | tee "$out/sizes.txt"
md_log "flatpak install test OK"
