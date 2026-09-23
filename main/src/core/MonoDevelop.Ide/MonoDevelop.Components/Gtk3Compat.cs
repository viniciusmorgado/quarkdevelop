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

		/// <summary>
		/// GTK2 <c>widget.State = state</c> (<c>gtk_widget_set_state</c>, not bound by GtkSharp 3): replaces the
		/// state-type flags of the widget and keeps the others (direction, backdrop, ...), as GTK3's deprecated
		/// implementation does.
		/// </summary>
		public static void SetState (this Gtk.Widget widget, Gtk.StateType state)
		{
			const Gtk.StateFlags stateTypeFlags = Gtk.StateFlags.Active | Gtk.StateFlags.Prelight | Gtk.StateFlags.Selected |
				Gtk.StateFlags.Insensitive | Gtk.StateFlags.Inconsistent | Gtk.StateFlags.Focused;
			if (widget.State == state)
				return;
			var flags = state.ToStateFlags ();
			widget.UnsetStateFlags (stateTypeFlags & ~flags);
			if (flags != Gtk.StateFlags.Normal)
				widget.SetStateFlags (flags, false);
		}

		/// <summary>The foreground (text) color of <paramref name="widget"/> in a GTK2 state (GTK2 <c>Style.Text/Fg</c>).</summary>
		public static Cairo.Color GetStyleTextColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			var c = widget.StyleContext.GetColor (state.ToStateFlags ());
			return new Cairo.Color (c.Red, c.Green, c.Blue, c.Alpha);
		}

		/// <summary>
		/// GTK2 <c>Style.Background (state)</c> (<c>bg[state]</c>): the opaque background color the widget shows.
		/// GTK3 widgets without a background of their own are transparent, so the first ancestor with a
		/// background is used, then the theme's <c>theme_bg_color</c>.
		/// </summary>
		public static Cairo.Color GetStyleBackgroundColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			for (var w = widget; w != null; w = w.Parent) {
				var c = QueryStyleColor (w, state, false, true);
				if (c.Alpha > 0)
					return new Cairo.Color (c.Red, c.Green, c.Blue);
			}
			return LookupThemeColor (widget, "theme_bg_color", new Cairo.Color (0.93, 0.93, 0.93));
		}

		/// <summary>
		/// GTK2 <c>Style.Base (state)</c> (<c>base[state]</c>): the background of text views and entries. As GTK3's
		/// own GtkStyle does, it is the background of the widget with the <c>entry</c> style class.
		/// </summary>
		public static Cairo.Color GetStyleBaseColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			var c = QueryStyleColor (widget, state, true, true);
			if (c.Alpha > 0)
				return new Cairo.Color (c.Red, c.Green, c.Blue);
			return LookupThemeColor (widget, "theme_base_color", new Cairo.Color (1, 1, 1));
		}

		/// <summary>GTK2 <c>Style.Light (state)</c>: the background color shaded by 1.3, as GTK2 and GTK3's GtkStyle compute it.</summary>
		public static Cairo.Color GetStyleLightColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			return Shade (widget.GetStyleBackgroundColor (state), LightnessMult);
		}

		/// <summary>GTK2 <c>Style.Dark (state)</c>: the background color shaded by 0.7, as GTK2 and GTK3's GtkStyle compute it.</summary>
		public static Cairo.Color GetStyleDarkColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			return Shade (widget.GetStyleBackgroundColor (state), DarknessMult);
		}

		/// <summary>GTK2 <c>Style.Mid (state)</c>: halfway between the light and dark colors.</summary>
		public static Cairo.Color GetStyleMidColor (this Gtk.Widget widget, Gtk.StateType state)
		{
			var bg = widget.GetStyleBackgroundColor (state);
			var light = Shade (bg, LightnessMult);
			var dark = Shade (bg, DarknessMult);
			return new Cairo.Color ((light.R + dark.R) / 2, (light.G + dark.G) / 2, (light.B + dark.B) / 2);
		}

		/// <summary>A GDK RGBA color (for the GTK3 <c>Override*Color</c> methods) from a Cairo color.</summary>
		public static Gdk.RGBA ToGdkRgba (this Cairo.Color color)
		{
			return new Gdk.RGBA { Red = color.R, Green = color.G, Blue = color.B, Alpha = color.A };
		}

		/// <summary>
		/// GTK2 <c>gtk_combo_box_get_active_text</c> (removed in GTK3): the entry text of a combo box with an
		/// entry, otherwise the text column of the active row (<c>null</c> when nothing is active).
		/// </summary>
		public static string GetActiveText (this Gtk.ComboBox combo)
		{
			if (combo.HasEntry)
				return (combo.Child as Gtk.Entry)?.Text;
			if (combo.Model == null || !combo.GetActiveIter (out Gtk.TreeIter iter))
				return null;
			return combo.Model.GetValue (iter, Math.Max (combo.EntryTextColumn, 0)) as string;
		}

		const double LightnessMult = 1.3;
		const double DarknessMult = 0.7;

		static Gdk.RGBA QueryStyleColor (Gtk.Widget widget, Gtk.StateType state, bool entry, bool background)
		{
			var style = widget.StyleContext;
			var flags = state.ToStateFlags ();
			style.Save ();
			if (entry)
				style.AddClass ("entry");
			else
				style.RemoveClass ("entry");
			style.State = flags;
			var c = background ? style.GetBackgroundColor (flags) : style.GetColor (flags);
			style.Restore ();
			return c;
		}

		static Cairo.Color LookupThemeColor (Gtk.Widget widget, string name, Cairo.Color fallback)
		{
			if (widget.StyleContext.LookupColor (name, out Gdk.RGBA c))
				return new Cairo.Color (c.Red, c.Green, c.Blue);
			return fallback;
		}

		/// <summary>GTK's <c>_gtk_style_shade</c>: scales lightness and saturation (HLS) by <paramref name="k"/>.</summary>
		internal static Cairo.Color Shade (Cairo.Color color, double k)
		{
			double r = color.R, g = color.G, b = color.B;
			RgbToHls (ref r, ref g, ref b);
			g = Math.Min (Math.Max (g * k, 0), 1);
			b = Math.Min (Math.Max (b * k, 0), 1);
			HlsToRgb (ref r, ref g, ref b);
			return new Cairo.Color (r, g, b, color.A);
		}

		static void RgbToHls (ref double r, ref double g, ref double b)
		{
			double red = r, green = g, blue = b;
			double max = Math.Max (red, Math.Max (green, blue));
			double min = Math.Min (red, Math.Min (green, blue));
			double l = (max + min) / 2, s = 0, h = 0;
			if (max != min) {
				s = l <= 0.5 ? (max - min) / (max + min) : (max - min) / (2 - max - min);
				double delta = max - min;
				if (red == max)
					h = (green - blue) / delta;
				else if (green == max)
					h = 2 + (blue - red) / delta;
				else
					h = 4 + (red - green) / delta;
				h *= 60;
				if (h < 0)
					h += 360;
			}
			r = h;
			g = l;
			b = s;
		}

		static void HlsToRgb (ref double h, ref double l, ref double s)
		{
			double lightness = l, saturation = s;
			double m2 = lightness <= 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
			double m1 = 2 * lightness - m2;
			if (saturation == 0) {
				h = l = s = lightness;
				return;
			}
			double red = HueToChannel (m1, m2, h + 120);
			double green = HueToChannel (m1, m2, h);
			double blue = HueToChannel (m1, m2, h - 120);
			h = red;
			l = green;
			s = blue;
		}

		static double HueToChannel (double m1, double m2, double hue)
		{
			while (hue > 360)
				hue -= 360;
			while (hue < 0)
				hue += 360;
			if (hue < 60)
				return m1 + (m2 - m1) * hue / 60;
			if (hue < 180)
				return m2;
			if (hue < 240)
				return m1 + (m2 - m1) * (240 - hue) / 60;
			return m1;
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
