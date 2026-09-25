//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See LICENSES/Apache-2.0.txt in the repository root for license information.
//
using System;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Platform
{
	[Obsolete ("Use the Microsoft.VisualStudio.Text.Editor APIs")]
	public static class PlatformExtensions
    {
        public static ITextBuffer GetPlatformTextBuffer(this MonoDevelop.Ide.Editor.TextEditor textEditor)
        {
            return textEditor.TextView.TextBuffer;
        }

        public static ITextView GetPlatformTextView(this MonoDevelop.Ide.Editor.TextEditor textEditor)
        {
            return textEditor.TextView;
        }

        public static MonoDevelop.Ide.Editor.ITextDocument GetTextEditor(this ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetProperty<MonoDevelop.Ide.Editor.ITextDocument>(typeof(MonoDevelop.Ide.Editor.ITextDocument));
        }

        public static MonoDevelop.Ide.Editor.ITextDocument GetTextEditor (this ITextView textView)
        {
            return textView.Properties.GetProperty<MonoDevelop.Ide.Editor.TextEditor>(typeof(MonoDevelop.Ide.Editor.TextEditor));
        }
    }
}