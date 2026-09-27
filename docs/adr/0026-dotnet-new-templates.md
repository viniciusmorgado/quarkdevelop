# 0026 — Project and file templates from `dotnet new`

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

The New Project and New File dialogs of MonoDevelop 8.6 list templates of three kinds:

- XML templates in the add-ins: `*.xpt.xml` project templates and `*.xft.xml` file templates, with their C#, Razor,
  JavaScript, CSS and image assets. They are registered on `/MonoDevelop/Ide/ProjectTemplates` and
  `/MonoDevelop/Ide/FileTemplates`. Most of them target .NET Framework, PCL, GTK# 2 or ASP.NET Web Forms, which the
  Linux build does not support (ADR 0017).
- Microsoft.TemplateEngine templates. Each one is registered by id on `/MonoDevelop/Ide/Templates` and
  `/MonoDevelop/Ide/ItemTemplates` (about 1,700 lines of `MonoDevelop.DotNetCore.addin.xml`, for SDKs 1.x to 3.1 and
  10.0). They are instantiated in process, and a wizard page chooses the target framework.
- The .NET Core 2017 template packages, downloaded at build time.

These lists do not follow the installed SDK. A template shipped by a new SDK, a workload or `dotnet new install`
is invisible until someone registers it in XML. A template may also be created differently from `dotnet new`: other
parameters, no post actions, no constraint checks. The maintainer decided (T152) that the dialogs show the templates
of `dotnet new`, that the IDE creates projects and items by running the CLI, and that the old templates are removed.

Two questions follow. How does the IDE read the list of templates, fast, without blocking the UI, and robustly
enough to classify new SDK templates automatically? Which of those templates does it show?

## Considered Options

For listing:

1. **Parse `dotnet new list`**. SDK 10 has no machine-readable output (no `--format json`). `--columns-all` prints
   a table for people: names and authors longer than the column are cut to `...`, there is no identity, no
   description, no parameters and no constraint, and the headers are localized. Solution templates show an empty
   type. Each call takes about 0.25 s.
2. **Read the CLI's own cache** (`~/.templateengine/dotnetcli/<sdk>/templatecache.json`). The format is internal to
   the template engine and changes between versions. The file exists only after the CLI has run once, and the IDE
   would read (or rebuild and overwrite) another program's state.
3. **Read the same template packages in process with Microsoft.TemplateEngine** (chosen). This is the library of
   `dotnet new` (10.0.401, already a dependency of MonoDevelop.Ide since T099). It reads the packages the CLI uses:
   the newest `dotnet/templates/<version>` folder of the SDK's major.minor, the workload packs in
   `dotnet/template-packs`, and the packages of `dotnet new install` listed in
   `$DOTNET_CLI_HOME|~/.templateengine/packages.json`. It returns full metadata: identity, group, names, description,
   short names, tags, `type` and `language` tags, parameters and constraints.

For creating, the only option considered is the CLI: `dotnet new <short name> -o <dir> -n <name> [--language F#]`,
`dotnet new sln --format sln` and `dotnet sln <sln> add <project>`.

## Decision Outcome

**Listing.** Option 3. `DotNetNewTemplateCatalog` hands the package list to a `TemplatePackageManager`. It uses the
host identifier `dotnetcli` (so the `dotnetcli.host.json` files apply) and in-memory settings, so it never writes
into `~/.templateengine`. It groups the templates by group identity, one entry per `dotnet new list` row, with the
languages of the group. Reading the 7 packages of SDK 10.0.401 (53 templates) takes 90–500 ms. The result is cached
in `<cache dir>/DotNetNewTemplates/<sdk version>.json`. The cache key is a fingerprint of the SDK version, the UI
culture and the path, time and size of every package, and a hit takes about 5–10 ms. The catalog is loaded in the
background when the IDE starts. A dialog that opens before the load finishes keeps the GTK main loop running while it
waits. Each time a dialog opens, the catalog is read again in the background (at most every 10 s), so templates
installed while the IDE runs appear the next time. If the engine reads nothing, the catalog falls back to parsing
`dotnet new list --columns-all` (English output). The same parser reads the test fixtures.

Tests check that the engine lists what the CLI lists (`DotNetNewTemplateTests.EngineListsWhatTheCliListsAsync`): the
same short names, languages, tags and classification as `dotnet new list --ignore-constraints`, and as
`dotnet new list` once the templates that need a project are removed.

**Rules for which templates are shown** (`DotNetNewTemplateClassifier`):

- **Languages:** C# and F# only, C# first and default. Other languages (VB) are dropped from a template's language
  list. A template that has none of the supported languages is hidden (e.g. the VB `module` item).
- **Platform:** Linux only. A template is hidden when a segment of its tags is `WinForms`, `WPF`, `WinUI` or `UWP`,
  or when an `os` constraint excludes Linux. The deny list has one entry that tags cannot identify: `webconfig`, an IIS
  file tagged `Config`.
- **Categories:** the first segment of the tags, as `dotnet new list` shows them: Common (with `Common/AI/MCP`), Web,
  Test, Config, MSBuild, Solution, or Other when there is no tag. Categories are ordered Common, Web, Test, Config,
  MSBuild, Solution, then by name.
- **Kinds:** `type` = `project` goes to New Project. Solution templates (type `solution`, or no type with the
  Solution tag) go to New Project for new solutions when their `format` parameter can write `.sln` (`sln`); the
  solution filter `slnf` needs an existing solution and is not offered. `type` = `item` goes to New File: Razor Page,
  MVC Controller, NUnit Test Item, .gitignore, global.json, .editorconfig, nuget.config, Directory.Build.props…
- **Constraints:** `project-capability` constraints are kept per language. The C# `class`, `interface`, `enum`,
  `record` and `struct` items (`CSharp`) are offered only for a project that has the capability, as `dotnet new` does,
  and the IDE passes `--project` when it creates them. This brings back Add > New Class.

**New Project.** `DotNetNewProjectTemplatingProvider` is the only provider on
`/MonoDevelop/Ide/ProjectTemplatingProviders`. It defines its own categories: one top-level `.NET` category, with a
category per first tag segment. For a new solution it runs `dotnet new <template>` into the project directory, then
`dotnet new sln --format sln` and `dotnet sln add` (SDK 10 writes `.slnx` by default, which the project model does not
read), and loads the solution. When a project is added to an open solution, it runs `dotnet new` and the dialog adds
the loaded project to the solution; the IDE saves the solution itself, because it owns the open solution file (a
`dotnet sln add` run behind its back would be overwritten on the next save). The new solution check ("file exists,
overwrite?") now runs before the template. CLI failures are shown with the CLI's output as details
(`DotNetCliException`).

**New File.** The dialog lists the visible item templates, by category, in the languages of the target project. It
runs `dotnet new <item> -o <folder> -n <name> [--language] [--project <proj>]`. Items with a fixed name (.gitignore,
global.json…) get no `-n`. The project is then re-evaluated, and SDK-style globs pick up the new files; sources that
the project does not glob (F#) are added to it. Without a project, the file is written to a folder chosen in the
dialog.

**Recent templates / Welcome page.** The recent templates of the New Project dialog are resolved against the same
provider. The Welcome page has no template list of its own.

### Consequences

- Good: the dialogs follow the installed SDK, workloads and `dotnet new install`, and new SDK templates are
  classified from their tags without IDE changes. Projects are created exactly as `dotnet new` creates them.
- Good: about 15,000 lines of XML registrations, XML templates and template assets are removed, along with the
  in-process template instantiation (`MicrosoftTemplateEngine*`, the DotNetCore wizard).
- Bad: projects are created by the SDK `dotnet` resolves for the target folder (a `global.json` there can select
  another SDK), while the list comes from the newest SDK.
- Bad: template options other than the language (framework, `--use-program-main`, authentication…) are not offered
  in the dialog; the defaults of the template apply.
- Bad: F# projects are created and kept in the solution, but the IDE has no F# language binding yet: they are loaded
  as unsupported projects (T154). Update 2026-09-25: the F# binding is back ([ADR 0027](0027-fsharp-binding.md)), and
  F# projects load as F# projects.
- Open: the XML template engine classes (`ProjectTemplate`, `FileTemplate` and their descriptors) remain in
  MonoDevelop.Ide for API compatibility (`FileTemplate` by id, TextTemplating), with nothing registered on
  `/MonoDevelop/Ide/ProjectTemplates` or `/MonoDevelop/Ide/FileTemplates` in the Linux build.
