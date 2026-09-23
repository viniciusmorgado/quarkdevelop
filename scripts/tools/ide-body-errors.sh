#!/usr/bin/env bash
# Full MonoDevelop.Ide compile that reaches method bodies: temporarily excludes only the TemplateEngine
# and ResX generator files (ported separately) and stubs the two references to them; restores everything.
# Output: out/ide-body-errors.txt. Used during M5b until the TemplateEngine port lands (T070-T082) (": error" lines, sorted, unique). Run inside the container from the repo root.
set -euo pipefail
I=main/src/core/MonoDevelop.Ide
T=$I/MonoDevelop.Ide.Templates
mkdir -p out/bodycheck-backup
cp $I/Gtk3PortPending.props $T/TemplatingService.cs $T/ProjectTemplate.cs out/bodycheck-backup/
restore() { cp out/bodycheck-backup/Gtk3PortPending.props $I/; cp out/bodycheck-backup/TemplatingService.cs out/bodycheck-backup/ProjectTemplate.cs $T/; }
trap restore EXIT
python3 - <<'PY'
T = "main/src/core/MonoDevelop.Ide/MonoDevelop.Ide.Templates/"
p = T + "TemplatingService.cs"; t = open(p, encoding="utf-8-sig").read()
t = t.replace("MicrosoftTemplateEngineItemTemplatingProvider itemTemplatingProvider;", "dynamic itemTemplatingProvider;")
t = t.replace("itemTemplatingProvider = new MicrosoftTemplateEngineItemTemplatingProvider ();", "itemTemplatingProvider = null;")
open(p, "w").write(t)
p = T + "ProjectTemplate.cs"; t = open(p, encoding="utf-8-sig").read()
t += "\nnamespace MonoDevelop.Ide.Templates { class TemplateMetadata : MonoDevelop.Core.Instrumentation.CounterMetadata { public string Id { get; set; } public string Name { get; set; } public string Language { get; set; } public string Platform { get; set; } } }\n"
open(p, "w").write(t)
PY
printf '<Project>\n  <ItemGroup>\n    <Compile Remove="MonoDevelop.Ide.Templates/MicrosoftTemplateEngine*.cs" />\n    <Compile Remove="MonoDevelop.Ide.CustomTools/*ResXFileCodeGenerator.cs" />\n  </ItemGroup>\n</Project>\n' > $I/Gtk3PortPending.props
{ dotnet build "$I/MonoDevelop.Ide.csproj" -clp:NoSummary -p:TreatWarningsAsErrors=false 2>&1 || true; } \
	| grep -E ": error [A-Z]" | sed -E "s# \[/[^]]*\]\$##" | sort -u > out/ide-body-errors.txt
echo "errors: $(wc -l < out/ide-body-errors.txt)"
