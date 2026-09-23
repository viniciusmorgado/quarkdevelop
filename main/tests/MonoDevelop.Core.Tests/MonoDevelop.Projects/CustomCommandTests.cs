//
// CustomCommandTests.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Serialization;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T056: <see cref="CustomCommand"/> parsing, tag expansion, cloning, serialization and execution of
	/// real processes.
	/// </summary>
	[TestFixture]
	public class CustomCommandTests : TestBase
	{
		sealed class RecordingMonitor : ProgressMonitor
		{
			readonly StringBuilder log = new StringBuilder ();

			public string Text {
				get {
					lock (log)
						return log.ToString ();
				}
			}

			protected override void OnWriteLog (string message)
			{
				lock (log)
					log.Append (message);
				base.OnWriteLog (message);
			}
		}

		string dir;
		Solution solution;

		[SetUp]
		public void CreateSolution ()
		{
			dir = Util.CreateTmpDir ("custom-command");
			solution = new Solution ();
			solution.FileName = Path.Combine (dir, "MySol.sln");
		}

		[TearDown]
		public void DisposeSolution ()
		{
			solution.Dispose ();
			Directory.Delete (dir, true);
		}

		[TestCase ("tool", "tool", "")]
		[TestCase ("tool  -a  -b ", "tool", "-a  -b")]
		[TestCase ("\"my tool\" --x y", "my tool", "--x y")]
		[TestCase ("\"unterminated arg", "\"unterminated arg", "")]
		[TestCase ("", "", "")]
		public void ParseCommand (string command, string expectedCommand, string expectedArgs)
		{
			var cmd = new CustomCommand { Command = command };
			cmd.ParseCommand (out var exe, out var args);
			Assert.AreEqual (expectedCommand, exe);
			Assert.AreEqual (expectedArgs, args);
		}

		[Test]
		public void CommandIsNeverNull ()
		{
			var cmd = new CustomCommand { Command = null };
			Assert.AreEqual ("", cmd.Command);
		}

		[TestCase (CustomCommandType.BeforeBuild, "Before Build")]
		[TestCase (CustomCommandType.Build, "Build")]
		[TestCase (CustomCommandType.AfterBuild, "After Build")]
		[TestCase (CustomCommandType.BeforeExecute, "Before Execute")]
		[TestCase (CustomCommandType.Execute, "Execute")]
		[TestCase (CustomCommandType.AfterExecute, "After Execute")]
		[TestCase (CustomCommandType.BeforeClean, "Before Clean")]
		[TestCase (CustomCommandType.Clean, "Clean")]
		[TestCase (CustomCommandType.AfterClean, "After Clean")]
		[TestCase (CustomCommandType.Custom, "Custom Command")]
		[TestCase ((CustomCommandType)999, "999")]
		public void TypeLabel (CustomCommandType type, string label)
		{
			Assert.AreEqual (label, new CustomCommand { Type = type }.TypeLabel);
		}

		static CustomCommand CreateSample ()
		{
			var cmd = new CustomCommand {
				Type = CustomCommandType.AfterBuild,
				Name = "sample",
				Command = "tool arg",
				WorkingDir = "sub",
				ExternalConsole = true,
				PauseExternalConsole = true
			};
			cmd.EnvironmentVariables["A"] = "1";
			return cmd;
		}

		[Test]
		public void CloneAndEquals ()
		{
			var cmd = CreateSample ();
			var clone = cmd.Clone ();
			Assert.AreNotSame (cmd, clone);
			Assert.IsTrue (cmd.Equals (clone));
			Assert.AreEqual ("sample", clone.Name);
			Assert.IsTrue (clone.ExternalConsole);
			Assert.IsTrue (clone.PauseExternalConsole);
			Assert.AreEqual ("1", clone.EnvironmentVariables["A"]);
			Assert.AreNotSame (cmd.EnvironmentVariables, clone.EnvironmentVariables);

			Action<CustomCommand>[] changes = {
				c => c.Command = "other",
				c => c.WorkingDir = "elsewhere",
				c => c.ExternalConsole = false,
				c => c.PauseExternalConsole = false,
				c => c.Type = CustomCommandType.Build,
				c => c.EnvironmentVariables ["B"] = "2",
				c => c.EnvironmentVariables ["A"] = "changed",
				c => { c.EnvironmentVariables.Remove ("A"); c.EnvironmentVariables ["Z"] = "1"; }
			};
			foreach (var change in changes) {
				var modified = cmd.Clone ();
				change (modified);
				Assert.IsFalse (cmd.Equals (modified));
			}
		}

		[Test]
		public void XmlSerializationRoundTrip ()
		{
			var cmd = CreateSample ();
			var serializer = new XmlDataSerializer (new DataContext ());
			var sw = new StringWriter ();
			serializer.Serialize (sw, cmd);
			var xml = sw.ToString ();
			StringAssert.Contains ("AfterBuild", xml);
			StringAssert.Contains ("EnvironmentVariables", xml);

			var back = serializer.Deserialize<CustomCommand> (new StringReader (xml));
			Assert.IsTrue (cmd.Equals (back));
			Assert.AreEqual ("sample", back.Name);
		}

		[Test]
		public void TagsAreExpanded ()
		{
			var cmd = new CustomCommand {
				Command = "\"${SolutionDir}/my tool\" --name ${SolutionName}",
				WorkingDir = "${SolutionName}-work"
			};
			var conf = ConfigurationSelector.Default;
			Assert.AreEqual (dir + "/my tool", cmd.GetCommandFile (solution, conf));
			Assert.AreEqual ("--name MySol", cmd.GetCommandArgs (solution, conf));
			Assert.AreEqual (Path.Combine (dir, "MySol-work"), cmd.GetCommandWorkingDir (solution, conf).ToString ());

			cmd.WorkingDir = null;
			Assert.AreEqual (solution.BaseDirectory, cmd.GetCommandWorkingDir (solution, conf));
		}

		[Test]
		public void CreateExecutionCommand ()
		{
			File.WriteAllText (Path.Combine (dir, "local-tool"), "");
			var cmd = new CustomCommand { Command = "local-tool first second", WorkingDir = "sub" };
			cmd.EnvironmentVariables["SOL"] = "${SolutionName}";

			var exec = cmd.CreateExecutionCommand (solution, ConfigurationSelector.Default);
			Assert.AreEqual (Path.Combine (dir, "local-tool"), exec.Command, "a tool in the base directory is used when it exists");
			Assert.AreEqual ("first second", exec.Arguments);
			Assert.AreEqual (Path.Combine (dir, "sub"), exec.WorkingDirectory);
			Assert.AreEqual ("MySol", exec.EnvironmentVariables["SOL"]);

			cmd = new CustomCommand { Command = "not-in-base-dir" };
			exec = cmd.CreateExecutionCommand (solution, ConfigurationSelector.Default);
			Assert.AreEqual ("not-in-base-dir", exec.Command);
			Assert.AreEqual (solution.BaseDirectory.ToString (), exec.WorkingDirectory);

			Assert.Throws<UserException> (() => new CustomCommand ().CreateExecutionCommand (solution, ConfigurationSelector.Default));
		}

		[Test]
		public void CanExecuteRequiresACommand ()
		{
			Assert.IsFalse (new CustomCommand ().CanExecute (solution, null, ConfigurationSelector.Default));
			Assert.IsTrue (new CustomCommand { Command = "true" }.CanExecute (solution, null, ConfigurationSelector.Default));
		}

		static async Task<bool> WithTimeout (Task<bool> execution)
		{
			var finished = await Task.WhenAny (execution, Task.Delay (TimeSpan.FromMinutes (1)));
			Assert.AreSame (execution, finished, "the custom command did not finish");
			return await execution;
		}

		[Test]
		public async Task ExecuteSucceeds ()
		{
			var cmd = new CustomCommand { Command = "sh -c \"echo custom-output\"" };
			using (var monitor = new RecordingMonitor ()) {
				Assert.IsTrue (await WithTimeout (cmd.Execute (monitor, solution, ConfigurationSelector.Default)));
				Assert.IsFalse (monitor.HasErrors);
				StringAssert.Contains ("Executing: sh -c \"echo custom-output\"", monitor.Text);
			}
		}

		[Test]
		public async Task ExecuteReportsExitCode ()
		{
			var cmd = new CustomCommand { Command = "sh -c \"exit 3\"" };
			using (var monitor = new RecordingMonitor ()) {
				Assert.IsFalse (await WithTimeout (cmd.Execute (monitor, solution, ConfigurationSelector.Default)));
				Assert.AreEqual ("Custom command failed (exit code: 3)", monitor.Errors.Single ().Message);
			}
		}

		[Test]
		public async Task ExecuteRequiresAnExistingWorkingDirectory ()
		{
			var cmd = new CustomCommand { Command = "true", WorkingDir = "does-not-exist" };
			using (var monitor = new RecordingMonitor ()) {
				Assert.IsFalse (await WithTimeout (cmd.Execute (monitor, solution, ConfigurationSelector.Default)));
				Assert.AreEqual ("Custom command working directory does not exist", monitor.Errors.Single ().Message);
			}
		}

		[Test]
		public async Task ExecuteReportsMissingPrograms ()
		{
			var cmd = new CustomCommand { Command = "md-no-such-program-" + Guid.NewGuid ().ToString ("N") };
			using (var monitor = new RecordingMonitor ()) {
				Assert.IsFalse (await WithTimeout (cmd.Execute (monitor, solution, ConfigurationSelector.Default)));
				StringAssert.StartsWith ("Failed to execute custom command", monitor.Errors.Single ().Message);
			}
		}
	}
}
