//
// XwtWindowTests.cs
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

using System.Collections.Generic;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	[TestFixture]
	public class XwtWindowTests
	{
		[SetUp]
		public void SetUp ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
		}

		// T107: the IDE's tooltip windows fade in from a timeout, which could run after the window was disposed. The GTK
		// backend then used the destroyed (freed) window, and the IDE test host crashed later in GObject toggle references.
		[Test]
		public void OpacityAfterDisposeLeavesTheDestroyedWindowAlone ()
		{
			var criticals = new List<string> ();
			uint handler = GLib.Log.SetLogHandler ("Gtk", GLib.LogLevelFlags.Critical, (domain, level, message) => criticals.Add (message));
			try {
				for (int i = 0; i < 5; i++) {
					var window = new Xwt.PopupWindow ();
					window.Content = new Xwt.Label ("tooltip");
					window.Dispose ();
					window.Opacity = 1;
				}
			} finally {
				GLib.Log.RemoveLogHandler ("Gtk", handler);
			}
			Assert.That (criticals, Is.Empty);
		}

		// T107: disposing an Xwt window destroyed its GTK window with GtkSharp's Widget.Destroy, which frees a toplevel
		// the wrapper still references; the wrapper then released the freed window a second time when it was disposed
		// or finalized (random crashes of the IDE test host).
		[Test]
		public void DisposedWindowIsReleasedOnce ()
		{
			var criticals = new List<string> ();
			uint handler = GLib.Log.SetLogHandler ("GLib-GObject", GLib.LogLevelFlags.Critical, (domain, level, message) => criticals.Add (message));
			try {
				for (int i = 0; i < 5; i++) {
					var window = new Xwt.PopupWindow ();
					window.Content = new Xwt.Label ("tooltip");
					var gtkWindow = (Gtk.Window)((Xwt.Backends.IWindowFrameBackend)Xwt.Toolkit.CurrentEngine.GetSafeBackend (window)).Window;
					window.Dispose ();
					// what the finalizer does later
					gtkWindow.Dispose ();
				}
			} finally {
				GLib.Log.RemoveLogHandler ("GLib-GObject", handler);
			}
			Assert.That (criticals, Is.Empty);
		}
	}
}
