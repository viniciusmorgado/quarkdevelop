# M5 evidence — GUI on GTK3

## T066 — Xwt vendored, GTK3 backend on .NET 10 (WS-2)

Source: `main/vendor/xwt/` (upstream mono/xwt `9a22465b52`, MIT; local patches in
`main/vendor/xwt/UPSTREAM.md`). The `main/external/xwt` submodule is removed.

Commands (dev container):

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1280x860x24" bash -c \
  "dotnet main/build/samples/xwt/Gtk3Test.dll & sleep 10; import -window root docs/evidence/M5/T066-xwt-gtk3-samples.png"'
```

Result (2026-09-23): the Xwt sample gallery (`TestApps/Gtk3Test` → `Samples`) starts on
`ToolkitType.Gtk3` with GtkSharp 3.24.24.95, SDK 10.0.401 and GTK 3.24.41. The window shows the menu bar,
the sample tree with the embedded icons, and the content pane. Nothing is written to stdout or stderr.
The process was stopped by the script after the screenshot (exit 143 = SIGTERM).

![Xwt samples on GTK3](T066-xwt-gtk3-samples.png)

Not covered yet: interacting with each sample, and `WebView` (WebKitGTK 3.0 is not available; see
UPSTREAM.md "Known gaps").

## M5b: MonoDevelop.Ide compiles on GTK3 (2026-09-23)

At commit `65392553f7` the whole `MonoDevelop.Ide` project compiles against GtkSharp 3, Roslyn 5.9
and .NET 10 with no excluded sources: `Gtk3PortPending.props` (900 of 1242 files at its peak) is gone.
The strict solution build (`dotnet build main/MonoDevelop.Linux.sln`, warnings as errors with the
per-project baselines) has 0 errors. `./scripts/test.sh`: MonoDevelop.Core.Tests 1065 passed /
9 skipped, MonoDevelop.Ide.Gtk3.Tests 25 passed.

GTK2-only APIs in the sources compiled by the Linux solution
([inventory-linux-sln.md](inventory-linux-sln.md), `./scripts/inventory.sh --linux-sln`):

| Pattern | Files |
|---|---|
| `ExposeEvent` (event, override, args) | 0 |
| `SizeRequested` (event, override, args) | 0 |
| `Gdk.GC` / `Gdk.Drawable` | 0 |
| `Gtk.Rc` (RC theming) | 0 (was 5; GTK 3 themes and per-widget CSS, see below) |

Compiling is not running: the IDE has not been started yet (M5c). Legacy code came back with 48 more
warning IDs on the Ide baseline and with 0.42% line coverage, which lowered the product total from
46.77% to 17.19% (ADR 0015 amendment).

### GTK 3 themes (T082, T083)

GTK 2 RC theming is gone from the Linux build. `GtkThemes` lists the GTK 3 themes of the XDG theme
directories (`<dir>/themes/<Name>/gtk-3.*/gtk.css`) plus the ones built into GTK (Adwaita,
HighContrast), and offers a theme's dark variant (`gtk-dark.css`) as `Name:dark`, the GTK_THEME syntax,
applied with `gtk-application-prefer-dark-theme`. Per-widget RC styles (tab close button, tree expander
size, compact scrolled window, paned handles) are CSS providers on the widget (`GtkCss`). The bundled
GTK 2 gtkrc themes (Xamarin engine, Mac, Windows) are no longer used. Tests: `GtkThemesTests`
(discovery, dark variants, applying a theme, a CSS style property set/replaced/removed on a real
widget). Screenshots of the IDE in a light and a dark theme wait for the IDE to start (M5c), so T083
stays open.

## M5c: the IDE runs (2026-09-23)

- **Start-up (T086, T087):** `dotnet main/build/bin/MonoDevelop.dll` reaches the main window and the
  Welcome page on GTK 3 / .NET 10 ([T086-ide-welcome.png](T086-ide-welcome.png)) with Core, Ide,
  GnomePlatform, Debugger and DesignerSupport add-ins and no errors in the log. Main window about 1.6 to
  3.7 s after the process starts (Xvfb, warm caches).
- **Smoke test (T103):** `MonoDevelop.dll --smoke-test [solution]` opens and builds a solution and exits
  0/1/2 (checked: Smoke.sln 0, Broken.csproj 1, a 2 s watchdog 2). X11: [T103-smoke-x11.png](T103-smoke-x11.png).
- **Wayland (T104):** the same smoke on headless Weston, `GDK_BACKEND=wayland`:
  [T104-smoke-wayland.png](T104-smoke-wayland.png). Both run in `scripts/ci.sh`.
- Not yet: C# editing features (T089) and the other add-ins.

## T085, T088 — the source editor on GTK 3 (M5c)

`MonoDevelop.SourceEditor2` (with the `Mono.TextEditor.Shared` document model) is SDK-style net10.0 on
GtkSharp 3.24.24.95; `MonoDevelop.SourceEditor.dll.config` is deleted (P/Invokes resolve through
`NativeLibraryMap`, `SourceEditorNativeLibraries`).

Commands (dev container):

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1600x1000x24" bash -c "dotnet main/build/bin/MonoDevelop.dll \
  -no-redirect main/tests/linux-smoke/Hello/Program.cs > out/ide.log 2>&1 & sleep 60; import -window root out/ide.png; kill %1"'
./scripts/pm ./scripts/test.sh --no-build
```

Result (2026-09-24): the IDE opens `Program.cs` in the source editor: line numbers, caret, syntax highlighting
(the string literal) and the quick task strip ([T088-editor.png](T088-editor.png)); a larger file shows
comments and keywords highlighted and the text area filling the document view
([T088-editor-highlighting.png](T088-editor-highlighting.png)). `out/ide.log` has no ERROR/FATAL line; the only
MEF composition error left is Roslyn's Pythia signature help provider (external access, not used).

The editor platform services that upstream came from the closed-source VS editor
(`Microsoft.VisualStudio.Platform.VSEditor`) are exported by SourceEditor2 (ISmartIndentationService,
IToolTipService, IViewElementFactoryService, IIntellisenseSessionStackMapService, ISignatureHelpBroker) or added
in `VSEditor/EditorPlatformServices.cs` (ILoggingServiceInternal, IObscuringTipManager, the `BraceCompletion/Enabled`
option definition).

Tests: `MonoDevelop.TextEditor.Tests` (the legacy Mono.TextEditor suite, converted) 289 passed, 12 skipped
(legacy `[Ignore]`), 16 quarantined ([quarantine.md](../M4/quarantine.md)); `MonoDevelop.Ide.Gtk3.Tests`
`SourceEditorTests` (editor MEF exports, text area in a GTK 3 scrolled window rendering a document offscreen
within the clip it is given, typing).

![Program.cs in the source editor](T088-editor.png)

## T089, T090 — the C# binding and Refactoring add-ins on Roslyn 5.9 (M5c)

`MonoDevelop.Refactoring` and `CSharpBinding` are SDK-style net10.0 add-ins on GTK 3 and Roslyn 5.9 (Publicizer),
without Roslyn EditorFeatures (ADR 0010 amendment), NRefactory (ADR 0019), MonoDevelop.UnitTesting (T101) or the
Cocoa/WPF TextEditor add-in (ADR 0012). CSharpBinding depends on the CSharpBinding.Core add-in and builds to
`AddIns/CSharpBinding`. Excluded sources are listed with their reason in each csproj; removed features in
`docs/BREAKING-CHANGES.md`.

Commands (dev container):

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1600x1000x24" bash -c "dotnet main/build/bin/MonoDevelop.dll -no-redirect \
  main/tests/linux-smoke/Smoke.sln main/tests/linux-smoke/Hello/Program.cs > out/ide.log 2>&1 & sleep 60; import -window root out/ide.png; kill %1"'
./scripts/pm ./scripts/test.sh --no-build
```

Result (2026-09-24): the IDE loads the Refactoring, CSharpBinding.Core and CSharpBinding add-ins; the Solution pad
shows the C# project icons; `Program.cs` has Roslyn semantic highlighting (class names, string literal) and the quick
task strip reports no errors ([T089-csharp.png](T089-csharp.png)). `out/ide.log` has no ERROR/FATAL line from these
add-ins (the only ERROR lines, also without them, are the core MSBuild evaluator failing on the SDK 10.0.401
property `_MSBuildVersionMajorMinor`).

Tests: `MonoDevelop.Ide.Gtk3.Tests` `CSharpBindingTests` — completion after `System.Console.` offers `WriteLine`
(US3-2), formatting of a document and on `;` without EditorFeatures, the lexer that replaces NRefactory's; 48 passed.
`MonoDevelop.Refactoring.Tests` (NUnit 3; the three legacy files wait for IdeUnitTests, T107) — host analyzers as
solution analyzer references give the compiler errors through `IDiagnosticAnalyzerService`, are added again when the
solution is replaced, and the code action progress tracker; 4 passed.

![Program.cs with the C# binding](T089-csharp.png)

## T100 — ADR 0020 NuGet client 7.9 and the NuGet add-in (M5c)

`MonoDevelop.PackageManagement` is an SDK-style net10.0 add-in on GTK 3 and the NuGet 7.9 client
([ADR 0020](../../adr/0020-nuget-client-version.md)): the NuGet assemblies the .NET SDK carries are compile-only in every
project (`main/msbuild/Linux/Common.targets`) and load from the SDK at run time, the same ones MSBuild's
`NuGetSdkResolver` uses; the add-in ships `NuGet.PackageManagement`, `NuGet.Resolver` and `Microsoft.Web.XmlTransform`.
`MonoDevelop.Refactoring`'s `PackageInstaller` services are compiled again, on Roslyn 5.9's `IPackageInstallerService` /
`ISymbolSearchService`. Excluded sources and API changes are listed in the csproj files and the ADR; removed features in
`docs/BREAKING-CHANGES.md`.

Commands (dev container):

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln
./scripts/pm ./scripts/check-assemblies.sh
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1600x1000x24" dotnet test \
  main/src/addins/MonoDevelop.PackageManagement/MonoDevelop.PackageManagement.Tests/MonoDevelop.PackageManagement.Tests.csproj'
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1600x1000x24" bash -c "dotnet main/build/bin/MonoDevelop.dll -no-redirect \
  main/tests/linux-smoke/Smoke.sln > out/ide.log 2>&1 & sleep 70; import -window root out/ide.png; kill %1"'
```

Result (2026-09-24): `main/build/bin` has no NuGet assembly and its `MonoDevelop.deps.json` no NuGet runtime asset;
`check-assemblies.sh` finds no duplicate. The IDE loads the add-in, restores the smoke solution on opening ("Packages
successfully restored", [T100-nuget.png](T100-nuget.png)), and `out/ide.log` no longer has the
`The SDK resolver type "NuGetSdkResolver" failed to load … NuGet.Common, Version=7.9.0.0` warning.

Tests: `MonoDevelop.PackageManagement.Tests` (NUnit 3) 704 passed, 3 skipped (legacy `[Ignore]`), 1 quarantined
(nuget.org, [quarantine.md](../M4/quarantine.md)); 10 legacy fixtures that restore .NET Framework / Xamarin samples from
nuget.org through `IdeTestBase` (T107) are not compiled. FR-010: `PackageOperationsEndToEndTests` adds (1.0.0), updates
(2.0.0), restores (assets file and extracted package deleted) and removes a package in a copy of
`tests/linux-smoke` through the add-in's package actions, with a local folder feed and global packages folder created by
the test, and resolves an MSBuild project SDK from that feed with the in-process `NuGetSdkResolver`.

![The smoke solution restored by the NuGet add-in](T100-nuget.png)

## T093, T095, T096, T098 — Assembly browser, hex editor, DocFood and Gettext add-ins (M5c)

`MonoDevelop.AssemblyBrowser`, `MonoDevelop.HexEditor`, `MonoDevelop.DocFood` and `MonoDevelop.Gettext` are SDK-style
net10.0 add-ins on GTK 3, in their MonoDevelop 8.6 folders (`AddIns/DisplayBindings/{AssemblyBrowser,HexEditor,Gettext}`,
`AddIns/BackendBindings`). Stetic output is frozen; DocFood's and HexEditor's `gui.stetic` are deleted (HexEditor's
Stetic project had no widgets and was never compiled).

- The assembly browser decompiles with ICSharpCode.Decompiler 11.1.0.9782 from nuget.org (metadata based, no
  Mono.Cecil; upstream got 5.0 transitively from Roslyn EditorFeatures), copied next to the add-in. It never used
  NRefactory; the MonoDoc lookup (`HelpExtensions.cs`, no callers) is not compiled. Decompilations of one assembly
  are serialized (`CSharpDecompiler` keeps its syntax tree in a field: selecting nodes quickly mixed two outputs).
- Gettext no longer depends on the Autotools and Deployment add-ins (ADR 0017): `MakefileHandler.cs` is not compiled,
  the `IDeployable` part of `TranslationProject` is behind `GETTEXT_DEPLOYMENT`, and the GTK 2 GtkSpell binding (its
  callers were commented out upstream) is not compiled. The catalog editor's text editors are hosted in frames
  (GTK 3 would wrap them in viewports) and entry colors are CSS (`Gtk3BaseColor`). The add-in stays off by default.
- Outside these projects: the source editor's scroll bar overview no longer creates a cairo context of the IDE
  window on Linux (it was unused there, and disposing it made GTK lose references of the window: the IDE crashed when
  the Gettext catalog editor opened); the vs-editor-api `Strings.resx` resources keep the manifest names their
  generated classes look up (an undo transaction for a text change set outside of one logged a missing resource).

Commands (dev container; a scratch profile with the Gettext add-in enabled, temporary start-up handlers opened the
assembly browser on `System.Console` in C# and `Xwt.dll` in the hex editor for the screenshots):

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln
./scripts/pm bash -lc 'xvfb-run -a -s "-screen 0 1600x1000x24" bash -c "dotnet main/build/bin/MonoDevelop.dll -no-redirect \
  main/tests/linux-smoke/Smoke.sln > out/ide.log 2>&1 & sleep 70; import -window root out/ide.png; kill %1"'
./scripts/pm ./scripts/ci.sh
```

Result (2026-09-24): the IDE loads the AssemblyBrowser and DocFood add-ins at start (HexEditor loads when a file is
opened in it, Gettext when enabled) and `out/ide.log` has no ERROR/FATAL line. The assembly browser shows
`System.Console` decompiled to C# ([T093-assembly-browser.png](T093-assembly-browser.png)), the hex editor `Xwt.dll`
([T095-hex-editor.png](T095-hex-editor.png)) and the Gettext catalog editor a PO file with a valid, a fuzzy and a
missing translation ([T098-gettext-po-editor.png](T098-gettext-po-editor.png)). DocFood's options panels are
commented out in its manifest upstream; its editor extension and documentation generator load with the add-in.

Tests (`MonoDevelop.Ide.Gtk3.Tests`, 64 passed): `AssemblyBrowserTests` (System.Console decompiled to C# with member
links, and disassembled to IL), `GettextTests` (a PO catalog read, changed and written back; the CSS base color of an
entry), `TextEditorOverviewTests` (an embedded editor redraws its overview after an options change),
`EditorResourcesTests` (the string resources of the 11 vs-editor-api assemblies are found). `./scripts/ci.sh`: every
step passes except `gui-smoke`, which fails the same way on an unmodified build of `1fefbbab95` (the IDE reports one
build error for Smoke.sln with no message while `mdtool build` and `dotnet build` succeed).

![System.Console in the assembly browser](T093-assembly-browser.png)
