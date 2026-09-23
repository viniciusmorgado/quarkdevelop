//
// SdkRoundTripTests.cs
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
using System.Threading.Tasks;
using MonoDevelop.Core;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T051 (constitution VI, compatibility): loading and saving an SDK-style net10.0 solution
	/// leaves the .sln and .csproj files byte-for-byte unchanged.
	/// </summary>
	[TestFixture]
	public class SdkRoundTripTests : TestBase
	{
		string workDir;

		[SetUp]
		public void CopySmokeSolution ()
		{
			workDir = Path.Combine (Path.GetTempPath (), "md-roundtrip-" + Guid.NewGuid ());
			var source = Path.Combine (Util.TestsRootDir, "linux-smoke");
			foreach (var file in Directory.GetFiles (source, "*", SearchOption.AllDirectories)) {
				var relative = Path.GetRelativePath (source, file);
				var parts = relative.Split (Path.DirectorySeparatorChar);
				if (parts.Contains ("bin") || parts.Contains ("obj"))
					continue;
				var target = Path.Combine (workDir, relative);
				Directory.CreateDirectory (Path.GetDirectoryName (target));
				File.Copy (file, target);
			}
		}

		[TearDown]
		public void DeleteCopy ()
		{
			if (workDir != null && Directory.Exists (workDir))
				Directory.Delete (workDir, true);
		}

		[Test]
		public async Task SolutionAndSdkProjectsRoundTripUnchanged ()
		{
			var slnFile = Path.Combine (workDir, "Smoke.sln");
			var projectFiles = Directory.GetFiles (workDir, "*.csproj", SearchOption.AllDirectories);
			Assert.That (projectFiles.Length, Is.GreaterThanOrEqualTo (3));
			var before = projectFiles.Append (slnFile).ToDictionary (f => f, File.ReadAllText);

			using (var sol = (Solution)await Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), slnFile)) {
				var projects = sol.GetAllProjects ().ToList ();
				Assert.That (projects.Select (p => p.Name), Is.SupersetOf (new[] { "Hello", "Greeter" }));
				Assert.IsTrue (projects.OfType<DotNetProject> ().All (p => p.MSBuildProject.GetReferencedSDKs ().Contains ("Microsoft.NET.Sdk")),
					"SDK-style projects are recognised");

				// Save twice: the first save may normalise, the second must be stable too.
				await sol.SaveAsync (Util.GetMonitor ());
				foreach (var p in projects)
					await p.SaveAsync (Util.GetMonitor ());
				await sol.SaveAsync (Util.GetMonitor ());
			}

			foreach (var entry in before)
				Assert.AreEqual (entry.Value, File.ReadAllText (entry.Key), Path.GetRelativePath (workDir, entry.Key) + " changed after load/save");
		}
	}
}
