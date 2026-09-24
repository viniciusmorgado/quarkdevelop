//
// VsTestDiscoveryAdapter.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2017 Xamarin, Inc (http://www.xamarin.com)
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.UnitTesting.VsTest
{
	class VsTestDiscoveryAdapter : VsTestAdapter
	{
		ProgressMonitor monitor;

		/// <summary>
		/// Creates the progress monitor if it does not already exist. This is done
		/// on the UI thread explicitly. The ProgressMonitors is a GuiSyncObject so this
		/// will be called on the UI thread implicitly otherwise. The progress monitor
		/// should not be created when the static Instance is created since this can
		/// result in a UI thread hang if Instance is called by both the UI thread and
		/// a background thread at the same time.
		/// </summary>
		async Task CreateProgressMonitor ()
		{
			// Without the workbench (tests, headless hosts) the messages go to the log.
			if (monitor != null || !UnitTestingIde.IsInitialized)
				return;

			await Runtime.RunInMainThread (() => {
				if (monitor != null)
					return;

				monitor = IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor (
					"TestDiscoveryConsole",
					GettextCatalog.GetString ("Test Discovery Console"),
					Stock.Console,
					false,
					true,
					false);
			});
		}

		public static VsTestDiscoveryAdapter Instance { get; } = new VsTestDiscoveryAdapter ();

		/// <summary>
		/// Discovers the tests of the project's output assembly. Requests are queued: vstest.console runs one at a time.
		/// </summary>
		public async Task<DiscoveredTests> DiscoverTestsAsync (Project project)
		{
			await CreateProgressMonitor ();
			var tests = new DiscoveredTests ();
			var testAssemblyFile = GetAssemblyFileName (project);
			if (!File.Exists (testAssemblyFile))
				return tests;

			var handler = new DiscoveryEventsHandler (this, tests);
			var runSettings = GetRunSettings (project);
			await RunRequestAsync (console => console.DiscoverTests (
				new [] { testAssemblyFile },
				runSettings,
				new TestPlatformOptions (),
				handler));
			return tests;
		}

		void OnTestMessage (TestMessageLevel level, string message)
		{
			if (monitor != null) {
				monitor.Log.WriteLine (message);
			} else if (level == TestMessageLevel.Error) {
				LoggingService.LogError ("Test discovery: {0}", message);
			} else {
				LoggingService.LogInfo ("Test discovery: {0}", message);
			}
		}

		class DiscoveryEventsHandler : ITestDiscoveryEventsHandler2
		{
			readonly VsTestDiscoveryAdapter adapter;
			readonly DiscoveredTests tests;

			public DiscoveryEventsHandler (VsTestDiscoveryAdapter adapter, DiscoveredTests tests)
			{
				this.adapter = adapter;
				this.tests = tests;
			}

			public void HandleDiscoveredTests (IEnumerable<TestCase> discoveredTestCases)
			{
				if (discoveredTestCases != null && discoveredTestCases.Any ())
					tests.Add (discoveredTestCases);
			}

			public void HandleDiscoveryComplete (DiscoveryCompleteEventArgs discoveryCompleteEventArgs, IEnumerable<TestCase> lastChunk)
			{
				HandleDiscoveredTests (lastChunk);
			}

			public void HandleLogMessage (TestMessageLevel level, string message)
			{
				adapter.OnTestMessage (level, message);
			}

			public void HandleRawMessage (string rawMessage)
			{
			}
		}
	}
}
