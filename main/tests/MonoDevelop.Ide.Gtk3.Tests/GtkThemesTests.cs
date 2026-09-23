//
// GtkThemesTests.cs
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
using System.Collections;
using System.IO;
using MonoDevelop.Components;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T083: GTK 3 theme discovery and dark variants (GtkThemes), and the per-widget CSS styles
	/// (GtkCss) that replace GTK 2's Gtk.Rc.
	/// </summary>
	[TestFixture]
	public class GtkThemesTests
	{
		static readonly string[] ExpectedThemes = { "Plain", "WithDark", "WithDark:dark", "Adwaita", "Adwaita:dark", "HighContrast" };

		string dir;

		string[] SearchDirs => new[] { dir };

		[SetUp]
		public void SetUp ()
		{
			dir = Path.Combine (Path.GetTempPath (), "gtk-themes-" + Guid.NewGuid ().ToString ("N"));
			CreateTheme ("Plain", "gtk-3.0", dark: false);
			CreateTheme ("WithDark", "gtk-3.22", dark: true);
			Directory.CreateDirectory (Path.Combine (dir, "Gtk2Only", "gtk-2.0"));
			File.WriteAllText (Path.Combine (dir, "Gtk2Only", "gtk-2.0", "gtkrc"), "");
			Directory.CreateDirectory (Path.Combine (dir, "NoCss", "gtk-3.0"));
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (dir, true);
		}

		void CreateTheme (string name, string gtkDir, bool dark)
		{
			var path = Path.Combine (dir, name, gtkDir);
			Directory.CreateDirectory (path);
			File.WriteAllText (Path.Combine (path, "gtk.css"), "");
			if (dark)
				File.WriteAllText (Path.Combine (path, "gtk-dark.css"), "");
		}

		[Test]
		public void FindThemesListsGtk3ThemesDarkVariantsAndBuiltInThemes ()
		{
			var themes = GtkThemes.FindThemes (new[] { dir, Path.Combine (dir, "missing") });

			Assert.That (themes, Is.SupersetOf (ExpectedThemes));
			Assert.That (themes, Has.None.StartsWith ("Plain:"));
			Assert.That (themes, Has.No.Member ("Gtk2Only"));
			Assert.That (themes, Has.No.Member ("NoCss"));
			Assert.That (themes, Is.Ordered.Using ((IComparer)StringComparer.Ordinal));
		}

		[Test]
		public void FindThemeDirectoryIgnoresTheVariantAndBuiltInThemes ()
		{
			Assert.AreEqual (Path.Combine (dir, "WithDark"), GtkThemes.FindThemeDirectory ("WithDark:dark", SearchDirs));
			Assert.IsNull (GtkThemes.FindThemeDirectory ("Adwaita", SearchDirs));
			Assert.IsNull (GtkThemes.FindThemeDirectory ("Gtk2Only", SearchDirs));
		}

		[TestCase ("Adwaita", "Adwaita", false)]
		[TestCase ("Adwaita:dark", "Adwaita", true)]
		[TestCase ("Name:with:colons", "Name:with:colons", false)]
		public void ParseSplitsTheDarkVariant (string theme, string name, bool dark)
		{
			GtkThemes.Parse (theme, out var parsedName, out var parsedDark);
			Assert.AreEqual (name, parsedName);
			Assert.AreEqual (dark, parsedDark);
		}

		[Test]
		public void ApplySetsThemeNameAndPreferDarkTheme ()
		{
			GtkFixture.Require ();
			var settings = Gtk.Settings.Default;
			var original = GtkThemes.GetCurrent (settings);
			try {
				GtkThemes.Apply (settings, "Adwaita:dark");
				Assert.AreEqual ("Adwaita", settings.ThemeName);
				Assert.IsTrue (settings.ApplicationPreferDarkTheme);
				Assert.AreEqual ("Adwaita:dark", GtkThemes.GetCurrent (settings));

				GtkThemes.Apply (settings, "Adwaita");
				Assert.IsFalse (settings.ApplicationPreferDarkTheme);
			} finally {
				GtkThemes.Apply (settings, original);
			}
		}

		static int ExpanderSize (Gtk.TreeView tree) => (int)tree.StyleGetProperty ("expander-size");

		[Test]
		public void SetStyleAppliesReplacesAndRemovesWidgetCss ()
		{
			GtkFixture.Require ();
			var tree = new Gtk.TreeView ();
			try {
				int defaultSize = ExpanderSize (tree);
				const string key = "test.expander";

				GtkCss.SetStyle (tree, key, "treeview { -GtkTreeView-expander-size: 5; }");
				Assert.AreEqual (5, ExpanderSize (tree));

				GtkCss.SetStyle (tree, key, "treeview { -GtkTreeView-expander-size: 9; }");
				Assert.AreEqual (9, ExpanderSize (tree), "the style under the same key is replaced");

				GtkCss.SetStyle (tree, key, null);
				Assert.AreEqual (defaultSize, ExpanderSize (tree));
			} finally {
				tree.Destroy ();
			}
		}

		[Test]
		public void InvalidCssIsRejected ()
		{
			GtkFixture.Require ();
			Assert.Throws<ArgumentException> (() => GtkCss.GetProvider ("treeview { color: ; "));
		}
	}
}
