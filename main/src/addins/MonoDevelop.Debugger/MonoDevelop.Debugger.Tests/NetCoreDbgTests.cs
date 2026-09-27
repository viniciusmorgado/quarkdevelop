//
// NetCoreDbgTests.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugging.Client;
using MonoDevelop.Core.Execution;
using MonoDevelop.Debugger.NetCoreDbg;
using NUnit.Framework;

namespace MonoDevelop.Debugger.Tests
{
	/// <summary>
	/// Debugging a .NET 10 console program with netcoredbg over the Debug Adapter Protocol (ADR 0016):
	/// breakpoint, locals, step over, continue and exit code, and an unhandled exception. The fixture program is
	/// built once in a temporary directory (outside the repository, so main/Directory.Build.props does not apply).
	/// </summary>
	[TestFixture]
	public class NetCoreDbgTests
	{
		static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds (30);

		static readonly string[] BuildArguments = { "build", "-nologo", "-v:q", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-o", "bin" };
		static readonly string[] ParsedArguments = { "one", "two words" };
		static readonly string[] ParsedLaunchArguments = { "a", "b c" };

		static readonly string[] FixtureSource = {
			"using System;",
			"",
			"static class Program",
			"{",
			"	static int Main (string [] args)",
			"	{",
			"		if (args.Length > 0 && args [0] == \"throw\")",
			"			throw new InvalidOperationException (\"netcoredbg test exception\"); // THROW",
			"		int first = 20;",
			"		int second = 22;",
			"		int sum = first + second; // BREAK",
			"		Console.WriteLine (\"sum=\" + sum); // STEP",
			"		return sum - 39;",
			"	}",
			"}",
		};

		string fixtureDirectory;
		string fixtureSourceFile;
		string fixtureProgram;
		string debugAdapter;

		static int LineOf (string marker) => Array.FindIndex (FixtureSource, line => line.EndsWith ("// " + marker, StringComparison.Ordinal)) + 1;

		// ConfigureAwait (false): the set-up thread has GTK's synchronization context (Gtk.Application.Init), whose loop
		// does not run while NUnit waits for the set-up.
		[OneTimeSetUp]
		public async Task BuildFixtureAsync ()
		{
			debugAdapter = NetCoreDbgLocator.FindDebugAdapter ();
			Assert.IsNotNull (debugAdapter, "netcoredbg is not on PATH (dotnet scripts/setup.cs installs it in ~/.local/bin)");

			fixtureDirectory = Path.Combine (Path.GetTempPath (), "md-netcoredbg-tests-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (fixtureDirectory);
			fixtureSourceFile = Path.Combine (fixtureDirectory, "Program.cs");
			await File.WriteAllLinesAsync (fixtureSourceFile, FixtureSource).ConfigureAwait (false);
			await File.WriteAllTextAsync (Path.Combine (fixtureDirectory, "NetCoreDbgFixture.csproj"),
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
				"  <PropertyGroup>\n" +
				"    <OutputType>Exe</OutputType>\n" +
				"    <TargetFramework>net10.0</TargetFramework>\n" +
				"    <UseAppHost>false</UseAppHost>\n" +
				"    <ImplicitUsings>disable</ImplicitUsings>\n" +
				"  </PropertyGroup>\n" +
				"</Project>\n").ConfigureAwait (false);

			var build = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
				WorkingDirectory = fixtureDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			foreach (var argument in BuildArguments)
				build.ArgumentList.Add (argument);
			RemoveTestHostEnvironment (build.Environment);
			using (var process = Process.Start (build))
			using (var timeout = new CancellationTokenSource (TimeSpan.FromMinutes (2))) {
				var stdout = process.StandardOutput.ReadToEndAsync ();
				var stderr = process.StandardError.ReadToEndAsync ();
				await process.WaitForExitAsync (timeout.Token).ConfigureAwait (false);
				Assert.AreEqual (0, process.ExitCode, "fixture build failed:\n" + await stdout.ConfigureAwait (false) + await stderr.ConfigureAwait (false));
			}
			fixtureProgram = Path.Combine (fixtureDirectory, "bin", "NetCoreDbgFixture.dll");
			FileAssert.Exists (fixtureProgram);
		}

		[OneTimeTearDown]
		public void DeleteFixture ()
		{
			if (fixtureDirectory != null && Directory.Exists (fixtureDirectory))
				Directory.Delete (fixtureDirectory, true);
		}

		/// <summary>
		/// The test host environment must not leak into the fixture build and the debuggee: the MSBuild locator's
		/// startup hook and the MSBuild variables of the test run.
		/// </summary>
		static void RemoveTestHostEnvironment (IDictionary<string, string> environment)
		{
			foreach (var name in environment.Keys.ToList ()) {
				if (name == "DOTNET_STARTUP_HOOKS" || name.StartsWith ("MSBUILD", StringComparison.OrdinalIgnoreCase))
					environment.Remove (name);
			}
		}

		[Test]
		public void EngineDebugsDotnetProgramCommands ()
		{
			var engine = new NetCoreDbgEngine ();
			var environment = new Dictionary<string, string> { ["GREETING"] = "hello" };
			var command = new ProcessExecutionCommand ("/usr/share/dotnet/dotnet", "\"/tmp/my app/app.dll\" one \"two words\"", "/tmp/work", environment);

			Assert.IsTrue (engine.CanDebugCommand (command));
			Assert.IsTrue (engine.IsDefaultDebugger (command));
			var startInfo = engine.CreateDebuggerStartInfo (command);
			Assert.AreEqual ("/tmp/my app/app.dll", startInfo.Command);
			CollectionAssert.AreEqual (ParsedArguments, ProcessArgumentBuilder.Parse (startInfo.Arguments));
			Assert.AreEqual ("/tmp/work", startInfo.WorkingDirectory);
			Assert.AreEqual ("hello", startInfo.EnvironmentVariables["GREETING"]);

			Assert.IsTrue (engine.CanDebugCommand (new ProcessExecutionCommand ("/tmp/app.dll", "")));
			Assert.IsFalse (engine.CanDebugCommand (new ProcessExecutionCommand ("/usr/bin/python3", "script.py")));
			Assert.IsFalse (engine.CanDebugCommand (new ProcessExecutionCommand ("/usr/share/dotnet/dotnet", "--info")));
			Assert.IsFalse (engine.CanDebugCommand (new NativeExecutionCommand ("/bin/true")));
		}

		[Test]
		public void EngineIsListedByDebuggingService ()
		{
			var engine = DebuggingService.GetDebuggerEngines ().SingleOrDefault (e => e.Id == "MonoDevelop.Debugger.NetCoreDbg");
			Assert.IsNotNull (engine, "engines: " + string.Join (", ", DebuggingService.GetDebuggerEngines ().Select (e => e.Id)));
			Assert.AreEqual (".NET Debugger (netcoredbg)", engine.Name);

			var command = new ProcessExecutionCommand ("dotnet", "\"" + fixtureProgram + "\"");
			Assert.IsTrue (DebuggingService.CanDebugCommand (command));
			var features = DebuggingService.GetSupportedFeaturesForCommand (command);
			Assert.AreEqual (DebuggerFeatures.Breakpoints | DebuggerFeatures.Stepping, features & (DebuggerFeatures.Breakpoints | DebuggerFeatures.Stepping));
			Assert.IsInstanceOf<NetCoreDbgSession> (engine.CreateSession ());
		}

		[Test]
		public void LocatorFindsTheAdapterOnPath ()
		{
			var directory = Path.Combine (fixtureDirectory, "path");
			Directory.CreateDirectory (directory);
			var adapter = Path.Combine (directory, "netcoredbg");
			File.WriteAllText (adapter, "");

			Assert.AreEqual (adapter, NetCoreDbgLocator.FindInPath ("netcoredbg", "/nonexistent" + Path.PathSeparator + directory));
			Assert.IsNull (NetCoreDbgLocator.FindInPath ("netcoredbg", "/nonexistent"));
			Assert.IsNull (NetCoreDbgLocator.FindInPath ("netcoredbg", null));
		}

		[Test]
		public void SessionLaunchProperties ()
		{
			var startInfo = new DebuggerStartInfo { Command = "/tmp/app.dll", Arguments = "a \"b c\"" };
			startInfo.EnvironmentVariables["X"] = "1";
			var properties = NetCoreDbgSession.CreateLaunchProperties (startInfo, true);

			Assert.AreEqual ("/tmp/app.dll", (string)properties["program"]);
			CollectionAssert.AreEqual (ParsedLaunchArguments, properties["args"].Select (a => (string)a));
			Assert.AreEqual ("/tmp", (string)properties["cwd"]);
			Assert.AreEqual ("1", (string)properties["env"]["X"]);
			Assert.IsTrue ((bool)properties["justMyCode"]);
		}

		[Test]
		public void SessionBreakpointLocalsStepOverAndExitCode ()
		{
			using (var run = new DebugRun (debugAdapter)) {
				var breakpoint = run.Breakpoints.Add (fixtureSourceFile, LineOf ("BREAK"));
				run.Start (fixtureProgram, fixtureDirectory);

				var hit = run.WaitFor (TargetEventType.TargetHitBreakpoint);
				Assert.AreSame (breakpoint, hit.BreakEvent);
				var frame = hit.Backtrace.GetFrame (0);
				Assert.AreEqual (fixtureSourceFile, frame.SourceLocation.FileName);
				Assert.AreEqual (LineOf ("BREAK"), frame.SourceLocation.Line);
				var locals = ReadLocals (frame);
				Assert.AreEqual ("20", locals["first"]);
				Assert.AreEqual ("22", locals["second"]);
				Assert.AreEqual ("0", locals["sum"]);

				run.Session.NextLine ();
				var step = run.WaitFor (TargetEventType.TargetStopped);
				frame = step.Backtrace.GetFrame (0);
				Assert.AreEqual (LineOf ("STEP"), frame.SourceLocation.Line);
				Assert.AreEqual ("42", ReadLocals (frame)["sum"]);

				run.Session.Continue ();
				var exited = run.WaitFor (e => e.Type == TargetEventType.TargetExited && e.ExitCode.HasValue, "exit with an exit code");
				Assert.AreEqual (3, exited.ExitCode);
				StringAssert.Contains ("sum=42", run.Output);
				CollectionAssert.IsEmpty (run.Errors);
			}
		}

		[Test]
		public void SessionStopsOnUnhandledException ()
		{
			using (var run = new DebugRun (debugAdapter)) {
				run.Start (fixtureProgram, fixtureDirectory, "throw");

				var stop = run.WaitFor (TargetEventType.UnhandledException);
				var frame = stop.Backtrace.GetFrame (0);
				Assert.AreEqual (LineOf ("THROW"), frame.SourceLocation.Line);

				run.Session.Stop ();
				CollectionAssert.IsEmpty (run.Errors);
			}
		}

		static Dictionary<string, string> ReadLocals (Mono.Debugging.Client.StackFrame frame)
		{
			var locals = frame.GetLocalVariables ();
			foreach (var value in locals) {
				if (value.IsEvaluating)
					value.WaitHandle.WaitOne (EventTimeout);
			}
			return locals.ToDictionary (value => value.Name, value => value.Value);
		}

		/// <summary>A netcoredbg session that records its target events, output and internal errors.</summary>
		sealed class DebugRun : IDisposable
		{
			readonly BlockingCollection<TargetEventArgs> events = new BlockingCollection<TargetEventArgs> ();
			readonly StringBuilder output = new StringBuilder ();

			public DebugRun (string debugAdapter)
			{
				Session = new NetCoreDbgSession (debugAdapter) {
					Breakpoints = Breakpoints,
					OutputWriter = (isStderr, text) => {
						lock (output)
							output.Append (text);
					},
					ExceptionHandler = ex => {
						Errors.Add (ex);
						return true;
					}
				};
				Session.TargetEvent += (sender, e) => events.Add (e);
			}

			public NetCoreDbgSession Session { get; }
			public BreakpointStore Breakpoints { get; } = new BreakpointStore ();
			public ConcurrentBag<Exception> Errors { get; } = new ConcurrentBag<Exception> ();

			public string Output {
				get {
					lock (output)
						return output.ToString ();
				}
			}

			public void Start (string program, string workingDirectory, string arguments = "")
			{
				var startInfo = new DebuggerStartInfo {
					Command = program,
					Arguments = arguments,
					WorkingDirectory = workingDirectory
				};
				// The debuggee inherits netcoredbg's environment, which is the test host's.
				startInfo.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = "";
				Session.Run (startInfo, new DebuggerSessionOptions {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
					ProjectAssembliesOnly = true
				});
			}

			public TargetEventArgs WaitFor (TargetEventType type)
			{
				return WaitFor (e => e.Type == type, type.ToString ());
			}

			public TargetEventArgs WaitFor (Func<TargetEventArgs, bool> match, string description)
			{
				var seen = new List<string> ();
				var watch = Stopwatch.StartNew ();
				while (watch.Elapsed < EventTimeout) {
					if (!events.TryTake (out var e, EventTimeout - watch.Elapsed))
						break;
					if (match (e))
						return e;
					seen.Add (e.Type.ToString ());
				}
				Assert.Fail ($"timed out waiting for {description}; events: {string.Join (", ", seen)}; errors: {string.Join ("; ", Errors)}; output: {Output}");
				return null;
			}

			public void Dispose ()
			{
				Session.Dispose ();
			}
		}
	}
}
