//
// PackageOperationsEndToEndTests.cs
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using NuGet.Packaging;
using NuGet.Versioning;
using NUnit.Framework;

namespace MonoDevelop.PackageManagement.Tests
{
	/// <summary>
	/// FR-010: adds, updates, restores and removes a NuGet package in an SDK-style project (a copy of
	/// tests/linux-smoke) through the add-in's package actions, the NuGet 7 client and the SDK's MSBuild; and resolves
	/// an MSBuild project SDK with MSBuild's NuGetSdkResolver in the IDE process (ADR 0020). The packages come from a
	/// local folder feed created by the test: no network access.
	/// </summary>
	[TestFixture]
	public class PackageOperationsEndToEndTests
	{
		const string PackageId = "MonoDevelop.Tests.LocalFeedPackage";
		const string SdkPackageId = "MonoDevelop.Tests.LocalFeedSdk";

		string rootDirectory;
		Solution solution;
		DotNetProject project;
		MonoDevelopSolutionManager solutionManager;
		DotNetProjectProxy dotNetProject;

		[OneTimeSetUp]
		public void CreateSolutionAndFeed ()
		{
			rootDirectory = Path.Combine (Path.GetTempPath (), "md-nuget-e2e-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (rootDirectory);

			string smokeDirectory = FindLinuxSmokeDirectory ();
			foreach (string projectName in new[] { "Hello", "Greeter" }) {
				string target = Path.Combine (rootDirectory, projectName);
				Directory.CreateDirectory (target);
				foreach (string file in Directory.GetFiles (Path.Combine (smokeDirectory, projectName)))
					File.Copy (file, Path.Combine (target, Path.GetFileName (file)));
			}
			File.Copy (Path.Combine (smokeDirectory, "Smoke.sln"), Path.Combine (rootDirectory, "Smoke.sln"));

			string feedDirectory = Path.Combine (rootDirectory, "feed");
			Directory.CreateDirectory (feedDirectory);
			CreatePackage (feedDirectory, PackageId, "1.0.0", ("lib/netstandard2.0/_._", ""));
			CreatePackage (feedDirectory, PackageId, "2.0.0", ("lib/netstandard2.0/_._", ""));
			CreatePackage (feedDirectory, SdkPackageId, "1.0.0",
				("Sdk/Sdk.props", "<Project><PropertyGroup><UsingLocalFeedSdkProps>true</UsingLocalFeedSdkProps></PropertyGroup></Project>"),
				("Sdk/Sdk.targets", "<Project><PropertyGroup><UsingLocalFeedSdkTargets>true</UsingLocalFeedSdkTargets></PropertyGroup></Project>"));

			// Only the local feed (<clear/> drops the user's sources) and a private global packages folder
			// (RestorePackagesPath wins over NUGET_PACKAGES).
			File.WriteAllText (Path.Combine (rootDirectory, "NuGet.Config"),
				"<configuration>\n" +
				"  <packageSources>\n" +
				"    <clear />\n" +
				$"    <add key=\"local\" value=\"{feedDirectory}\" />\n" +
				"  </packageSources>\n" +
				"</configuration>\n");
			File.WriteAllText (Path.Combine (rootDirectory, "Directory.Build.props"),
				"<Project>\n" +
				"  <PropertyGroup>\n" +
				"    <RestorePackagesPath>$(MSBuildThisFileDirectory)global-packages</RestorePackagesPath>\n" +
				"    <NuGetAudit>false</NuGetAudit>\n" +
				"  </PropertyGroup>\n" +
				"</Project>\n");
		}

		// Loaded by the test, which runs on the main loop's synchronization context (MainSynchronizationContextAttribute).
		async Task LoadSolutionAsync ()
		{
			solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (
				new ProgressMonitor (),
				Path.Combine (rootDirectory, "Smoke.sln"));
			project = solution.GetAllProjects ().OfType<DotNetProject> ().Single (p => p.Name == "Hello");
			solutionManager = new MonoDevelopSolutionManager (solution);
			dotNetProject = new DotNetProjectProxy (project);
		}

		[OneTimeTearDown]
		public void DeleteSolution ()
		{
			solution?.Dispose ();
			if (rootDirectory != null && Directory.Exists (rootDirectory))
				Directory.Delete (rootDirectory, true);
		}

		[Test]
		[Timeout (300000)] // three restores and project re-evaluations through the MSBuild builder
		public async Task AddUpdateRestoreRemoveFromLocalFeedAsync ()
		{
			// NuGet's messages (restore errors, ...) go to the test output.
			var messages = new List<string> ();
			EventHandler<PackageOperationMessageLoggedEventArgs> logMessage = (sender, e) => {
				lock (messages)
					messages.Add (e.Message.ToString ());
			};
			PackageManagementServices.PackageManagementEvents.PackageOperationMessageLogged += logMessage;
			try {
				await AddUpdateRestoreRemoveAsync ();
			} finally {
				PackageManagementServices.PackageManagementEvents.PackageOperationMessageLogged -= logMessage;
				lock (messages)
					TestContext.Out.WriteLine (string.Join (Environment.NewLine, messages));
			}
		}

		async Task AddUpdateRestoreRemoveAsync ()
		{
			await LoadSolutionAsync ();
			Assert.That (solutionManager.GetNuGetProject (dotNetProject), Is.InstanceOf<DotNetCoreNuGetProject> ());

			// Add
			var install = new InstallNuGetPackageAction (
				solutionManager.CreateSourceRepositoryProvider ().GetRepositories (),
				solutionManager,
				dotNetProject,
				new NuGetProjectContext (solutionManager.Settings)) {
				PackageId = PackageId,
				Version = new NuGetVersion ("1.0.0"),
				LicensesMustBeAccepted = false,
				OpenReadmeFile = false
			};
			await Task.Run (() => install.Execute ());

			Assert.AreEqual ("1.0.0", GetPackageReferenceVersion ());
			Assert.AreEqual ("1.0.0", GetRestoredVersion ());
			Assert.IsTrue (File.Exists (Path.Combine (rootDirectory, "global-packages", PackageId.ToLowerInvariant (), "1.0.0", PackageId.ToLowerInvariant () + ".nuspec")));

			// Update (to the newest version of the feed)
			var update = new UpdateNuGetPackageAction (solutionManager, dotNetProject) {
				PackageId = PackageId
			};
			await Task.Run (() => update.Execute ());

			Assert.AreEqual ("2.0.0", GetPackageReferenceVersion ());
			Assert.AreEqual ("2.0.0", GetRestoredVersion ());

			// Restore (after the assets file and the extracted package are gone)
			File.Delete (GetAssetsFilePath ());
			Directory.Delete (Path.Combine (rootDirectory, "global-packages", PackageId.ToLowerInvariant (), "2.0.0"), true);
			// The action of Restore on the project's Packages node for PackageReference projects.
			var restore = new RestoreNuGetPackagesInNuGetIntegratedProject (
				project,
				(DotNetCoreNuGetProject)solutionManager.GetNuGetProject (dotNetProject),
				solutionManager);
			await Task.Run (() => restore.Execute ());

			Assert.AreEqual ("2.0.0", GetRestoredVersion ());
			Assert.IsTrue (Directory.Exists (Path.Combine (rootDirectory, "global-packages", PackageId.ToLowerInvariant (), "2.0.0")));

			// Remove
			var uninstall = new UninstallNuGetPackageAction (solutionManager, dotNetProject) {
				PackageId = PackageId
			};
			await Task.Run (() => uninstall.Execute ());

			Assert.IsNull (GetPackageReferenceVersion ());
			Assert.IsNull (GetRestoredVersion ());
		}

		[Test]
		public async Task MSBuildSdkFromLocalFeedResolvedInProcessAsync ()
		{
			string projectDirectory = Path.Combine (rootDirectory, "SdkProject");
			Directory.CreateDirectory (projectDirectory);
			string projectFile = Path.Combine (projectDirectory, "SdkProject.csproj");
			await File.WriteAllTextAsync (projectFile, $"<Project Sdk=\"{SdkPackageId}/1.0.0\" />\n");

			// NuGetSdkResolver extracts the SDK package to the global packages folder: keep it in the test's folder.
			string globalPackages = Environment.GetEnvironmentVariable ("NUGET_PACKAGES");
			Environment.SetEnvironmentVariable ("NUGET_PACKAGES", Path.Combine (rootDirectory, "sdk-packages"));
			try {
				using (var sdkProject = (Project)await Services.ProjectService.ReadSolutionItem (new ProgressMonitor (), projectFile)) {
					Assert.AreEqual ("true", sdkProject.MSBuildProject.EvaluatedProperties.GetValue ("UsingLocalFeedSdkProps"));
					Assert.AreEqual ("true", sdkProject.MSBuildProject.EvaluatedProperties.GetValue ("UsingLocalFeedSdkTargets"));
				}
			} finally {
				Environment.SetEnvironmentVariable ("NUGET_PACKAGES", globalPackages);
			}
			Assert.IsTrue (Directory.Exists (Path.Combine (rootDirectory, "sdk-packages", SdkPackageId.ToLowerInvariant (), "1.0.0")));
		}

		string GetPackageReferenceVersion ()
		{
			var document = XDocument.Load (project.FileName);
			return document.Descendants ("PackageReference")
				.Where (item => (string)item.Attribute ("Include") == PackageId)
				.Select (item => (string)item.Attribute ("Version"))
				.SingleOrDefault ();
		}

		string GetAssetsFilePath ()
		{
			return Path.Combine (project.BaseDirectory, "obj", "project.assets.json");
		}

		string GetRestoredVersion ()
		{
			var lockFile = new NuGet.ProjectModel.LockFileFormat ().Read (GetAssetsFilePath ());
			return lockFile.Libraries
				.Where (library => library.Name == PackageId)
				.Select (library => library.Version.ToNormalizedString ())
				.SingleOrDefault ();
		}

		static void CreatePackage (string feedDirectory, string id, string version, params (string path, string content)[] files)
		{
			var builder = new PackageBuilder {
				Id = id,
				Version = NuGetVersion.Parse (version),
				Description = "Package for the MonoDevelop NuGet add-in tests."
			};
			builder.Authors.Add ("MonoDevelop");
			foreach (var (path, content) in files) {
				builder.Files.Add (new PhysicalPackageFile (new MemoryStream (System.Text.Encoding.UTF8.GetBytes (content))) {
					TargetPath = path
				});
			}
			using (var stream = File.Create (Path.Combine (feedDirectory, $"{id}.{version}.nupkg")))
				builder.Save (stream);
		}

		static string FindLinuxSmokeDirectory ()
		{
			var directory = new DirectoryInfo (AppContext.BaseDirectory);
			while (directory != null) {
				string candidate = Path.Combine (directory.FullName, "tests", "linux-smoke");
				if (File.Exists (Path.Combine (candidate, "Smoke.sln")))
					return candidate;
				directory = directory.Parent;
			}
			throw new DirectoryNotFoundException ("tests/linux-smoke not found above " + AppContext.BaseDirectory);
		}
	}
}
