//
// NetCoreDbgLocator.cs
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
using MonoDevelop.Core;

namespace MonoDevelop.Debugger.NetCoreDbg
{
	/// <summary>
	/// Finds the netcoredbg executable: the path set in the <see cref="PathPropertyName"/> property (MonoDevelop
	/// preferences), else <c>netcoredbg</c> on PATH (the development container and the Flatpak install it there).
	/// </summary>
	public static class NetCoreDbgLocator
	{
		public const string PathPropertyName = "MonoDevelop.Debugger.NetCoreDbg.Path";

		const string ExecutableName = "netcoredbg";

		/// <summary>The netcoredbg executable, or null when it is not installed.</summary>
		public static string FindDebugAdapter ()
		{
			var configured = PropertyService.Get<string> (PathPropertyName);
			if (!string.IsNullOrEmpty (configured))
				return File.Exists (configured) ? configured : null;
			return FindInPath (ExecutableName, Environment.GetEnvironmentVariable ("PATH"));
		}

		internal static string FindInPath (string fileName, string searchPath)
		{
			if (string.IsNullOrEmpty (searchPath))
				return null;
			foreach (var directory in searchPath.Split (Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
				var candidate = Path.Combine (directory, fileName);
				if (File.Exists (candidate))
					return candidate;
			}
			return null;
		}
	}
}
