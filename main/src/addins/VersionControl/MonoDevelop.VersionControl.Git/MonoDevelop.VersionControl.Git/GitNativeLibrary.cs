//
// GitNativeLibrary.cs
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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace MonoDevelop.VersionControl.Git
{
	/// <summary>
	/// LibGit2Sharp looks for its native libgit2 in runtimes/&lt;rid&gt;/native of the application (the host's deps.json)
	/// and in its own folder. The add-in ships LibGit2Sharp and the Linux builds of libgit2 in the add-in folder, which the
	/// host does not know about, so a failed lookup of libgit2 falls back to the add-in's runtimes folder (ADR 0013).
	/// GlobalSettings.NativeLibraryPath does not help: on Linux it expects the file without the "lib" prefix.
	/// </summary>
	static class GitNativeLibrary
	{
		static Assembly libGit2Sharp;
		static string nativeDirectory;
		static IntPtr handle;

#pragma warning disable CA2255 // module initializers are intended for this: the fallback must be in place before libgit2 loads
		[ModuleInitializer]
		internal static void Register ()
		{
			nativeDirectory = FindNativeDirectory (Path.GetDirectoryName (typeof (GitNativeLibrary).Assembly.Location));
			if (nativeDirectory == null)
				return; // e.g. a test host, where libgit2 is in the application's deps.json
			libGit2Sharp = typeof (LibGit2Sharp.Repository).Assembly;
			var context = AssemblyLoadContext.GetLoadContext (libGit2Sharp) ?? AssemblyLoadContext.Default;
			context.ResolvingUnmanagedDll += ResolveLibGit2;
		}
#pragma warning restore CA2255

		static IntPtr ResolveLibGit2 (Assembly assembly, string libraryName)
		{
			if (assembly != libGit2Sharp || !libraryName.StartsWith ("git2-", StringComparison.Ordinal))
				return IntPtr.Zero;
			if (handle == IntPtr.Zero) {
				var path = Path.Combine (nativeDirectory, "lib" + libraryName + ".so");
				if (NativeLibrary.TryLoad (path, out var loaded))
					handle = loaded;
			}
			return handle;
		}

		internal static string FindNativeDirectory (string baseDirectory)
		{
			if (string.IsNullOrEmpty (baseDirectory))
				return null;
			var arch = RuntimeInformation.ProcessArchitecture.ToString ().ToLowerInvariant ();
			foreach (var rid in new[] { RuntimeInformation.RuntimeIdentifier, "linux-" + arch, "linux-musl-" + arch }) {
				var directory = Path.Combine (baseDirectory, "runtimes", rid, "native");
				if (Directory.Exists (directory))
					return directory;
			}
			return null;
		}
	}
}
