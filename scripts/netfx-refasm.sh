#!/usr/bin/env bash
# Install .NET Framework reference assemblies for the legacy test fixtures (task T134).
#
# Hundreds of projects under main/tests/test-projects are old-style .NET Framework projects
# (v2.0 … v4.7.2). On Linux there is no .NETFramework reference directory, so building them
# fails with MSB3644. This script downloads the Microsoft.NETFramework.ReferenceAssemblies.*
# packages from nuget.org (versions and SHA-256 pinned below), verifies them, and merges their
# build/.NETFramework/v* folders into one TargetFrameworkRootPath:
#     $HOME/.cache/monodevelop/netfx-refasm/.NETFramework/v4.5 …
# The test harness (main/tests/UnitTests/Util.cs) points copied fixtures at it. Idempotent.
#
# Usage: ./scripts/pm ./scripts/netfx-refasm.sh
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

root="${MD_NETFX_REFASM:-$HOME/.cache/monodevelop/netfx-refasm}"
downloads="$HOME/.cache/monodevelop/netfx-refasm-downloads"
mkdir -p "$root/.NETFramework" "$downloads"

# moniker version sha256 (nuget.org package microsoft.netframework.referenceassemblies.<moniker>)
packages="
net20 1.0.3 7f74997fec338dde70f5a8c9fd09aa6fe21286731e7b133cc133d644e4e3c5e8
net40 1.0.3 54d6e20a1b61caf79395d6d71d091265e81f5a5705ac4ae52af45ca143c3c694
net45 1.0.3 23a9f94ea3e2cb88cd8341af75b811c6fb5cb82516fc696e95ed4620279128e3
net451 1.0.3 770b33dfa64dc430c9d9ba4e1438b920dee905f7ea60791147d70689f68692d3
net461 1.0.3 bd52289e5fb8765090bb189b399a06477a350c0863018a455b5e5d9e45298cc9
net47 1.0.3 b1e7f2ea457ee84fdacaa67d0b37445b31606e4f75c8939a57bf4a6836001d45
net472 1.0.3 ffa0a5570a39f911399164d0581ffddef99b5e3dfbaa5f220e5ce22969bcf57c
"

while read -r moniker version sha; do
	[[ -n "$moniker" ]] || continue
	id="microsoft.netframework.referenceassemblies.$moniker"
	stamp="$root/.$moniker-$version"
	if [[ -f "$stamp" ]]; then
		continue
	fi
	nupkg="$downloads/$id.$version.nupkg"
	if [[ ! -f "$nupkg" ]] || ! echo "$sha  $nupkg" | sha256sum -c --status; then
		md_log "downloading $id $version"
		curl -fsSL -o "$nupkg.tmp" "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
		mv "$nupkg.tmp" "$nupkg"
	fi
	echo "$sha  $nupkg" | sha256sum -c --status || md_die "checksum mismatch for $nupkg"
	tmp="$(mktemp -d)"
	unzip -q "$nupkg" 'build/.NETFramework/*' -d "$tmp"
	cp -r "$tmp/build/.NETFramework/." "$root/.NETFramework/"
	rm -rf "$tmp"
	touch "$stamp"
	echo "installed $moniker ($version)"
done <<< "$packages"

ls "$root/.NETFramework"
