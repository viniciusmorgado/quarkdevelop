//
// DotNetCoreTestSetUp.cs
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
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: MonoDevelop.DotNetCore.Tests.MainSynchronizationContext]

namespace MonoDevelop.DotNetCore.Tests
{
	/// <summary>
	/// GuiUnit and mdtool run-md-tests initialized the MonoDevelop runtime and its main loop before NUnit 3 (ADR 0015).
	/// The tests use the project model (C# projects: MonoDevelop.CSharpBinding.Core), Runtime.RunInMainThread and the
	/// add-in's project extension (DotNetCoreProjectExtension), so the add-ins of the test folder are registered and
	/// the add-in is loaded once (as in the PackageManagement tests).
	/// </summary>
	[SetUpFixture]
	public class DotNetCoreTestSetUp
	{
		[OneTimeSetUp]
		public void InitializeRuntime ()
		{
			if (!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY")) ||
				!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY"))) {
				Gtk.Application.Init ();
			}
			// Register the add-ins of the test folder in the registry TestHost uses before the runtime starts: the add-in
			// engine activates the root add-ins whose assemblies are already loaded only when they are in the registry at
			// initialization.
			var host = Path.GetFileName (Path.TrimEndingDirectorySeparator (AppContext.BaseDirectory));
			var configRoot = Path.Combine (UnitTests.Util.TestsRootDir, "config", host);
			Directory.CreateDirectory (configRoot);
			using (var registry = new Mono.Addins.AddinRegistry (configRoot, AppContext.BaseDirectory)) {
				registry.Update (null);
			}
			UnitTests.TestHost.EnsureInitialized ();
			Mono.Addins.AddinManager.LoadAddin (null, "MonoDevelop.DotNetCore");
		}
	}

	/// <summary>
	/// Under GuiUnit the tests ran on the main thread with the GTK synchronization context: the Dependencies folder
	/// caches continue on TaskScheduler.FromCurrentSynchronizationContext. The tests get the runtime's main loop
	/// (UnitTests.TestHost) as their synchronization context.
	/// </summary>
	[AttributeUsage (AttributeTargets.Assembly)]
	public sealed class MainSynchronizationContextAttribute : Attribute, ITestAction
	{
		[ThreadStatic]
		static SynchronizationContext previousContext;

		public ActionTargets Targets => ActionTargets.Test;

		public void BeforeTest (ITest test)
		{
			previousContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext (UnitTests.TestHost.MainSynchronizationContext);
		}

		public void AfterTest (ITest test)
		{
			SynchronizationContext.SetSynchronizationContext (previousContext);
			previousContext = null;
		}
	}
}
