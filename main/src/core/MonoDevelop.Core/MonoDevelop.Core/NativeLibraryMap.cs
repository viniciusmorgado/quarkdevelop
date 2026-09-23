//
// NativeLibraryMap.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Maps the Windows-style native library names used in <c>[DllImport]</c> declarations to Linux
	/// sonames (ADR 0013). Replaces the Mono <c>&lt;dllmap&gt;</c> entries of the <c>*.dll.config</c>
	/// files, which CoreCLR ignores. Assemblies with P/Invokes call <see cref="Register"/> once, from a
	/// module initializer.
	/// </summary>
	public static class NativeLibraryMap
	{
		static readonly Dictionary<string, string> linuxNames = new Dictionary<string, string> (StringComparer.Ordinal) {
			// C library: "libc" would resolve to libc.so, which is a linker script, not a library.
			["libc"] = "libc.so.6",
			// gettext lives in glibc on Linux.
			["intl"] = "libc.so.6",
			["libintl"] = "libc.so.6",
			// GLib / GObject / GIO
			["libglib-2.0-0.dll"] = "libglib-2.0.so.0",
			["libgobject-2.0-0.dll"] = "libgobject-2.0.so.0",
			["libgio-2.0-0.dll"] = "libgio-2.0.so.0",
			// GTK stack: the Linux build uses GTK 3 (ADR 0011)
			["libgtk-win32-2.0-0.dll"] = "libgtk-3.so.0",
			["libgdk-win32-2.0-0.dll"] = "libgdk-3.so.0",
			["libgtk-3-0.dll"] = "libgtk-3.so.0",
			["libgdk-3-0.dll"] = "libgdk-3.so.0",
			["libatk-1.0-0.dll"] = "libatk-1.0.so.0",
			["libpango-1.0-0.dll"] = "libpango-1.0.so.0",
			["libpangocairo-1.0-0.dll"] = "libpangocairo-1.0.so.0",
			["libcairo-2.dll"] = "libcairo.so.2",
			["libgdk_pixbuf-2.0-0.dll"] = "libgdk_pixbuf-2.0.so.0",
		};

		static readonly ConcurrentDictionary<Assembly, bool> registered = new ConcurrentDictionary<Assembly, bool> ();

		/// <summary>
		/// Returns the Linux soname for a library name used in a <c>[DllImport]</c>, or null when the name
		/// needs no mapping.
		/// </summary>
		public static string GetLinuxName (string libraryName)
		{
			if (libraryName == null)
				return null;
			return linuxNames.TryGetValue (libraryName, out var mapped) ? mapped : null;
		}

		/// <summary>
		/// Installs the resolver for <paramref name="assembly"/>. Idempotent; does nothing on Windows and
		/// macOS, where the original names are used.
		/// </summary>
		public static void Register (Assembly assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException (nameof (assembly));
			if (!OperatingSystem.IsLinux () || !registered.TryAdd (assembly, true))
				return;
			NativeLibrary.SetDllImportResolver (assembly, Resolve);
		}

		static IntPtr Resolve (string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
		{
			var mapped = GetLinuxName (libraryName);
			if (mapped != null && NativeLibrary.TryLoad (mapped, assembly, searchPath, out var handle))
				return handle;
			// Fall back to the default probing for names that need no mapping.
			return IntPtr.Zero;
		}

#pragma warning disable CA2255 // module initializers are intended for this: P/Invokes resolve before first use
		[ModuleInitializer]
		internal static void RegisterCore () => Register (typeof (NativeLibraryMap).Assembly);
#pragma warning restore CA2255
	}
}
