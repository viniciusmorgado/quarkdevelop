//
// VsTestRunAdapter.cs
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Projects;

namespace MonoDevelop.UnitTesting.VsTest
{
	class VsTestRunAdapter : VsTestAdapter
	{
		public VsTestRunAdapter ()
		{
		}

		public static VsTestRunAdapter Instance { get; } = new VsTestRunAdapter ();

		class RunOrDebugJob
		{
			public TestContext TestContext { get; }
			public Project Project { get; }
			public TestResultBuilder TestResultBuilder { get; }
			public TaskCompletionSource<UnitTestResult> TaskSource { get; }
			public ProcessAsyncOperation ProcessOperation { get; set; }

			public RunOrDebugJob (TestContext testContext, IVsTestTestProvider rootTest)
			{
				TestContext = testContext;
				TestResultBuilder = new TestResultBuilder (testContext, rootTest);
				Project = rootTest.Project;
				TaskSource = TestResultBuilder.TaskSource;
			}
		}

		UnitTestResult ReportRunFailure (TestContext testContext, Exception exception)
		{
			testContext.Monitor.ReportRuntimeError (exception.Message, exception);
			return UnitTestResult.CreateFailure (exception);
		}

		UnitTestResult HandleMissingAssemblyOnRun (TestContext testContext, string testAssemblyPath)
		{
			var exception = new FileNotFoundException (
				GettextCatalog.GetString ("Unable to run tests. Assembly not found '{0}'", testAssemblyPath),
				testAssemblyPath);

			return ReportRunFailure (testContext, exception);
		}

		/// <summary>
		/// Runs the tests of the provider (all the tests of the project when it has no test list). With an execution
		/// handler (the IDE's run and debug modes) the test host is started by that handler (custom test host launch),
		/// so its output goes to the IDE's console and a debugger can start it; otherwise vstest.console starts it.
		/// </summary>
		public async Task<UnitTestResult> RunTests (
			UnitTest test,
			TestContext testContext,
			IVsTestTestProvider testProvider)
		{
			var runJob = new RunOrDebugJob (testContext, testProvider);
			try {
				string testAssemblyPath = GetAssemblyFileName (testProvider.Project);
				if (!File.Exists (testAssemblyPath))
					return HandleMissingAssemblyOnRun (testContext, testAssemblyPath);

				var tests = testProvider.GetTests ()?.ToList ();
				var sources = new [] { testAssemblyPath };
				var runSettings = GetRunSettings (testProvider.Project);
				var handler = new RunEventsHandler (this, runJob);
				ITestHostLauncher launcher = testContext.ExecutionContext?.ExecutionHandler != null ? new TestHostLauncher (this, runJob) : null;

				await RunRequestAsync (console => {
					using (testContext.Monitor.CancellationToken.Register (() => CancelTestRun (console, runJob))) {
						testContext.Monitor.CancellationToken.ThrowIfCancellationRequested ();
						if (launcher != null) {
							if (tests == null)
								console.RunTestsWithCustomTestHost (sources, runSettings, new TestPlatformOptions (), handler, launcher);
							else
								console.RunTestsWithCustomTestHost (tests, runSettings, new TestPlatformOptions (), handler, launcher);
						} else {
							if (tests == null)
								console.RunTests (sources, runSettings, new TestPlatformOptions (), handler);
							else
								console.RunTests (tests, runSettings, new TestPlatformOptions (), handler);
						}
					}
				}, testContext.Monitor.CancellationToken);

				// An aborted run (e.g. the test host crashed) completes without results.
				runJob.TaskSource.TrySetResult (runJob.TestResultBuilder.TestResult);
				return await runJob.TaskSource.Task;
			} catch (OperationCanceledException) {
				return runJob.TestResultBuilder.TestResult;
			} catch (Exception ex) {
				testContext.Monitor.ReportRuntimeError (
					GettextCatalog.GetString ("Failed to run tests."),
					ex);

				if (!runJob.TaskSource.Task.IsCompleted)
					runJob.TestResultBuilder.CreateFailure (ex);
				return runJob.TestResultBuilder.TestResult;
			}
		}

		static void OnTestMessage (RunOrDebugJob runJob, string message)
		{
			runJob.TestContext.Monitor.WriteGlobalLog (message + Environment.NewLine);
		}

		void CancelTestRun (Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Interfaces.IVsTestConsoleWrapper console, RunOrDebugJob runJob)
		{
			runJob.TaskSource.TrySetCanceled ();

			try {
				console.CancelTestRun ();
			} catch (Exception ex) {
				LoggingService.LogError ("CancelTestRun error.", ex);
			}

			try {
				if (runJob.ProcessOperation != null) {
					if (!runJob.ProcessOperation.IsCompleted)
						runJob.ProcessOperation.Cancel ();
					runJob.ProcessOperation = null;
				}
			} catch (Exception ex) {
				LoggingService.LogError ("CancelTestRun error.", ex);
			}
		}

		int StartCustomTestHost (TestProcessStartInfo startInfo, RunOrDebugJob runJob)
		{
			var currentTestContext = runJob.TestContext;
			OperationConsole console = currentTestContext.ExecutionContext.ConsoleFactory.CreateConsole (
				OperationConsoleFactory.CreateConsoleOptions.Default.WithTitle (GettextCatalog.GetString ("Unit Tests")));

			// The test host of a .NET test project is `dotnet exec ... testhost.dll`: a native command. The legacy add-in
			// used the DotNetCore add-in's DotNetCoreExecutionCommand here (T099 not ported yet).
			ExecutionCommand command = new NativeExecutionCommand (
				startInfo.FileName,
				startInfo.Arguments,
				startInfo.WorkingDirectory,
				startInfo.EnvironmentVariables);

			runJob.ProcessOperation = currentTestContext.ExecutionContext.ExecutionHandler.Execute (command, console);
			var eventProcessSet = new ManualResetEvent (false);
			runJob.ProcessOperation.ProcessIdSet += delegate {
				eventProcessSet.Set ();
			};
			if (runJob.ProcessOperation.ProcessId == 0) {
				if (!eventProcessSet.WaitOne (5000) && runJob.ProcessOperation.ProcessId == 0) {
					throw new TimeoutException ("Timeout, process id not set.");
				}
			}
			return runJob.ProcessOperation.ProcessId;
		}

		int LaunchTestHost (TestProcessStartInfo startInfo, RunOrDebugJob runJob)
		{
			try {
				return StartCustomTestHost (startInfo, runJob);
			} catch (Exception ex) {
				LoggingService.LogError ("Unable to start custom test host.", ex);
				runJob.TestContext.Monitor.ReportRuntimeError (GettextCatalog.GetString ("Unable to start test host."), ex);
				throw;
			}
		}

		class RunEventsHandler : ITestRunEventsHandler
		{
			readonly VsTestRunAdapter adapter;
			readonly RunOrDebugJob runJob;

			public RunEventsHandler (VsTestRunAdapter adapter, RunOrDebugJob runJob)
			{
				this.adapter = adapter;
				this.runJob = runJob;
			}

			public void HandleTestRunStatsChange (TestRunChangedEventArgs testRunChangedArgs)
			{
				if (testRunChangedArgs != null)
					runJob.TestResultBuilder.OnTestRunChanged (testRunChangedArgs);
			}

			public void HandleTestRunComplete (
				TestRunCompleteEventArgs testRunCompleteArgs,
				TestRunChangedEventArgs lastChunkArgs,
				ICollection<AttachmentSet> runContextAttachments,
				ICollection<string> executorUris)
			{
				if (testRunCompleteArgs.Error != null)
					runJob.TestContext.Monitor.ReportRuntimeError (testRunCompleteArgs.Error.Message, testRunCompleteArgs.Error);
				runJob.TestResultBuilder.OnTestRunComplete (new TestRunCompletePayload {
					TestRunCompleteArgs = testRunCompleteArgs,
					LastRunTests = lastChunkArgs,
					RunAttachments = runContextAttachments,
					ExecutorUris = executorUris
				});
			}

			public int LaunchProcessWithDebuggerAttached (TestProcessStartInfo testProcessStartInfo)
			{
				return adapter.LaunchTestHost (testProcessStartInfo, runJob);
			}

			public void HandleLogMessage (TestMessageLevel level, string message)
			{
				OnTestMessage (runJob, message);
			}

			public void HandleRawMessage (string rawMessage)
			{
			}
		}

		/// <summary>
		/// Starts the test host with the execution handler of the test context. Not a debug launcher: a debugging
		/// execution handler starts the test host under the debugger itself.
		/// </summary>
		class TestHostLauncher : ITestHostLauncher
		{
			readonly VsTestRunAdapter adapter;
			readonly RunOrDebugJob runJob;

			public TestHostLauncher (VsTestRunAdapter adapter, RunOrDebugJob runJob)
			{
				this.adapter = adapter;
				this.runJob = runJob;
			}

			public bool IsDebug => false;

			public int LaunchTestHost (TestProcessStartInfo defaultTestHostStartInfo)
			{
				return adapter.LaunchTestHost (defaultTestHostStartInfo, runJob);
			}

			public int LaunchTestHost (TestProcessStartInfo defaultTestHostStartInfo, CancellationToken cancellationToken)
			{
				return adapter.LaunchTestHost (defaultTestHostStartInfo, runJob);
			}
		}
	}
}
