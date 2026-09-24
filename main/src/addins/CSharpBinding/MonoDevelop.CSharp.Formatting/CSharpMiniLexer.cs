//
// CSharpMiniLexer.cs
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

namespace MonoDevelop.CSharp.Formatting
{
	/// <summary>
	/// A minimal C# lexer that tracks whether a position is inside a string, character literal, comment or
	/// preprocessor directive. It replaces NRefactory's CSharpCompletionEngineBase.MiniLexer (NRefactory is removed,
	/// ADR 0019) for the text editor indentation, which only needs these states for the text before the caret.
	/// </summary>
	sealed class CSharpMiniLexer
	{
		readonly string text;

		public bool IsFistNonWs { get; private set; } = true;
		public bool IsInSingleComment { get; private set; }
		public bool IsInString { get; private set; }
		public bool IsInVerbatimString { get; private set; }
		public bool IsInChar { get; private set; }
		public bool IsInMultiLineComment { get; private set; }
		public bool IsInPreprocessorDirective { get; private set; }

		public CSharpMiniLexer (string text)
		{
			this.text = text ?? throw new ArgumentNullException (nameof (text));
		}

		/// <summary>
		/// Scans the text. <paramref name="act"/> is called for every character with its index, after the states are
		/// updated; the scan stops when it returns true.
		/// </summary>
		public void Parse (Func<char, int, bool> act = null)
		{
			for (int i = 0; i < text.Length; i++) {
				char ch = text[i];
				char nextCh = i + 1 < text.Length ? text[i + 1] : '\0';
				switch (ch) {
				case '#':
					if (IsFistNonWs)
						IsInPreprocessorDirective = true;
					break;
				case '/':
					if (IsInString || IsInChar || IsInVerbatimString || IsInSingleComment || IsInMultiLineComment)
						break;
					if (nextCh == '/') {
						i++;
						IsInSingleComment = true;
						IsInPreprocessorDirective = false;
					}
					if (nextCh == '*')
						IsInMultiLineComment = true;
					break;
				case '*':
					if (IsInString || IsInChar || IsInVerbatimString || IsInSingleComment)
						break;
					if (nextCh == '/') {
						i++;
						IsInMultiLineComment = false;
					}
					break;
				case '@':
					if (IsInString || IsInChar || IsInVerbatimString || IsInSingleComment || IsInMultiLineComment)
						break;
					if (nextCh == '"') {
						i++;
						IsInVerbatimString = true;
					}
					break;
				case '\n':
				case '\r':
					IsInSingleComment = false;
					IsInString = false;
					IsInChar = false;
					IsFistNonWs = true;
					IsInPreprocessorDirective = false;
					break;
				case '\\':
					if (IsInString || IsInChar)
						i++;
					break;
				case '"':
					if (IsInSingleComment || IsInMultiLineComment || IsInChar)
						break;
					if (IsInVerbatimString) {
						if (nextCh == '"') {
							i++;
							break;
						}
						IsInVerbatimString = false;
						break;
					}
					IsInString = !IsInString;
					break;
				case '\'':
					if (IsInSingleComment || IsInMultiLineComment || IsInString || IsInVerbatimString)
						break;
					IsInChar = !IsInChar;
					break;
				}
				if (act != null && act (ch, i))
					return;
				IsFistNonWs &= char.IsWhiteSpace (ch);
			}
		}
	}
}
