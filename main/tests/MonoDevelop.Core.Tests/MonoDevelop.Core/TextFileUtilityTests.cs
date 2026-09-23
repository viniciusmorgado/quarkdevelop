//
// TextFileUtilityTests.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core.Text;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Tasks T046/T056: text file encoding detection and round trips on .NET 10, where legacy code pages
	/// only exist through CodePagesEncodingProvider (registered by Runtime.Initialize).
	/// </summary>
	[TestFixture]
	public class TextFileUtilityTests : TestBase
	{
		const string Sample = "héllo wörld – ünïcode ✓";
		string dir;

		[SetUp]
		public void CreateDir ()
		{
			dir = Path.Combine (Path.GetTempPath (), "md-textfile-" + Guid.NewGuid ());
			Directory.CreateDirectory (dir);
		}

		[TearDown]
		public void DeleteDir ()
		{
			Directory.Delete (dir, true);
		}

		static Encoding Utf8WithBom => new UTF8Encoding (true);

		[Test]
		public void Utf8WithBomRoundTrip ()
		{
			var file = Path.Combine (dir, "bom.txt");
			TextFileUtility.WriteText (file, Sample, Utf8WithBom, true);
			CollectionAssert.AreEqual (new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes (file).AsSpan (0, 3).ToArray ());

			var text = TextFileUtility.GetText (File.ReadAllBytes (file), out var encoding, out var hasBom);
			Assert.AreEqual (Sample, text);
			Assert.AreEqual (Encoding.UTF8.WebName, encoding.WebName);
			Assert.IsTrue (hasBom);
			Assert.AreEqual (Sample, TextFileUtility.ReadAllText (file));
		}

		[Test]
		public void Utf8WithoutBomIsDetected ()
		{
			var bytes = new UTF8Encoding (false).GetBytes (Sample);
			var text = TextFileUtility.GetText (bytes, out var encoding, out var hasBom);
			Assert.AreEqual (Sample, text);
			Assert.AreEqual ("utf-8", encoding.WebName);
			Assert.IsFalse (hasBom);
		}

		[TestCase ("utf-16")]
		[TestCase ("utf-16BE")]
		[TestCase ("utf-32")]
		public void UnicodeBomsAreRecognised (string name)
		{
			var encoding = Encoding.GetEncoding (name);
			var bytes = TextFileUtility.GetBuffer (Sample, encoding, true);
			var text = TextFileUtility.GetText (bytes, out var detected, out var hasBom);
			Assert.AreEqual (Sample, text);
			Assert.AreEqual (encoding.WebName, detected.WebName);
			Assert.IsTrue (hasBom);
		}

		[Test]
		public void LegacyWindowsCodePageIsDetected ()
		{
			// "café" in windows-1252: 0xE9 is not valid UTF-8.
			var bytes = Encoding.GetEncoding (1252).GetBytes ("café crème");
			var text = TextFileUtility.GetText (bytes, out var encoding);
			Assert.AreEqual ("café crème", text);
			Assert.AreNotEqual ("utf-8", encoding.WebName);
		}

		[Test]
		public void PlainAsciiText ()
		{
			Assert.IsTrue (TextFileUtility.IsASCII ("plain text 123"));
			Assert.IsFalse (TextFileUtility.IsASCII (Sample));
			Assert.AreEqual ("plain", TextFileUtility.GetText (Encoding.ASCII.GetBytes ("plain")));
		}

		[Test]
		public void BinaryContentIsRecognised ()
		{
			// Binary = no text encoding verifier accepts the content (a short run of NULs still passes as UTF-16).
			var assembly = typeof (TextFileUtility).Assembly.Location;
			Assert.IsTrue (TextFileUtility.IsBinary (assembly));
			Assert.IsTrue (TextFileUtility.IsBinary (File.ReadAllBytes (assembly)));
			Assert.IsFalse (TextFileUtility.IsBinary (Encoding.UTF8.GetBytes (Sample)));
		}

		[Test]
		public void OpenStreamReportsTheBom ()
		{
			using (var reader = TextFileUtility.OpenStream (new MemoryStream (TextFileUtility.GetBuffer (Sample, Utf8WithBom, true)), out var hasBom)) {
				Assert.IsTrue (hasBom);
				Assert.AreEqual (Sample, reader.ReadToEnd ());
			}
			using (var reader = TextFileUtility.OpenStream (new UTF8Encoding (false).GetBytes (Sample)))
				Assert.AreEqual (Sample, reader.ReadToEnd ());
		}

		[Test]
		public void GetBufferOnlyAddsTheBomWhenAsked ()
		{
			Assert.AreEqual (Encoding.UTF8.GetByteCount (Sample) + 3, TextFileUtility.GetBuffer (Sample, Utf8WithBom, true).Length);
			Assert.AreEqual (Encoding.UTF8.GetByteCount (Sample), TextFileUtility.GetBuffer (Sample, Utf8WithBom, false).Length);
		}

		[Test]
		public async Task AsyncReadAndWrite ()
		{
			var file = Path.Combine (dir, "async.txt");
			await TextFileUtility.WriteTextAsync (file, Sample, Encoding.Unicode, true);
			var content = await TextFileUtility.ReadAllTextAsync (file);
			Assert.AreEqual (Sample, content.Text);
			Assert.AreEqual (Encoding.Unicode.WebName, content.Encoding.WebName);

			var fromStream = await TextFileUtility.GetTextAsync (new MemoryStream (File.ReadAllBytes (file)));
			Assert.AreEqual (Sample, fromStream.Text);

			var fromName = await TextFileUtility.GetTextAsync (file, CancellationToken.None);
			Assert.AreEqual (Sample, fromName.Text);

			CollectionAssert.AreEqual (File.ReadAllBytes (file), await TextFileUtility.ReadAllBytesAsync (file));
		}

		[Test]
		public void ReadWithAnExplicitEncoding ()
		{
			var file = Path.Combine (dir, "latin1.txt");
			var latin1 = Encoding.GetEncoding ("iso-8859-1");
			File.WriteAllBytes (file, latin1.GetBytes ("Grüße"));
			Assert.AreEqual ("Grüße", TextFileUtility.ReadAllText (file, latin1));
			File.WriteAllBytes (file, latin1.GetBytes ("café crème"));
			Assert.AreEqual ("café crème", TextFileUtility.GetText (file, out _));
		}

		[Test]
		public void WriteTextToAStream ()
		{
			var stream = new MemoryStream ();
			TextFileUtility.WriteText (stream, Sample, Encoding.UTF8, false);
			Assert.AreEqual (Sample, Encoding.UTF8.GetString (stream.ToArray ()));
		}
	}
}
