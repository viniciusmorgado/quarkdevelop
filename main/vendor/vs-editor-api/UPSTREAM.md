# Vendored: vs-editor-api (text editor subset)

| | |
|---|---|
| Upstream | https://github.com/microsoft/vs-editor-api |
| Source commit | `5ce6368e5b71451d75a388db524e5e104e222122` (submodule main/external/vs-editor-api, "Sync vs-editor-core @ 3a6d05e", 2020-01-17) |
| License | MIT (LICENSE) |
| Vendored on | 2026-09-23 (task T068, ADR 0005, ADR 0012) |

The main/external/vs-editor-api submodule is removed.

## Contents

These projects are vendored with their upstream assembly names, assembly version 16.0.0.0 and
Microsoft public-key signing (`build/msfinal.snk`, public key only). Keeping the signing preserves
their identity and their `InternalsVisibleTo`.

| Layer | Projects |
|---|---|
| Contracts | `Core/Def` (CoreUtility), `Text/Def/{TextData,TextLogic,TextUI,Internal}`, `Language/Def/{Language,Intellisense,StandardClassification}` |
| Utilities | `Text/Util/{TextDataUtil,TextLogicUtil,TextUIUtil}`, `Language/Util/LanguageUtil` |
| Implementations | `Core/Impl`, `Language/Impl/Language`, `Text/Impl/{TextModel,TextBufferUndoManager,StandaloneUndo,TextSearch,ClassificationAggregator,ClassificationType,TagAggregator,DifferenceAlgorithm,EditorOptions,EditorPrimitives,EditorOperations,Navigation,Commanding,Outlining,PatternMatching,XPlat/MultiCaretImpl}` |

These parts are not vendored:
- the FPF stubs of WPF, which were Mac-only;
- `TextUIWpf` and `TextUICocoa`;
- `TextUICocoaUtil`;
- `BraceCompletion`, because SourceEditor2 has its own brace completion;
- the unit tests.

## Local patches

Listed per commit in `git log -- main/vendor/vs-editor-api`; summary:

- **Build:** `Directory.Build.props` builds everything for `net10.0` with the repository props and
  keeps the upstream `NoWarn` list. The `Strings.resx` resources keep the manifest names their generated
  `Strings` classes look up (SDK names; `MultiCaretImpl.csproj` sets its `LogicalName`). Analyzer findings are
  baselined in `main/msbuild/Linux/warning-baselines/`.
- **Project files:** .NET Framework `<Reference>` items were removed. MEF comes from the
  System.ComponentModel.Composition package. References to `TextUIWpf`, `Microsoft.VisualStudio.Imaging`
  and `Microsoft.VisualStudio.Utilities` (Windows/VS-only) were dropped.
- **`src/Shims/WpfPrimitives`** (assembly `MonoDevelop.Wpf.Primitives`) provides the WPF types the
  contracts mention, in their original namespaces:
  - `System.Windows`: `Point`, `Vector`, `Size`, `Rect`, `Thickness`, `UIElement`;
  - `System.Windows.Media`: `ImageSource`, `Geometry`, `RectangleGeometry`, `GeometryGroup`.

  It is deliberately not called `WindowsBase`, because .NET ships a `WindowsBase` facade.
  `scripts/check-assemblies.sh` fails if a built assembly shadows a framework assembly.
- **WPF-only files are not built:**
  - the Intellisense presenter styles and `ITextFormattable` (brushes and text formatting);
  - `Internal/Language/CompletionPresenterStylePrivate.cs`,
    `Internal/Language/Intellisense/VisualTreeExtensions.cs` and `Internal/TextUIWpf/**`;
  - `TextUIUtil/{TransformedDispatcherCollection,WpfHelper}.cs` (WPF dispatcher and Win32 interop).

  MonoDevelop uses none of them, except SourceEditor2's WPF view code, which gets GTK equivalents
  in M5c. The `ThemeInfo` assembly attribute of Internal is removed.
- **`BulkObservableCollection`:** the WPF `Dispatcher` is replaced by the `SynchronizationContext`
  of the creating thread.
- **`EditorOperations`:**
  - copy, cut and paste go through `IEditorClipboard` / `EditorClipboard.Current` (new file
    `EditorClipboard.cs`) instead of the WPF clipboard. The host installs its clipboard (GTK in the
    IDE), and an in-process clipboard is the default.
  - the zoom limits read `DefaultTextViewOptions.MaxZoomLevelId`/`MinZoomLevelId`.
- **`Markers.GetMarkerGeometryFromRectangles`** returns a `GeometryGroup` of the rectangles instead
  of a merged WPF `PathGeometry` outline.
- **`FileUtilities.CreateFileStream`** counts the hard links of an existing file with `statx` on Linux
  (upstream threw `PlatformNotSupportedException`, so saving a text document over an existing file failed; T107).
- **`TextImageLoader`** throws `InvalidDataException` instead of `FileFormatException`, which moved
  to System.IO.Packaging on .NET.
- **`WeakReferenceForDictionaryKey`** (TextDataUtil, StandaloneUndo): the formatter-serialization
  constructor and `GetObjectData` are removed (SYSLIB0051).
