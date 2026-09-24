//
// CSharpFormatter.cs
//
// Author:
//       Mike Krüger <mkrueger@novell.com>
//
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects.Policies;
using Roslyn.Utilities;

using System.Linq;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace MonoDevelop.CSharp.Formatting
{
	class CSharpFormatter : AbstractCodeFormatter
	{
		static internal readonly string MimeType = "text/x-csharp";

		public override bool SupportsOnTheFlyFormatting { get { return true; } }

		public override bool SupportsCorrectingIndent { get { return true; } }

		public override bool SupportsPartialDocumentFormatting { get { return true; } }

		protected override void CorrectIndentingImplementation (PolicyContainer policyParent, Ide.Editor.TextEditor editor, int line)
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return;
			CorrectIndentingImplementationAsync (editor, doc.DocumentContext, line, line, default).Ignore ();
		}

		protected async override Task CorrectIndentingImplementationAsync (Ide.Editor.TextEditor editor, DocumentContext context, int startLine, int endLine, CancellationToken cancellationToken)
		{
			if (editor.IndentationTracker == null)
				return;
			var startSegment = editor.GetLine (startLine);
			if (startSegment == null)
				return;
			var endSegment = startLine != endLine ? editor.GetLine (endLine) : startSegment;
			if (endSegment == null)
				return;

			try {
				var document = context.AnalysisDocument;

				var formattingService = MonoDevelop.CSharp.Formatting.RoslynFormattingService.Instance;
				if (formattingService == null || !formattingService.SupportsFormatSelection)
					return;

				// Linux: no contained-document (ASP.NET) rule; the default rules and the C# policy of the document.
				var options = await context.GetOptionsAsync (cancellationToken).ConfigureAwait (false);
				var changes = await formattingService.GetFormattingChangesAsync (
					document, new TextSpan (startSegment.Offset, endSegment.EndOffset - startSegment.Offset), options, cancellationToken).ConfigureAwait (false);

				if (changes == null)
					return;
				await Runtime.RunInMainThread (delegate {
					editor.ApplyTextChanges (changes);
					editor.FixVirtualIndentation ();
				});
			} catch (Exception e) {
				LoggingService.LogError ("Error while indenting", e);
			}
		}
		protected override async void OnTheFlyFormatImplementation (Ide.Editor.TextEditor editor, DocumentContext context, int startOffset, int length)
		{
			var doc = context.AnalysisDocument;

			var formattingService = MonoDevelop.CSharp.Formatting.RoslynFormattingService.Instance;
			if (formattingService == null || !formattingService.SupportsFormatSelection)
				return;

			var changes = await formattingService.GetFormattingChangesAsync (doc, new TextSpan (startOffset, length), await context.GetOptionsAsync (), default (System.Threading.CancellationToken));
			if (changes == null)
				return;
			editor.ApplyTextChanges (changes);
			editor.FixVirtualIndentation ();
		}

		public static string FormatText (Microsoft.CodeAnalysis.Options.OptionSet optionSet, string input, int startOffset, int endOffset)
		{
			var inputTree = CSharpSyntaxTree.ParseText (input);

			var root = inputTree.GetRoot ();
			var doc = Formatter.Format (root, new TextSpan (startOffset, endOffset - startOffset), IdeApp.TypeSystemService.Workspace, optionSet);
			var result = doc.ToFullString ();
			return result.Substring (startOffset, endOffset + result.Length - input.Length - startOffset);
		}

		protected override ITextSource FormatImplementation (PolicyContainer policyParent, string mimeType, ITextSource input, int startOffset, int length)
		{
			var chain = IdeServices.DesktopService.GetMimeTypeInheritanceChain (mimeType);
			var policy = policyParent.Get<CSharpFormattingPolicy> (chain);
			var textPolicy = policyParent.Get<TextStylePolicy> (chain);
			var optionSet = policy.CreateOptions (textPolicy);
			// Roslyn 4+: an OptionSet can no longer be layered over document options (IDocumentOptions is gone); the
			// policy alone formats text that is not a workspace document (.editorconfig applies to workspace documents).

			return new StringTextSource (FormatText (optionSet, input.Text, startOffset, startOffset + length));
		}


	}
}
