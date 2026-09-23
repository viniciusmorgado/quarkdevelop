//
// NativeLibraryTests.cs
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
using System.Reflection;
using MonoDevelop.Components;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T084: the P/Invokes of MonoDevelop.Ide use Windows library names ("libglib-2.0-0.dll", ...). On
	/// CoreCLR they resolve through NativeLibraryMap (registered by the assembly's module initializer), which
	/// replaces the Mono dllmap entries of the deleted MonoDevelop.Ide.dll.config.
	/// </summary>
	[TestFixture]
	public class NativeLibraryTests
	{
		[Test]
		public void IdePInvokeWithWindowsLibraryNameResolvesOnLinux ()
		{
			// FastPangoAttrList: [DllImport (PangoUtil.LIBGLIB = "libglib-2.0-0.dll")] static extern int g_slist_length (IntPtr l); NULL is the empty list.
			var method = typeof (FastPangoAttrList).GetMethod ("g_slist_length", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull (method);
			Assert.AreEqual (0, method.Invoke (null, new object[] { IntPtr.Zero }));
		}
	}
}
