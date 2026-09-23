//
// MSBuildRegistration.cs
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
using System.Linq;
using Microsoft.Build.Locator;

namespace MonoDevelop.Core.Assemblies
{
	/// <summary>
	/// Registers the MSBuild of the installed .NET SDK with Microsoft.Build.Locator (ADR 0008).
	/// Hosts (mdtool, the IDE, test hosts) must call <see cref="EnsureRegistered"/> before any
	/// Microsoft.Build type is loaded; MonoDevelop compiles against MSBuild without shipping it.
	/// </summary>
	public static class MSBuildRegistration
	{
		static readonly object registrationLock = new object ();

		/// <summary>The SDK MSBuild directory that was registered, if any.</summary>
		public static string RegisteredMSBuildPath { get; private set; }

		/// <summary>
		/// Registers the newest .NET SDK (or <paramref name="msbuildPath"/> when given). Idempotent;
		/// returns false when no SDK could be found.
		/// </summary>
		public static bool EnsureRegistered (string msbuildPath = null)
		{
			lock (registrationLock) {
				if (MSBuildLocator.IsRegistered)
					return true;
				var instances = MSBuildLocator.QueryVisualStudioInstances (new VisualStudioInstanceQueryOptions {
					DiscoveryTypes = DiscoveryType.DotNetSdk
				}).ToList ();
				var instance = msbuildPath != null
					? instances.FirstOrDefault (i => SamePath (i.MSBuildPath, msbuildPath))
					: instances.OrderByDescending (i => i.Version).FirstOrDefault ();
				if (instance != null) {
					MSBuildLocator.RegisterInstance (instance);
					RegisteredMSBuildPath = instance.MSBuildPath;
					return true;
				}
				if (msbuildPath != null) {
					MSBuildLocator.RegisterMSBuildPath (msbuildPath);
					RegisteredMSBuildPath = msbuildPath;
					return true;
				}
				return false;
			}
		}

		static bool SamePath (string a, string b)
		{
			return string.Equals (
				System.IO.Path.GetFullPath (a).TrimEnd (System.IO.Path.DirectorySeparatorChar),
				System.IO.Path.GetFullPath (b).TrimEnd (System.IO.Path.DirectorySeparatorChar),
				StringComparison.Ordinal);
		}
	}
}
