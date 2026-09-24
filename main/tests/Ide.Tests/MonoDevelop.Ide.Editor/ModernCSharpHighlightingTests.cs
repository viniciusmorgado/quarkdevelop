//
// ModernCSharpHighlightingTests.cs
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.Core;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor.Highlighting;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Ide.Editor
{
	/// <summary>
	/// The lexical C# highlighting (the C# grammar of the editor, C#.sublime-syntax) of C# 8 to 14 constructs (T138).
	/// The sample is the source of main/tests/linux-smoke/Modern, which the smoke tests build.
	/// </summary>
	[TestFixture]
	sealed class ModernCSharpHighlightingTests : IdeTestBase
	{
		// (file, text on the line, token in that text, expected innermost scope prefix)
		static readonly object[] ModernConstructs = {
			// global using, using static, alias of any type (C# 10, C# 12)
			Case ("GlobalUsings.cs", "global using static System.Math;", "global", "keyword.other.context"),
			Case ("GlobalUsings.cs", "global using static System.Math;", "using", "keyword.other.namespace"),
			Case ("GlobalUsings.cs", "global using static System.Math;", "static", "keyword.other.modifiers"),
			Case ("GlobalUsings.cs", "global using Point = (int X, int Y);", "int", "keyword.other.type"),
			// top-level statements, with expressions, collection expressions, target-typed new
			Case ("Program.cs", "Person older = ada with { Born = 1814 };", "with", "keyword.operator"),
			Case ("Program.cs", "int[] more = [.. numbers, 4, 5];", "..", "keyword.operator.range"),
			Case ("Program.cs", "int[] more = [.. numbers, 4, 5];", "4", "constant.numeric"),
			Case ("Program.cs", "Person ada = new (\"Ada\", \"Lovelace\")", "new", "keyword.operator"),
			// file-scoped namespaces, records, record structs, required and init, file-local types, primary constructors
			Case ("Records.cs", "namespace Modern.Shapes;", "namespace", "keyword.other.namespace"),
			Case ("Records.cs", "namespace Modern.Shapes;", "Modern.Shapes", "entity.name.namespace"),
			Case ("Records.cs", "// File-scoped namespace (C# 10), records (C# 9), record structs", "record", "comment"),
			Case ("Records.cs", "public record Person (string First, string Last)", "record", "keyword.other.declaration"),
			Case ("Records.cs", "public readonly record struct Vector", "record", "keyword.other.declaration"),
			Case ("Records.cs", "public readonly record struct Vector", "struct", "keyword.other.declaration"),
			Case ("Records.cs", "public required int Born { get; init; }", "required", "keyword.other.modifiers"),
			Case ("Records.cs", "public required int Born { get; init; }", "init", "keyword.other.property"),
			Case ("Records.cs", "public class Counter (int start)", "int", "keyword.other.type"),
			Case ("Records.cs", "file sealed class Hidden", "file", "keyword.other.modifiers"),
			Case ("Records.cs", "nameof (Hidden)", "nameof", "keyword.operator"),
			// switch expressions, relational, logical and list patterns, case guards
			Case ("Patterns.cs", "Circle { Radius: > 0 and < 10 } circle", "and", "keyword.operator"),
			Case ("Patterns.cs", "Rectangle (var width, var height) when width == height", "when", "keyword.other.selection"),
			Case ("Patterns.cs", "Rectangle rectangle =>", "rectangle", "source.cs"),
			Case ("Patterns.cs", "[1, .., 3] =>", "..", "keyword.operator.range"),
			Case ("Patterns.cs", "[var first, .. var rest]", "..", "keyword.operator.range"),
			Case ("Patterns.cs", "or (>= 'A' and <= 'Z')", "or", "keyword.operator"),
			Case ("Patterns.cs", "or (>= 'A' and <= 'Z')", "'A'", "string"),
			Case ("Patterns.cs", "value is not null", "not", "keyword.operator"),
			Case ("Patterns.cs", "value is not null", "null", "constant.language"),
			// raw string literals over several lines, interpolated raw strings, UTF-8 literals
			Case ("Strings.cs", "public const string Json = \"\"\"", "\"\"\"", "string"),
			Case ("Strings.cs", "{ \"name\": \"MonoDevelop\", \"empty\": \"\" }", "{ \"name\": \"MonoDevelop\", \"empty\": \"\" }", "string"),
			Case ("Strings.cs", "\"\"\";", "\"\"\"", "string"),
			Case ("Strings.cs", "\"\"\";", ";", "source.cs"),
			Case ("Strings.cs", "=> $\"\"\"", "$\"\"\"", "string"),
			Case ("Strings.cs", "Hello, \"{name}\"!", "Hello, \"", "string"),
			Case ("Strings.cs", "Hello, \"{name}\"!", "name", "source.cs"),
			Case ("Strings.cs", "Hello, \"{name}\"!", "\"!", "string"),
			Case ("Strings.cs", "=> $$\"\"\"", "$$\"\"\"", "string"),
			Case ("Strings.cs", "{ \"count\": {{count}} }", "{ \"count\": ", "string"),
			Case ("Strings.cs", "{{count}} }", "count", "source.cs"),
			Case ("Strings.cs", "{{count}} }", " }", "string"),
			Case ("Strings.cs", "\"MonoDevelop\"u8", "\"MonoDevelop\"", "string"),
			Case ("Strings.cs", "\"MonoDevelop\"u8", "u8", "keyword.other"),
			// static abstract and default interface members, notnull, allows ref struct, scoped, nint and nuint
			Case ("Generics.cs", "static abstract TSelf Unit", "abstract", "keyword.other.modifiers"),
			Case ("Generics.cs", "public ref struct Word", "ref", "keyword.other.parameter"),
			Case ("Generics.cs", "where T : IMeasured, allows ref struct", "allows", "keyword.other.context"),
			Case ("Generics.cs", "where TKey : notnull", "notnull", "keyword.other.context"),
			Case ("Generics.cs", "(scoped ReadOnlySpan<int> values)", "scoped", "keyword.other.parameter"),
			Case ("Generics.cs", "Offset (nint start, nuint count)", "nint", "keyword.other.type"),
			Case ("Generics.cs", "Offset (nint start, nuint count)", "nuint", "keyword.other.type"),
			// function pointers and the unmanaged constraint
			Case ("Pointers.cs", "delegate* managed<int, int> twice", "delegate", "keyword.other.declaration"),
			Case ("Pointers.cs", "delegate* managed<int, int> twice", "managed", "keyword.other.context"),
			Case ("Pointers.cs", "delegate* unmanaged<int, int> function", "unmanaged", "keyword.other.context"),
			Case ("Pointers.cs", "where T : unmanaged", "unmanaged", "keyword.other.context"),
			// extension members and the field keyword (C# 14)
			Case ("Extensions.cs", "extension(string text)", "extension", "keyword.other.declaration"),
			Case ("Extensions.cs", "public static class StringExtensions", "StringExtensions", "source.cs"),
			Case ("Extensions.cs", "get => field;", "field", "keyword.other.property"),
			Case ("Extensions.cs", "set => field = value", "field", "keyword.other.property"),
		};

		static object[] Case (string file, string text, string token, string scope) => new object[] { file, text, token, scope };

		static readonly Dictionary<string, List<(string Text, IReadOnlyList<ColoredSegment> Segments)>> tokenizedFiles =
			new Dictionary<string, List<(string, IReadOnlyList<ColoredSegment>)>> ();

		static SyntaxHighlightingDefinition GetCSharpDefinition ()
		{
			var definition = SyntaxHighlightingService.GetSyntaxHighlightingDefinition ("Program.cs", null);
			Assert.IsNotNull (definition, "no highlighting definition for .cs files");
			Assert.AreEqual ("C#", definition.Name);
			return definition;
		}

		/// <summary>Highlights every line of <paramref name="text"/> the way the editor does, from the first line on.</summary>
		static List<(string Text, IReadOnlyList<ColoredSegment> Segments)> Tokenize (string text)
		{
			var definition = GetCSharpDefinition ();
			definition.PrepareMatches ();
			var editor = TextEditorFactory.CreateNewEditor ();
			editor.Text = text;
			var highlighting = new SyntaxHighlighting (definition, editor);
			editor.SyntaxHighlighting = highlighting;
			var lines = new List<(string, IReadOnlyList<ColoredSegment>)> ();
			foreach (var line in editor.GetLines ()) {
				var highlighted = highlighting.GetHighlightedLineAsync (line, CancellationToken.None).WaitAndGetResult (CancellationToken.None);
				lines.Add ((editor.GetTextAt (line), highlighted.Segments));
			}
			return lines;
		}

		static List<(string Text, IReadOnlyList<ColoredSegment> Segments)> TokenizeSampleFile (string file)
		{
			if (!tokenizedFiles.TryGetValue (file, out var lines)) {
				var path = Path.Combine (UnitTests.Util.TestsRootDir, "linux-smoke", "Modern", file);
				tokenizedFiles[file] = lines = Tokenize (File.ReadAllText (path));
			}
			return lines;
		}

		/// <summary>
		/// Every character of <paramref name="token"/>, in the first line that contains <paramref name="text"/>, has an
		/// innermost scope that starts with <paramref name="scope"/>.
		/// </summary>
		static void AssertScope (List<(string Text, IReadOnlyList<ColoredSegment> Segments)> lines, string text, string token, string scope)
		{
			int lineIndex = lines.FindIndex (l => l.Text.Contains (text, System.StringComparison.Ordinal));
			Assert.That (lineIndex, Is.GreaterThanOrEqualTo (0), "no line contains: " + text);
			var (lineText, segments) = lines[lineIndex];
			int tokenOffset = IndexOfToken (text, token);
			Assert.That (tokenOffset, Is.GreaterThanOrEqualTo (0), "the token is not in the text");
			int start = lineText.IndexOf (text, System.StringComparison.Ordinal) + tokenOffset;
			for (int column = start; column < start + token.Length; column++) {
				var segment = segments.FirstOrDefault (s => s.Contains (column));
				Assert.IsNotNull (segment, $"line {lineIndex + 1}: no segment at column {column + 1}");
				var innermost = segment.ScopeStack.Peek ();
				Assert.That (innermost.StartsWith (scope, System.StringComparison.Ordinal),
					$"line {lineIndex + 1} column {column + 1} ('{token}' in \"{lineText.Trim ()}\"): expected {scope}, was {string.Join (", ", segment.ScopeStack)}");
			}
		}

		/// <summary>The index of <paramref name="token"/> in <paramref name="text"/>, as a whole word when it is a name.</summary>
		static int IndexOfToken (string text, string token)
		{
			bool IsNameCharacter (int index) => index >= 0 && index < text.Length && (char.IsLetterOrDigit (text[index]) || text[index] == '_');
			bool isName = char.IsLetter (token[0]);
			for (int index = text.IndexOf (token, System.StringComparison.Ordinal); index >= 0; index = text.IndexOf (token, index + 1, System.StringComparison.Ordinal)) {
				if (!isName || (!IsNameCharacter (index - 1) && !IsNameCharacter (index + token.Length)))
					return index;
			}
			return -1;
		}

		[TestCaseSource (nameof (ModernConstructs))]
		public void ModernConstructHasItsScope (string file, string text, string token, string scope)
		{
			AssertScope (TokenizeSampleFile (file), text, token, scope);
		}

		/// <summary>The contextual keywords of C# 9 to 14 stay identifiers where they are used as names.</summary>
		[TestCase ("var field = type.GetField (\"x\");", "field")]
		[TestCase ("int record = 1, and = 2, not = 3, with = 4;", "record")]
		[TestCase ("int record = 1, and = 2, not = 3, with = 4;", "and")]
		[TestCase ("int record = 1, and = 2, not = 3, with = 4;", "not")]
		[TestCase ("int record = 1, and = 2, not = 3, with = 4;", "with")]
		[TestCase ("Open (file, extension);", "file")]
		[TestCase ("Open (file, extension);", "extension")]
		[TestCase ("extension.Run ();", "extension")]
		[TestCase ("bool required = scoped;", "required")]
		[TestCase ("bool required = scoped;", "scoped")]
		[TestCase ("Use (managed, notnull, allows);", "managed")]
		[TestCase ("Use (managed, notnull, allows);", "notnull")]
		[TestCase ("Use (managed, notnull, allows);", "allows")]
		public void ContextualKeywordUsedAsNameIsNotAKeyword (string line, string name)
		{
			AssertScope (Tokenize (line + "\n"), line, name, "source.cs");
		}

		[Test]
		public void RawStringWithMoreQuotesEndsAtTheSameNumberOfQuotes ()
		{
			var lines = Tokenize ("var s = \"\"\"\"\n  \"\"\" inside\n  \"\"\"\";\nint after = 1;\n");
			AssertScope (lines, "\"\"\" inside", "\"\"\" inside", "string");
			AssertScope (lines, "int after = 1;", "int", "keyword.other.type");
		}

		[TestCase ("long mask = 0x12345679AFFEuL;", "0x12345679AFFEuL", "constant.numeric")]
		[TestCase ("double d = 123.45678e-09d;", "123.45678e-09d", "constant.numeric.float")]
		[TestCase ("float f = 5f + 0.5f;", "5f", "constant.numeric.float")]
		[TestCase ("int million = 1_000_000;", "1_000_000", "constant.numeric")]
		[TestCase ("int bits = 0b1111_0000;", "0b1111_0000", "constant.numeric.binary")]
		[TestCase ("var slice = values[1..3];", "..", "keyword.operator.range")]
		[TestCase ("var slice = values[1..3];", "3", "constant.numeric")]
		[TestCase ("var s = $\"{{foo}}\";", "$\"{{foo}}\"", "string")]
		[TestCase ("var s = $@\"test\" + @$\"test\";", "$@\"test\"", "string")]
		[TestCase ("var s = $@\"test\" + @$\"test\";", "@$\"test\"", "string")]
		[TestCase ("var s = @\"a b\"u8;", "@\"a b\"", "string")]
		[TestCase ("var s = @\"a b\"u8;", "u8", "keyword.other")]
		[TestCase ("var s = \"\"\"a b\"\"\"u8;", "\"\"\"a b\"\"\"", "string")]
		[TestCase ("var s = \"\"\"a b\"\"\"u8;", "u8", "keyword.other")]
		public void LiteralHasItsScope (string line, string token, string scope)
		{
			AssertScope (Tokenize (line + "\n"), line, token, scope);
		}

		[Test]
		public void NullableDirectiveIsAPreprocessorLine ()
		{
			AssertScope (Tokenize ("#nullable enable\n"), "#nullable enable", "#nullable", "meta.preprocessor");
		}
	}
}
