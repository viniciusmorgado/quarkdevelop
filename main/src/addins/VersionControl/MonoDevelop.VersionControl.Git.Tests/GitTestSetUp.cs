//
// GitTestSetUp.cs
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
using NUnit.Framework;

// The tests live in MonoDevelop.VersionControl.Tests, MonoDevelop.VersionControl.Git.Tests and
// MonoDevelop.VersionControl.Views: a set-up fixture in their common namespace runs once for all of them.
namespace MonoDevelop.VersionControl
{
	/// <summary>
	/// The repository tests use the MonoDevelop runtime (add-in extension points of the version control add-ins,
	/// the main thread, file services). GuiUnit and mdtool run-md-tests initialized it, and GTK, before NUnit 3
	/// (ADR 0015). The diff color tests read the IDE styles, which need GTK and a display (Xvfb in scripts/test.sh).
	/// </summary>
	[SetUpFixture]
	public class GitTestSetUp
	{
		public static bool GtkAvailable { get; private set; }

		[OneTimeSetUp]
		public void InitializeRuntime ()
		{
			if (!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY")) ||
				!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY"))) {
				Gtk.Application.Init ();
				MonoDevelop.Components.GtkToplevelReferences.Install ();
				GtkAvailable = true;
			}
			// Register the add-ins of the test folder (Core, Ide, VersionControl, VersionControl.Git) in the registry
			// TestHost uses before the runtime starts: the add-in engine activates the root add-ins whose assemblies
			// are already loaded only when they are in the registry at initialization (as in EditorTestEnvironment).
			var host = Path.GetFileName (Path.TrimEndingDirectorySeparator (AppContext.BaseDirectory));
			var configRoot = Path.Combine (UnitTests.Util.TestsRootDir, "config", host);
			Directory.CreateDirectory (configRoot);
			using (var registry = new Mono.Addins.AddinRegistry (configRoot, AppContext.BaseDirectory))
				registry.Update (null);
			UnitTests.TestHost.EnsureInitialized ();
			// mdtool loaded the add-ins under test; here the Git add-in and its dependencies (VersionControl, Ide,
			// ...) are loaded explicitly so that their extension points (VersionControlService) exist.
			Mono.Addins.AddinManager.LoadAddin (null, "MonoDevelop.VersionControl.Git");
		}

		public static void RequireGtk ()
		{
			if (!GtkAvailable)
				Assert.Ignore ("no display (run under Xvfb: ./scripts/pm ./scripts/test.sh)");
		}
	}
}
