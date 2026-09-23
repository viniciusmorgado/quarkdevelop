//
// TextEncodingTests.cs
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

using System.Linq;
using System.Reflection;
using System.Text;
using MonoDevelop.Core;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects.Text
{
	/// <summary>
	/// Task T056: the encoding list offered by the IDE (<see cref="TextEncoding"/>) on .NET 10, where legacy
	/// code pages come from CodePagesEncodingProvider (registered by Runtime.Initialize).
	/// </summary>
	[TestFixture]
	public class TextEncodingTests : TestBase
	{
		const string ConversionProperty = "MonoDevelop.Projects.Text.ConversionEncodings";

		[Test]
		public void SupportedEncodingsAreResolvable ()
		{
			var all = TextEncoding.SupportedEncodings;
			Assert.AreSame (all, TextEncoding.SupportedEncodings, "the list is computed once");
			Assert.Greater (all.Length, 40);
			Assert.AreEqual (all.Length, all.Select (e => e.Id).Distinct ().Count ());
			foreach (var e in all) {
				Assert.IsNotNull (e.Encoding, e.Id);
				Assert.IsFalse (string.IsNullOrEmpty (e.Name), e.Id);
			}
		}

		[TestCase ("UTF-8", 65001, "Unicode")]
		[TestCase ("UTF-16", 1200, "Unicode")]
		[TestCase ("ISO-8859-15", 28605, "Western")]
		[TestCase ("SHIFT_JIS", 932, "Japanese")]
		[TestCase ("WINDOWS-1250", 1250, "Central European")]
		[TestCase ("WINDOWS-1258", 1258, "Vietnamese")]
		public void GetEncodingById (string id, int codePage, string name)
		{
			var e = TextEncoding.GetEncoding (id);
			Assert.IsNotNull (e, id);
			Assert.AreEqual (id, e.Id);
			Assert.AreEqual (name, e.Name);
			Assert.AreEqual (codePage, e.CodePage);
		}

		[Test]
		public void Utf8WithoutBom ()
		{
			var e = TextEncoding.GetEncoding ("UTF-8 (No BOM)");
			Assert.IsNotNull (e);
			Assert.AreEqual (65001, e.CodePage);
			Assert.AreEqual (0, e.Encoding.GetPreamble ().Length);
			Assert.AreSame (e.Encoding, e.Encoding, "the encoding is created once");
		}

		[Test]
		public void LookupsByEncodingAndCodePage ()
		{
			var utf16 = TextEncoding.GetEncoding ("UTF-16");
			Assert.AreSame (utf16, TextEncoding.GetEncoding (utf16.Encoding));
			Assert.AreSame (utf16, TextEncoding.GetEncoding (1200));
			Assert.AreEqual ("UTF-8", TextEncoding.GetEncoding (65001).Id);

			Assert.IsNull (TextEncoding.GetEncoding ("NOT-AN-ENCODING"));
			Assert.IsNull (TextEncoding.GetEncoding (new UTF32Encoding (true, true, true)));
			Assert.IsNull (TextEncoding.GetEncoding (-5));
		}

		[Test]
		public void UnknownEncodingFallsBackToUtf8 ()
		{
			var e = new TextEncoding ("X-MD-UNKNOWN", "Nothing");
			Assert.AreEqual ("X-MD-UNKNOWN", e.Id);
			Assert.AreEqual ("Nothing", e.Name);
			Assert.AreSame (Encoding.UTF8, e.Encoding);
		}

		[Test]
		public void ConversionEncodingsArePersisted ()
		{
			var original = TextEncoding.ConversionEncodings;
			try {
				Assert.IsTrue (original.Length > 0);

				var chosen = new[] { TextEncoding.GetEncoding ("UTF-8"), TextEncoding.GetEncoding ("WINDOWS-1252") };
				TextEncoding.ConversionEncodings = chosen;
				Assert.AreSame (chosen, TextEncoding.ConversionEncodings);
				Assert.AreEqual ("UTF-8 WINDOWS-1252", PropertyService.Get (ConversionProperty, ""));
			} finally {
				TextEncoding.ConversionEncodings = original;
			}
		}

		static void ResetConversionCache ()
		{
			typeof (TextEncoding).GetField ("conversion", BindingFlags.NonPublic | BindingFlags.Static).SetValue (null, null);
		}

		[Test]
		public void ConversionEncodingsDefaults ()
		{
			var original = TextEncoding.ConversionEncodings;
			var originalProperty = PropertyService.Get (ConversionProperty, "");
			try {
				PropertyService.Set (ConversionProperty, "");
				ResetConversionCache ();
				CollectionAssert.AreEqual (new[] { "UTF-8", "ISO-8859-15", "UTF-16" }, TextEncoding.ConversionEncodings.Select (e => e.Id));

				// Old configurations get UTF-16 inserted as the second choice; unknown ids are dropped.
				PropertyService.Set (ConversionProperty, "ISO-8859-1 NOT-AN-ENCODING UTF-8");
				ResetConversionCache ();
				CollectionAssert.AreEqual (new[] { "ISO-8859-1", "UTF-16", "UTF-8" }, TextEncoding.ConversionEncodings.Select (e => e.Id));
			} finally {
				PropertyService.Set (ConversionProperty, originalProperty);
				TextEncoding.ConversionEncodings = original;
			}
		}

		[Test]
		public void DefaultEncoding ()
		{
			Assert.AreEqual ("UTF-8", TextEncoding.DefaultEncoding);
		}
	}
}
