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

			if (descriptor.SpecialKey != SpecialKey.Return)
				TryFormat (formattingService, descriptor.KeyChar, Editor.CaretOffset, default (CancellationToken));
			return result;
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
