//
// Catalog.cs
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

namespace Mono.Addins.GuiGtk3
{
	/// <summary>
	/// Translation hooks for the add-in manager UI (MonoDevelop local patch, see UPSTREAM.md). Upstream
	/// used Mono.Unix.Catalog, i.e. the host application's native gettext domain; the host now plugs in
	/// its own catalog (MonoDevelop: GettextCatalog). Untranslated text is returned when no hook is set.
	/// </summary>
	public static class Localization
	{
		public static Func<string, string> GetStringHandler { get; set; }

		public static Func<string, string, int, string> GetPluralStringHandler { get; set; }
	}

	static class Catalog
	{
		public static string GetString (string phrase)
		{
			var handler = Localization.GetStringHandler;
			return handler != null ? handler (phrase) : phrase;
		}

		public static string GetPluralString (string singular, string plural, int n)
		{
			var handler = Localization.GetPluralStringHandler;
			if (handler != null)
				return handler (singular, plural, n);
			return n == 1 ? singular : plural;
		}
	}
}

namespace Mono.Addins.GuiGtk3
{
	/// <summary>
	/// GtkStyle colours for GtkSharp 3.24, which dropped the deprecated GtkStyle accessors
	/// (MonoDevelop local patch). Colours come from the widget's StyleContext.
	/// </summary>
	static class StyleColors
	{
		public static Gdk.Color Background (Gtk.Widget widget)
		{
			return ToGdkColor (widget.StyleContext.GetBackgroundColor (Gtk.StateFlags.Normal));
		}

		static Gdk.Color ToGdkColor (Gdk.RGBA rgba)
		{
			return new Gdk.Color ((byte)(rgba.Red * 255), (byte)(rgba.Green * 255), (byte)(rgba.Blue * 255));
		}
	}
}
