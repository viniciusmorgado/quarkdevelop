//
// SdkEvaluationTests.cs
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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Projects.MSBuild;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T063 (risk R4): the IDE's own MSBuild evaluator (DefaultMSBuildEngine) must agree with the
	/// .NET SDK's MSBuild on SDK-style net10.0 projects: key properties and the Compile items.
	/// </summary>
	[TestFixture]
	[Category ("Integration")]
	public class SdkEvaluationTests : TestBase
	{
		static readonly string [] ComparedProperties = {
			"TargetFramework", "TargetFrameworkIdentifier", "TargetFrameworkVersion", "TargetFrameworkMoniker",
			"OutputType", "AssemblyName", "RootNamespace", "Configuration", "Platform", "LangVersion",
			"Nullable", "ImplicitUsings", "UsingMicrosoftNETSdk",
		};

		// Reserved properties are not listed among a project's evaluated properties; the probe project copies
		// them into its own properties (MD_<name>) so both evaluators can be compared.
		static readonly string [] ReservedProperties = {
			"MSBuildVersion", "MSBuildAssemblyVersion", "VisualStudioVersion", "MSBuildRuntimeType", "MSBuildToolsVersion",
		};

		string workDir;

		[SetUp]
		public void CopySmokeSolution ()
		{
			workDir = Path.Combine (Path.GetTempPath (), "md-evaluation-" + Guid.NewGuid ());
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
		public void MSBuildPropertyFunctionsMissingFromTheEvaluatorComeFromMSBuild ()
		{
			var functions = MSBuildEvaluationContext.LoadMSBuildIntrinsicFunctions ();
			foreach (var name in new [] { "GetTargetFrameworkIdentifier", "IsTargetFrameworkCompatible", "VersionGreaterThanOrEquals", "AreFeaturesEnabled", "StableStringHash" })
				Assert.IsTrue (functions.ContainsKey (name), name);
		}

		[Test]
		public void ReservedPropertiesMatchDotNetMSBuild ()
		{
			var file = Path.Combine (workDir, "Probe.proj");
			File.WriteAllText (file, "<Project>\n  <PropertyGroup>\n" +
				string.Concat (ReservedProperties.Select (n => $"    <MD_{n}>$({n})</MD_{n}>\n")) +
				"  </PropertyGroup>\n  <Target Name=\"Build\" />\n</Project>\n");
			var expected = QueryDotNetMSBuild (file, ReservedProperties.Select (n => "MD_" + n), items: false)
				.RootElement.GetProperty ("Properties");

			var project = new MSBuildProject ();
			project.Load (file);
			project.Evaluate ();
			foreach (var name in ReservedProperties)
				Assert.AreEqual (expected.GetProperty ("MD_" + name).GetString (), project.EvaluatedProperties.GetValue ("MD_" + name) ?? "", name);
			project.Dispose ();
		}

		[TestCase ("Hello/Hello.csproj")]
		[TestCase ("Greeter/Greeter.csproj")]
		public async Task EvaluationMatchesDotNetMSBuild (string relativePath)
		{
			var file = Path.Combine (workDir, relativePath);
			Util.RunMSBuild ($"/t:Restore \"{file}\"");
			var expected = QueryDotNetMSBuild (file, ComparedProperties, items: true);

			using (var project = (DotNetProject)await Services.ProjectService.ReadSolutionItem (Util.GetMonitor (), file)) {
				var evaluated = project.MSBuildProject.EvaluatedProperties;
				var props = expected.RootElement.GetProperty ("Properties");
				foreach (var name in ComparedProperties)
					Assert.AreEqual (props.GetProperty (name).GetString (), evaluated.GetValue (name) ?? "", name);

				var expectedCompile = expected.RootElement.GetProperty ("Items").GetProperty ("Compile").EnumerateArray ()
					.Select (i => (string)new FilePath (i.GetProperty ("FullPath").GetString ()).FullPath)
					.Where (p => !p.Contains ("/obj/")) // generated files exist only after a build
					.OrderBy (p => p, StringComparer.Ordinal).ToList ();
				var actualCompile = (await project.GetSourceFilesAsync (project.Configurations [0].Selector))
					.Where (f => f.BuildAction == BuildAction.Compile)
					.Select (f => (string)f.FilePath.FullPath)
					.Where (p => !p.Contains ("/obj/"))
					.OrderBy (p => p, StringComparer.Ordinal).ToList ();
				CollectionAssert.AreEqual (expectedCompile, actualCompile);
			}
		}

		static JsonDocument QueryDotNetMSBuild (string projectFile, System.Collections.Generic.IEnumerable<string> properties, bool items)
		{
			var psi = new ProcessStartInfo (DotNetCoreSdkInfo.GetDotNetHostPath ()) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			psi.ArgumentList.Add ("msbuild");
			psi.ArgumentList.Add (projectFile);
			psi.ArgumentList.Add ("-nodeReuse:false");
			if (items)
				psi.ArgumentList.Add ("-getItem:Compile");
			foreach (var name in properties)
				psi.ArgumentList.Add ("-getProperty:" + name);
			using (var p = Process.Start (psi)) {
				var output = p.StandardOutput.ReadToEndAsync ();
				var error = p.StandardError.ReadToEndAsync ();
				Assert.IsTrue (p.WaitForExit (120000), "dotnet msbuild timed out");
				Assert.AreEqual (0, p.ExitCode, output.Result + error.Result);
				return JsonDocument.Parse (output.Result);
			}
		}
	}
}
