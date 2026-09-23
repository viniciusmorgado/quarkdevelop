//
// MSBuildErrorParserTests.cs
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

using NUnit.Framework;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T056: parsing of single-line compiler/MSBuild diagnostics in the canonical
	/// "origin(position): subcategory category code: message" format.
	/// </summary>
	[TestFixture]
	public class MSBuildErrorParserTests
	{
		static MSBuildErrorParser.Result Parse (string line)
		{
			var result = MSBuildErrorParser.TryParseLine (line);
			Assert.IsNotNull (result, "Could not parse: " + line);
			return result;
		}

		[Test]
		public void FileWithLineAndColumn ()
		{
			var r = Parse ("Program.cs(10,5): error CS1002: ; expected");
			Assert.AreEqual ("Program.cs", r.Origin);
			Assert.AreEqual (10, r.Line);
			Assert.AreEqual (5, r.Column);
			Assert.AreEqual (0, r.EndLine);
			Assert.AreEqual (0, r.EndColumn);
			Assert.IsTrue (r.IsError);
			Assert.AreEqual ("CS1002", r.Code);
			Assert.AreEqual ("", r.Subcategory);
			Assert.AreEqual ("; expected", r.Message);
		}

		[Test]
		public void FullRangeWarning ()
		{
			var r = Parse ("/src/a b/File.cs(1,2,3,4): warning CS0168: The variable 'x' is declared but never used");
			Assert.AreEqual ("/src/a b/File.cs", r.Origin);
			Assert.AreEqual (1, r.Line);
			Assert.AreEqual (2, r.Column);
			Assert.AreEqual (3, r.EndLine);
			Assert.AreEqual (4, r.EndColumn);
			Assert.IsFalse (r.IsError);
			Assert.AreEqual ("CS0168", r.Code);
			Assert.AreEqual ("The variable 'x' is declared but never used", r.Message);
		}

		[TestCase ("foo.cs(7,3-9): error X1: m", 7, 3, 0, 9)]
		[TestCase ("foo.cs(7-9): error X1: m", 7, 0, 9, 0)]
		[TestCase ("foo.cs(12): error X1: m", 12, 0, 0, 0)]
		[TestCase ("foo.cs (12) : error X1: m", 12, 0, 0, 0)]
		public void PositionForms (string line, int startLine, int startColumn, int endLine, int endColumn)
		{
			var r = Parse (line);
			Assert.AreEqual ("foo.cs", r.Origin);
			Assert.AreEqual (startLine, r.Line);
			Assert.AreEqual (startColumn, r.Column);
			Assert.AreEqual (endLine, r.EndLine);
			Assert.AreEqual (endColumn, r.EndColumn);
			Assert.AreEqual ("m", r.Message);
		}

		[TestCase ("file(1,2,3): error X1: m")]
		[TestCase ("file(1-2,3): error X1: m")]
		[TestCase ("file(1-2-3): error X1: m")]
		[TestCase ("file(1,2-3-4): error X1: m")]
		public void UnexpectedPositionPatternsAreDiscarded (string line)
		{
			var r = Parse (line);
			Assert.AreEqual ("file", r.Origin);
			Assert.AreEqual (0, r.Line);
			Assert.AreEqual (0, r.Column);
			Assert.AreEqual (0, r.EndLine);
			Assert.AreEqual (0, r.EndColumn);
		}

		[TestCase ("file(abc): error X1: m")]
		[TestCase ("file(1,x): error X1: m")]
		[TestCase ("file(1,2,3,y): error X1: m")]
		[TestCase ("file(1-z): error X1: m")]
		[TestCase ("file(1,2-q): error X1: m")]
		public void NonNumericPositionIsPartOfTheFileName (string line)
		{
			var r = Parse (line);
			StringAssert.StartsWith ("file(", r.Origin);
			StringAssert.EndsWith (")", r.Origin);
			Assert.AreEqual (0, r.Line);
			Assert.IsTrue (r.IsError);
		}

		[Test]
		public void OverflowingPositionIsTreatedAsZero ()
		{
			var r = Parse ("a.cs(99999999999,2): error X1: m");
			Assert.AreEqual ("a.cs", r.Origin);
			Assert.AreEqual (0, r.Line);
			Assert.AreEqual (2, r.Column);
		}

		[Test]
		public void ToolOriginWithoutPosition ()
		{
			var r = Parse ("csc : error CS2001: Source file 'x.cs' could not be found");
			Assert.AreEqual ("csc", r.Origin);
			Assert.AreEqual (0, r.Line);
			Assert.AreEqual ("CS2001", r.Code);
			Assert.AreEqual ("Source file 'x.cs' could not be found", r.Message);
		}

		[Test]
		public void Subcategory ()
		{
			var r = Parse ("MSBUILD : Compile error MSB1234: Something bad");
			Assert.AreEqual ("MSBUILD", r.Origin);
			Assert.AreEqual ("Compile", r.Subcategory);
			Assert.AreEqual ("MSB1234", r.Code);
			Assert.IsTrue (r.IsError);
			Assert.AreEqual ("Something bad", r.Message);
		}

		[Test]
		public void WindowsDriveLetterIsPartOfTheOrigin ()
		{
			var r = Parse (@"C:\src\a.cs(3,4): warning CS1: text");
			Assert.AreEqual (@"C:\src\a.cs", r.Origin);
			Assert.AreEqual (3, r.Line);
			Assert.AreEqual (4, r.Column);
			Assert.IsFalse (r.IsError);
		}

		[Test]
		public void MissingOriginParsesTheFirstSectionAsCategory ()
		{
			var r = Parse ("error CS1234: bad thing");
			Assert.IsNull (r.Origin);
			Assert.IsTrue (r.IsError);
			Assert.AreEqual ("CS1234", r.Code);
			Assert.AreEqual ("bad thing", r.Message);

			// A colon in the message is not mistaken for a category separator.
			r = Parse ("warning W1: key: value");
			Assert.IsNull (r.Origin);
			Assert.IsFalse (r.IsError);
			Assert.AreEqual ("W1", r.Code);
			Assert.AreEqual ("key: value", r.Message);
		}

		[Test]
		public void LeadingColonMeansEmptyOrigin ()
		{
			var r = Parse ("  : error CS1: msg");
			Assert.IsNull (r.Origin);
			Assert.AreEqual ("CS1", r.Code);
			Assert.AreEqual ("msg", r.Message);
		}

		[TestCase ("a.cs(1): ERROR X1: m", true)]
		[TestCase ("a.cs(1): Warning X1: m", false)]
		public void CategoryIsCaseInsensitive (string line, bool isError)
		{
			Assert.AreEqual (isError, Parse (line).IsError);
		}

		[TestCase ("a.cs(1): error X1:", "")]
		[TestCase ("a.cs(1): error X1:    ", "")]
		[TestCase ("a.cs(1): error X1: x", "x")]
		[TestCase ("a.cs(1): error X1: x  ", "x")]
		[TestCase ("a.cs(1): error X1:   spaced   message  ", "spaced   message")]
		public void MessageIsTrimmed (string line, string message)
		{
			Assert.AreEqual (message, Parse (line).Message);
		}

		[TestCase ("")]
		[TestCase ("    ")]
		[TestCase ("ab")]
		[TestCase ("Build succeeded.")]
		[TestCase ("a.cs(1,1): info XX01: hmm")]
		[TestCase ("Time Elapsed 00:00:01.23")]
		[TestCase ("x:y")]
		public void NonDiagnosticLinesAreRejected (string line)
		{
			Assert.IsNull (MSBuildErrorParser.TryParseLine (line));
		}
	}
}
