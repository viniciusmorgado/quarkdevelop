//
// VsTestAdapter.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2017 Xamarin Inc. (http://xamarin.com)
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.PackageManagement;
using MonoDevelop.Projects;

namespace MonoDevelop.UnitTesting.VsTest
{
	/// <summary>
	/// A vstest.console process in design mode, the one `dotnet test` uses: the vstest.console.dll of the .NET SDK
	/// whose MSBuild the IDE loaded, driven with the VSTest client (VsTestConsoleWrapper), which negotiates the protocol
	/// version with it. The legacy add-in hosted the design mode socket itself and ran the .NET Framework
	/// vstest.console.exe of the Microsoft.TestPlatform package with Mono (not on .NET).
	/// One request at a time per process (the protocol has no request ids): the discovery and the run adapters have
	/// their own process, so a long test run does not block test discovery. The requests use the synchronous API of
	/// the client on a thread pool thread (its asynchronous API is obsolete: "The async APIs don't work").
	/// </summary>
	abstract class VsTestAdapter : IDisposable
	{
		readonly SemaphoreSlim requestLock = new SemaphoreSlim (1, 1);
		IVsTestConsoleWrapper vsTestConsole;

		internal static string GetRunSettings (Project project)
		{
			// No TestAdaptersPaths: the test host loads the adapters the test project copies next to the test assembly
			// (NUnit3.TestAdapter, xunit.runner.visualstudio, MSTest.TestAdapter), like `dotnet test`.
			return "<RunSettings>" + new Microsoft.VisualStudio.TestPlatform.ObjectModel.RunConfiguration () {
				TargetFramework = Framework.FromString ((project as DotNetProject)?.TargetFramework?.Id?.ToString ()),
				DisableAppDomain = true,
				ResultsDirectory = project.BaseIntermediateOutputPath.Combine (Constants.ResultsDirectoryName),
				ShouldCollectSourceInformation = false,
				TestSessionTimeout = int.MaxValue
			}.ToXml ().OuterXml + "</RunSettings>";
		}

		/// <summary>
		/// Whether the project is a VSTest test project: SDK-style test projects set IsTestProject
		/// (Microsoft.NET.Test.Sdk, MSTest.Sdk); otherwise, like the legacy add-in, a restored package that contains a
		/// test adapter.
		/// </summary>
		public static bool IsTestProject (Project project)
		{
			if (project is DotNetProject && project.MSBuildProject?.EvaluatedProperties?.GetValue<bool> ("IsTestProject") == true)
				return true;
			return !string.IsNullOrEmpty (GetTestAdapters (project));
		}

		static ConditionalWeakTable<Project, Tuple<HashSet<string>, string>> projectTestAdapterListCache = new ConditionalWeakTable<Project, Tuple<HashSet<string>, string>> ();

		public static string GetTestAdapters (Project project)
		{
			var nugetsFolders = PackageManagementServices.ProjectOperations.GetInstalledPackages (project).Select (p => p.InstallPath);
			lock (projectTestAdapterListCache) {
				if (projectTestAdapterListCache.TryGetValue (project, out var cachePackages))
					if (cachePackages.Item1.SetEquals (nugetsFolders))
						return cachePackages.Item2;

				var result = string.Empty;
				bool cache = true;
				foreach (var folder in nugetsFolders) {
					if (string.IsNullOrEmpty (folder))
						continue;
					if (!Directory.Exists (folder)) {
						//NuGet gives us valid location of where package will be restored
						//so we may not cache invalid result until package has been actually restored
						cache = false;
						continue;
					}
					foreach (var path in Directory.GetFiles (folder, "*.TestAdapter.dll", SearchOption.AllDirectories))
						result += path + ";";
					foreach (var path in Directory.GetFiles (folder, "*.testadapter.dll", SearchOption.AllDirectories))
						if (!result.Contains (path))
							result += path + ";";
				}
				if (result.Length > 0)
					result = result.Remove (result.Length - 1);
				projectTestAdapterListCache.Remove (project);
				if (cache)
					projectTestAdapterListCache.Add (project, new Tuple<HashSet<string>, string> (new HashSet<string> (nugetsFolders), result));
				return result;
			}
		}

		/// <summary>The vstest.console.dll of the SDK whose MSBuild the IDE uses (ADR 0008).</summary>
		internal static string GetVsTestConsolePath ()
		{
			string sdkDirectory = MSBuildRegistration.RegisteredMSBuildPath;
			if (string.IsNullOrEmpty (sdkDirectory)) {
				var sdks = DotNetCoreSdkInfo.FindAll ();
				sdkDirectory = sdks.Count > 0 ? sdks [0].MSBuildPath : null;
			}
			if (string.IsNullOrEmpty (sdkDirectory))
				throw new InvalidOperationException (GettextCatalog.GetString ("No .NET SDK found: cannot run vstest.console."));
			return Path.Combine (sdkDirectory, "vstest.console.dll");
		}

		/// <summary>
		/// Runs one request against the vstest.console process, starting it first if needed. The process is restarted
		/// for the next request when a request fails (e.g. vstest.console exited).
		/// </summary>
		protected async Task RunRequestAsync (Action<IVsTestConsoleWrapper> request, CancellationToken cancellationToken = default)
		{
			await requestLock.WaitAsync (cancellationToken).ConfigureAwait (false);
			try {
				await Task.Run (() => {
					var console = GetVsTestConsole ();
					try {
						request (console);
					} catch (Exception ex) when (!(ex is OperationCanceledException)) {
						LoggingService.LogError ("vstest.console request failed.", ex);
						Restart ();
						throw;
					}
				}, cancellationToken).ConfigureAwait (false);
			} finally {
				requestLock.Release ();
			}
		}

		IVsTestConsoleWrapper GetVsTestConsole ()
		{
			if (vsTestConsole != null)
				return vsTestConsole;

			string vsTestConsolePath = GetVsTestConsolePath ();
			if (!File.Exists (vsTestConsolePath))
				throw new FileNotFoundException (GettextCatalog.GetString ("vstest.console not found: {0}", vsTestConsolePath), vsTestConsolePath);

			var parameters = new ConsoleParameters ();
#if DIAGNOSTIC_LOGGING
			LoggingService.CreateLogFile ("vstest", out var filename).Dispose ();
			parameters.LogFilePath = filename;
#endif
			// A .dll path runs with the dotnet host (the one running MonoDevelop, or the first on PATH).
			var console = new VsTestConsoleWrapper (vsTestConsolePath, parameters);
			console.StartSession ();
			vsTestConsole = console;
			return console;
		}

		void Restart ()
		{
			var console = vsTestConsole;
			vsTestConsole = null;
			try {
				console?.EndSession ();
			} catch (Exception ex) {
				LoggingService.LogError ("vstest.console stop error.", ex);
			}
		}

		public void Dispose ()
		{
			Restart ();
			requestLock.Dispose ();
		}

		public static string GetAssemblyFileName (Project project)
		{
			FilePath outputFile = project.GetOutputFileName (UnitTestingIde.ActiveConfiguration);
			// .NET test projects are executables (Microsoft.NET.Test.Sdk sets OutputType=Exe) whose assembly is a .dll;
			// without the DotNetCore add-in's project extension (T099) the project model names it .exe.
			if (outputFile.HasExtension (".exe") && !File.Exists (outputFile)) {
				FilePath dll = outputFile.ChangeExtension (".dll");
				if (File.Exists (dll))
					return dll;
			}
			return outputFile;
		}
	}
}
