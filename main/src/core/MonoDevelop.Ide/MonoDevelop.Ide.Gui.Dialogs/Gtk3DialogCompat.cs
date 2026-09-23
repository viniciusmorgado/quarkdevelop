//
// Gtk3DialogCompat.cs
//
// Copyright (c) 2026 MonoDevelop contributors
//
// Small GTK2 -> GTK3 (GtkSharp 3.24) helpers for the Ide dialogs and option panels (ADR 0011).
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

namespace MonoDevelop.Ide.Gui.Dialogs
{
	internal static class Gtk3DialogCompat
	{
		/// <summary>GTK2 <c>widget.Style.Base (state)</c>: the background of text and list views.</summary>
		public static Gdk.Color GetStyleBase (Gtk.Widget widget, Gtk.StateFlags state)
		{
			var ctx = widget.StyleContext;
			ctx.Save ();
			ctx.AddClass ("view");
			// GTK3: themes may paint backgrounds with images; the CSS background colour is the closest match
#pragma warning disable CS0612, CS0618 // gtk_style_context_get_background_color is deprecated since 3.16
			var c = ctx.GetBackgroundColor (state);
#pragma warning restore CS0612, CS0618
			ctx.Restore ();
			return ToGdkColor (c);
		}

		/// <summary>GTK2 <c>widget.Style.Background (state)</c>.</summary>
		public static Gdk.Color GetStyleBackground (Gtk.Widget widget, Gtk.StateFlags state)
		{
#pragma warning disable CS0612, CS0618 // gtk_style_context_get_background_color is deprecated since 3.16
			return ToGdkColor (widget.StyleContext.GetBackgroundColor (state));
#pragma warning restore CS0612, CS0618
		}

		/// <summary>
		/// GTK2 <c>ComboBox.ActiveText</c> on a model-based combo: the string in column 0 of the active row
		/// (GTK3 only has it on ComboBoxText).
		/// </summary>
		public static string GetActiveText (Gtk.ComboBox combo)
		{
			if (combo.Model == null || !combo.GetActiveIter (out Gtk.TreeIter iter))
				return null;
			return combo.Model.GetValue (iter, 0) as string;
		}

		public static Gdk.Color ToGdkColor (Gdk.RGBA c)
		{
			return new Gdk.Color ((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));
		}
	}
}
