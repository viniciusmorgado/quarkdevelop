//
// EditorFormattingServiceTextEditorExtension.cs
//
// Author:
//       Mike Krüger <mikkrg@microsoft.com>
//
// Copyright (c) 2018 Microsoft Corporation. All rights reserved.
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
using MonoDevelop.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using Roslyn.Utilities;

namespace MonoDevelop.Ide.Editor.Extension
{
	// Roslyn 5.9: IEditorFormattingService (EditorFeatures) is gone; typed character formatting uses the
	// Workspaces ISyntaxFormattingService. Format on return is no longer offered by that service.
	[Obsolete]
	partial class EditorFormattingServiceTextEditorExtension : TextEditorExtension
	{
		protected override void Initialize()
		{
			base.Initialize();
		}

		public override bool KeyPress(KeyDescriptor descriptor)
		{
			var result = base.KeyPress (descriptor);

			var doc = DocumentContext.AnalysisDocument;
			if (doc == null)
				return result;

			var formattingService = doc.Project.Services.GetService<ISyntaxFormattingService> ();
			if (formattingService == null)
				return result;

			if (descriptor.SpecialKey == SpecialKey.None && SupportsFormattingOnTypedCharacter (doc.Project.Language, descriptor.KeyChar))
				TryFormat (formattingService, descriptor.KeyChar, Editor.CaretOffset, default (CancellationToken));
			return result;
		}

		// The characters whose typing formats what they end, as the formatting service of Roslyn EditorFeatures offered them
		// (CSharpFormattingInteractionService.SupportsFormattingOnTypedCharacter), which the port to Workspaces had dropped:
		// every other key (letters, Tab, Backspace) formatted the enclosing statement too, with the caret moved away, e.g.
		// to the next lines when Tab was pressed on a new line.
		const string TriggerCharacters = ";{}#nte:)";

		bool SupportsFormattingOnTypedCharacter (string language, char ch)
		{
			var preferences = IdeApp.Preferences.Roslyn.For (language);
			bool smartIndent = Editor.Options.IndentStyle == IndentStyle.Smart || Editor.Options.IndentStyle == IndentStyle.Virtual;
			// a brace typed at the start of a line goes to its place with smart indentation, even without formatting on typing
			if (smartIndent && (ch == '{' || ch == '}'))
				return true;
			if (!preferences.AutoFormattingOnTyping)
				return false;
			if (ch == '}' && !preferences.AutoFormattingOnCloseBrace)
				return false;
			if (ch == ';' && !preferences.AutoFormattingOnSemicolon)
				return false;
			if ((ch == '#' || ch == 'n') && !smartIndent)
				return false;
			return TriggerCharacters.IndexOf (ch) >= 0;
		}

		bool TryFormat (ISyntaxFormattingService formattingService, char typedChar, int position, CancellationToken cancellationToken)
		{
			var document = DocumentContext.AnalysisDocument;
			var parsedDocument = ParsedDocument.CreateSynchronously (document, cancellationToken);
			if (!formattingService.ShouldFormatOnTypedCharacter (parsedDocument, typedChar, position, cancellationToken))
				return false;

			var formattingOptions = document.GetSyntaxFormattingOptionsAsync (cancellationToken).AsTask ().WaitAndGetResult (cancellationToken);
			IEnumerable<TextChange> changes = formattingService.GetFormattingChangesOnTypedCharacter (parsedDocument, position, new IndentationOptions (formattingOptions), cancellationToken);
			var line = Editor.GetLineByOffset (position);
			if (typedChar == '#') {
				changes = changes.Where (c => c.Span.Start >= line.Offset);
			}

			if (changes == null) {
				return false;
			}
			Editor.ApplyTextChanges (changes);
			return true;
		}
	}
}
