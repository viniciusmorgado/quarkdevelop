//
// DebuggerTestSetUp.cs
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
using System.Reflection;
using NUnit.Framework;

[assembly: AssemblyVersion (MonoDevelop.BuildInfo.Version)]

namespace MonoDevelop.Debugger.Tests
{
	/// <summary>
	/// GuiUnit and mdtool run-md-tests initialized the MonoDevelop runtime before NUnit 3 (ADR 0015). The debugger
	/// add-ins of the test folder (Debugger, Debugger.VsCodeDebugProtocol, Debugger.NetCoreDbg) are registered in the
	/// registry TestHost uses before the runtime starts, and the engine add-in is loaded, so that DebuggingService
	/// lists the engines.
	/// </summary>
	[SetUpFixture]
	public class DebuggerTestSetUp
	{
		[OneTimeSetUp]
		public void InitializeRuntime ()
		{
			// DebuggingService's static state creates GTK objects (IdeApp): scripts/test.sh runs the tests under Xvfb.
			if (!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY")) ||
				!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY"))) {
				Gtk.Application.Init ();
			}
			var host = Path.GetFileName (Path.TrimEndingDirectorySeparator (AppContext.BaseDirectory));
			var configRoot = Path.Combine (UnitTests.Util.TestsRootDir, "config", host);
			Directory.CreateDirectory (configRoot);
			using (var registry = new Mono.Addins.AddinRegistry (configRoot, AppContext.BaseDirectory)) {
				registry.Update (null);
			}
			UnitTests.TestHost.EnsureInitialized ();
			// Loads Debugger and Debugger.VsCodeDebugProtocol too (add-in dependencies).
			Mono.Addins.AddinManager.LoadAddin (null, "MonoDevelop.Debugger.NetCoreDbg");
		}
	}
}
