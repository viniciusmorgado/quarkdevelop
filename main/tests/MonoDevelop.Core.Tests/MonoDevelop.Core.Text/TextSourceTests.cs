//
// TextSourceTests.cs
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

#pragma warning disable CS0618 // the text model types are obsolete but still used by the IDE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core.Text
{
	/// <summary>
	/// Task T056: <see cref="TextChange"/> and <see cref="TextChangeEventArgs"/> offset mapping and inversion.
	/// </summary>
	[TestFixture]
	public class TextChangeTests
	{
		[Test]
		public void StringConstructors ()
		{
			var c = new TextChange (4, "old", "newer");
			Assert.AreEqual (4, c.Offset);
			Assert.AreEqual (4, c.NewOffset);
			Assert.AreEqual ("old", c.RemovedText.Text);
			Assert.AreEqual ("newer", c.InsertedText.Text);
			Assert.AreEqual (3, c.RemovalLength);
			Assert.AreEqual (5, c.InsertionLength);
			Assert.AreEqual (2, c.ChangeDelta);
			Assert.AreEqual ("[TextChange: Offset=4, NewOffset=4, RemovedText=old, InsertedText=newer]", c.ToString ());

			c = new TextChange (4, 9, (string)null, (string)null);
			Assert.AreEqual (9, c.NewOffset);
			Assert.AreSame (StringTextSource.Empty, c.RemovedText);
			Assert.AreSame (StringTextSource.Empty, c.InsertedText);
			Assert.AreEqual (0, c.ChangeDelta);

			c = new TextChange (1, (string)null, "x");
			Assert.AreEqual (0, c.RemovalLength);
			Assert.AreEqual (1, c.InsertionLength);

			Assert.Throws<ArgumentOutOfRangeException> (() => new TextChange (-1, "a", "b"));
			Assert.Throws<ArgumentOutOfRangeException> (() => new TextChange (-1, 0, "a", "b"));
		}

		[Test]
		public void TextSourceConstructors ()
		{
			var removed = new StringTextSource ("ab");
			var c = new TextChange (2, removed, null);
			Assert.AreSame (removed, c.RemovedText);
			Assert.AreSame (StringTextSource.Empty, c.InsertedText);
			Assert.AreEqual (-2, c.ChangeDelta);

			c = new TextChange (2, 7, null, removed);
			Assert.AreEqual (7, c.NewOffset);
			Assert.AreSame (StringTextSource.Empty, c.RemovedText);
			Assert.AreSame (removed, c.InsertedText);

			Assert.Throws<ArgumentOutOfRangeException> (() => new TextChange (-1, removed, removed));
			Assert.Throws<ArgumentOutOfRangeException> (() => new TextChange (-1, 0, removed, removed));
		}

		[Test]
		public void EventArgsConstructors ()
		{
			var e = new TextChangeEventArgs (3, "a", "bc");
			Assert.AreEqual (1, e.TextChanges.Count);
			Assert.AreEqual (3, e.TextChanges[0].NewOffset);
			Assert.AreEqual ("[TextChangeEventArgs: #TextChanges=1]", e.ToString ());

			e = new TextChangeEventArgs (3, new StringTextSource ("a"), new StringTextSource ("bc"));
			Assert.AreEqual ("bc", e.TextChanges[0].InsertedText.Text);

			e = new TextChangeEventArgs (3, 5, new StringTextSource ("a"), null);
			Assert.AreEqual (5, e.TextChanges[0].NewOffset);
			Assert.AreEqual (0, e.TextChanges[0].InsertionLength);

			Assert.Throws<ArgumentNullException> (() => new TextChangeEventArgs (null));
		}

		[Test]
		public void GetNewOffsetForASingleReplacement ()
		{
			// "0123456789": replace "345" (offset 3) with "ab".
			var e = new TextChangeEventArgs (3, 3, "345", "ab");
			Assert.AreEqual (0, e.GetNewOffset (0));
			Assert.AreEqual (5, e.GetNewOffset (3), "an offset inside the removed text moves to the end of the insertion");
			Assert.AreEqual (5, e.GetNewOffset (4));
			Assert.AreEqual (5, e.GetNewOffset (6));
			Assert.AreEqual (8, e.GetNewOffset (9));
		}

		[Test]
		public void GetNewOffsetForMultipleChanges ()
		{
			var e = new TextChangeEventArgs (new List<TextChange> {
				new TextChange (2, "", "xx"),
				new TextChange (10, "abcd", "")
			});
			Assert.AreEqual (1, e.GetNewOffset (1));
			Assert.AreEqual (7, e.GetNewOffset (5));
			Assert.AreEqual (12, e.GetNewOffset (12));
			Assert.AreEqual (16, e.GetNewOffset (18));
		}

		[Test]
		public void InvertRestoresOffsets ()
		{
			var e = new TextChangeEventArgs (new List<TextChange> {
				new TextChange (1, "a", "bbb"),
				new TextChange (6, "cc", "")
			});
			var inverted = e.Invert ();
			Assert.AreEqual (2, inverted.TextChanges.Count);
			Assert.AreEqual (6, inverted.TextChanges[0].Offset);
			Assert.AreEqual ("", inverted.TextChanges[0].RemovedText.Text);
			Assert.AreEqual ("cc", inverted.TextChanges[0].InsertedText.Text);
			Assert.AreEqual ("bbb", inverted.TextChanges[1].RemovedText.Text);
			Assert.AreEqual ("a", inverted.TextChanges[1].InsertedText.Text);
		}
	}

	/// <summary>
	/// Task T056: the immutable <see cref="StringTextSource"/> and the <see cref="TextSourceExtension"/> helpers.
	/// </summary>
	[TestFixture]
	public class StringTextSourceTests : TestBase
	{
		const string Sample = "Hello, wörld!";

		[Test]
		public void BasicAccessors ()
		{
			var s = new StringTextSource (Sample);
			Assert.AreEqual (Sample.Length, s.Length);
			Assert.AreEqual (Sample, s.Text);
			Assert.AreEqual ('w', s.GetCharAt (7));
			Assert.AreEqual ('ö', s[8]);
			Assert.AreEqual ("wörld", s.GetTextAt (7, 5));
			Assert.AreSame (Encoding.UTF8, s.Encoding);
			Assert.IsNull (s.Version);
			Assert.AreSame (s, s.CreateSnapshot ());
			Assert.AreEqual ("lo", s.CreateSnapshot (3, 2).Text);
			Assert.AreEqual ("", StringTextSource.Empty.Text);

			Assert.Throws<ArgumentNullException> (() => new StringTextSource (null));
			Assert.Throws<ArgumentNullException> (() => new StringTextSource (null, (ITextSourceVersion)null));
		}

		[Test]
		public void VersionAndEncoding ()
		{
			var provider = new TextSourceVersionProvider ();
			var s = new StringTextSource ("abc", provider.CurrentVersion, Encoding.Unicode);
			Assert.AreSame (provider.CurrentVersion, s.Version);
			Assert.AreSame (Encoding.Unicode, s.Encoding);

			var ascii = s.WithEncoding (Encoding.ASCII);
			Assert.AreSame (Encoding.ASCII, ascii.Encoding);
			Assert.AreEqual ("abc", ascii.Text);
		}

		[Test]
		public void WithBomTogglesTheUtf8Preamble ()
		{
			var withBom = new StringTextSource ("x", Encoding.UTF8);
			var withoutBom = withBom.WithBom (false);
			Assert.AreEqual (0, withoutBom.Encoding.GetPreamble ().Length);
			Assert.AreEqual (3, withoutBom.WithBom (true).Encoding.GetPreamble ().Length);
			Assert.AreSame (withBom.Encoding, withBom.WithBom (true).Encoding);

			// Encodings without a BOM concept are kept as they are.
			var ascii = new StringTextSource ("x", Encoding.ASCII);
			Assert.AreSame (Encoding.ASCII, ascii.WithBom (true).Encoding);
		}

		[Test]
		public void Readers ()
		{
			var s = new StringTextSource ("line1\nline2");
			using (var reader = s.CreateReader ())
				Assert.AreEqual ("line1", reader.ReadLine ());
			using (var reader = s.CreateReader (6, 5))
				Assert.AreEqual ("line2", reader.ReadToEnd ());
			using (var reader = s.CreateReader (new TextSegment (2, 3)))
				Assert.AreEqual ("ne1", reader.ReadToEnd ());
		}

		[Test]
		public void Writers ()
		{
			var s = new StringTextSource (Sample);
			var sw = new StringWriter ();
			s.WriteTextTo (sw);
			s.WriteTextTo (sw, 0, 5);
			s.WriteTextTo (sw, new TextSegment (7, 5));
			Assert.AreEqual (Sample + "Hello" + "wörld", sw.ToString ());

			var chars = new char[5];
			s.CopyTo (7, chars, 0, 5);
			Assert.AreEqual ("wörld", new string (chars));

			Assert.Throws<ArgumentNullException> (() => s.WriteTextTo ((TextWriter)null));
			Assert.Throws<ArgumentNullException> (() => s.WriteTextTo (null, 0, 1));
			Assert.Throws<ArgumentNullException> (() => s.WriteTextTo (null, new TextSegment (0, 1)));
			Assert.Throws<ArgumentNullException> (() => s.WriteTextTo (sw, (ISegment)null));
		}

		[Test]
		public void ExtensionHelpers ()
		{
			ITextSource s = new StringTextSource (Sample);
			Assert.AreEqual ("wörld", s.GetTextAt (new TextSegment (7, 5)));
			Assert.AreEqual ("lo, w", s.GetTextBetween (3, 8));
			Assert.AreEqual ("", s.GetTextBetween (4, 4));
			Assert.AreEqual ("Hello", s.CreateSnapshot (new TextSegment (0, 5)).Text);

			Assert.Throws<InvalidOperationException> (() => s.GetTextBetween (5, 2));
			Assert.Throws<ArgumentNullException> (() => s.GetTextBetween (-1, 2));
			Assert.Throws<ArgumentNullException> (() => s.GetTextBetween (0, Sample.Length + 1));

			ITextSource none = null;
			Assert.Throws<ArgumentNullException> (() => none.GetTextAt (new TextSegment (0, 1)));
			Assert.Throws<ArgumentNullException> (() => none.GetTextBetween (0, 1));
			Assert.Throws<ArgumentNullException> (() => none.WriteTextTo ("file"));
			Assert.Throws<ArgumentNullException> (() => none.WriteTextTo (new StringWriter (), new TextSegment (0, 1)));
			Assert.Throws<ArgumentNullException> (() => none.CreateReader (new TextSegment (0, 1)));
			Assert.Throws<ArgumentNullException> (() => s.CreateReader ((ISegment)null));
			Assert.Throws<ArgumentNullException> (() => none.CreateSnapshot (new TextSegment (0, 1)));
			Assert.Throws<ArgumentNullException> (() => s.CreateSnapshot ((ISegment)null));
		}

		[Test]
		public void FileRoundTrip ()
		{
			var file = Path.Combine (Path.GetTempPath (), "md-textsource-" + Guid.NewGuid () + ".txt");
			try {
				ITextSource s = new StringTextSource (Sample, Encoding.UTF8);
				s.WriteTextTo (file);

				var fromFile = StringTextSource.ReadFrom (file);
				Assert.AreEqual (Sample, fromFile.Text);
				Assert.AreEqual ("utf-8", fromFile.Encoding.WebName);

				using (var stream = File.OpenRead (file))
					Assert.AreEqual (Sample, StringTextSource.ReadFrom (stream).Text);

				var fromBytes = StringTextSource.ReadFrom (Encoding.Unicode.GetPreamble ().Concat (Encoding.Unicode.GetBytes (Sample)).ToArray ());
				Assert.AreEqual (Sample, fromBytes.Text);
				Assert.AreEqual (Encoding.Unicode.WebName, fromBytes.Encoding.WebName);
			} finally {
				File.Delete (file);
			}
		}
	}

	/// <summary>
	/// Task T056: <see cref="TextSourceVersionProvider"/> version chains and offset tracking between versions.
	/// </summary>
	[TestFixture]
	public class TextSourceVersionProviderTests
	{
		[Test]
		public void VersionsAreOrderedAndBelongToTheirDocument ()
		{
			var provider = new TextSourceVersionProvider ();
			var v0 = provider.CurrentVersion;
			provider.AppendChange (new TextChangeEventArgs (0, 0, "", "abc"));
			var v1 = provider.CurrentVersion;

			Assert.AreNotSame (v0, v1);
			Assert.AreEqual (-1, v0.CompareAge (v1));
			Assert.AreEqual (1, v1.CompareAge (v0));
			Assert.AreEqual (0, v1.CompareAge (v1));
			Assert.IsTrue (v0.BelongsToSameDocumentAs (v1));

			var other = new TextSourceVersionProvider ().CurrentVersion;
			Assert.IsFalse (v0.BelongsToSameDocumentAs (other));
			Assert.IsFalse (v0.BelongsToSameDocumentAs (null));
			Assert.Throws<ArgumentException> (() => v0.CompareAge (other));
			Assert.Throws<ArgumentNullException> (() => v0.CompareAge (null));
			Assert.Throws<ArgumentNullException> (() => provider.AppendChange (null));
		}

		[Test]
		public void ChangesBetweenVersions ()
		{
			var provider = new TextSourceVersionProvider ();
			var v0 = provider.CurrentVersion;
			var first = new TextChangeEventArgs (0, 0, "", "abc");
			var second = new TextChangeEventArgs (10, 10, "xy", "");
			provider.AppendChange (first);
			provider.AppendChange (second);
			var v2 = provider.CurrentVersion;

			CollectionAssert.AreEqual (new[] { first, second }, v0.GetChangesTo (v2).ToArray ());
			CollectionAssert.IsEmpty (v2.GetChangesTo (v2));

			var backwards = v2.GetChangesTo (v0).ToArray ();
			Assert.AreEqual (2, backwards.Length);
			Assert.AreEqual ("xy", backwards[0].TextChanges[0].InsertedText.Text, "the last change is undone first");
			Assert.AreEqual ("abc", backwards[1].TextChanges[0].RemovedText.Text);

			// Offset 5 moves right by the insertion; offset 20 also moves left by the removal.
			Assert.AreEqual (8, v0.MoveOffsetTo (v2, 5));
			Assert.AreEqual (21, v0.MoveOffsetTo (v2, 20));
			Assert.AreEqual (5, v2.MoveOffsetTo (v0, 8));
			Assert.AreEqual (7, v0.MoveOffsetTo (v0, 7));
		}
	}
}
