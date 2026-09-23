//
// Gtk3Compat.cs
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

namespace MonoDevelop.Components
{
	/// <summary>
	/// Stands in for GTK2's Gdk.EventExpose inside drawing code ported to GTK3's
	/// <c>OnDrawn (Cairo.Context)</c> by scripts/tools/gtk3-codemod.py (ADR 0011, task T071).
	/// GTK2 code drew in GdkWindow coordinates (offset by the allocation for widgets without a
	/// window); GTK3 hands a context whose origin is the widget's allocation. The contexts created
	/// here undo that translation so ported code keeps its coordinates.
	/// </summary>
	public sealed class Gtk3ExposeEvent
	{
		readonly Gtk.Widget widget;
		readonly Cairo.Context cr;

		public Gtk3ExposeEvent (Gtk.Widget widget, Cairo.Context cr)
		{
			this.widget = widget ?? throw new ArgumentNullException (nameof (widget));
			this.cr = cr ?? throw new ArgumentNullException (nameof (cr));
		}

		/// <summary>The context GTK passed to OnDrawn.</summary>
		public Cairo.Context Context => cr;

		/// <summary>The GdkWindow the widget draws on (its own or its parent's).</summary>
		public Gdk.Window Window => widget.Window;

		/// <summary>X offset between GTK2 window coordinates and GTK3 widget coordinates.</summary>
		public int OffsetX => widget.HasWindow ? 0 : widget.Allocation.X;

		/// <summary>Y offset between GTK2 window coordinates and GTK3 widget coordinates.</summary>
		public int OffsetY => widget.HasWindow ? 0 : widget.Allocation.Y;

		/// <summary>The area to redraw, in GTK2 (window) coordinates.</summary>
		public Gdk.Rectangle Area {
			get {
				var clip = cr.ClipExtents ();
				return new Gdk.Rectangle (
					(int)Math.Floor (clip.X) + OffsetX, (int)Math.Floor (clip.Y) + OffsetY,
					(int)Math.Ceiling (clip.Width), (int)Math.Ceiling (clip.Height));
			}
		}

		/// <summary>
		/// A context drawing on the same target as the GTK3 one, in GTK2 (window) coordinates. Its state
		/// is saved on creation and restored when it is disposed, so <c>using (var ctx = ...)</c> works.
		/// </summary>
		public Cairo.Context CreateContext ()
		{
			return new Gtk3DrawContext (cr, -OffsetX, -OffsetY);
		}

		sealed class Gtk3DrawContext : Cairo.Context
		{
			readonly Cairo.Context owner;
			bool restored;

			public Gtk3DrawContext (Cairo.Context owner, double dx, double dy) : base (owner.Handle, false)
			{
				this.owner = owner;
				owner.Save ();
				if (dx != 0 || dy != 0)
					owner.Translate (dx, dy);
			}

			protected override void Dispose (bool disposing)
			{
				if (!restored) {
					restored = true;
					owner.Restore ();
				}
				base.Dispose (disposing);
			}
		}
	}

	/// <summary>GTK2-style helpers on top of the GTK3 API, for code ported by scripts/tools/gtk3-codemod.py.</summary>
	public static class Gtk3CompatExtensions
	{
		/// <summary>GTK2 <c>gtk_widget_size_request</c>: the widget's natural size.</summary>
		public static Gtk.Requisition SizeRequest (this Gtk.Widget widget)
		{
			widget.GetPreferredSize (out _, out Gtk.Requisition natural);
			return natural;
		}
	}
}
