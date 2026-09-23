//
// NativeLibraryResolver.cs
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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Replaces the Mono &lt;dllmap&gt; entries of Xwt.Gtk3.dll.config, which CoreCLR ignores
	/// (MonoDevelop local patch, ADR 0013). Xwt cannot depend on MonoDevelop.Core, so it carries its
	/// own copy of the Linux soname table for the libraries it imports directly.
	/// </summary>
	static class NativeLibraryResolver
	{
		static readonly Dictionary<string, string> linuxNames = new Dictionary<string, string> (StringComparer.Ordinal) {
			[GtkInterop.LIBGLIB] = "libglib-2.0.so.0",
			[GtkInterop.LIBGOBJECT] = "libgobject-2.0.so.0",
			[GtkInterop.LIBATK] = "libatk-1.0.so.0",
			[GtkInterop.LIBGTK] = "libgtk-3.so.0",
			[GtkInterop.LIBGDK] = "libgdk-3.so.0",
			[GtkInterop.LIBPANGO] = "libpango-1.0.so.0",
			[GtkInterop.LIBPANGOCAIRO] = "libpangocairo-1.0.so.0",
			[GtkInterop.LIBFONTCONFIG] = "libfontconfig.so.1",
			["libc"] = "libc.so.6",
		};

		internal static string GetLinuxName (string libraryName)
		{
			return libraryName != null && linuxNames.TryGetValue (libraryName, out var mapped) ? mapped : null;
		}

		static IntPtr Resolve (string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
		{
			var mapped = GetLinuxName (libraryName);
			if (mapped != null && NativeLibrary.TryLoad (mapped, assembly, searchPath, out var handle))
				return handle;
			return IntPtr.Zero;
		}

#pragma warning disable CA2255 // module initializers are intended for this: P/Invokes resolve before first use
		[ModuleInitializer]
		internal static void Register ()
		{
			if (OperatingSystem.IsLinux ())
				NativeLibrary.SetDllImportResolver (typeof (NativeLibraryResolver).Assembly, Resolve);
		}
#pragma warning restore CA2255
	}
}
