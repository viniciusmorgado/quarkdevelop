//
// VsTestEndToEndTests.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Execution;
using MonoDevelop.Projects;
using MonoDevelop.UnitTesting.VsTest;
using NUnit.Framework;

namespace MonoDevelop.UnitTesting.Tests
{
	/// <summary>
	/// FR-011: the add-in discovers and runs the tests of NUnit, xUnit and MSTest projects through VSTest. A copy of
	/// tests/unittesting-samples (one passing and one failing test per framework) is restored, loaded in the project
	/// model and built; the add-in's test providers create the test tree (UnitTestService.BuildTest), the VSTest
	/// adapter discovers the tests with the vstest.console of the .NET SDK and runs them with the test host started by
	/// the execution handler, as the IDE does. No network: the packages come from the global packages folder
	/// (PackageDownload items of this project) and the samples' NuGet.Config has no package source.
	/// </summary>
	[TestFixture]
	public class VsTestEndToEndTests
	{
		static readonly string[] SampleTestNames = { "AdditionFails", "AdditionPasses" };

		string rootDirectory;
		Solution solution;

		[OneTimeSetUp]
		public void CopySamples ()
		{
			rootDirectory = Path.Combine (Path.GetTempPath (), "md-vstest-e2e-" + Guid.NewGuid ().ToString ("N"));
			CopyDirectory (FindSamplesDirectory (), rootDirectory);
			File.WriteAllText (Path.Combine (rootDirectory, "NuGet.Config"),
				"<configuration>\n" +
				"  <packageSources>\n" +
				"    <clear />\n" +
				"  </packageSources>\n" +
				"</configuration>\n");
		}

		[OneTimeTearDown]
		public void DeleteSamples ()
		{
			solution?.Dispose ();
			if (rootDirectory != null && Directory.Exists (rootDirectory))
				Directory.Delete (rootDirectory, true);
		}

		[TestCase ("NUnitSample")]
		[TestCase ("XUnitSample")]
		[TestCase ("MSTestSample")]
		[Timeout (300000)] // the first test restores and builds the samples
		public async Task DiscoverAndRunTestsAsync (string projectName)
		{
			await EnsureSolutionBuiltAsync ();
			var project = solution.GetAllProjects ().Single (p => p.Name == projectName);

			string testAssembly = VsTestAdapter.GetAssemblyFileName (project);
			Assert.That (File.Exists (testAssembly), "test assembly " + testAssembly);

			// Discovery
			var projectTests = UnitTestService.BuildTest (project) as UnitTestGroup;
			Assert.That (projectTests, Is.InstanceOf<VsTestProjectTestSuite> (), "the VSTest provider of the add-in creates the tests of " + projectName);
			await projectTests.Refresh (CancellationToken.None);
			Assert.AreEqual (TestStatus.Ready, projectTests.Status, "test discovery of " + projectName);

			var tests = GetTestCases (projectTests).ToList ();
			CollectionAssert.AreEquivalent (SampleTestNames, tests.Select (t => t.Name));
			foreach (var test in tests) {
				Assert.AreEqual (projectName, test.FixtureTypeNamespace);
				Assert.AreEqual ("CalculatorTests", test.FixtureTypeName);
			}

			// Run all the tests of the project
			var monitor = new RecordingTestMonitor ();
			var result = await RunAsync (projectTests, monitor);
			Assert.IsEmpty (monitor.Errors, "runtime errors");
			Assert.AreEqual (1, result.Passed, "passed tests of " + projectName);
			Assert.AreEqual (1, result.Failures, "failed tests of " + projectName);

			var passing = tests.Single (t => t.Name == "AdditionPasses");
			var failing = tests.Single (t => t.Name == "AdditionFails");
			Assert.AreEqual (ResultStatus.Success, passing.GetLastResult ()?.Status);
			Assert.AreEqual (ResultStatus.Failure, failing.GetLastResult ()?.Status);
			Assert.That (failing.GetLastResult ().Message, Is.Not.Empty, "assertion message of the failing test");
			Assert.That (monitor.EndedTests, Is.SupersetOf (new[] { passing, failing }));

			// Run a single test
			monitor = new RecordingTestMonitor ();
			result = await RunAsync (failing, monitor);
			Assert.IsEmpty (monitor.Errors, "runtime errors");
			Assert.AreEqual (ResultStatus.Failure, result.Status, "result of " + failing.FixtureTypeName + "." + failing.Name);
			Assert.AreEqual (ResultStatus.Failure, failing.GetLastResult ()?.Status);
		}

		async Task EnsureSolutionBuiltAsync ()
		{
			if (solution != null)
				return;

			string solutionFile = Path.Combine (rootDirectory, "UnitTestingSamples.sln");
			// Restore like `mdtool build` (dotnet restore), from the global packages folder only. No build servers: a
			// lingering MSBuild node would keep the redirected output open.
			await RunDotNetAsync (rootDirectory, "restore", solutionFile, "-nologo", "--disable-build-servers");

			var monitor = new ProgressMonitor ();
			solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (monitor, solutionFile);
			var buildResult = await solution.Build (monitor, "Debug");
			Assert.AreEqual (0, buildResult.ErrorCount, string.Join (Environment.NewLine, buildResult.Errors.Select (e => e.ToString ())));
		}

		/// <summary>Runs the tests as the test session of the IDE does, with the default execution handler.</summary>
		static Task<UnitTestResult> RunAsync (UnitTest test, RecordingTestMonitor monitor)
		{
			var executionContext = new MonoDevelop.Projects.ExecutionContext (Runtime.ProcessService.DefaultExecutionHandler, new CapturingConsoleFactory (), null);
			var context = new TestContext (monitor, executionContext, DateTime.Now);
			return Task.Run (() => test.Run (context));
		}

		static IEnumerable<UnitTest> GetTestCases (UnitTest test)
		{
			if (test is UnitTestGroup group)
				return group.Tests.SelectMany (GetTestCases);
			return new[] { test };
		}

		static async Task RunDotNetAsync (string workingDirectory, params string[] arguments)
		{
			var startInfo = new ProcessStartInfo (DotNetCoreSdkInfo.GetDotNetHostPath ()) {
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			foreach (var argument in arguments)
				startInfo.ArgumentList.Add (argument);
			using var process = Process.Start (startInfo);
			var output = process.StandardOutput.ReadToEndAsync ();
			var error = process.StandardError.ReadToEndAsync ();
			await process.WaitForExitAsync ();
			Assert.AreEqual (0, process.ExitCode, "dotnet " + string.Join (" ", arguments) + Environment.NewLine + await output + await error);
		}

		static void CopyDirectory (string source, string target)
		{
			Directory.CreateDirectory (target);
			foreach (string file in Directory.GetFiles (source))
				File.Copy (file, Path.Combine (target, Path.GetFileName (file)));
			foreach (string directory in Directory.GetDirectories (source)) {
				string name = Path.GetFileName (directory);
				if (name != "bin" && name != "obj")
					CopyDirectory (directory, Path.Combine (target, name));
			}
		}

		static string FindSamplesDirectory ()
		{
			var directory = new DirectoryInfo (AppContext.BaseDirectory);
			while (directory != null) {
				string candidate = Path.Combine (directory.FullName, "tests", "unittesting-samples");
				if (File.Exists (Path.Combine (candidate, "UnitTestingSamples.sln")))
					return candidate;
				directory = directory.Parent;
			}
			throw new DirectoryNotFoundException ("tests/unittesting-samples not found above " + AppContext.BaseDirectory);
		}

		sealed class RecordingTestMonitor : ITestProgressMonitor
		{
			readonly List<UnitTest> endedTests = new List<UnitTest> ();
			readonly List<string> errors = new List<string> ();

			public CancellationToken CancellationToken => CancellationToken.None;

			public IReadOnlyList<UnitTest> EndedTests {
				get {
					lock (endedTests)
						return endedTests.ToList ();
				}
			}

			public IReadOnlyList<string> Errors {
				get {
					lock (errors)
						return errors.ToList ();
				}
			}

			public void BeginTest (UnitTest test)
			{
			}

			public void EndTest (UnitTest test, UnitTestResult result)
			{
				lock (endedTests)
					endedTests.Add (test);
			}

			public void ReportRuntimeError (string message, Exception exception)
			{
				lock (errors)
					errors.Add (message + ": " + exception);
			}

			public void WriteGlobalLog (string message)
			{
				NUnit.Framework.TestContext.Out.Write (message);
			}
		}

		/// <summary>The console of the test host: its output goes to the test output.</summary>
		sealed class CapturingConsoleFactory : OperationConsoleFactory
		{
			protected override OperationConsole OnCreateConsole (CreateConsoleOptions options)
			{
				return new CapturingConsole ();
			}
		}

		sealed class CapturingConsole : OperationConsole
		{
			readonly StringWriter output = new StringWriter ();
			readonly TextWriter writer;

			public CapturingConsole ()
			{
				writer = TextWriter.Synchronized (output);
			}

			public override TextReader In => TextReader.Null;

			public override TextWriter Out => writer;

			public override TextWriter Error => writer;

			public override TextWriter Log => writer;

			public override void Dispose ()
			{
				writer.Flush ();
				NUnit.Framework.TestContext.Out.Write (output.ToString ());
				base.Dispose ();
			}
		}
	}
}
