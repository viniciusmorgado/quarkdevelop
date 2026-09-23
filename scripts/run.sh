#!/usr/bin/env bash
# Start the IDE from the build output.
# Usage: ./scripts/pm ./scripts/run.sh [--headless] [IDE arguments...]
#   --headless  run under Xvfb (CI, smoke tests); otherwise the container needs a display
#               (X11: PM_PODMAN_ARGS="-e DISPLAY -v /tmp/.X11-unix:/tmp/.X11-unix";
#                Wayland: forward WAYLAND_DISPLAY and XDG_RUNTIME_DIR, see docs/linux/setup.md)
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

ide="$MD_ROOT/main/build/bin/MonoDevelop.dll"
[[ -f "$ide" ]] || md_die "IDE not built yet ($ide); run ./scripts/build.sh"

export MONODEVELOP_LOCALE_PATH="${MONODEVELOP_LOCALE_PATH:-$MD_ROOT/main/build/locale}"

if [[ "${1:-}" == "--headless" ]]; then
	shift
	exec xvfb-run -a -s "-screen 0 1600x1000x24" dotnet "$ide" "$@"
fi
exec dotnet "$ide" "$@"
