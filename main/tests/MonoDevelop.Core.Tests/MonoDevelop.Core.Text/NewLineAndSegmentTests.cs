//
// NewLineAndSegmentTests.cs
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
using System.Linq;
using NUnit.Framework;

namespace MonoDevelop.Core.Text
{
	/// <summary>
	/// Task T056: Unicode new line classification (Unicode TR #13) of <see cref="NewLine"/>.
	/// </summary>
	[TestFixture]
	public class NewLineTests
	{
		[TestCase ('\n', UnicodeNewline.LF)]
		[TestCase ('\r', UnicodeNewline.CR)]
		[TestCase ('\u0085', UnicodeNewline.NEL)]
		[TestCase ('\u2028', UnicodeNewline.LS)]
		[TestCase ('\u2029', UnicodeNewline.PS)]
		public void SingleCharDelimiters (char ch, UnicodeNewline type)
		{
			Assert.IsTrue (NewLine.IsNewLine (ch));
			Assert.AreEqual (1, NewLine.GetDelimiterLength (ch, 'x'));
			Assert.AreEqual (1, NewLine.GetDelimiterLength (ch, () => 'x'));
			Assert.AreEqual (1, NewLine.GetDelimiterLength (ch));
			Assert.AreEqual (type, NewLine.GetDelimiterType (ch, 'x'));
			Assert.AreEqual (type, NewLine.GetDelimiterType (ch, () => 'x'));
			Assert.AreEqual (type, NewLine.GetDelimiterType (ch));

			Assert.IsTrue (NewLine.TryGetDelimiterLengthAndType (ch, out int length, out UnicodeNewline detected, 'x'));
			Assert.AreEqual (1, length);
			Assert.AreEqual (type, detected);
			Assert.IsTrue (NewLine.TryGetDelimiterLengthAndType (ch, out length, out detected, () => 'x'));
			Assert.AreEqual (1, length);
			Assert.AreEqual (type, detected);
			Assert.IsTrue (NewLine.TryGetDelimiterLengthAndType (ch, out length, out detected));
			Assert.AreEqual (1, length);
			Assert.AreEqual (type, detected);

			Assert.AreEqual (ch.ToString (), NewLine.GetString (type));
		}

		[Test]
		public void CarriageReturnLineFeedIsOneTwoCharDelimiter ()
		{
			Assert.AreEqual (2, NewLine.GetDelimiterLength ('\r', '\n'));
			Assert.AreEqual (2, NewLine.GetDelimiterLength ('\r', () => '\n'));
			Assert.AreEqual (UnicodeNewline.CRLF, NewLine.GetDelimiterType ('\r', '\n'));
			Assert.AreEqual (UnicodeNewline.CRLF, NewLine.GetDelimiterType ('\r', () => '\n'));

			Assert.IsTrue (NewLine.TryGetDelimiterLengthAndType ('\r', out int length, out UnicodeNewline type, '\n'));
			Assert.AreEqual (2, length);
			Assert.AreEqual (UnicodeNewline.CRLF, type);
			Assert.IsTrue (NewLine.TryGetDelimiterLengthAndType ('\r', out length, out type, () => '\n'));
			Assert.AreEqual (2, length);
			Assert.AreEqual (UnicodeNewline.CRLF, type);

			Assert.AreEqual ("\r\n", NewLine.GetString (UnicodeNewline.CRLF));

			// LF followed by CR is two separate delimiters.
			Assert.AreEqual (1, NewLine.GetDelimiterLength ('\n', '\r'));
		}

		[TestCase ('\v')]
		[TestCase ('\f')]
		public void VerticalTabAndFormFeedAreNewLinesWithoutAType (char ch)
		{
			Assert.IsTrue (NewLine.IsNewLine (ch));
			Assert.AreEqual (1, NewLine.GetDelimiterLength (ch, 'x'));
			Assert.AreEqual (1, NewLine.GetDelimiterLength (ch));
			Assert.AreEqual (UnicodeNewline.Unknown, NewLine.GetDelimiterType (ch, 'x'));
			Assert.IsFalse (NewLine.TryGetDelimiterLengthAndType (ch, out int length, out UnicodeNewline type, 'x'));
			Assert.AreEqual (-1, length);
			Assert.AreEqual (UnicodeNewline.Unknown, type);
		}

		[TestCase ('a')]
		[TestCase (' ')]
		[TestCase ('\t')]
		[TestCase ('\0')]
		public void OrdinaryCharsAreNotDelimiters (char ch)
		{
			Assert.IsFalse (NewLine.IsNewLine (ch));
			Assert.AreEqual (0, NewLine.GetDelimiterLength (ch, '\n'));
			Assert.AreEqual (0, NewLine.GetDelimiterLength (ch, () => '\n'));
			Assert.AreEqual (UnicodeNewline.Unknown, NewLine.GetDelimiterType (ch, '\n'));
			Assert.AreEqual (UnicodeNewline.Unknown, NewLine.GetDelimiterType (ch, () => '\n'));
			Assert.IsFalse (NewLine.TryGetDelimiterLengthAndType (ch, out int length, out UnicodeNewline type, '\n'));
			Assert.AreEqual (-1, length);
			Assert.AreEqual (UnicodeNewline.Unknown, type);
			Assert.IsFalse (NewLine.TryGetDelimiterLengthAndType (ch, out length, out type, () => '\n'));
			Assert.AreEqual (-1, length);
		}

		[Test]
		public void GetString ()
		{
			Assert.AreEqual ("", NewLine.GetString (UnicodeNewline.Unknown));
			Assert.Throws<ArgumentOutOfRangeException> (() => NewLine.GetString ((UnicodeNewline)42));
		}

		[Test]
		public void CallbackIsNotInvokedForNonCarriageReturn ()
		{
			int calls = 0;
			Func<char> next = () => { calls++; return '\n'; };
			NewLine.GetDelimiterLength ('\n', next);
			NewLine.GetDelimiterType ('a', next);
			NewLine.TryGetDelimiterLengthAndType ('\u2028', out _, out _, next);
			Assert.AreEqual (0, calls);
			NewLine.GetDelimiterLength ('\r', next);
			Assert.AreEqual (1, calls);
		}
	}

	/// <summary>
	/// Task T056: <see cref="TextSegment"/>, <see cref="AbstractSegment"/> and the <see cref="ISegmentExtensions"/> helpers.
	/// </summary>
	[TestFixture]
	public class TextSegmentTests
	{
		sealed class Segment : AbstractSegment
		{
			public Segment (int offset, int length) : base (offset, length)
			{
			}

			public Segment (ISegment segment) : base (segment)
			{
			}
		}

		[Test]
		public void Properties ()
		{
			var s = new TextSegment (5, 3);
			Assert.AreEqual (5, s.Offset);
			Assert.AreEqual (3, s.Length);
			Assert.AreEqual (8, s.EndOffset);
			Assert.IsFalse (s.IsEmpty);
			Assert.IsFalse (s.IsInvalid);
			Assert.IsTrue (new TextSegment (5, 0).IsEmpty);
			Assert.IsTrue (TextSegment.Invalid.IsInvalid);
			Assert.AreEqual ("[TextSegment: Offset=5, Length=3]", s.ToString ());
		}

		[Test]
		public void Equality ()
		{
			var a = new TextSegment (1, 2);
			var b = new TextSegment (1, 2);
			var c = new TextSegment (1, 3);
			Assert.IsTrue (a == b);
			Assert.IsFalse (a != b);
			Assert.IsTrue (a != c);
			Assert.IsTrue (a.Equals (b));
			Assert.IsFalse (a.Equals (c));
			Assert.IsTrue (TextSegment.Equals (a, b));
			Assert.AreEqual (a.GetHashCode (), b.GetHashCode ());

			// Equals (object) compares with any ISegment.
			Assert.IsTrue (a.Equals ((object)new Segment (1, 2)));
			Assert.IsFalse (a.Equals ((object)new Segment (1, 5)));
			Assert.IsFalse (a.Equals ((object)"not a segment"));
			Assert.IsFalse (a.Equals ((object)null));
		}

		[Test]
		public void FromBounds ()
		{
			var s = TextSegment.FromBounds (3, 10);
			Assert.AreEqual (3, s.Offset);
			Assert.AreEqual (7, s.Length);
			Assert.AreEqual (TextSegment.FromBounds (4, 4), new TextSegment (4, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => TextSegment.FromBounds (5, 4));
		}

		[Test]
		public void CopyConstructors ()
		{
			var s = new TextSegment (new Segment (2, 4));
			Assert.AreEqual (2, s.Offset);
			Assert.AreEqual (4, s.Length);

			var a = new Segment (s);
			Assert.AreEqual (2, a.Offset);
			Assert.AreEqual (4, a.Length);
			Assert.AreEqual (6, a.EndOffset);
			Assert.IsFalse (a.IsEmpty);
			Assert.IsFalse (a.IsInvalid);
			Assert.IsTrue (new Segment (-1, 0).IsInvalid);
			Assert.IsTrue (new Segment (3, 0).IsEmpty);
			Assert.AreEqual ("[AbstractSegment: Offset=2, Length=4]", a.ToString ());

			Assert.Throws<ArgumentOutOfRangeException> (() => new Segment (0, -1));
			Assert.Throws<ArgumentNullException> (() => new Segment (null));
		}

		[Test]
		public void InsideAndContains ()
		{
			var s = new TextSegment (10, 5);

			// TextSegment.IsInside is end inclusive.
			Assert.IsFalse (s.IsInside (9));
			Assert.IsTrue (s.IsInside (10));
			Assert.IsTrue (s.IsInside (15));
			Assert.IsFalse (s.IsInside (16));

			ISegment i = s;
			Assert.IsFalse (i.IsInside (9));
			Assert.IsTrue (i.IsInside (15));
			Assert.IsFalse (i.IsInside (16));

			// Contains (offset) is end exclusive.
			Assert.IsFalse (i.Contains (9));
			Assert.IsTrue (i.Contains (10));
			Assert.IsTrue (i.Contains (14));
			Assert.IsFalse (i.Contains (15));

			Assert.IsTrue (i.Contains (10, 5));
			Assert.IsTrue (i.Contains (15, 0));
			Assert.IsFalse (i.Contains (15, 1));
			Assert.IsFalse (i.Contains (9, 2));

			Assert.IsTrue (i.Contains (new TextSegment (11, 2)));
			Assert.IsTrue (i.Contains (new TextSegment (10, 5)));
			Assert.IsFalse (i.Contains (new TextSegment (11, 5)));
			Assert.IsFalse (i.Contains (new TextSegment (8, 3)));
		}

		[Test]
		public void OverlapsWith ()
		{
			ISegment s = new TextSegment (10, 5);
			Assert.IsTrue (s.OverlapsWith (new TextSegment (12, 10)));
			Assert.IsTrue (s.OverlapsWith (new TextSegment (0, 11)));
			Assert.IsFalse (s.OverlapsWith (new TextSegment (15, 3)));
			Assert.IsFalse (s.OverlapsWith (new TextSegment (5, 5)));
			Assert.IsFalse (s.OverlapsWith (new TextSegment (12, 0)), "empty segments never overlap");
		}

		[Test]
		public void IsInvalidExtension ()
		{
			Assert.IsTrue (((ISegment)TextSegment.Invalid).IsInvalid ());
			Assert.IsFalse (((ISegment)new TextSegment (0, 0)).IsInvalid ());
		}

		[Test]
		public void NullArguments ()
		{
			ISegment none = null;
			ISegment some = new TextSegment (0, 1);
			Assert.Throws<ArgumentNullException> (() => none.Contains (0));
			Assert.Throws<ArgumentNullException> (() => none.Contains (0, 1));
			Assert.Throws<ArgumentNullException> (() => none.Contains (some));
			Assert.Throws<ArgumentNullException> (() => some.Contains (none));
			Assert.Throws<ArgumentNullException> (() => none.IsInside (0));
			Assert.Throws<ArgumentNullException> (() => none.IsInvalid ());
			Assert.Throws<ArgumentNullException> (() => none.AdjustSegment (new TextChangeEventArgs (0, 0, "", "x")));
			Assert.Throws<ArgumentNullException> (() => ((ISegment[])null).AdjustSegments (new TextChangeEventArgs (0, 0, "", "x")).ToList ());
		}

		[Test]
		public void AdjustSegment ()
		{
			ISegment s = new TextSegment (10, 5);

			// Insertion before the segment moves it.
			var moved = s.AdjustSegment (new TextChangeEventArgs (2, 2, "", "abc"));
			Assert.AreEqual (new TextSegment (13, 5), moved);

			// Removal after the segment does not change it.
			Assert.AreEqual (s, s.AdjustSegment (new TextChangeEventArgs (20, 20, "xyz", "")));

			// Removal inside the segment shrinks it.
			Assert.AreEqual (new TextSegment (10, 3), s.AdjustSegment (new TextChangeEventArgs (11, 11, "ab", "")));

			var all = new ISegment[] { new TextSegment (0, 1), new TextSegment (5, 2) }
				.AdjustSegments (new TextChangeEventArgs (3, 3, "", "--"))
				.ToList ();
			Assert.AreEqual (new TextSegment (0, 1), all[0]);
			Assert.AreEqual (new TextSegment (7, 2), all[1]);
		}
	}
}
