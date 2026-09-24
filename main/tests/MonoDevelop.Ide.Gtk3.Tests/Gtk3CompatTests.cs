//
// Gtk3CompatTests.cs
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
using MonoDevelop.Components;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T136: Gtk3ExposeEvent keeps GTK2 drawing coordinates for code ported to OnDrawn, and the
	/// contexts it creates leave the GTK context as they found it.
	/// </summary>
	[TestFixture]
	public class Gtk3CompatTests
	{
		/// <summary>Shows the widget at (10, 20) inside an offscreen toplevel and lets GTK allocate it.</summary>
		static T Place<T> (T widget) where T : Gtk.Widget
		{
			var window = new Gtk.OffscreenWindow ();
			var container = new Gtk.Fixed ();
			container.Put (widget, 10, 20);
			widget.SetSizeRequest (100, 50);
			window.Add (container);
			window.ShowAll ();
			while (Gtk.Application.EventsPending ())
				Gtk.Application.RunIteration ();
			Assert.AreEqual (new Gdk.Rectangle (10, 20, 100, 50), widget.Allocation);
			return widget;
		}

		static (Cairo.ImageSurface surface, Cairo.Context cr) CreateContext ()
		{
			var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, 200, 100);
			return (surface, new Cairo.Context (surface));
		}

		[Test]
		public void WidgetWithoutWindowUsesItsAllocationAsOffset ()
		{
			GtkFixture.Require ();
			var label = Place (new Gtk.Label ("x"));
			Assert.IsFalse (label.HasWindow);
			var (surface, cr) = CreateContext ();
			using (surface)
			using (cr) {
				var expose = new Gtk3ExposeEvent (label, cr);
				Assert.AreEqual (10, expose.OffsetX);
				Assert.AreEqual (20, expose.OffsetY);
				Assert.AreSame (cr, expose.Context);
				// The whole 200x100 surface is dirty; in GTK2 (window) coordinates it starts at the allocation.
				Assert.AreEqual (new Gdk.Rectangle (10, 20, 200, 100), expose.Area);
			}
		}

		[Test]
		public void WidgetWithWindowHasNoOffset ()
		{
			GtkFixture.Require ();
			var box = Place (new Gtk.EventBox ());
			Assert.IsTrue (box.HasWindow);
			var (surface, cr) = CreateContext ();
			using (surface)
			using (cr) {
				var expose = new Gtk3ExposeEvent (box, cr);
				Assert.AreEqual (0, expose.OffsetX);
				Assert.AreEqual (0, expose.OffsetY);
				Assert.AreEqual (new Gdk.Rectangle (0, 0, 200, 100), expose.Area);
			}
		}

		[Test]
		public void CreatedContextTranslatesAndRestores ()
		{
			GtkFixture.Require ();
			var label = Place (new Gtk.Label ("x"));
			var (surface, cr) = CreateContext ();
			using (surface)
			using (cr) {
				cr.SetSourceRGB (1, 0, 0);
				var expose = new Gtk3ExposeEvent (label, cr);
				using (var ctx = expose.CreateContext ()) {
					// GTK2 code draws at allocation coordinates: (10, 20) lands on the widget origin.
					double x = 10, y = 20;
					ctx.UserToDevice (ref x, ref y);
					Assert.AreEqual (0, x, 1e-9);
					Assert.AreEqual (0, y, 1e-9);
					ctx.SetSourceRGB (0, 0, 1);
				}
				// Translation and source are restored on the GTK context.
				double ox = 10, oy = 20;
				cr.UserToDevice (ref ox, ref oy);
				Assert.AreEqual (10, ox, 1e-9);
				Assert.AreEqual (20, oy, 1e-9);
				var source = (Cairo.SolidPattern)cr.GetSource ();
				Assert.AreEqual (1, source.Color.R, 1e-9);
				Assert.AreEqual (0, source.Color.B, 1e-9);
			}
		}

		[System.Runtime.InteropServices.DllImport ("libgtk-3.so.0")]
		static extern void gtk_cell_renderer_get_preferred_width (IntPtr cell, IntPtr widget, IntPtr minimum_size, out int natural_size);

		[System.Runtime.InteropServices.DllImport ("libgtk-3.so.0")]
		static extern void gtk_cell_renderer_get_preferred_height (IntPtr cell, IntPtr widget, out int minimum_size, IntPtr natural_size);

		/// <summary>
		/// GTK asks a cell renderer for one size only with a NULL pointer for the other (the Test Results pad of the unit
		/// testing add-in terminated the IDE with a NullReferenceException in CellRendererImage.OnGetPreferredWidth).
		/// </summary>
		[Test]
		public void CellRendererImageAcceptsNullSizePointers ()
		{
			GtkFixture.Require ();
			var renderer = new FixedSizeCellRendererImage ();
			var view = new Gtk.TreeView ();
			gtk_cell_renderer_get_preferred_width (renderer.Handle, view.Handle, IntPtr.Zero, out int naturalWidth);
			gtk_cell_renderer_get_preferred_height (renderer.Handle, view.Handle, out int minimumHeight, IntPtr.Zero);
			Assert.AreEqual (16, naturalWidth);
			Assert.AreEqual (12, minimumHeight);
		}

		/// <summary>A fixed size instead of an image (the image service needs the add-in engine).</summary>
		sealed class FixedSizeCellRendererImage : CellRendererImage
		{
			protected override void OnGetSize (Gtk.Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
			{
				x_offset = y_offset = 0;
				width = 16;
				height = 12;
			}
		}

		[Test]
		public void SizeRequestReturnsTheNaturalSize ()
		{
			GtkFixture.Require ();
			var label = new Gtk.Label ("some text");
			label.Show ();
			var requisition = Gtk3CompatExtensions.SizeRequest (label);
			label.GetPreferredSize (out _, out Gtk.Requisition natural);
			Assert.Greater (requisition.Width, 0);
			Assert.AreEqual (natural.Width, requisition.Width);
			Assert.AreEqual (natural.Height, requisition.Height);
		}

		[Test]
		public void SetStateReplacesTheStateTypeFlagsOnly ()
		{
			GtkFixture.Require ();
			var button = new Gtk.Button ();
			button.SetStateFlags (Gtk.StateFlags.DirLtr, false);

			button.SetState (Gtk.StateType.Prelight);
			Assert.IsTrue (button.StateFlags.HasFlag (Gtk.StateFlags.Prelight));

			button.SetState (Gtk.StateType.Selected);
			Assert.IsTrue (button.StateFlags.HasFlag (Gtk.StateFlags.Selected));
			Assert.IsFalse (button.StateFlags.HasFlag (Gtk.StateFlags.Prelight));

			button.SetState (Gtk.StateType.Normal);
			Assert.IsFalse (button.StateFlags.HasFlag (Gtk.StateFlags.Selected));
			Assert.IsFalse (button.StateFlags.HasFlag (Gtk.StateFlags.Prelight));
			// Flags that are not a GTK2 state type are kept, as gtk_widget_set_state does.
			Assert.IsTrue (button.StateFlags.HasFlag (Gtk.StateFlags.DirLtr));
		}

		/// <summary>Gives the widget a background color through CSS (the non-deprecated way).</summary>
		static void SetBackground (Gtk.Widget widget, string cssColor)
		{
			var css = new Gtk.CssProvider ();
			css.LoadFromData ("* { background-color: " + cssColor + "; }");
			widget.StyleContext.AddProvider (css, uint.MaxValue);
		}

		[Test]
		public void LightDarkAndMidShadeTheBackgroundLikeGtkStyle ()
		{
			GtkFixture.Require ();
			var box = new Gtk.EventBox ();
			SetBackground (box, "rgb(50%, 50%, 50%)");

			var bg = box.GetStyleBackgroundColor (Gtk.StateType.Normal);
			Assert.AreEqual (0.5, bg.R, 1e-6);
			Assert.AreEqual (1, bg.A, 1e-6);
			// Grey has no saturation: GTK's shade only scales the lightness (x1.3 light, x0.7 dark).
			Assert.AreEqual (0.65, box.GetStyleLightColor (Gtk.StateType.Normal).R, 1e-6);
			Assert.AreEqual (0.35, box.GetStyleDarkColor (Gtk.StateType.Normal).G, 1e-6);
			Assert.AreEqual (0.5, box.GetStyleMidColor (Gtk.StateType.Normal).B, 1e-6);

			// Saturation is scaled too: pure red (l 0.5, s 1) darkens to l 0.35, s 0.7, keeping its hue.
			var red = new Gtk.EventBox ();
			SetBackground (red, "rgb(255, 0, 0)");
			var dark = red.GetStyleDarkColor (Gtk.StateType.Normal);
			Assert.AreEqual (0.595, dark.R, 1e-6);
			Assert.AreEqual (0.105, dark.G, 1e-6);
			Assert.AreEqual (0.105, dark.B, 1e-6);
		}

		[Test]
		public void BackgroundColorOfATransparentWidgetComesFromItsAncestors ()
		{
			GtkFixture.Require ();
			var box = new Gtk.EventBox ();
			SetBackground (box, "rgb(20%, 40%, 60%)");
			var label = new Gtk.Label ("x");
			box.Add (label);

			var bg = label.GetStyleBackgroundColor (Gtk.StateType.Normal);
			Assert.AreEqual (0.2, bg.R, 1e-6);
			Assert.AreEqual (0.4, bg.G, 1e-6);
			Assert.AreEqual (0.6, bg.B, 1e-6);
			Assert.AreEqual (1, bg.A, 1e-6);

			// Base comes from the entry style class and is always opaque.
			Assert.AreEqual (1, label.GetStyleBaseColor (Gtk.StateType.Normal).A, 1e-6);
		}

		[Test]
		public void ToGdkRgbaKeepsTheComponents ()
		{
			var rgba = new Cairo.Color (0.1, 0.2, 0.3, 0.4).ToGdkRgba ();
			Assert.AreEqual (0.1, rgba.Red, 1e-9);
			Assert.AreEqual (0.2, rgba.Green, 1e-9);
			Assert.AreEqual (0.3, rgba.Blue, 1e-9);
			Assert.AreEqual (0.4, rgba.Alpha, 1e-9);
		}

		[Test]
		public void GetActiveTextReadsTheActiveRowOrTheEntry ()
		{
			GtkFixture.Require ();
			var combo = new Gtk.ComboBoxText ();
			combo.AppendText ("one");
			combo.AppendText ("two");
			Assert.IsNull (combo.GetActiveText ());
			combo.Active = 1;
			Assert.AreEqual ("two", combo.GetActiveText ());

			var store = new Gtk.ListStore (typeof (string));
			store.AppendValues ("alpha");
			var withEntry = Gtk.ComboBox.NewWithModelAndEntry (store);
			withEntry.EntryTextColumn = 0;
			withEntry.Entry.Text = "typed";
			Assert.AreEqual ("typed", withEntry.GetActiveText ());
			withEntry.Active = 0;
			Assert.AreEqual ("alpha", withEntry.GetActiveText ());
		}
	}
}
