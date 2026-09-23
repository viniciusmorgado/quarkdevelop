//
// ProcessArgumentBuilderTests.cs
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
using NUnit.Framework;

namespace MonoDevelop.Core.Execution
{
	/// <summary>
	/// Task T056: building (quoting/escaping) and parsing process command lines with <see cref="ProcessArgumentBuilder"/>.
	/// </summary>
	[TestFixture]
	public class ProcessArgumentBuilderTests
	{
		[Test]
		public void AddDoesNotQuote ()
		{
			var b = new ProcessArgumentBuilder ();
			Assert.IsNull (b.ProcessPath);
			Assert.AreEqual ("", b.ToString ());
			b.Add ("-a");
			b.Add ("b c", "d");
			Assert.AreEqual ("-a b c d", b.ToString ());
		}

		[Test]
		public void AddQuotedEscapesQuotesAndBackslashes ()
		{
			var b = new ProcessArgumentBuilder ("/usr/bin/tool");
			Assert.AreEqual ("/usr/bin/tool", b.ProcessPath);
			b.AddQuoted ("plain");
			b.AddQuoted ((string)null);
			b.AddQuoted ("with space", "say \"hi\"", @"C:\dir\");
			Assert.AreEqual (@"""plain"" ""with space"" ""say \""hi\"""" ""C:\\dir\\""", b.ToString ());
		}

		[Test]
		public void AddQuotedFormat ()
		{
			var b = new ProcessArgumentBuilder ();
			b.AddQuotedFormat ("-out:{0}", "my file.dll");
			b.AddQuotedFormat ("-{0}:{1}", "define", "A;B");
			Assert.AreEqual ("\"-out:my file.dll\" \"-define:A;B\"", b.ToString ());
		}

		[Test]
		public void Quote ()
		{
			Assert.AreEqual ("\"\"", ProcessArgumentBuilder.Quote (""));
			Assert.AreEqual ("\"a b\"", ProcessArgumentBuilder.Quote ("a b"));
			Assert.AreEqual ("\"a\\\"b\\\\\"", ProcessArgumentBuilder.Quote ("a\"b\\"));
		}

		[TestCase ("", new string[0])]
		[TestCase ("   \t ", new string[0])]
		[TestCase ("a b  c", new[] { "a", "b", "c" })]
		[TestCase ("a\tb", new[] { "a", "b" })]
		[TestCase ("\"hello world\" 'x y'", new[] { "hello world", "x y" })]
		[TestCase (@"foo\ bar", new[] { "foo bar" })]
		[TestCase (@"""a\""b""", new[] { "a\"b" })]
		[TestCase (@"a\\b", new[] { @"a\b" })]
		[TestCase (@"""C:\\dir\\"" x", new[] { @"C:\dir\", "x" })]
		[TestCase ("'it\"s'", new[] { "it\"s" })]
		[TestCase ("pre\"quoted part\"post next", new[] { "prequoted partpost", "next" })]
		[TestCase ("-define:'A B' -x", new[] { "-define:A B", "-x" })]
		public void Parse (string commandLine, string[] expected)
		{
			CollectionAssert.AreEqual (expected, ProcessArgumentBuilder.Parse (commandLine));
			Assert.IsTrue (ProcessArgumentBuilder.TryParse (commandLine, out var argv));
			CollectionAssert.AreEqual (expected, argv);
		}

		[TestCase ("\"unterminated", "No matching quote found.")]
		[TestCase ("a 'b", "No matching quote found.")]
		[TestCase ("x\"y", "No matching quote found.")]
		[TestCase (@"abc\", "Incomplete escape sequence.")]
		public void ParseErrors (string commandLine, string message)
		{
			Assert.IsFalse (ProcessArgumentBuilder.TryParse (commandLine, out var argv));
			Assert.IsNull (argv);
			var ex = Assert.Throws<FormatException> (() => ProcessArgumentBuilder.Parse (commandLine));
			Assert.AreEqual (message, ex.Message);
		}

		[Test]
		public void QuotedArgumentsRoundTripThroughParse ()
		{
			var args = new[] { "simple", "with space", "quote\"inside", @"back\slash", "tab\there", "'single'" };
			var b = new ProcessArgumentBuilder ();
			b.AddQuoted (args);
			CollectionAssert.AreEqual (args, ProcessArgumentBuilder.Parse (b.ToString ()));
		}
	}
}
