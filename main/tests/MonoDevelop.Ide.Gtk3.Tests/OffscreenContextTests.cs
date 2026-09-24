//
// OffscreenContextTests.cs
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
using System.Runtime.InteropServices;
using System.Threading;
using MonoDevelop.Components;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Contexts created outside of a draw handler. The threaded renderer and CreateXwtContext made them with
	/// gdk_cairo_create on the widget's window, deprecated on GTK 3 (where a window is drawn only in its draw
	/// handler); as the overview of the text editor's scroll bar, they now draw off screen on Linux.
	/// </summary>
	[TestFixture]
	public class OffscreenContextTests
	{
		// One toplevel for the fixture, never destroyed: collecting the wrapper of a destroyed toplevel made
		// from C# (GtkSharp 3.24.24.95) removes a toggle reference of the freed window (GLib-GObject-CRITICAL
		// g_object_remove_toggle_ref), which crashed later tests of the process.
		static Gtk.OffscreenWindow host;

		static void Iterate (int milliseconds)
		{
			var end = DateTime.UtcNow.AddMilliseconds (milliseconds);
			while (DateTime.UtcNow < end) {
				while (GLib.MainContext.Iteration (false)) {
				}
				Thread.Sleep (10);
			}
		}

		static Gtk.DrawingArea Show (int width, int height)
		{
			if (host == null)
				host = new Gtk.OffscreenWindow ();
			var area = new Gtk.DrawingArea ();
			area.SetSizeRequest (width, height);
			host.Add (area);
			host.ShowAll ();
			Iterate (200);
			return area;
		}

		static void Remove (Gtk.DrawingArea area)
		{
			host.Remove (area);
			area.Destroy ();
			Iterate (50);
		}

		[Test]
		public void ThreadedRendererDrawsOffscreenAndShowsOnItsOwner ()
		{
			GtkFixture.Require ();
			var area = Show (200, 100);
			ThreadedRenderer renderer = null;
			int draws = 0, shows = 0;
			area.Drawn += (o, args) => {
				if (renderer != null && renderer.Show (args.Cr))
					shows++;
			};
			try {
				renderer = new ThreadedRenderer (area);
				for (int i = 0; i < 5; i++) {
					// A new size makes the renderer create a new surface.
					area.SetSizeRequest (200 + i * 20, 100 + i * 10);
					Iterate (100);
					renderer.QueueThreadedDraw (cr => {
						draws++;
						cr.SetSourceRGB (1, 0, 0);
						cr.Paint ();
					});
					Iterate (100);
				}

				Assert.AreEqual (5, draws);
				Assert.Greater (shows, 0);
				Assert.AreEqual (280, area.Allocation.Width);
				// What the renderer drew off screen is what it shows.
				using (var image = new Cairo.ImageSurface (Cairo.Format.Argb32, area.Allocation.Width, area.Allocation.Height)) {
					using (var cr = new Cairo.Context (image))
						Assert.IsTrue (renderer.Show (cr));
					image.Flush ();
					Assert.AreEqual (unchecked((int)0xFFFF0000), Marshal.ReadInt32 (image.DataPtr));
				}
				Assert.IsTrue (area.IsRealized);
				Assert.IsFalse (area.Window.IsDestroyed);
			} finally {
				renderer?.Dispose ();
				Remove (area);
			}
		}

		[Test]
		public void XwtContextOfAWidgetCanBeUsedAndDisposed ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
			var area = Show (200, 100);
			try {
				for (int i = 0; i < 5; i++) {
					using (var ctx = area.CreateXwtContext ()) {
						ctx.SetColor (Xwt.Drawing.Colors.Red);
						ctx.Rectangle (0, 0, 10, 10);
						ctx.Fill ();
						using (var layout = new Xwt.Drawing.TextLayout { Text = "overview" }) {
							Assert.Greater (layout.GetSize ().Width, 0);
							ctx.DrawTextLayout (layout, 0, 0);
						}
					}
					Iterate (50);
				}

				Assert.IsTrue (area.IsRealized);
				Assert.IsFalse (area.Window.IsDestroyed);
			} finally {
				Remove (area);
			}
		}
	}
}
