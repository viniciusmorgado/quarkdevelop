//
// GuiTestHost.cs
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
using System.IO;
using System.Threading;
using MonoDevelop.Core;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnitTests;

namespace IdeUnitTests
{
	/// <summary>
	/// The IDE test host (ADR 0015): what GuiUnit and <c>mdtool run-md-tests</c> provided on .NET Framework. GTK, the
	/// MonoDevelop runtime and Xwt are initialized as IdeStartup does, on the calling thread, which becomes the main
	/// thread of the runtime and runs the GLib main loop while NUnit waits for async tests
	/// (<see cref="System.Windows.Forms.WindowsFormsSynchronizationContext"/>).
	/// Call it from a <c>[SetUpFixture]</c> in the global namespace of the test assembly; the tests must run on that
	/// thread (<c>MDTestSingleThread</c>, main/msbuild/Linux/Test.targets). Needs a display (Xvfb in scripts/test.sh).
	/// </summary>
	public static class GuiTestHost
	{
		static bool initialized;
		static SynchronizationContext mainContext;

		public static void EnsureInitialized ()
		{
			if (initialized)
				return;
			if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY")) &&
				string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY")))
				throw new InvalidOperationException ("The IDE tests need a display: run them under Xvfb (./scripts/pm ./scripts/test.sh)");

			// One profile and add-in registry per test host (output folder), as UnitTests.TestHost does.
			var host = Path.GetFileName (Path.TrimEndingDirectorySeparator (AppContext.BaseDirectory));
			var configRoot = Path.Combine (Util.TestsRootDir, "config", host);
			Directory.CreateDirectory (configRoot);
			Environment.SetEnvironmentVariable ("MONODEVELOP_PROFILE", configRoot);
			Environment.SetEnvironmentVariable ("MONO_ADDINS_REGISTRY", configRoot);
			Environment.SetEnvironmentVariable ("XDG_CONFIG_HOME", configRoot);

			Gtk.Application.Init ();
			// An exception in a GLib callback (e.g. a continuation posted to the main loop) is logged, as in the IDE;
			// without a handler GLib# ends the process.
			GLib.ExceptionManager.UnhandledException += args => {
				LoggingService.LogError ("Unhandled exception in the GLib main loop", (Exception)args.ExceptionObject);
				args.ExitApplication = false;
			};

			// The add-ins of the host folder (Core, Ide and the add-ins the tests reference) are registered before the
			// runtime starts: the add-in engine activates the root add-ins whose assemblies are already loaded only
			// when they are in the registry at initialization.
			using (var registry = new Mono.Addins.AddinRegistry (configRoot, AppContext.BaseDirectory))
				registry.Update (null);

			MonoDevelop.Ide.DispatchService.Initialize ();
			mainContext = new System.Windows.Forms.WindowsFormsSynchronizationContext (MonoDevelop.Ide.DispatchService.SynchronizationContext);
			SynchronizationContext.SetSynchronizationContext (mainContext);
			Runtime.MainSynchronizationContext = mainContext;
			MonoDevelop.Ide.RoslynServices.RoslynService.Initialize ();
			Runtime.Initialize (true);
			Runtime.Preferences.EnableUpdaterForCurrentSession = false;
			// The mock shell of IdeTestBase for every test of the host, whatever the order of the fixtures: a fixture
			// that asked for IShell first created the real workbench window.
			Runtime.RegisterServiceType<MonoDevelop.Ide.Gui.Shell.IShell, MockShell> ();
			Runtime.RegisterServiceType<MonoDevelop.Ide.Gui.ProgressMonitorManager, MockProgressMonitorManager> ();
			Xwt.Application.InitializeAsGuest (Xwt.ToolkitType.Gtk3);
			// Gtk.Application.Init (called again by Xwt) installs GLib#'s own synchronization context: set ours again.
			SynchronizationContext.SetSynchronizationContext (mainContext);
			TestHost.SetInitialized (mainContext);
			initialized = true;
		}

		/// <summary>
		/// Sets the synchronization context of the main thread again when a test replaced it (Gtk.Application.Init
		/// installs GLib#'s own): NUnit keeps the main loop running only while ours is current.
		/// </summary>
		public static void RestoreMainThreadContext ()
		{
			if (mainContext != null && Runtime.IsMainThread && SynchronizationContext.Current != mainContext)
				SynchronizationContext.SetSynchronizationContext (mainContext);
		}
	}

	/// <summary>
	/// Applied to a test assembly that uses <see cref="GuiTestHost"/>: restores the synchronization context of the main
	/// thread before each fixture and each test (<see cref="GuiTestHost.RestoreMainThreadContext"/>).
	/// </summary>
	[AttributeUsage (AttributeTargets.Assembly)]
	public sealed class GuiTestContextAttribute : Attribute, ITestAction
	{
		public ActionTargets Targets => ActionTargets.Suite | ActionTargets.Test;

		public void BeforeTest (ITest test) => GuiTestHost.RestoreMainThreadContext ();

		public void AfterTest (ITest test)
		{
		}
	}
}
