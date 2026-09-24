//
// Gtk3BaseColor.cs
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

using System.Globalization;

namespace MonoDevelop.Gettext.Editor
{
	/// <summary>
	/// GTK 3 replacement for GTK 2 <c>Widget.ModifyBase (StateType.Normal, color)</c>: the background of an entry or a
	/// text view set through a CSS provider on the widget's own style context (GTK 3 has no "base" color).
	/// </summary>
	static class Gtk3BaseColor
	{
		const string DataKey = "MonoDevelop.Gettext.Editor.Gtk3BaseColor";

		/// <summary>Sets the base color of <paramref name="widget"/>; null restores the theme color.</summary>
		public static void Set (Gtk.Widget widget, Gdk.Color? color)
		{
			if (widget.Data[DataKey] is Gtk.CssProvider current)
				widget.StyleContext.RemoveProvider (current);
			widget.Data[DataKey] = null;
			if (color == null)
				return;

			var c = color.Value;
			var css = string.Format (
				CultureInfo.InvariantCulture,
				"entry, textview text {{ background-color: #{0:x2}{1:x2}{2:x2}; background-image: none; }}",
				c.Red >> 8, c.Green >> 8, c.Blue >> 8);
			var provider = new Gtk.CssProvider ();
			provider.LoadFromData (css);
			widget.StyleContext.AddProvider (provider, Gtk.StyleProviderPriority.Application);
			widget.Data[DataKey] = provider;
		}
	}
}
