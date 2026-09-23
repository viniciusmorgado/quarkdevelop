//
// Gtk3IdeCompat.cs
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

// Namespace MonoDevelop.Ide (not .Gui) so that code in every MonoDevelop.Ide.* namespace sees the
// extension methods without an extra using.
namespace MonoDevelop.Ide
{
	/// <summary>
	/// GTK2 <c>Gtk.Style</c> accessors (Bg/Fg/Base/Text/Light, FontDescription, Paint*) rebuilt on top of
	/// the GTK3 <c>StyleContext</c>, plus GTK2 combo box text helpers, for MonoDevelop.Ide code ported to
	/// GTK3 in M5b. GTK3 has no separate "base"/"text" colors: they are read with the "view" style class,
	/// which is what GTK3 themes use for the content of entries, tree views and text views.
	/// </summary>
	internal static class Gtk3IdeCompat
	{
		static Gtk.Window fallbackStyleWindow;

		/// <summary>GTK2 <c>Style.Background (state)</c> (the "bg" color).</summary>
		public static Gdk.Color GetStyleBackground (this Gtk.Widget widget, Gtk.StateType state)
		{
			return ToGdkColor (GetThemeColor (widget, state.ToStateFlags (), null, true));
		}

		/// <summary>GTK2 <c>Style.Foreground (state)</c> (the "fg" color).</summary>
		public static Gdk.Color GetStyleForeground (this Gtk.Widget widget, Gtk.StateType state)
		{
			return ToGdkColor (GetThemeColor (widget, state.ToStateFlags (), null, false));
		}

		/// <summary>GTK2 <c>Style.Base (state)</c>: the background of text/list content.</summary>
		public static Gdk.Color GetStyleBase (this Gtk.Widget widget, Gtk.StateType state)
		{
			return ToGdkColor (GetThemeColor (widget, state.ToStateFlags (), "view", true));
		}

		/// <summary>GTK2 <c>Style.Text (state)</c>: the foreground of text/list content.</summary>
		public static Gdk.Color GetStyleText (this Gtk.Widget widget, Gtk.StateType state)
		{
			return ToGdkColor (GetThemeColor (widget, state.ToStateFlags (), "view", false));
		}

		/// <summary>
		/// GTK2 <c>Style.Light (state)</c>. GTK2 computed it as the "bg" color with its lightness scaled by 1.3;
		/// GTK3 has no such color, so it is computed the same way from the GTK3 background.
		/// </summary>
		public static Gdk.Color GetStyleLight (this Gtk.Widget widget, Gtk.StateType state)
		{
			var bg = GetThemeColor (widget, state.ToStateFlags (), null, true);
			var hsl = new HslColor (bg.Red, bg.Green, bg.Blue);
			hsl.L = Math.Min (1.0, hsl.L * 1.3);
			return hsl;
		}

		/// <summary>
		/// A color of the widget's theme in the given state. GTK2 styles always had opaque bg/base colors,
		/// while many GTK3 widgets (labels, boxes) have a transparent background: in that case the parents
		/// are asked, and finally a toplevel window, whose themed background is always painted.
		/// </summary>
		static Gdk.RGBA GetThemeColor (Gtk.Widget widget, Gtk.StateFlags state, string styleClass, bool background)
		{
			if (!background)
				return GetContextColor (widget.StyleContext, state, styleClass, false);
			for (var w = widget; w != null; w = w.Parent) {
				var c = GetContextColor (w.StyleContext, state, styleClass, true);
				if (c.Alpha > 0)
					return c;
			}
			if (fallbackStyleWindow == null)
				fallbackStyleWindow = new Gtk.Window (Gtk.WindowType.Toplevel);
			return GetContextColor (fallbackStyleWindow.StyleContext, state, styleClass, true);
		}

		static Gdk.RGBA GetContextColor (Gtk.StyleContext ctx, Gtk.StateFlags state, string styleClass, bool background)
		{
			ctx.Save ();
			ctx.State = state;
			if (styleClass != null)
				ctx.AddClass (styleClass);
			var c = background ? ctx.GetBackgroundColor (state) : ctx.GetColor (state);
			ctx.Restore ();
			return c;
		}

		/// <summary>GTK2 <c>Style.FontDescription</c>: the widget's font in its current state.</summary>
		public static Pango.FontDescription GetStyleFont (this Gtk.Widget widget)
		{
			var ctx = widget.StyleContext;
			return ctx.GetFont (ctx.State);
		}

		/// <summary>
		/// GTK2 <c>Gtk.Style.PaintArrow</c>: draws a themed arrow centered in the given rectangle
		/// (GTK3 arrows are square, so the arrow takes the rectangle's smaller side).
		/// </summary>
		public static void RenderArrow (this Gtk.Widget widget, Cairo.Context cr, Gtk.StateType state, Gtk.ArrowType arrow, double x, double y, double width, double height)
		{
			double angle;
			switch (arrow) {
			case Gtk.ArrowType.Up:
				angle = 0;
				break;
			case Gtk.ArrowType.Right:
				angle = Math.PI / 2;
				break;
			case Gtk.ArrowType.Down:
				angle = Math.PI;
				break;
			case Gtk.ArrowType.Left:
				angle = 3 * Math.PI / 2;
				break;
			default:
				return;
			}
			double size = Math.Min (width, height);
			var ctx = widget.StyleContext;
			ctx.Save ();
			ctx.State = state.ToStateFlags ();
			ctx.RenderArrow (cr, angle, x + (width - size) / 2, y + (height - size) / 2, size);
			ctx.Restore ();
		}

		/// <summary>GTK2 <c>Gtk.Style.PaintResizeGrip</c>: draws a themed resize grip (GTK3 "grip" handle).</summary>
		public static void RenderResizeGrip (this Gtk.Widget widget, Cairo.Context cr, Gtk.StateType state, Gdk.WindowEdge edge, double x, double y, double width, double height)
		{
			var ctx = widget.StyleContext;
			ctx.Save ();
			ctx.State = state.ToStateFlags ();
			ctx.AddClass ("grip");
			ctx.JunctionSides = edge == Gdk.WindowEdge.SouthWest ? Gtk.JunctionSides.CornerBottomLeft : Gtk.JunctionSides.CornerBottomRight;
			ctx.RenderHandle (cr, x, y, width, height);
			ctx.Restore ();
		}

		/// <summary>
		/// GTK2 <c>gtk_combo_box_append_text</c> on a combo whose model is a list store with a string column
		/// (what <c>ComboBox.NewText</c> created); a <c>Gtk.ComboBoxText</c> appends through its own API.
		/// </summary>
		public static void AppendText (this Gtk.ComboBox combo, string text)
		{
			if (combo is Gtk.ComboBoxText comboText) {
				comboText.AppendText (text);
				return;
			}
			((Gtk.ListStore)combo.Model).AppendValues (text);
		}

		/// <summary>GTK2 <c>gtk_combo_box_get_active_text</c> for combos built like <see cref="AppendText"/>.</summary>
		public static string GetActiveText (this Gtk.ComboBox combo)
		{
			if (combo is Gtk.ComboBoxText comboText)
				return comboText.ActiveText;
			if (!combo.GetActiveIter (out Gtk.TreeIter iter))
				return null;
			return combo.Model.GetValue (iter, 0) as string;
		}

		/// <summary>Converts a GTK3 RGBA to the GTK2 Gdk.Color (alpha dropped).</summary>
		public static Gdk.Color ToGdkColor (this Gdk.RGBA c)
		{
			return new Gdk.Color ((byte)Math.Round (Clamp (c.Red) * 255), (byte)Math.Round (Clamp (c.Green) * 255), (byte)Math.Round (Clamp (c.Blue) * 255));
		}

		static double Clamp (double v)
		{
			return v < 0 ? 0 : v > 1 ? 1 : v;
		}
	}
}
