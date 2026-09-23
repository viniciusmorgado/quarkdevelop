#!/usr/bin/env bash
# Verify the toolchain inside the dev container and restore local tools. Idempotent.
# Usage: ./scripts/pm ./scripts/setup.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

cd "$MD_ROOT"

md_log "toolchain"
sdk="$(dotnet --version)"
case "$sdk" in
	10.0.*) echo "dotnet SDK $sdk" ;;
	*) md_die "expected a .NET 10 SDK (global.json), got $sdk" ;;
esac
dotnet msbuild -version -nologo | tail -1 | sed 's/^/MSBuild /'
pkg-config --modversion gtk+-3.0 2>/dev/null | sed 's/^/GTK /' || echo "GTK $(ls /usr/lib/*/libgtk-3.so.0 >/dev/null 2>&1 && echo present || echo missing)"
if command -v mono >/dev/null 2>&1; then
	md_die "Mono must not be present in the reference environment"
fi
echo "Mono: absent (as required)"

if [[ -f dotnet-tools.json || -f .config/dotnet-tools.json ]]; then
	md_log "restoring local dotnet tools"
	dotnet tool restore
fi

md_log ".NET Framework reference assemblies for legacy test fixtures"
"$MD_ROOT/scripts/netfx-refasm.sh" | tail -1

md_log "submodules"
git submodule status | awk '{ state = substr($0, 1, 1); if (state == "-") missing++ } END { print (missing ? missing : 0) " uninitialized" }'

md_log "setup OK"
