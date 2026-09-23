//
// GtkCss.cs
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

namespace MonoDevelop.Components
{
	/// <summary>
	/// Per-widget styles on GTK 3: CSS providers attached to a widget's own style context. They replace the
	/// GTK 2 <c>gtk_rc_parse_string</c> "style"/"widget" rules, which GTK 3 ignores. Style properties are
	/// written as <c>-GtkClass-property: value;</c>.
	/// </summary>
	static class GtkCss
	{
		static readonly Dictionary<string, Gtk.CssProvider> providers = new Dictionary<string, Gtk.CssProvider> (StringComparer.Ordinal);

		/// <summary>A provider for the given CSS, shared by all widgets that use the same CSS (GTK thread only).</summary>
		public static Gtk.CssProvider GetProvider (string css)
		{
			if (!providers.TryGetValue (css, out var provider)) {
				provider = new Gtk.CssProvider ();
				bool loaded;
				try {
					loaded = provider.LoadFromData (css);
				} catch (GLib.GException e) {
					throw new ArgumentException ("Invalid CSS: " + css, nameof (css), e);
				}
				if (!loaded)
					throw new ArgumentException ("Invalid CSS: " + css, nameof (css));
				providers [css] = provider;
			}
			return provider;
		}

		/// <summary>
		/// Sets the style of <paramref name="widget"/> registered under <paramref name="key"/>, replacing the one
		/// set before with the same key. A null <paramref name="css"/> removes it.
		/// </summary>
		public static void SetStyle (Gtk.Widget widget, string key, string css)
		{
			var provider = css == null ? null : GetProvider (css);
			var current = widget.Data [key] as Gtk.CssProvider;
			if (current == provider)
				return;
			if (current != null)
				widget.StyleContext.RemoveProvider (current);
			if (provider != null)
				widget.StyleContext.AddProvider (provider, Gtk.StyleProviderPriority.Application);
			widget.Data [key] = provider;
		}
	}
}
