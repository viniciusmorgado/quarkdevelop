//
// NativeLibraryMapTests.cs
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

using System.IO;
using System.Reflection;
using System.Text;
using MonoDevelop.Projects.Text;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>Task T045 (ADR 0013): dllmap replacement.</summary>
	[TestFixture]
	public class NativeLibraryMapTests
	{
		[TestCase ("libglib-2.0-0.dll", "libglib-2.0.so.0")]
		[TestCase ("libgobject-2.0-0.dll", "libgobject-2.0.so.0")]
		[TestCase ("libgtk-win32-2.0-0.dll", "libgtk-3.so.0")]
		[TestCase ("libc", "libc.so.6")]
		[TestCase ("intl", "libc.so.6")]
		public void MapsWindowsNamesToLinuxSonames (string dllImportName, string expected)
		{
			Assert.AreEqual (expected, NativeLibraryMap.GetLinuxName (dllImportName));
		}

		[Test]
		public void UnknownNamesAreNotMapped ()
		{
			Assert.IsNull (NativeLibraryMap.GetLinuxName ("libsomething.so.1"));
			Assert.IsNull (NativeLibraryMap.GetLinuxName (null));
		}

		[Test]
		public void RegisteringTwiceIsHarmless ()
		{
			// The core assembly registers itself from its module initializer; SetDllImportResolver would
			// throw on a second registration.
			Assert.DoesNotThrow (() => NativeLibraryMap.Register (typeof (NativeLibraryMap).Assembly));
		}

		[Test]
		[Platform ("Linux")]
		public void LibcImportsResolve ()
		{
			// FilePath.ResolveLinks P/Invokes realpath from "libc".
			var dir = Path.Combine (Path.GetTempPath (), "md-realpath-" + System.Guid.NewGuid ());
			Directory.CreateDirectory (dir);
			try {
				var resolved = new FilePath (Path.Combine (dir, ".", "")).ResolveLinks ();
				Assert.AreEqual (new FilePath (dir).FullPath, resolved.FullPath);
			} finally {
				Directory.Delete (dir);
			}
		}

		[Test]
		[Platform ("Linux")]
		public void GlibImportsResolve ()
		{
			// TextFile converts encodings through g_convert from "libglib-2.0-0.dll".
			var convert = typeof (TextFile).GetMethod ("ConvertToBytes", BindingFlags.NonPublic | BindingFlags.Static);
			var latin1 = Encoding.Latin1.GetBytes ("olá");
			var utf8 = (byte[])convert.Invoke (null, new object[] { latin1, (long)latin1.Length, "UTF-8", "ISO-8859-1" });
			Assert.AreEqual ("olá", Encoding.UTF8.GetString (utf8));
		}
	}
}
