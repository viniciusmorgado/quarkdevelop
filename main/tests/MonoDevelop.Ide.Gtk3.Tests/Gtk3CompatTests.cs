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
	}
}
