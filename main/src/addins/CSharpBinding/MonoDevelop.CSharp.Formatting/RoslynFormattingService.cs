//
// RoslynFormattingService.cs
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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.CSharp.Formatting
{
	/// <summary>
	/// Replaces Roslyn's IEditorFormattingService, which is part of EditorFeatures (not used on Linux, T089), with the
	/// formatting services of Roslyn Workspaces: whole document or span formatting (Formatter), formatting on a typed
	/// character and on paste (ISyntaxFormattingService). The options are the C# formatting policy of the document
	/// when the caller passes it (MonoDevelop policies no longer reach Roslyn's document options), else the options of
	/// the document (.editorconfig and defaults).
	/// </summary>
	sealed class RoslynFormattingService
	{
		public static readonly RoslynFormattingService Instance = new RoslynFormattingService ();

		RoslynFormattingService ()
		{
		}

		public bool SupportsFormatDocument => true;

		public bool SupportsFormatOnPaste => true;

		public bool SupportsFormatSelection => true;

		// Roslyn 4+ has no format-on-return: the new line is indented by the editor's indentation tracker.
		public bool SupportsFormatOnReturn => false;

		static async Task<SyntaxFormattingOptions> GetOptionsAsync (Document document, OptionSet policyOptions, CancellationToken cancellationToken)
		{
			if (policyOptions != null)
				return Formatter.GetFormattingOptions (document.Project.Solution.Workspace, policyOptions, document.Project.Language);
			return await document.GetSyntaxFormattingOptionsAsync (cancellationToken).ConfigureAwait (false);
		}

		public Task<IList<TextChange>> GetFormattingChangesAsync (Document document, TextSpan? textSpan, CancellationToken cancellationToken)
		{
			return GetFormattingChangesAsync (document, textSpan, null, cancellationToken);
		}

		public async Task<IList<TextChange>> GetFormattingChangesAsync (Document document, TextSpan? textSpan, OptionSet policyOptions, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync (cancellationToken).ConfigureAwait (false);
			var options = await GetOptionsAsync (document, policyOptions, cancellationToken).ConfigureAwait (false);
			var span = textSpan ?? root.FullSpan;
			return Formatter.GetFormattedTextChanges (root, new[] { span }, document.Project.Solution.Services, options, cancellationToken);
		}

		public Task<IList<TextChange>> GetFormattingChangesOnReturnAsync (Document document, int caretPosition, CancellationToken cancellationToken)
		{
			return Task.FromResult<IList<TextChange>> (null);
		}

		public async Task<IList<TextChange>> GetFormattingChangesOnPasteAsync (Document document, TextSpan textSpan, CancellationToken cancellationToken)
		{
			var service = document.Project.Services.GetService<ISyntaxFormattingService> ();
			if (service == null)
				return null;
			var parsedDocument = await ParsedDocument.CreateAsync (document, cancellationToken).ConfigureAwait (false);
			var options = await GetOptionsAsync (document, null, cancellationToken).ConfigureAwait (false);
			return service.GetFormattingChangesOnPaste (parsedDocument, textSpan, options, cancellationToken);
		}

		public bool SupportsFormattingOnTypedCharacter (Document document, char ch)
		{
			return ch == ';' || ch == '}' || ch == '#' || ch == 'n' || ch == 't' || ch == 'e';
		}

		public async Task<IList<TextChange>> GetFormattingChangesAsync (Document document, char typedChar, int caretPosition, CancellationToken cancellationToken)
		{
			var service = document.Project.Services.GetService<ISyntaxFormattingService> ();
			if (service == null)
				return null;
			var parsedDocument = await ParsedDocument.CreateAsync (document, cancellationToken).ConfigureAwait (false);
			if (!service.ShouldFormatOnTypedCharacter (parsedDocument, typedChar, caretPosition, cancellationToken))
				return null;
			var options = await GetOptionsAsync (document, null, cancellationToken).ConfigureAwait (false);
			return service.GetFormattingChangesOnTypedCharacter (parsedDocument, caretPosition, new IndentationOptions (options), cancellationToken);
		}
	}
}
