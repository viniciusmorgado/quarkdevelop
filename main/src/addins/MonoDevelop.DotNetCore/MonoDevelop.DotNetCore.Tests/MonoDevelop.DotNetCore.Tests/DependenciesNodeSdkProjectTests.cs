//
// DependenciesNodeSdkProjectTests.cs
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
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.DotNetCore.NodeBuilders;
using MonoDevelop.Ide.Tasks;
using MonoDevelop.Projects;
using NuGet.Packaging;
using NuGet.Versioning;
using NUnit.Framework;
using UnitTests;
using NuGetPackageDependency = NuGet.Packaging.Core.PackageDependency;

namespace MonoDevelop.DotNetCore.Tests
{
	/// <summary>
	/// The Dependencies folder of an SDK-style net10.0 project (T099): frameworks, packages with their dependencies and
	/// restore warnings, and project references. The packages come from a local folder feed created by the test (no
	/// network access); the project is restored with the installed SDK.
	/// </summary>
	[TestFixture]
	public class DependenciesNodeSdkProjectTests
	{
		const string PackageId = "MonoDevelop.Tests.DependencyPackage";
		const string TransitivePackageId = "MonoDevelop.Tests.TransitivePackage";

		string rootDirectory;
		Solution solution;
		DotNetProject project;

		[OneTimeSetUp]
		public void CreateSolution ()
		{
			rootDirectory = Path.Combine (Path.GetTempPath (), "md-dotnetcore-deps-" + Guid.NewGuid ().ToString ("N"));
			string feedDirectory = Path.Combine (rootDirectory, "feed");
			Directory.CreateDirectory (feedDirectory);

			// The package asks for TransitivePackage >= 0.9.0 but the feed has 1.0.0 only: restore warns with NU1603.
			CreatePackage (feedDirectory, TransitivePackageId, "1.0.0");
			CreatePackage (feedDirectory, PackageId, "1.0.0", new NuGetPackageDependency (TransitivePackageId, VersionRange.Parse ("0.9.0")));

			WriteFile ("NuGet.Config",
				"<configuration>\n" +
				"  <packageSources>\n" +
				"    <clear />\n" +
				$"    <add key=\"local\" value=\"{feedDirectory}\" />\n" +
				"  </packageSources>\n" +
				"</configuration>\n");
			WriteFile ("Directory.Build.props",
				"<Project>\n" +
				"  <PropertyGroup>\n" +
				"    <RestorePackagesPath>$(MSBuildThisFileDirectory)global-packages</RestorePackagesPath>\n" +
				"    <NuGetAudit>false</NuGetAudit>\n" +
				"  </PropertyGroup>\n" +
				"</Project>\n");
			WriteFile ("App/App.csproj",
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
				"  <PropertyGroup>\n" +
				"    <OutputType>Exe</OutputType>\n" +
				"    <TargetFramework>net10.0</TargetFramework>\n" +
				"  </PropertyGroup>\n" +
				"  <ItemGroup>\n" +
				$"    <PackageReference Include=\"{PackageId}\" Version=\"1.0.0\" />\n" +
				"    <ProjectReference Include=\"../Lib/Lib.csproj\" />\n" +
				"  </ItemGroup>\n" +
				"</Project>\n");
			WriteFile ("App/Program.cs", "class Program { static void Main () { } }\n");
			WriteFile ("Lib/Lib.csproj",
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
				"  <PropertyGroup>\n" +
				"    <TargetFramework>net10.0</TargetFramework>\n" +
				"  </PropertyGroup>\n" +
				"</Project>\n");
			WriteFile ("Dependencies.sln",
				"Microsoft Visual Studio Solution File, Format Version 12.00\n" +
				"Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"App\", \"App/App.csproj\", \"{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B001}\"\n" +
				"EndProject\n" +
				"Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Lib\", \"Lib/Lib.csproj\", \"{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B002}\"\n" +
				"EndProject\n" +
				"Global\n" +
				"\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\n" +
				"\t\tDebug|Any CPU = Debug|Any CPU\n" +
				"\tEndGlobalSection\n" +
				"\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n" +
				"\t\t{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B001}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n" +
				"\t\t{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B001}.Debug|Any CPU.Build.0 = Debug|Any CPU\n" +
				"\t\t{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n" +
				"\t\t{2B1C0F57-8D5F-4C63-9F57-3D1E83C8B002}.Debug|Any CPU.Build.0 = Debug|Any CPU\n" +
				"\tEndGlobalSection\n" +
				"EndGlobal\n");

			Restore (Path.Combine (rootDirectory, "App", "App.csproj"));
		}

		[OneTimeTearDown]
		public void DeleteSolution ()
		{
			solution?.Dispose ();
			if (rootDirectory != null && Directory.Exists (rootDirectory)) {
				Directory.Delete (rootDirectory, true);
			}
		}

		[Test]
		public async Task DependenciesFolderShowsFrameworkPackagesAndProjectsAsync ()
		{
			solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), Path.Combine (rootDirectory, "Dependencies.sln"));
			project = solution.GetAllProjects ().OfType<DotNetProject> ().SingleOrDefault (p => p.Name == "App");
			Assert.IsNotNull (project, DescribeItems ());
			Assert.AreEqual (2, solution.GetAllProjects ().OfType<DotNetProject> ().Count (), DescribeItems ());
			Assert.IsTrue (project.HasFlavor<DotNetCoreProjectExtension> ());

			var dependenciesNode = new DependenciesNode (project, new FakeUpdatedPackagesInWorkspace ());
			var packagesRead = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var frameworksRead = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			dependenciesNode.PackageDependencyCache.PackageDependenciesChanged += (sender, e) => packagesRead.TrySetResult (true);
			dependenciesNode.FrameworkReferencesCache.FrameworkReferencesChanged += (sender, e) => frameworksRead.TrySetResult (true);
			dependenciesNode.PackageDependencyCache.Refresh ();
			dependenciesNode.FrameworkReferencesCache.Refresh ();
			await WaitAsync (packagesRead.Task, "package dependencies");
			await WaitAsync (frameworksRead.Task, "framework references");

			var children = dependenciesNode.GetChildNodes ().ToList ();

			// Frameworks
			var frameworksFolder = children.OfType<FrameworkReferencesNode> ().Single ();
			var framework = frameworksFolder.GetChildNodes ().Single ();
			Assert.AreEqual ("Microsoft.NETCore.App", framework.GetLabel ());

			// Packages: the direct reference, its dependency and the restore warning about the dependency.
			var packagesFolder = children.OfType<PackageDependenciesNode> ().Single ();
			var package = packagesFolder.GetChildNodes ().Single ();
			Assert.AreEqual (PackageId, package.Name);
			Assert.AreEqual ("(1.0.0)", package.GetSecondaryLabel ());
			Assert.IsTrue (package.IsTopLevel);
			Assert.IsTrue (package.CanBeRemoved);
			Assert.IsTrue (package.HasDependencies ());

			var transitivePackage = package.GetDependencyNodes ().Single ();
			Assert.AreEqual (TransitivePackageId, transitivePackage.Name);
			Assert.AreEqual ("(1.0.0)", transitivePackage.GetSecondaryLabel ());
			Assert.IsFalse (transitivePackage.CanBeRemoved);
			Assert.AreEqual (TaskSeverity.Warning, transitivePackage.GetStatusSeverity ());

			var diagnostic = transitivePackage.GetDependencyNodes ().Single ();
			Assert.IsTrue (diagnostic.IsDiagnostic);
			Assert.AreEqual ("NU1603", diagnostic.Name);
			StringAssert.Contains (TransitivePackageId, diagnostic.GetStatusMessage ());

			// Projects
			var projectsFolder = children.OfType<ProjectDependenciesNode> ().Single ();
			var projectReference = projectsFolder.GetChildNodes ().Single ();
			Assert.AreEqual ("Lib", projectReference.ResolveProject (solution)?.Name);

			// No SDK folder: net10.0 frameworks are listed under Frameworks.
			Assert.IsFalse (children.OfType<SdkDependenciesNode> ().Any ());
		}

		// The tasks complete on the main loop thread (the caches' continuations), not in the test's context.
#pragma warning disable VSTHRD003
		string DescribeItems ()
		{
			return string.Join (", ", solution.GetAllItems<SolutionItem> ().Select (item =>
				item.GetType ().Name + " " + item.Name + " " + (item as UnknownSolutionItem)?.LoadError));
		}

		static async Task WaitAsync (Task task, string what)
		{
			if (await Task.WhenAny (task, Task.Delay (TimeSpan.FromMinutes (2))) != task) {
				Assert.Fail ("Timed out reading the " + what + ".");
			}
		}
#pragma warning restore VSTHRD003

		void WriteFile (string relativePath, string content)
		{
			string path = Path.Combine (rootDirectory, relativePath);
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			File.WriteAllText (path, content);
		}

		static void Restore (string projectFile)
		{
			var startInfo = new ProcessStartInfo (DotNetCoreSdkInfo.GetDotNetHostPath ()) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			startInfo.ArgumentList.Add ("restore");
			startInfo.ArgumentList.Add (projectFile);
			// The test host's MSBuild environment (MSBuildLocator) must not leak into the SDK's own MSBuild.
			startInfo.Environment.Remove ("MSBUILD_EXE_PATH");
			startInfo.Environment.Remove ("MSBuildExtensionsPath");
			startInfo.Environment.Remove ("MSBuildSDKsPath");
			using var process = Process.Start (startInfo);
			string output = process.StandardOutput.ReadToEnd () + process.StandardError.ReadToEnd ();
			Assert.IsTrue (process.WaitForExit (120000), "Timeout restoring NuGet packages.");
			Assert.AreEqual (0, process.ExitCode, output);
		}

		static void CreatePackage (string feedDirectory, string id, string version, params NuGetPackageDependency[] dependencies)
		{
			var builder = new PackageBuilder {
				Id = id,
				Version = NuGetVersion.Parse (version),
				Description = "Package for the MonoDevelop .NET Core add-in tests."
			};
			builder.Authors.Add ("MonoDevelop");
			builder.DependencyGroups.Add (new PackageDependencyGroup (NuGet.Frameworks.NuGetFramework.AnyFramework, dependencies));
			builder.Files.Add (new PhysicalPackageFile (new MemoryStream (Array.Empty<byte> ())) {
				TargetPath = "lib/netstandard2.0/_._"
			});
			using (var stream = File.Create (Path.Combine (feedDirectory, $"{id}.{version}.nupkg"))) {
				builder.Save (stream);
			}
		}
	}
}
