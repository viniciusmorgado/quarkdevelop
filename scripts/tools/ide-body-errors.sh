#!/usr/bin/env bash
# Full MonoDevelop.Ide compile that reaches method bodies: temporarily empties Gtk3PortPending.props (the
# compiler binds method bodies only when there are no declaration errors), then restores it.
# Used during M5b (T070-T082). Output: out/ide-body-errors.txt (": error" lines, sorted, unique).
# Run inside the container from the repo root.
set -euo pipefail
I=main/src/core/MonoDevelop.Ide
mkdir -p out/bodycheck-backup
cp $I/Gtk3PortPending.props out/bodycheck-backup/
restore() { cp out/bodycheck-backup/Gtk3PortPending.props $I/; }
trap restore EXIT
printf '<Project />\n' > $I/Gtk3PortPending.props
{ dotnet build "$I/MonoDevelop.Ide.csproj" -clp:NoSummary -p:TreatWarningsAsErrors=false 2>&1 || true; } \
	| grep -E ": error [A-Z]" | sed -E "s# \[/[^]]*\]\$##" | sort -u > out/ide-body-errors.txt
echo "errors: $(wc -l < out/ide-body-errors.txt)"
