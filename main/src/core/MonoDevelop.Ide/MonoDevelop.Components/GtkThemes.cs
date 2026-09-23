//
// GtkThemes.cs
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
using System.IO;
using System.Linq;

namespace MonoDevelop.Components
{
	/// <summary>
	/// GTK 3 themes, named like GTK_THEME: "Name" or "Name:dark" for the dark variant (a theme's
	/// gtk-dark.css, selected with gtk-application-prefer-dark-theme). GTK 3 themes live in
	/// &lt;themes dir&gt;/&lt;Name&gt;/gtk-3.0 (or gtk-3.N) with a gtk.css; GTK 2's gtk_rc_get_theme_dir and gtkrc are gone.
	/// </summary>
	static class GtkThemes
	{
		public const string DarkVariantSuffix = ":dark";
		public const string DefaultTheme = "Adwaita";

		/// <summary>Themes compiled into GTK 3 as resources, available without a directory.</summary>
		static readonly string [] builtInThemes = { "Adwaita", "Adwaita" + DarkVariantSuffix, "HighContrast", "HighContrastInverse" };

		/// <summary>The directories GTK 3 searches for themes, in its order.</summary>
		public static IEnumerable<string> GetSearchDirectories ()
		{
			var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
			var dataHome = Environment.GetEnvironmentVariable ("XDG_DATA_HOME");
			if (string.IsNullOrEmpty (dataHome))
				dataHome = Path.Combine (home, ".local", "share");
			yield return Path.Combine (dataHome, "themes");
			yield return Path.Combine (home, ".themes");
			var dataDirs = Environment.GetEnvironmentVariable ("XDG_DATA_DIRS");
			if (string.IsNullOrEmpty (dataDirs))
				dataDirs = "/usr/local/share:/usr/share";
			foreach (var dir in dataDirs.Split (':', StringSplitOptions.RemoveEmptyEntries))
				yield return Path.Combine (dir, "themes");
		}

		/// <summary>Installed and built-in GTK 3 themes, including dark variants, sorted.</summary>
		public static List<string> FindThemes (IEnumerable<string> searchDirectories)
		{
			var themes = new SortedSet<string> (builtInThemes, StringComparer.Ordinal);
			foreach (var themesDir in searchDirectories) {
				if (string.IsNullOrEmpty (themesDir) || !Directory.Exists (themesDir))
					continue;
				foreach (var dir in Directory.GetDirectories (themesDir)) {
					var gtk3 = GetGtk3Directory (dir);
					if (gtk3 == null)
						continue;
					var name = Path.GetFileName (dir);
					themes.Add (name);
					if (File.Exists (Path.Combine (gtk3, "gtk-dark.css")))
						themes.Add (name + DarkVariantSuffix);
				}
			}
			return themes.ToList ();
		}

		/// <summary>The directory of an installed theme (null for a built-in or missing theme).</summary>
		public static string FindThemeDirectory (string theme, IEnumerable<string> searchDirectories)
		{
			Parse (theme, out var name, out _);
			return searchDirectories
				.Select (d => Path.Combine (d, name))
				.FirstOrDefault (d => GetGtk3Directory (d) != null);
		}

		static string GetGtk3Directory (string themeDir)
		{
			if (!Directory.Exists (themeDir))
				return null;
			return Directory.GetDirectories (themeDir, "gtk-3.*")
				.FirstOrDefault (d => File.Exists (Path.Combine (d, "gtk.css")));
		}

		public static void Parse (string theme, out string name, out bool dark)
		{
			dark = theme.EndsWith (DarkVariantSuffix, StringComparison.Ordinal);
			name = dark ? theme.Substring (0, theme.Length - DarkVariantSuffix.Length) : theme;
		}

		/// <summary>The theme the settings select, as "Name" or "Name:dark".</summary>
		public static string GetCurrent (Gtk.Settings settings)
		{
			return settings.ThemeName + (settings.ApplicationPreferDarkTheme ? DarkVariantSuffix : "");
		}

		public static void Apply (Gtk.Settings settings, string theme)
		{
			Parse (theme, out var name, out var dark);
			if (settings.ThemeName != name)
				settings.ThemeName = name;
			if (settings.ApplicationPreferDarkTheme != dark)
				settings.ApplicationPreferDarkTheme = dark;
		}

		public static string GetDisplayName (string theme)
		{
			Parse (theme, out var name, out var dark);
			return dark ? Core.GettextCatalog.GetString ("{0} (dark)", name) : name;
		}
	}
}
