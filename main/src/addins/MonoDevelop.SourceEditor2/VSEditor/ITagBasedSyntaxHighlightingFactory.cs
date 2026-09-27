//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See LICENSES/Apache-2.0.txt in the repository root for license information.
//
namespace Microsoft.VisualStudio.Platform
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using MonoDevelop.Ide.Editor.Highlighting;

    /// <summary>
    /// Creates a syntax highlighter for VS ITextBuffer suitable for use in MD.
    /// </summary>
    /// <remarks>This is a MEF component part, and should be imported as follows:
    /// [Import]
    /// ITagBasedSyntaxHighlightingFactory factory = null;
    /// </remarks>
    public interface ITagBasedSyntaxHighlightingFactory
	{
		ISyntaxHighlighting CreateSyntaxHighlighting (ITextView textView);
		ISyntaxHighlighting CreateSyntaxHighlighting (ITextView textView, string defaultScope);
    }
}
