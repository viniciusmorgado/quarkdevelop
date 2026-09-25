//
// ImageViewTests.cs
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
using System.Runtime.InteropServices;
using System.Threading;
using MonoDevelop.Components;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// ImageView, the icon widget of the IDE's menus (CommandMenuItem, LinkCommandEntry, ContextMenuExtensionsGtk),
	/// buttons and pads.
	/// </summary>
	[TestFixture]
#pragma warning disable CS0612 // GtkImageMenuItem is deprecated in GTK 3; the IDE's menus still use it
	public class ImageViewTests
	{
		// One toplevel for the fixture, never destroyed (see OffscreenContextTests).
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

		static Xwt.Drawing.BitmapImage RedSquare (int size)
		{
			using (var builder = new Xwt.Drawing.ImageBuilder (size, size)) {
				builder.Context.SetColor (Xwt.Drawing.Colors.Red);
				builder.Context.Rectangle (0, 0, size, size);
				builder.Context.Fill ();
				return builder.ToBitmap ();
			}
		}

		/// <summary>
		/// gtk_image_menu_item_set_image sets the pixel size of the image, so GTK 3 wants a GtkImage there. While ImageView
		/// was a GtkMisc, every menu icon logged "gtk_image_set_pixel_size: assertion 'GTK_IS_IMAGE (image)' failed".
		/// </summary>
		[Test]
		public void IsTheImageOfAMenuItemWithoutGtkCriticals ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();

			var criticals = new List<string> ();
			uint handler = GLib.Log.SetLogHandler ("Gtk", GLib.LogLevelFlags.Critical, (domain, level, message) => criticals.Add (message));
			var item = new Gtk.ImageMenuItem ("Item");
			try {
				var view = new ImageView (RedSquare (16));
				item.Image = view;

				Assert.AreSame (view, item.Image);
				Assert.IsEmpty (criticals);
			} finally {
				GLib.Log.RemoveLogHandler ("Gtk", handler);
				item.Destroy ();
			}
		}

		/// <summary>
		/// ImageView takes the size of its Xwt image, also as a GtkImage, whose own content is empty, and in a menu item,
		/// which sets the GtkImage pixel size to 16. The item always shows its image, as the IDE's context menus do
		/// (GtkWorkarounds.ForceImageOnMenuItem): with the default gtk-menu-images setting, GTK 3 hides menu images, and a
		/// hidden widget has no size.
		/// </summary>
		[Test]
		public void TakesTheSizeOfItsImage ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();

			var item = new Gtk.ImageMenuItem ("Item") { AlwaysShowImage = true };
			try {
				var view = new ImageView (RedSquare (24));
				item.Image = view;

				view.GetPreferredWidth (out int minimumWidth, out int naturalWidth);
				view.GetPreferredHeight (out int minimumHeight, out int naturalHeight);
				view.GetPreferredHeightAndBaselineForWidth (24, out int minimumForWidth, out int naturalForWidth, out int minimumBaseline, out int naturalBaseline);

				Assert.AreEqual ((24, 24), (minimumWidth, naturalWidth));
				Assert.AreEqual ((24, 24), (minimumHeight, naturalHeight));
				Assert.AreEqual ((24, 24), (minimumForWidth, naturalForWidth));
				Assert.AreEqual ((-1, -1), (minimumBaseline, naturalBaseline));
			} finally {
				item.Destroy ();
			}
		}

		/// <summary>
		/// Next to a label in a horizontal box, as in buttons and menu item labels, ImageView gets the size of its image
		/// and draws it: GtkImage neither sizes nor draws it.
		/// </summary>
		[Test]
		public void DrawsItsImageInAHorizontalBox ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();

			if (host == null)
				host = new Gtk.OffscreenWindow ();
			var box = new Gtk.Box (Gtk.Orientation.Horizontal, 0);
			var view = new ImageView (RedSquare (24));
			box.PackStart (view, false, false, 0);
			box.PackStart (new Gtk.Label ("Label"), false, false, 0);
			host.Add (box);
			host.ShowAll ();
			Iterate (200);
			try {
				Assert.AreEqual (24, view.Allocation.Width);
				Assert.AreEqual (24, view.Allocation.Height);

				using (var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, 24, 24)) {
					using (var cr = new Cairo.Context (surface))
						view.Draw (cr);
					surface.Flush ();
					int red = 0;
					for (int offset = 0; offset < 24 * 24 * 4; offset += 4)
						if (Marshal.ReadInt32 (surface.DataPtr, offset) == unchecked((int)0xFFFF0000))
							red++;
					Assert.AreEqual (24 * 24, red, "the image was not drawn over the whole allocation");
				}
			} finally {
				host.Remove (box);
				box.Destroy ();
				Iterate (50);
			}
		}
	}
#pragma warning restore CS0612
}
