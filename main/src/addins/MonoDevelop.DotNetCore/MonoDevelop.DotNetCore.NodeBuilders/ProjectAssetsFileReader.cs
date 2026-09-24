//
// ProjectAssetsFileReader.cs
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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace MonoDevelop.DotNetCore.NodeBuilders
{
	/// <summary>
	/// Reads the package dependencies of an SDK-style project from its restore output (obj/project.assets.json).
	/// Since .NET 5 the SDK's ResolvePackageDependenciesDesignTime target returns only the top-level packages
	/// (_PackageDependenciesDesignTime), not the _DependenciesDesignTime items with the target frameworks, the
	/// dependency graph and the restore diagnostics that DotNetProject.GetPackageDependencies reads. The assets
	/// file has that information: the Dependencies folder shows the same tree as before (T099).
	/// </summary>
	static class ProjectAssetsFileReader
	{
		public static List<PackageDependencyInfo> Read (Projects.DotNetProject project)
		{
			FilePath assetsFile = GetAssetsFilePath (project);
			if (!File.Exists (assetsFile)) {
				return new List<PackageDependencyInfo> ();
			}

			try {
				LockFile lockFile = new LockFileFormat ().Read (assetsFile);
				return Read (lockFile);
			} catch (Exception ex) {
				LoggingService.LogError ("Unable to read '" + assetsFile + "'.", ex);
				return new List<PackageDependencyInfo> ();
			}
		}

		static FilePath GetAssetsFilePath (Projects.DotNetProject project)
		{
			string path = project.MSBuildProject?.EvaluatedProperties?.GetValue ("ProjectAssetsFile");
			if (string.IsNullOrEmpty (path)) {
				return project.BaseDirectory.Combine ("obj", "project.assets.json");
			}

			return project.BaseDirectory.Combine (path).FullPath;
		}

		/// <summary>
		/// One framework node per target framework of the assets file (runtime-specific targets are skipped), with
		/// the packages the project references directly (including implicit ones) as children.
		/// </summary>
		internal static List<PackageDependencyInfo> Read (LockFile lockFile)
		{
			var frameworks = new List<PackageDependencyInfo> ();
			foreach (LockFileTarget target in lockFile.Targets) {
				if (string.IsNullOrEmpty (target.RuntimeIdentifier)) {
					frameworks.Add (ReadTarget (lockFile, target));
				}
			}

			return frameworks;
		}

		static PackageDependencyInfo ReadTarget (LockFile lockFile, LockFileTarget target)
		{
			var packages = new Dictionary<string, LockFileTargetLibrary> (StringComparer.OrdinalIgnoreCase);
			foreach (LockFileTargetLibrary library in target.Libraries) {
				if (IsPackage (library)) {
					packages[library.Name] = library;
				}
			}

			var dependencies = new Dictionary<string, PackageDependencyInfo> (StringComparer.OrdinalIgnoreCase);
			var diagnosticKeys = AddDiagnostics (lockFile, target, dependencies);

			foreach (LockFileTargetLibrary library in packages.Values) {
				var childKeys = (library.Dependencies ?? Enumerable.Empty<NuGet.Packaging.Core.PackageDependency> ())
					.Select (dependency => GetKey (packages, dependency.Id))
					.Where (key => key != null)
					.Concat (GetDiagnosticKeys (diagnosticKeys, library.Name));

				string version = library.Version?.ToNormalizedString ();
				dependencies[library.Name + "/" + version] = new PackageDependencyInfo (
					library.Name,
					version,
					childKeys.ToImmutableArray ());
			}

			var topLevelKeys = new List<string> ();
			foreach (LibraryDependency dependency in GetDirectPackageDependencies (lockFile, target)) {
				string key = GetKey (packages, dependency.Name);
				if (key == null) {
					// Not restored (unknown package, no source): shown with the requested version and its diagnostics.
					string version = dependency.LibraryRange?.VersionRange?.OriginalString ?? string.Empty;
					key = dependency.Name + "/" + version;
					dependencies[key] = new PackageDependencyInfo (
						dependency.Name,
						version,
						GetDiagnosticKeys (diagnosticKeys, dependency.Name).ToImmutableArray ());
				}
				topLevelKeys.Add (key);
			}

			var framework = new PackageDependencyInfo (
				target.TargetFramework.Framework,
				target.TargetFramework.Version.ToString (),
				topLevelKeys.ToImmutableArray ());

			return PackageDependencyNodeCache.BuildDependencyTree (
				new List<PackageDependencyInfo> { framework },
				dependencies).Single ();
		}

		static bool IsPackage (LockFileTargetLibrary library)
		{
			return string.Equals (library.Type, "package", StringComparison.OrdinalIgnoreCase);
		}

		static string GetKey (Dictionary<string, LockFileTargetLibrary> packages, string name)
		{
			if (packages.TryGetValue (name, out LockFileTargetLibrary library)) {
				return library.Name + "/" + library.Version?.ToNormalizedString ();
			}

			return null;
		}

		static IEnumerable<LibraryDependency> GetDirectPackageDependencies (LockFile lockFile, LockFileTarget target)
		{
			TargetFrameworkInformation framework = lockFile.PackageSpec?.TargetFrameworks
				.FirstOrDefault (item => item.FrameworkName.Equals (target.TargetFramework));
			if (framework == null) {
				return Enumerable.Empty<LibraryDependency> ();
			}

			return framework.Dependencies
				.Where (dependency => dependency.LibraryRange.TypeConstraintAllows (LibraryDependencyTarget.Package));
		}

		/// <summary>
		/// Restore warnings and errors about a package of this target, keyed by the package name.
		/// </summary>
		static Dictionary<string, List<string>> AddDiagnostics (
			LockFile lockFile,
			LockFileTarget target,
			Dictionary<string, PackageDependencyInfo> dependencies)
		{
			var diagnosticKeys = new Dictionary<string, List<string>> (StringComparer.OrdinalIgnoreCase);
			foreach (IAssetsLogMessage message in lockFile.LogMessages) {
				if (string.IsNullOrEmpty (message.LibraryId)) {
					continue;
				}

				if (message.TargetGraphs != null && message.TargetGraphs.Any () && !message.TargetGraphs.Contains (target.Name)) {
					continue;
				}

				string code = message.Code.ToString ();
				string key = "diagnostic/" + message.LibraryId + "/" + code + "/" + dependencies.Count;
				dependencies[key] = new PackageDependencyInfo (
					message.LibraryId,
					string.Empty,
					ImmutableArray<string>.Empty,
					isDiagnostic: true,
					diagnosticCode: code,
					diagnosticMessage: message.Message);

				if (!diagnosticKeys.TryGetValue (message.LibraryId, out List<string> keys)) {
					keys = new List<string> ();
					diagnosticKeys[message.LibraryId] = keys;
				}
				keys.Add (key);
			}

			return diagnosticKeys;
		}

		static IEnumerable<string> GetDiagnosticKeys (Dictionary<string, List<string>> diagnosticKeys, string packageName)
		{
			if (diagnosticKeys.TryGetValue (packageName, out List<string> keys)) {
				return keys;
			}

			return Enumerable.Empty<string> ();
		}
	}
}
