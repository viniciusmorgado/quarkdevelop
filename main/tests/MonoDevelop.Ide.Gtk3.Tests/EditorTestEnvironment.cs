//
// EditorTestEnvironment.cs
//
// Copyright (c) 2026 MonoDevelop contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Mono.TextEditor;
using MonoDevelop.Ide.Composition;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// What the source editor needs outside of the IDE (tasks T085/T088): the MonoDevelop runtime, Xwt on GTK 3
	/// and the MEF composition of the editor platform. Shared by MonoDevelop.Ide.Gtk3.Tests and
	/// MonoDevelop.TextEditor.Tests (linked source). The GTK parts need a display (the tests run under Xvfb).
	/// </summary>
	static class EditorTestEnvironment
	{
		static bool initialized;

		// The editor platform assemblies the IDE composes (MonoDevelop.Ide.addin.xml, /MonoDevelop/Ide/Composition).
		static readonly string[] editorPlatformAssemblies = {
			"Microsoft.VisualStudio.Language.Utilities",
			"Microsoft.VisualStudio.Text.Data.Utilities",
			"Microsoft.VisualStudio.Text.Logic.Utilities",
			"Microsoft.VisualStudio.Text.UI.Utilities",
			"Microsoft.VisualStudio.CoreUtilityImplementation",
			"Microsoft.VisualStudio.Language.Implementation",
			"Microsoft.VisualStudio.Logic.Text.BufferUndoManager.Implementation",
			"Microsoft.VisualStudio.Logic.Text.Classification.Aggregator.Implementation",
			"Microsoft.VisualStudio.Logic.Text.Classification.LookUp.Implementation",
			"Microsoft.VisualStudio.Logic.Text.Find.Implementation",
			"Microsoft.VisualStudio.Logic.Text.Navigation.Implementation",
			"Microsoft.VisualStudio.Logic.Text.Tagging.Aggregator.Implementation",
			"Microsoft.VisualStudio.Text.Differencing.Implementation",
			"Microsoft.VisualStudio.Text.EditorOptions.Implementation",
			"Microsoft.VisualStudio.Text.Implementation.StandaloneUndo",
			"Microsoft.VisualStudio.Text.Model.Implementation",
			"Microsoft.VisualStudio.Text.MultiCaret.Implementation",
			"Microsoft.VisualStudio.Text.Outlining.Implementation",
			"Microsoft.VisualStudio.Text.PatternMatching.Implementation",
			"Microsoft.VisualStudio.UI.Text.Commanding.Implementation",
			"Microsoft.VisualStudio.UI.Text.EditorOperations.Implementation",
			"Microsoft.VisualStudio.UI.Text.EditorPrimitives.Implementation",
			"Microsoft.VisualStudio.Language",
			"Microsoft.VisualStudio.Language.StandardClassification",
			"Microsoft.VisualStudio.Text.Logic",
			"Microsoft.VisualStudio.Text.UI",
		};

		/// <summary>Initializes the runtime and the editor composition once per process, on the calling thread.</summary>
		public static void EnsureInitialized ()
		{
			if (initialized)
				return;
			InitializeRuntime ();
			// Services the editor uses, initialized here, on the main thread (the text area reads the platform
			// telemetry of the desktop service; the editor fonts come from the font service).
			InitializeService<DesktopService> ();
			InitializeService<MonoDevelop.Ide.Fonts.FontService> ();
			ComposeEditorPlatform ();
			initialized = true;
		}

		/// <summary>
		/// The editor's text model comes from MEF (PlatformCatalog). The IDE composes the assemblies its add-ins list;
		/// the test composes the editor platform assemblies (main/build/bin), MonoDevelop.Ide and the source editor
		/// add-in directly (not from the add-in extension points) and installs the result as CompositionManager.Instance.
		/// </summary>
		static void ComposeEditorPlatform ()
		{
			var binDir = Path.GetFullPath (Path.Combine (AppContext.BaseDirectory, "..", "..", "bin"));
			AssemblyLoadContext.Default.Resolving += (context, name) => {
				var path = Path.Combine (binDir, name.Name + ".dll");
				return File.Exists (path) ? context.LoadFromAssemblyPath (path) : null;
			};
			var assemblies = new HashSet<Assembly> (editorPlatformAssemblies.Select (n => Assembly.Load (new AssemblyName (n)))) {
				typeof (CompositionManager).Assembly,
				typeof (MonoTextEditor).Assembly,
			};
			// Not the default cache location (the Ide add-in's data folder): the MEF cache of the test is never read.
			var cacheDir = Path.Combine (AppContext.BaseDirectory, "mef-cache");
			var caching = new CompositionManager.Caching (assemblies, new CompositionManager.RuntimeCompositionExceptionHandler (), file => Path.Combine (cacheDir, file));
			// Composed on the thread pool and waited for: awaiting VS MEF under NUnit's async set-up context deadlocks.
#pragma warning disable VSTHRD002 // test set-up: no UI thread to block
			var (runtimeComposition, _) = Task.Run (() => CompositionManager.CreateRuntimeCompositionFromDiscovery (caching)).GetAwaiter ().GetResult ();
#pragma warning restore VSTHRD002
			var manager = new CompositionManager ();
			var factory = runtimeComposition.CreateExportProviderFactory ();
			SetProperty (manager, nameof (CompositionManager.RuntimeComposition), runtimeComposition);
			SetProperty (manager, nameof (CompositionManager.ExportProviderFactory), factory);
			SetProperty (manager, nameof (CompositionManager.ExportProvider), factory.CreateExportProvider ());
			typeof (CompositionManager).GetField ("instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue (null, manager);
		}

		/// <summary>
		/// The editor uses the MonoDevelop runtime (add-in extension points, properties, logging). It is initialized
		/// as the IDE does, on this thread (the GTK one), with an isolated profile as UnitTests.TestHost uses, unless a
		/// test host (IdeUnitTests.GuiTestHost) initialized it already.
		/// </summary>
		static void InitializeRuntime ()
		{
			if (MonoDevelop.Core.Runtime.Initialized)
				return;
			var configRoot = Path.Combine (AppContext.BaseDirectory, "config");
			Directory.CreateDirectory (configRoot);
			Environment.SetEnvironmentVariable ("MONODEVELOP_PROFILE", configRoot);
			Environment.SetEnvironmentVariable ("MONO_ADDINS_REGISTRY", configRoot);
			Environment.SetEnvironmentVariable ("XDG_CONFIG_HOME", configRoot);
			// GTK is used from this thread, as the IDE's main thread (gtk_init can be called again).
			Gtk.Application.Init ();
			// Register the add-ins of the test folder first: the add-in engine activates the root add-ins (Core, Ide)
			// whose assemblies are already loaded only when they are in the registry at initialization.
			using (var registry = new Mono.Addins.AddinRegistry (configRoot, AppContext.BaseDirectory))
				registry.Update (null);
			DispatchService.Initialize ();
			var testContext = System.Threading.SynchronizationContext.Current;
			System.Threading.SynchronizationContext.SetSynchronizationContext (DispatchService.SynchronizationContext);
			MonoDevelop.Core.Runtime.MainSynchronizationContext = DispatchService.SynchronizationContext;
			MonoDevelop.Core.Runtime.Initialize (true);
			// NUnit waits on this thread for async set-ups and tests: continuations posted to the GLib context would
			// never run. RunInMainThread still posts to the GLib main loop (pumped by the GTK tests).
			System.Threading.SynchronizationContext.SetSynchronizationContext (testContext);
			// Margins and markers use Xwt images and fonts, on the GTK 3 backend as in the IDE (IdeStartup).
			Xwt.Application.InitializeAsGuest (Xwt.ToolkitType.Gtk3);
		}

		static void InitializeService<T> () where T : MonoDevelop.Core.Service
		{
			var service = MonoDevelop.Core.Runtime.GetService<T> ();
			while (!service.IsCompleted) {
				if (!GLib.MainContext.Iteration (false))
					System.Threading.Thread.Sleep (10);
			}
			if (service.IsFaulted)
				throw new InvalidOperationException ($"{typeof (T).Name} failed to initialize", service.Exception);
		}

		static void SetProperty (object target, string name, object value)
		{
			target.GetType ().GetProperty (name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue (target, value);
		}
	}
}
