//
// DotNetNewListParser.cs
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
using System.Linq;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// Reads the table printed by <c>dotnet new list --columns-all</c> (columns: template name, short names, languages,
	/// type, author, tags). The table is made for people: long names and authors are cut ("…") and there is no
	/// identity or description, so it is only the fallback of <see cref="DotNetNewTemplateCatalog"/> and the format of
	/// the test fixtures (ADR 0026).
	/// </summary>
	static class DotNetNewListParser
	{
		const int NameColumn = 0, ShortNameColumn = 1, LanguageColumn = 2, TypeColumn = 3, AuthorColumn = 4, TagsColumn = 5;

		public static List<DotNetNewTemplate> Parse (string output)
		{
			var templates = new List<DotNetNewTemplate> ();
			var lines = (output ?? string.Empty).Replace ("\r\n", "\n").Split ('\n');

			int separator = Array.FindIndex (lines, IsSeparatorLine);
			if (separator < 0)
				return templates;

			var columns = GetColumns (lines[separator]);
			if (columns.Count < TagsColumn + 1)
				return templates;

			for (int i = separator + 1; i < lines.Length; i++) {
				string line = lines[i];
				if (string.IsNullOrWhiteSpace (line))
					break;
				var template = ParseRow (line, columns);
				if (template != null)
					templates.Add (template);
			}
			return templates;
		}

		static bool IsSeparatorLine (string line)
		{
			string trimmed = line.Trim ();
			return trimmed.Length > 0 && trimmed.All (c => c == '-' || c == ' ') && trimmed.Contains ("  ");
		}

		/// <summary>The start and length of each column: the runs of dashes of the separator line.</summary>
		static List<(int Start, int Length)> GetColumns (string separator)
		{
			var columns = new List<(int, int)> ();
			int i = 0;
			while (i < separator.Length) {
				if (separator[i] != '-') {
					i++;
					continue;
				}
				int start = i;
				while (i < separator.Length && separator[i] == '-')
					i++;
				columns.Add ((start, i - start));
			}
			return columns;
		}

		static DotNetNewTemplate ParseRow (string line, List<(int Start, int Length)> columns)
		{
			string Cell (int column)
			{
				var (start, length) = columns[column];
				if (start >= line.Length)
					return string.Empty;
				// the last column may be longer than its dashes
				int end = column == columns.Count - 1 ? line.Length : Math.Min (line.Length, start + length);
				return line.Substring (start, end - start).Trim ();
			}

			string name = Cell (NameColumn);
			var shortNames = SplitList (Cell (ShortNameColumn));
			if (name.Length == 0 || shortNames.Count == 0)
				return null;

			return new DotNetNewTemplate {
				Identity = shortNames[0],
				Name = name,
				ShortNames = shortNames,
				Languages = SplitList (Cell (LanguageColumn)).Select (language => language.Trim ('[', ']')).ToList (),
				Type = Cell (TypeColumn),
				Author = Cell (AuthorColumn),
				Tags = Cell (TagsColumn).Split ('/', StringSplitOptions.RemoveEmptyEntries).Select (tag => tag.Trim ()).ToList (),
				UsesName = true
			};
		}

		static List<string> SplitList (string cell)
		{
			return cell.Split (',', StringSplitOptions.RemoveEmptyEntries)
				.Select (item => item.Trim ())
				.Where (item => item.Length > 0)
				.ToList ();
		}
	}
}
