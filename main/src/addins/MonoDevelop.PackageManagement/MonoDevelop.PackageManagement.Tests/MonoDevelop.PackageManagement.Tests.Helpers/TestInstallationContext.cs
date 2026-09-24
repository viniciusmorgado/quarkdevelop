//
// TestInstallationContext.cs
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
using System.Linq;
using System.Reflection;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace MonoDevelop.PackageManagement.Tests.Helpers
{
	/// <summary>
	/// NuGet 5 to 7 adapters for the tests: the installation context now lists the target framework aliases of the
	/// project (strings) instead of NuGetFramework plus an original framework map, and BuildIntegratedProjectAction
	/// has no public constructor any more.
	/// </summary>
	static class TestInstallationContext
	{
		public static BuildIntegratedInstallationContext Create (
			IEnumerable<NuGetFramework> successfulFrameworks,
			IEnumerable<NuGetFramework> unsuccessfulFrameworks,
			IDictionary<NuGetFramework, string> originalFrameworks)
		{
			return new BuildIntegratedInstallationContext {
				SuccessfulFrameworks = GetAliases (successfulFrameworks, originalFrameworks),
				UnsuccessfulFrameworks = GetAliases (unsuccessfulFrameworks, originalFrameworks),
			};
		}

		static List<string> GetAliases (IEnumerable<NuGetFramework> frameworks, IDictionary<NuGetFramework, string> originalFrameworks)
		{
			if (frameworks == null)
				return new List<string> ();

			return frameworks.Select (framework => {
				if (originalFrameworks != null && originalFrameworks.TryGetValue (framework, out string alias))
					return alias;
				return framework.GetShortFolderName ();
			}).ToList ();
		}

		public static BuildIntegratedProjectAction CreateProjectAction (
			NuGetProject project,
			PackageIdentity packageIdentity,
			NuGetProjectActionType actionType,
			LockFile originalLockFile,
			RestoreResultPair restoreResultPair,
			IReadOnlyList<SourceRepository> sources,
			IReadOnlyList<NuGetProjectAction> originalActions,
			BuildIntegratedInstallationContext installationContext)
		{
			return (BuildIntegratedProjectAction)Activator.CreateInstance (
				typeof (BuildIntegratedProjectAction),
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new object[] {
					project,
					packageIdentity,
					actionType,
					originalLockFile,
					restoreResultPair,
					sources,
					originalActions,
					installationContext,
					null
				},
				null);
		}
	}
}
