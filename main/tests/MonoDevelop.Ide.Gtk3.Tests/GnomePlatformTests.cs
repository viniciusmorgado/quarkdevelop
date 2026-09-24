//
// GnomePlatformTests.cs
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
using System.Diagnostics;
using System.IO;
using System.Threading;
using MonoDevelop.Platform;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T087: the GNOME/freedesktop platform service on .NET — gio's P/Invokes resolve through NativeLibraryMap
	/// (GnomePlatform.dll.config is gone) and URLs are opened with xdg-open.
	/// </summary>
	[TestFixture]
	[System.Runtime.Versioning.SupportedOSPlatform ("linux")]
	public class GnomePlatformTests
	{
		sealed class TestPlatform : GnomePlatform
		{
			public string MimeTypeForUri (string uri) => OnGetMimeTypeForUri (uri);
		}

		[Test]
		public void GioReportsMimeTypesOfFiles ()
		{
			var dir = Path.Combine (Path.GetTempPath (), "md-gio-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (dir);
			try {
				var file = Path.Combine (dir, "notes.txt");
				File.WriteAllText (file, "some text\n");
				var platform = new TestPlatform ();

				// repeated: freeing memory gio owns (fixed) corrupted the heap after a few calls
				for (int i = 0; i < 20; i++) {
					Assert.AreEqual ("text/plain", platform.MimeTypeForUri (new Uri (file).AbsoluteUri));
					Assert.AreEqual ("inode/directory", platform.MimeTypeForUri (new Uri (dir).AbsoluteUri));
				}
				// a file that does not exist: the type of its name (T107; it was null)
				Assert.AreEqual ("text/plain", platform.MimeTypeForUri (new Uri (Path.Combine (dir, "missing.txt")).AbsoluteUri));
				Assert.IsNull (platform.MimeTypeForUri (new Uri (Path.Combine (dir, "missing-no-extension")).AbsoluteUri));
			} finally {
				Directory.Delete (dir, true);
			}
		}

		[Test]
		public void GioReportsMimeTypesOfFilePaths ()
		{
			// The IDE passes file paths, not URIs (T107: gio reported no type for them).
			var dir = Path.Combine (Path.GetTempPath (), "md-gio-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (dir);
			try {
				var file = Path.Combine (dir, "notes.txt");
				File.WriteAllText (file, "some text\n");
				var platform = new TestPlatform ();

				Assert.AreEqual ("text/plain", platform.MimeTypeForUri (file));
				Assert.AreEqual ("inode/directory", platform.MimeTypeForUri (dir));
				Assert.AreEqual ("text/plain", platform.MimeTypeForUri (Path.Combine (dir, "missing.txt")));
				Assert.AreEqual ("application/xslt+xml", platform.MimeTypeForUri ("relative-and-missing.xslt"));
			} finally {
				Directory.Delete (dir, true);
			}
		}

		[Test]
		public void ShowUrlRunsXdgOpen ()
		{
			var dir = Path.Combine (Path.GetTempPath (), "md-xdg-open-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (dir);
			var record = Path.Combine (dir, "opened");
			var script = Path.Combine (dir, "xdg-open");
			File.WriteAllText (script, "#!/bin/sh\nprintf '%s' \"$1\" > \"" + record + "\"\n");
			File.SetUnixFileMode (script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			var path = Environment.GetEnvironmentVariable ("PATH");
			try {
				Environment.SetEnvironmentVariable ("PATH", dir + Path.PathSeparator + path);
				new TestPlatform ().ShowUrl ("https://www.monodevelop.com/?a=1&b=2");

				var watch = Stopwatch.StartNew ();
				while (!File.Exists (record) && watch.Elapsed < TimeSpan.FromSeconds (10))
					Thread.Sleep (50);
				Thread.Sleep (100);
				Assert.AreEqual ("https://www.monodevelop.com/?a=1&b=2", File.ReadAllText (record));
			} finally {
				Environment.SetEnvironmentVariable ("PATH", path);
				Directory.Delete (dir, true);
			}
		}
	}
}
