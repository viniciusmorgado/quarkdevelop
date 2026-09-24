//
// GettextTests.cs
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
using System.IO;
using MonoDevelop.Gettext;
using MonoDevelop.Gettext.Editor;
using NUnit.Framework;

// StyleContext.GetBackgroundColor (deprecated in GTK 3.16) reads the background CSS gives an entry.
#pragma warning disable CS0612

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T098: the Gettext add-in without the Autotools and Deployment add-ins reads and writes PO catalogs (its
	/// catalog editor needs the workbench; docs/evidence/M5/T098-gettext-po-editor.png shows it in the IDE).
	/// </summary>
	[TestFixture]
	public class GettextTests
	{
		const string Po = "msgid \"\"\nmsgstr \"\"\n\"Content-Type: text/plain; charset=UTF-8\\n\"\n\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"\n\n"
			+ "#: Program.cs:10\nmsgid \"Hello\"\nmsgstr \"Hallo\"\n\n"
			+ "#, fuzzy\nmsgid \"World\"\nmsgstr \"Welt\"\n\n"
			+ "msgid \"Missing\"\nmsgstr \"\"\n";

		string directory;

		[SetUp]
		public void CreateDirectory ()
		{
			directory = Path.Combine (Path.GetTempPath (), "md-gettext-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
		}

		[TearDown]
		public void DeleteDirectory ()
		{
			Directory.Delete (directory, true);
		}

		string WritePo ()
		{
			var file = Path.Combine (directory, "de.po");
			File.WriteAllText (file, Po);
			return file;
		}

		[Test]
		public void CatalogReadsTranslationsAndWritesThemBack ()
		{
			var file = WritePo ();
			var catalog = new Catalog (null);

			Assert.IsTrue (catalog.Load (null, file));

			Assert.AreEqual (3, catalog.Count);
			Assert.AreEqual ("Hallo", catalog.FindItem ("Hello").GetTranslation (0));
			Assert.IsTrue (catalog.FindItem ("World").IsFuzzy);
			Assert.IsFalse (catalog.FindItem ("Missing").IsTranslated);
			Assert.AreEqual (2, catalog.PluralFormsCount);

			catalog.FindItem ("Missing").SetTranslation ("Fehlt", 0);
			var saved = Path.Combine (directory, "saved.po");
			Assert.IsTrue (catalog.Save (saved));

			var reloaded = new Catalog (null);
			Assert.IsTrue (reloaded.Load (null, saved));
			Assert.AreEqual ("Fehlt", reloaded.FindItem ("Missing").GetTranslation (0));
			Assert.AreEqual ("Hallo", reloaded.FindItem ("Hello").GetTranslation (0));
		}

		[Test]
		public void BaseColorIsSetAndRemovedOnAnEntry ()
		{
			GtkFixture.Require ();
			var entry = new Gtk.Entry ();
			try {
				var themed = entry.StyleContext.GetBackgroundColor (Gtk.StateFlags.Normal);

				Gtk3BaseColor.Set (entry, new Gdk.Color (204, 0, 0));
				var red = entry.StyleContext.GetBackgroundColor (Gtk.StateFlags.Normal);
				Gtk3BaseColor.Set (entry, null);
				var restored = entry.StyleContext.GetBackgroundColor (Gtk.StateFlags.Normal);

				Assert.AreEqual (0.8, red.Red, 0.01);
				Assert.AreEqual (0, red.Green, 0.01);
				Assert.AreEqual (0, red.Blue, 0.01);
				Assert.AreEqual (themed.Red, restored.Red, 0.001);
				Assert.AreEqual (themed.Green, restored.Green, 0.001);
				Assert.AreEqual (themed.Blue, restored.Blue, 0.001);
			} finally {
				entry.Destroy ();
			}
		}
	}
}
