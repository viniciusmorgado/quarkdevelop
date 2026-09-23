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
	}

	/// <summary>
	/// A context drawing on the same target as another one. The owner's state is saved on creation and
	/// restored on dispose, and disposing it only drops a reference, so ported code can keep its
	/// <c>using (var ctx = CairoHelper.Create (window))</c> blocks around the context GTK3 hands out.
	/// </summary>
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

	/// <summary>GTK2-style helpers on top of the GTK3 API, for code ported by scripts/tools/gtk3-codemod.py.</summary>
	public static class Gtk3CompatExtensions
	{
		/// <summary>GTK2 <c>gtk_widget_size_request</c>: the widget's natural size.</summary>
		public static Gtk.Requisition SizeRequest (this Gtk.Widget widget)
		{
			widget.GetPreferredSize (out _, out Gtk.Requisition natural);
			return natural;
		}

		/// <summary>
		/// A context on the same target as <paramref name="cr"/> whose state is restored when it is disposed;
		/// stands in for GTK2's <c>CairoHelper.Create (drawable)</c> in code that receives a context from GTK3.
		/// </summary>
		public static Cairo.Context CreateSharedContext (this Cairo.Context cr)
		{
			return new Gtk3DrawContext (cr, 0, 0);
		}

		/// <summary>The GTK3 state flags matching a GTK2 state type.</summary>
		public static Gtk.StateFlags ToStateFlags (this Gtk.StateType state)
		{
			switch (state) {
			case Gtk.StateType.Active:
				return Gtk.StateFlags.Active;
			case Gtk.StateType.Prelight:
				return Gtk.StateFlags.Prelight;
			case Gtk.StateType.Selected:
				return Gtk.StateFlags.Selected;
			case Gtk.StateType.Insensitive:
				return Gtk.StateFlags.Insensitive;
			case Gtk.StateType.Inconsistent:
				return Gtk.StateFlags.Inconsistent;
			case Gtk.StateType.Focused:
				return Gtk.StateFlags.Focused;
			default:
				return Gtk.StateFlags.Normal;
			}
		}

		/// <summary>The foreground (text) color of <paramref name="widget"/> in a GTK2 state (GTK2 <c>Style.Text/Fg</c>).</summary>
		public static Cairo.Color GetStyleTextColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			var c = widget.StyleContext.GetColor (state.ToStateFlags ());
			return new Cairo.Color (c.Red, c.Green, c.Blue, c.Alpha);
		}

		/// <summary>
		/// GTK2 <c>drawable.DrawLayout (widget.Style.TextGC (state), x, y, layout)</c>: renders the layout with
		/// the widget's style in the given state.
		/// </summary>
		public static void DrawLayout (this Cairo.Context cr, Gtk.Widget widget, Gtk.StateType state, double x, double y, Pango.Layout layout)
		{
			var style = widget.StyleContext;
			style.Save ();
			style.State = state.ToStateFlags ();
			style.RenderLayout (cr, x, y, layout);
			style.Restore ();
		}

		/// <summary>
		/// GTK2 <c>gtk_cell_renderer_get_size</c> (deprecated in GTK3, not bound by GtkSharp): the renderer's
		/// natural size and its offsets inside <paramref name="cellArea"/> (which may be empty).
		/// </summary>
		public static void GetSize (this Gtk.CellRenderer cell, Gtk.Widget widget, ref Gdk.Rectangle cellArea, out int xOffset, out int yOffset, out int width, out int height)
		{
			cell.GetPreferredWidth (widget, out _, out width);
			cell.GetPreferredHeightForWidth (widget, width, out _, out height);
			cell.Gtk3CalcOffset (widget, cellArea, width, height, out xOffset, out yOffset);
		}

		/// <summary>GTK's <c>_gtk_cell_renderer_calc_offset</c>: where a cell of the given size goes inside <paramref name="cellArea"/>.</summary>
		public static void Gtk3CalcOffset (this Gtk.CellRenderer cell, Gtk.Widget widget, Gdk.Rectangle cellArea, int width, int height, out int xOffset, out int yOffset)
		{
			cell.GetAlignment (out float xalign, out float yalign);
			if (widget != null && widget.Direction == Gtk.TextDirection.Rtl)
				xalign = 1.0f - xalign;
			xOffset = Math.Max ((int)(xalign * (cellArea.Width - width)), 0);
			yOffset = Math.Max ((int)(yalign * (cellArea.Height - height)), 0);
		}
	}
}
