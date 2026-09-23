//
// StringTableTests.cs
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
using System.Collections.Immutable;
using System.Text;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Task T056: string interning through the local and shared caches of <see cref="StringTable"/>.
	/// </summary>
	[TestFixture]
	public class StringTableTests
	{
		// Unique text per test so the process wide shared table cannot already contain it.
		static string Unique (string prefix) => prefix + Guid.NewGuid ().ToString ("N");

		[Test]
		public void AllOverloadsReturnTheSameInstance ()
		{
			var table = new StringTable ();
			var text = Unique ("word");
			var padded = "<<" + text + ">>";

			var first = table.Add (padded, 2, text.Length);
			Assert.AreEqual (text, first);
			Assert.AreSame (first, table.Add (padded, 2, text.Length));
			Assert.AreSame (first, table.Add (padded.ToCharArray (), 2, text.Length));
			Assert.AreSame (first, table.Add (new StringBuilder (text)));
			Assert.AreSame (first, table.Add (new string (text.ToCharArray ())));
		}

		[Test]
		public void EachOverloadCanInsertFirst ()
		{
			var t1 = Unique ("chars");
			var fromChars = new StringTable ().Add (("#" + t1).ToCharArray (), 1, t1.Length);
			Assert.AreEqual (t1, fromChars);

			var t2 = Unique ("builder");
			var fromBuilder = new StringTable ().Add (new StringBuilder (t2));
			Assert.AreEqual (t2, fromBuilder);

			var t3 = Unique ("string");
			var fromString = new StringTable ().Add (new string (t3.ToCharArray ()));
			Assert.AreEqual (t3, fromString);

			// A different table finds the entries through the shared cache.
			var other = new StringTable ();
			Assert.AreSame (fromChars, other.Add (t1.ToCharArray (), 0, t1.Length));
			Assert.AreSame (fromBuilder, other.Add (new StringBuilder (t2)));
			Assert.AreSame (fromString, other.Add (t3, 0, t3.Length));
		}

		[Test]
		public void SingleCharacters ()
		{
			var table = new StringTable ();
			var a = table.Add ('中');
			Assert.AreEqual ("中", a);
			Assert.AreSame (a, table.Add ('中'));
			Assert.AreSame (a, new StringTable ().Add ('中'));
			Assert.AreSame (a, table.Add ("中"));
		}

		[Test]
		public void ManyStringsStayConsistent ()
		{
			// More strings than the local cache holds: colliding entries are replaced but lookups stay correct.
			var table = new StringTable ();
			var prefix = Unique ("n");
			var added = new List<string> ();
			for (int i = 0; i < 3000; i++)
				added.Add (table.Add (prefix + i));
			for (int i = 0; i < 3000; i++)
				Assert.AreEqual (prefix + i, table.Add (prefix + i));
			Assert.AreSame (added[2999], table.Add (prefix + 2999));
		}

		[Test]
		public void SharedTable ()
		{
			var text = Unique ("shared");
			var shared = StringTable.AddShared (text);
			Assert.AreSame (text, shared, "the first instance is stored");
			Assert.AreSame (shared, StringTable.AddShared (new string (text.ToCharArray ())));
			Assert.AreSame (shared, StringTable.AddShared (new StringBuilder (text)));
			Assert.AreSame (shared, StringTable.AddShared ("[" + text + "]", 1, text.Length));

			var builderText = Unique ("sb");
			var fromBuilder = StringTable.AddShared (new StringBuilder (builderText));
			Assert.AreEqual (builderText, fromBuilder);
			Assert.AreSame (fromBuilder, StringTable.AddShared (builderText));

			var sub = Unique ("sub");
			var fromSubstring = StringTable.AddShared ("__" + sub, 2, sub.Length);
			Assert.AreEqual (sub, fromSubstring);
			Assert.AreSame (fromSubstring, new StringTable ().Add (sub));
		}

		[Test]
		public void SharedUtf8 ()
		{
			var ascii = Unique ("ascii");
			var first = StringTable.AddSharedUTF8 (Encoding.UTF8.GetBytes (ascii));
			Assert.AreEqual (ascii, first);
			Assert.AreSame (first, StringTable.AddSharedUTF8 (Encoding.UTF8.GetBytes (ascii)));
			Assert.AreSame (first, StringTable.AddShared (ascii));

			// Non-ASCII text is decoded but not cached.
			var unicode = Unique ("ünï");
			var a = StringTable.AddSharedUTF8 (Encoding.UTF8.GetBytes (unicode));
			var b = StringTable.AddSharedUTF8 (Encoding.UTF8.GetBytes (unicode));
			Assert.AreEqual (unicode, a);
			Assert.AreEqual (unicode, b);
			Assert.AreNotSame (a, b);
		}

		[Test]
		public void TextEquals ()
		{
			Assert.IsTrue (StringTable.TextEquals ("bc", "abcd", 1, 2));
			Assert.IsFalse (StringTable.TextEquals ("bd", "abcd", 1, 2));
			Assert.IsFalse (StringTable.TextEquals ("bc", "abcd", 1, 3));

			Assert.IsTrue (StringTable.TextEquals ("abc", new StringBuilder ("abc")));
			Assert.IsFalse (StringTable.TextEquals ("abc", new StringBuilder ("abd")));
			Assert.IsFalse (StringTable.TextEquals ("abc", new StringBuilder ("ab")));

			Assert.IsTrue (StringTable.TextEqualsASCII ("abc", Encoding.ASCII.GetBytes ("abc")));
			Assert.IsFalse (StringTable.TextEqualsASCII ("abc", Encoding.ASCII.GetBytes ("abd")));
			Assert.IsFalse (StringTable.TextEqualsASCII ("abc", Encoding.ASCII.GetBytes ("abcd")));

			Assert.IsTrue (StringTable.TextEquals ("abc", "xabcx".AsSpan (1, 3)));
			Assert.IsFalse (StringTable.TextEquals ("abc", "ABC".AsSpan ()));
		}
	}

	/// <summary>
	/// Task T056: FNV-1a hashing (with the published test vectors) and hash combination helpers of <see cref="Hash"/>.
	/// </summary>
	[TestFixture]
	public class HashTests
	{
		[TestCase ("", unchecked((int)0x811c9dc5))]
		[TestCase ("a", unchecked((int)0xe40c292c))]
		[TestCase ("foobar", unchecked((int)0xbf9cf968))]
		public void FnvTestVectors (string text, int expected)
		{
			var bytes = Encoding.ASCII.GetBytes (text);
			Assert.AreEqual (expected, Hash.GetFNVHashCode (text));
			Assert.AreEqual (expected, Hash.GetFNVHashCode (bytes));
			Assert.AreEqual (expected, Hash.GetFNVHashCode (ImmutableArray.Create (bytes)));
			Assert.AreEqual (expected, Hash.GetFNVHashCode ((ReadOnlySpan<byte>)bytes, out bool isAscii));
			Assert.IsTrue (isAscii);
			Assert.AreEqual (expected, Hash.GetFNVHashCode (new StringBuilder (text)));
			Assert.AreEqual (expected, Hash.GetFNVHashCode (("__" + text).ToCharArray (), 2, text.Length));
			Assert.AreEqual (expected, Hash.GetFNVHashCode ("__" + text + "__", 2, text.Length));
			Assert.AreEqual (expected, Hash.GetFNVHashCode ("__" + text, 2));
			Assert.AreEqual (expected, Hash.CombineFNVHash (Hash.FnvOffsetBias, text));
		}

		[Test]
		public void SingleCharAndIncrementalHashing ()
		{
			Assert.AreEqual (Hash.GetFNVHashCode ("a"), Hash.GetFNVHashCode ('a'));
			var incremental = Hash.CombineFNVHash (Hash.CombineFNVHash (Hash.FnvOffsetBias, 'f'), "oobar");
			Assert.AreEqual (Hash.GetFNVHashCode ("foobar"), incremental);
		}

		[Test]
		public void NonAsciiBytesAreDetected ()
		{
			Hash.GetFNVHashCode ((ReadOnlySpan<byte>)Encoding.UTF8.GetBytes ("é"), out bool isAscii);
			Assert.IsFalse (isAscii);
		}

		[Test]
		public void Combine ()
		{
			Assert.AreEqual (unchecked(3 * (int)0xA5555529 + 7), Hash.Combine (7, 3));
			Assert.AreEqual (unchecked((int)0xA5555529 + 5), Hash.Combine (true, 5));
			Assert.AreEqual (5, Hash.Combine (false, 5));

			var s = "text";
			Assert.AreEqual (unchecked(2 * (int)0xA5555529 + s.GetHashCode ()), Hash.Combine (s, 2));
			Assert.AreEqual (unchecked(2 * (int)0xA5555529), Hash.Combine ((string)null, 2));
		}

		[Test]
		public void CombineValues ()
		{
			var values = new[] { "a", null, "b", "c" };
			int expected = Hash.Combine ("c".GetHashCode (), Hash.Combine ("b".GetHashCode (), Hash.Combine ("a".GetHashCode (), 0)));
			int firstTwo = Hash.Combine ("a".GetHashCode (), 0);

			Assert.AreEqual (expected, Hash.CombineValues (values));
			Assert.AreEqual (expected, Hash.CombineValues ((IEnumerable<string>)values));
			Assert.AreEqual (expected, Hash.CombineValues (ImmutableArray.Create (values)));
			Assert.AreEqual (expected, Hash.CombineValues (values, StringComparer.Ordinal));

			Assert.AreEqual (firstTwo, Hash.CombineValues (values, 2));
			Assert.AreEqual (firstTwo, Hash.CombineValues ((IEnumerable<string>)values, 2));
			Assert.AreEqual (firstTwo, Hash.CombineValues (ImmutableArray.Create (values), 2));
			Assert.AreEqual (firstTwo, Hash.CombineValues (values, StringComparer.Ordinal, 2));

			Assert.AreEqual (
				Hash.CombineValues (new[] { "A", "B" }, StringComparer.OrdinalIgnoreCase),
				Hash.CombineValues (new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase));

			Assert.AreEqual (0, Hash.CombineValues ((string[])null));
			Assert.AreEqual (0, Hash.CombineValues ((IEnumerable<string>)null));
			Assert.AreEqual (0, Hash.CombineValues (default (ImmutableArray<string>)));
			Assert.AreEqual (0, Hash.CombineValues ((IEnumerable<string>)null, StringComparer.Ordinal));
		}
	}
}
