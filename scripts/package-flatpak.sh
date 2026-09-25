#!/usr/bin/env bash
# Build the Flatpak bundle of the IDE (M7, T123, ADR 0022). Idempotent.
# Usage: PM_PROFILE=flatpak ./scripts/pm ./scripts/package-flatpak.sh [--rebuild-ide]
#   --rebuild-ide  run ./scripts/build.sh -c Release (ContinuousIntegrationBuild=true) first even if
#                  main/build/bin exists (it is also run when the IDE has not been built yet). An existing
#                  build must have been made with ContinuousIntegrationBuild=true, as CI does: the script
#                  refuses a bundle whose files contain the checkout path.
# Outputs:
#   out/monodevelop.flatpak         single-file bundle (runtime org.gnome.Platform//51 from Flathub)
#   out/monodevelop.flatpak.sha256  its checksum (sha256sum format)
#   out/monodevelop.cdx.json        CycloneDX SBOM of the shipped NuGet packages and the bundled .NET SDK
#   out/flatpak/                    logs and validator output
# The Flathub runtimes, the flatpak-builder cache and the build directory live in the md-flatpak podman
# volume (/var/lib/md-flatpak), never in the host's flatpak installation.
set -euo pipefail
# shellcheck source=scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
md_require_container "$@"

app_id=io.github.viniciusmorgado.MonoDevelop
# the Flatpak files are written to out/flatpak-files by the "Flatpak files" step of the release workflow
files="$MD_ROOT/out/flatpak-files"
manifest="$files/$app_id.yml"
flathub=https://dl.flathub.org/repo/flathub.flatpakrepo
store="${MD_FLATPAK_STORE:-/var/lib/md-flatpak}"
export FLATPAK_USER_DIR="${FLATPAK_USER_DIR:-$store/user}"
work="$MD_OUT/flatpak"

rebuild_ide=0
while [[ $# -gt 0 ]]; do
	case "$1" in
		--rebuild-ide) rebuild_ide=1; shift ;;
		*) md_die "unknown argument: $1" ;;
	esac
done

command -v flatpak-builder >/dev/null 2>&1 \
	|| md_die "flatpak-builder not found; run with PM_PROFILE=flatpak ./scripts/pm $0"
[[ -f "$manifest" ]] || md_die "$manifest not found: the release workflow writes the Flatpak files"
[[ -d "$store" && -w "$store" ]] || md_die "$store is not a writable volume (PM_PROFILE=flatpak mounts md-flatpak)"

cd "$MD_ROOT"
mkdir -p "$work"
start_all=$SECONDS
timings="$work/timings.txt"
: > "$timings"
timed() {
	local name="$1" start=$SECONDS
	shift
	local status
	md_log "$name"
	# errexit inside the step (a subshell outside any `||`/`if` context), status kept explicitly
	set +e
	(set -e; "$@")
	status=$?
	set -e
	printf '%-12s %4ds%s\n' "$name" $((SECONDS - start)) "$([[ $status == 0 ]] || echo " (exit $status)")" | tee -a "$timings" >&2
	return "$status"
}

# 1. The IDE (Release). The same SDK image as the dev container, so the output is the same.
# ContinuousIntegrationBuild maps source and PDB paths to /_/: the bundle must not carry the path of
# the checkout it was built from.
if [[ "$rebuild_ide" == 1 || ! -f main/build/bin/MonoDevelop.dll ]]; then
	timed build-ide env ContinuousIntegrationBuild=true ./scripts/build.sh -c Release
fi

# 2. Desktop entry, AppStream metadata and MIME types
validate() {
	desktop-file-validate "$files/$app_id.desktop"
	# --no-net: the screenshot URLs point at the default branch, which may not have them yet
	appstreamcli validate --no-net --explain "$files/$app_id.metainfo.xml"
	xmllint_mime="$files/$app_id.xml"
	python3 -c 'import sys, xml.dom.minidom; xml.dom.minidom.parse(sys.argv[1])' "$xmllint_mime"
}
timed validate validate 2>&1 | tee "$work/validate.log"

# 3. Flathub runtime, SDK and .NET extension (cached in the volume)
flatpak remote-add --user --if-not-exists flathub "$flathub"

# 4. Build and export to a local repository. --disable-rofiles-fuse: no FUSE mounts in the container.
rm -rf "$store/repo"
timed flatpak-build flatpak-builder --user --install-deps-from=flathub --assumeyes \
	--disable-rofiles-fuse --force-clean --ccache=false \
	--state-dir="$store/builder" --repo="$store/repo" \
	"$store/build-dir" "$manifest" > "$work/flatpak-builder.log" 2>&1 \
	|| { tail -40 "$work/flatpak-builder.log" >&2; md_die "flatpak-builder failed (log: out/flatpak/flatpak-builder.log)"; }

# No file of the app may contain the checkout path (a build without ContinuousIntegrationBuild=true)
leaked="$(grep -rlF "$MD_ROOT" "$store/build-dir/files" || true)"
if [[ -n "$leaked" ]]; then
	md_die "$(wc -l <<< "$leaked") files of the bundle contain the checkout path (e.g. $(head -1 <<< "$leaked")); rebuild with --rebuild-ide"
fi

# The exported (installed) metadata must still validate
appstreamcli validate --no-net "$store/build-dir/files/share/metainfo/$app_id.metainfo.xml" >> "$work/validate.log" 2>&1
desktop-file-validate "$store/build-dir/export/share/applications/$app_id.desktop" >> "$work/validate.log" 2>&1

# 5. Single-file bundle; --runtime-repo lets `flatpak install` fetch the runtime from Flathub
timed bundle flatpak build-bundle --runtime-repo="$flathub" "$store/repo" "$MD_OUT/monodevelop.flatpak.tmp" "$app_id"
mv -f "$MD_OUT/monodevelop.flatpak.tmp" "$MD_OUT/monodevelop.flatpak"
(cd "$MD_OUT" && sha256sum monodevelop.flatpak > monodevelop.flatpak.sha256)

# 6. SBOM: NuGet packages of the shipped projects (tests and development-only packages excluded) plus
# the bundled .NET SDK
sbom() {
	local sdk_version version revision
	sdk_version="$(basename "$(find "$store/build-dir/files/lib/dotnet/sdk" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)")"
	version="$(sed -n 's/^Version=//p' version.config)"
	revision="$(git rev-parse --short=12 HEAD 2>/dev/null || echo unknown)"
	version="${MD_RELEASE_VERSION:-$version+$revision}"
	dotnet tool restore > /dev/null
	dotnet CycloneDX "$MD_SLN" --output "$work/sbom" --filename bom.json --output-format Json \
		--exclude-test-projects --exclude-dev --disable-package-restore \
		--set-name MonoDevelop --set-version "$version" > "$work/cyclonedx.log" 2>&1
	jq --arg sdk "$sdk_version" --arg id "$app_id" '
		.metadata.component.description = "MonoDevelop for Linux, Flatpak " + $id
		| .components += [{
			"type": "framework",
			"bom-ref": ("pkg:generic/microsoft/dotnet-sdk@" + $sdk),
			"supplier": { "name": "Microsoft" },
			"name": "dotnet-sdk",
			"version": $sdk,
			"description": ".NET SDK bundled in /app/lib/dotnet (org.freedesktop.Sdk.Extension.dotnet10)",
			"licenses": [{ "license": { "id": "MIT" } }],
			"purl": ("pkg:generic/microsoft/dotnet-sdk@" + $sdk)
		}]' "$work/sbom/bom.json" > "$MD_OUT/monodevelop.cdx.json"
}
timed sbom sbom

printf '%-12s %4ds\n' total $((SECONDS - start_all)) | tee -a "$timings" >&2
md_log "$(du -h "$MD_OUT/monodevelop.flatpak" | cut -f1) out/monodevelop.flatpak, $(jq '.components | length' "$MD_OUT/monodevelop.cdx.json") SBOM components"
cat "$MD_OUT/monodevelop.flatpak.sha256"
