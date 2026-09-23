//
// BuildCancellationTests.cs
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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T061 (ADR 0008): the out-of-process builder has no Thread.Abort on .NET; cancelling a build
	/// goes through BuildManager.CancelAllSubmissions. A build stuck in a long task must stop soon
	/// after cancellation, and the caller must not stay blocked.
	/// </summary>
	[TestFixture]
	[Category ("Integration")]
	public class BuildCancellationTests : TestBase
	{
		const string SlowProject = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <Target Name=""Slow"" BeforeTargets=""CoreCompile"">
    <Exec Command=""sleep 300"" />
  </Target>
</Project>";

		string workDir;

		[SetUp]
		public void CreateProject ()
		{
			workDir = Path.Combine (Path.GetTempPath (), "md-cancel-" + Guid.NewGuid ());
			Directory.CreateDirectory (workDir);
			File.WriteAllText (Path.Combine (workDir, "Slow.csproj"), SlowProject);
			File.WriteAllText (Path.Combine (workDir, "Class1.cs"), "public class Class1 { }\n");
		}

		[TearDown]
		public void DeleteProject ()
		{
			if (workDir != null && Directory.Exists (workDir))
				Directory.Delete (workDir, true);
		}

		[Test]
		[Platform ("Linux")]
		public async Task CancellingABuildStopsIt ()
		{
			var file = Path.Combine (workDir, "Slow.csproj");
			Util.RunMSBuild ($"/t:Restore \"{file}\"");
			using (var project = (DotNetProject)await Services.ProjectService.ReadSolutionItem (Util.GetMonitor (), file))
			using (var cts = new CancellationTokenSource ()) {
				var monitor = new ProgressMonitor (cts);
				var watch = Stopwatch.StartNew ();
				var build = project.Build (monitor, project.Configurations [0].Selector);

				// Give the builder time to start and reach the Exec task, then cancel.
				await Task.Delay (TimeSpan.FromSeconds (15));
				Assert.IsFalse (build.IsCompleted, "the build should still be running the 300 s task");
				cts.Cancel ();

				var finished = await Task.WhenAny (build, Task.Delay (TimeSpan.FromSeconds (60)));
				Assert.AreSame (build, finished, "the build did not stop within 60 s of the cancellation");
				Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (120));

				await build;
				// The Exec task runs before CoreCompile: a cancelled build never produces the assembly.
				Assert.IsFalse (File.Exists (Path.Combine (workDir, "bin", "Debug", "net10.0", "Slow.dll")));
				Assert.IsFalse (File.Exists (Path.Combine (workDir, "obj", "Debug", "net10.0", "Slow.dll")));
			}
		}
	}
}
