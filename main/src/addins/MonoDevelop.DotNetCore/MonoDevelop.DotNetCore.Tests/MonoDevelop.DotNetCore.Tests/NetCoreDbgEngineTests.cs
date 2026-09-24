//
// NetCoreDbgEngineTests.cs
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

using System.Linq;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Debugger;
using MonoDevelop.Projects;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.DotNetCore.Tests
{
	/// <summary>
	/// The execution command of a .NET SDK console project is debugged with netcoredbg (ADR 0016, T112): the engine of
	/// the MonoDevelop.Debugger.NetCoreDbg add-in is the one DebuggingService picks for it.
	/// </summary>
	[TestFixture]
	sealed class NetCoreDbgEngineTests : DotNetCoreTestBase
	{
		[Test]
		public async Task SdkConsoleProjectIsDebuggedWithNetCoreDbgAsync ()
		{
			Mono.Addins.AddinManager.LoadAddin (null, "MonoDevelop.Debugger.NetCoreDbg");
			string solutionFileName = Util.GetSampleProject ("dotnetcore-console", "dotnetcore-sdk-console.sln");
			using (var solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName)) {
				var project = (DotNetProject)solution.GetAllProjects ().Single ();
				var configuration = (DotNetProjectConfiguration)project.Configurations[0];
				var runConfiguration = (ProjectRunConfiguration)project.RunConfigurations.First ();

				var command = project.CreateExecutionCommand (configuration.Selector, configuration, runConfiguration);

				Assert.IsInstanceOf<DotNetCoreExecutionCommand> (command);
				Assert.IsTrue (DebuggingService.CanDebugCommand (command));
				var engine = DebuggingService.GetDebuggerEngines ().Single (e => e.CanDebugCommand (command));
				Assert.AreEqual ("MonoDevelop.Debugger.NetCoreDbg", engine.Id);
				var startInfo = engine.CreateDebuggerStartInfo (command);
				Assert.AreEqual ("dotnetcore-sdk-console.dll", System.IO.Path.GetFileName (startInfo.Command));
			}
		}
	}
}
