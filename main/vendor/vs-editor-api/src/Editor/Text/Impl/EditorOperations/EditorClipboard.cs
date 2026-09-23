//
// EditorClipboard.cs
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

namespace Microsoft.VisualStudio.Text.Operations
{
	/// <summary>
	/// Clipboard used by the editor operations (copy, cut, paste). MonoDevelop local patch: upstream used the
	/// WPF clipboard; the host installs its own backend (the GTK clipboard in the IDE) through
	/// <see cref="EditorClipboard.Current"/>.
	/// </summary>
	public interface IEditorClipboard
	{
		/// <summary>True when the clipboard holds text.</summary>
		bool ContainsText ();

		/// <summary>
		/// Returns the clipboard text (null when there is none) and whether it was copied by the editor in
		/// line mode (no selection) or from a box selection.
		/// </summary>
		string GetText (out bool lineCutCopyTag, out bool boxCutCopyTag);

		/// <summary>Replaces the clipboard content. <paramref name="rtf"/> may be null.</summary>
		void SetText (string text, string rtf, bool lineCutCopyTag, bool boxCutCopyTag);
	}

	public static class EditorClipboard
	{
		static IEditorClipboard current = new InProcessEditorClipboard ();

		/// <summary>The clipboard backend; an in-process clipboard until the host sets one.</summary>
		public static IEditorClipboard Current {
			get => current;
			set => current = value ?? new InProcessEditorClipboard ();
		}
	}

	/// <summary>Clipboard that only lives in this process (headless use and tests).</summary>
	public sealed class InProcessEditorClipboard : IEditorClipboard
	{
		readonly object gate = new object ();
		string text;
		bool lineTag, boxTag;

		public string Rtf { get; private set; }

		public bool ContainsText ()
		{
			lock (gate)
				return text != null;
		}

		public string GetText (out bool lineCutCopyTag, out bool boxCutCopyTag)
		{
			lock (gate) {
				lineCutCopyTag = lineTag;
				boxCutCopyTag = boxTag;
				return text;
			}
		}

		public void SetText (string text, string rtf, bool lineCutCopyTag, bool boxCutCopyTag)
		{
			lock (gate) {
				this.text = text ?? string.Empty;
				Rtf = rtf;
				lineTag = lineCutCopyTag;
				boxTag = boxCutCopyTag;
			}
		}
	}
}
