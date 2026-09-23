#!/usr/bin/env python3
"""Convert a legacy (ToolsVersion 4.0) MonoDevelop csproj into an SDK-style net10.0 project.

ADR 0003: sources stay explicit (Compile items are copied), shared settings come from
main/msbuild/Linux/*. The output is a starting point: references that have no mapping are
emitted as XML comments ("TODO(convert)") for manual review.

Usage (inside the dev container):
    ./scripts/pm python3 scripts/convert-legacy-csproj.py path/to/Project.csproj [--write]
Without --write the new project is printed to stdout.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

NS = "{http://schemas.microsoft.com/developer/msbuild/2003}"

# ProjectReference to legacy submodule projects -> PackageReference (central versions).
PROJECT_TO_PACKAGE = {
    "Mono.Addins.csproj": "Mono.Addins",
    "Mono.Addins.Setup.csproj": "Mono.Addins.Setup",
    "Mono.Addins.CecilReflector.csproj": "Mono.Addins.CecilReflector",
}
# ProjectReferences dropped from the Linux build (replaced by NUnit, see ADR 0015).
DROPPED_PROJECTS = {"GuiUnit_NET_4_5.csproj"}
# Framework references that are implicit on .NET 10.
IMPLICIT_REFERENCES = {
    "System", "System.Core", "System.Xml", "System.Xml.Linq", "System.Data", "System.Net.Http",
    "System.Runtime.Serialization", "Microsoft.CSharp", "System.IO.Compression",
    "System.IO.Compression.FileSystem", "System.Numerics", "System.ComponentModel.DataAnnotations",
    "System.IO.FileSystem", "System.IO.FileSystem.Primitives", "System.Runtime.InteropServices.RuntimeInformation",
    "System.ComponentModel.Composition", "System.Web.Extensions", "mscorlib",
}
# Legacy framework/package references with a known NuGet replacement.
REFERENCE_TO_PACKAGE = {
    "nunit.framework": "NUnit",
    "System.Configuration": "System.Configuration.ConfigurationManager",
    "Mono.Cecil": "Mono.Cecil",
    "Mono.Posix": "Mono.Unix",
    "Newtonsoft.Json": "Newtonsoft.Json",
}
PACKAGE_RENAMES = {"NUnit": "NUnit", "Moq": "Moq", "Castle.Core": "Castle.Core"}


def local(tag):
    return tag.replace(NS, "")


def attr_path(value):
    return value.replace("\\", "/")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    write = "--write" in sys.argv
    if len(args) != 1:
        print(__doc__)
        return 2
    path = args[0]
    tree = ET.parse(path)
    root = tree.getroot()
    if root.get("Sdk"):
        print(f"{path} is already SDK-style", file=sys.stderr)
        return 1

    props = {}
    for pg in root.iter(NS + "PropertyGroup"):
        if pg.get("Condition"):
            continue
        for child in pg:
            props.setdefault(local(child.tag), (child.text or "").strip())

    compiles, embedded, nones, ivts, projrefs, pkgrefs, todos = [], [], [], [], [], [], []
    for item in root.iter():
        tag = local(item.tag)
        inc = item.get("Include")
        if inc is None:
            continue
        meta = {local(c.tag): (c.text or "").strip() for c in item}
        cond = item.get("Condition")
        if tag == "Compile":
            line = f'    <Compile Include="{attr_path(inc)}"'
            if "Link" in meta:
                line += f' Link="{attr_path(meta["Link"])}"'
            compiles.append(line + " />" + (f"  <!-- was Condition=\"{cond}\" -->" if cond else ""))
        elif tag == "EmbeddedResource":
            line = f'    <EmbeddedResource Include="{attr_path(inc)}"'
            if "LogicalName" in meta:
                line += f' LogicalName="{meta["LogicalName"]}"'
            embedded.append(line + " />")
        elif tag in ("None", "Content") and meta.get("CopyToOutputDirectory"):
            nones.append(f'    <None Include="{attr_path(inc)}" CopyToOutputDirectory="{meta["CopyToOutputDirectory"]}" />')
        elif tag == "InternalsVisibleTo":
            ivts.append(f'    <InternalsVisibleTo Include="{inc}" />')
        elif tag == "ProjectReference":
            name = os.path.basename(attr_path(inc))
            if name in PROJECT_TO_PACKAGE:
                pkgrefs.append(PROJECT_TO_PACKAGE[name])
            elif name in DROPPED_PROJECTS:
                todos.append(f"dropped ProjectReference {inc} (ADR 0015)")
            else:
                private = "" if meta.get("Private", "").lower() != "false" else ' Private="false"'
                projrefs.append(f'    <ProjectReference Include="{attr_path(inc)}"{private} />')
        elif tag == "PackageReference":
            pkgrefs.append(PACKAGE_RENAMES.get(inc, inc))
        elif tag == "Reference":
            short = inc.split(",")[0]
            if short in IMPLICIT_REFERENCES:
                continue
            if short in REFERENCE_TO_PACKAGE:
                pkgrefs.append(REFERENCE_TO_PACKAGE[short])
            else:
                todos.append(f"Reference {inc}" + (f" (HintPath {meta['HintPath']})" if "HintPath" in meta else ""))
        elif tag in ("Import",):
            continue

    for imp in root.iter(NS + "Import"):
        proj = imp.get("Project", "")
        if "ReferencesGtk" in proj or "ReferencesVSEditor" in proj:
            todos.append(f"legacy import {proj} (GTK2 / VS editor references)")

    name = os.path.splitext(os.path.basename(path))[0]
    out = ['<Project Sdk="Microsoft.NET.Sdk">',
           "  <!-- Converted from the legacy csproj by scripts/convert-legacy-csproj.py (ADR 0003). -->",
           "  <PropertyGroup>"]
    out.append(f"    <AssemblyName>{props.get('AssemblyName') or name}</AssemblyName>")
    if props.get("RootNamespace"):
        out.append(f"    <RootNamespace>{props['RootNamespace']}</RootNamespace>")
    if props.get("OutputType") and props["OutputType"].lower() in ("exe", "winexe"):
        out.append("    <OutputType>Exe</OutputType>")
    if props.get("AddinBuildDir"):
        out.append(f"    <AddinBuildDir>{props['AddinBuildDir']}</AddinBuildDir>")
    if props.get("AllowUnsafeBlocks", "").lower() == "true":
        out.append("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>")
    out.append("  </PropertyGroup>")
    for group in (compiles, embedded, nones, ivts, projrefs):
        if group:
            out += ["", "  <ItemGroup>"] + group + ["  </ItemGroup>"]
    if pkgrefs:
        out += ["", "  <ItemGroup>"] + [f'    <PackageReference Include="{p}" />' for p in dict.fromkeys(pkgrefs)] + ["  </ItemGroup>"]
    if todos:
        out += [""] + [f"  <!-- TODO(convert): {t} -->" for t in todos]
    out.append("</Project>")
    text = "\n".join(out) + "\n"
    if write:
        with open(path, "w") as f:
            f.write(text)
        print(f"converted {path}: {len(compiles)} sources, {len(projrefs)} project refs, "
              f"{len(set(pkgrefs))} packages, {len(todos)} TODOs", file=sys.stderr)
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
