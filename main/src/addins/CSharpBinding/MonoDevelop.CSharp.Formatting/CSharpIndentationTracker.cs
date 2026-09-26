// 
// CSharpIndentVirtualSpaceManager.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
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
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.Core;
using Microsoft.CodeAnalysis.Shared.Extensions;
using MonoDevelop.Core.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;

namespace MonoDevelop.CSharp.Formatting
{
	class CSharpIndentationTracker : IndentationTracker
	{
		readonly Ide.Editor.TextEditor editor;
		readonly DocumentContext context;
		readonly OptionSet optionSet;
		int cacheSpaceCount = -1, oldTabCount = -1;
		string cachedIndentString;

		public override IndentationTrackerFeatures SupportedFeatures => IndentationTrackerFeatures.SmartBackspace | IndentationTrackerFeatures.CustomIndentationEngine;

		/// <param name="optionSet">The C# formatting policy of the document, or null for the options of the workspace.</param>
		public CSharpIndentationTracker (Ide.Editor.TextEditor editor, DocumentContext context, OptionSet optionSet = null)
		{
			this.editor = editor;
			this.context = context;
			this.optionSet = optionSet;
		}

		#region IndentationTracker implementation
		public override string GetIndentationString (int lineNumber)
		{
			if (lineNumber < 1 || lineNumber > editor.LineCount) 
				return "";
			var doc = context.AnalysisDocument;
			if (doc == null)
				return editor.GetLineIndent (lineNumber);
			int? indentation = GetDesiredIndentation (lineNumber - 1);
			if (indentation.HasValue)
				return CalculateIndentationString (indentation.Value);

			var line = editor.GetLine (lineNumber);
			if (line == null)
				return editor.GetLineIndent (lineNumber);
			try {
				if (line.Contains (editor.CaretOffset)) {
					var syntaxRoot = doc.GetSyntaxRootSynchronously (default);
					var token = syntaxRoot.FindTokenOnLeftOfPosition (editor.CaretOffset);
					if (token.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.CommaToken))
						return line.GetIndentation (editor) + CalculateIndentationString (editor.Options.IndentationSize);
				}
			} catch (Exception e) {
				LoggingService.LogError ("Error while calculating the indentation string.", e);
			}

			return line.GetIndentation (editor);
		}

		/// <summary>
		/// The column (tabs expanded) where Roslyn's indentation service puts a line of the editor (0-based), or null.
		/// Upstream the SmartIndent of Roslyn EditorFeatures computed it, through the ISmartIndentationService of the
		/// editor; EditorFeatures is not used on Linux (T089), so no C# smart indent was registered there and every new
		/// line started at the indentation of the empty line itself: column 0.
		/// </summary>
		int? GetDesiredIndentation (int lineIndex)
		{
			try {
				var document = editor.TextView.TextBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges ();
				var service = document?.GetLanguageService<IIndentationService> ();
				if (service == null)
					return null;
				var parsedDocument = ParsedDocument.CreateSynchronously (document, CancellationToken.None);
				var result = service.GetIndentation (parsedDocument, lineIndex, new IndentationOptions (GetFormattingOptions (document)), CancellationToken.None);
				var text = parsedDocument.Text;
				return GetColumn (text, text.Lines.GetLineFromPosition (result.BasePosition).Start, result.BasePosition) + result.Offset;
			} catch (Exception e) {
				LoggingService.LogError ("Error while calculating the smart indentation.", e);
				return null;
			}
		}

		/// <summary>
		/// The C# formatting options: the policy of the document (or the workspace options), with the indentation settings
		/// of the editor (text style policy or .editorconfig), which also write the indentation.
		/// </summary>
		SyntaxFormattingOptions GetFormattingOptions (Document document)
		{
			var options = Formatter.GetFormattingOptions (document.Project.Solution.Workspace, optionSet, document.Project.Language);
			return options with {
				LineFormatting = new LineFormattingOptions {
					UseTabs = !editor.Options.TabsToSpaces,
					TabSize = editor.Options.TabSize,
					IndentationSize = editor.Options.IndentationSize,
					NewLine = editor.EolMarker,
				}
			};
		}

		/// <summary>
		/// The column of a position in its line, a tab moving to the next tab stop.
		/// </summary>
		int GetColumn (SourceText text, int lineStart, int position)
		{
			int tabSize = Math.Max (1, editor.Options.TabSize);
			int column = 0;
			for (int i = lineStart; i < position; i++)
				column = text [i] == '\t' ? column + tabSize - column % tabSize : column + 1;
			return column;
		}

		string CalculateIndentationString (int spaceCount)
		{
			int tabCount = 0;
			if (!editor.Options.TabsToSpaces) {
				tabCount = spaceCount / editor.Options.TabSize;
				spaceCount = spaceCount % editor.Options.TabSize;
			}
			if (cacheSpaceCount != spaceCount || oldTabCount != tabCount) {
				string tabString = new string ('\t', tabCount);
				string spaceString = new string (' ', spaceCount);
				cacheSpaceCount = spaceCount;
				oldTabCount = tabCount;
				return cachedIndentString = tabString + spaceString;
			}

			return cachedIndentString;
		}
		#endregion
	}
}
