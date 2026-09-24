//
// EditorTextBufferExtensions.cs
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
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Text
{
	/// <summary>
	/// The bridge between editor text buffers/snapshots and Roslyn source texts. It used to come from Roslyn's
	/// EditorFeatures.Text assembly, which is not available for Roslyn 5.9 on nuget.org; this is a minimal
	/// replacement with the extension methods MonoDevelop uses (same names, so callers keep compiling).
	/// </summary>
	static class EditorTextBufferExtensions
	{
		/// <summary>
		/// Gets the source text container of a text buffer (one per buffer).
		/// </summary>
		public static SourceTextContainer AsTextContainer (this ITextBuffer buffer)
			=> TextBufferContainer.From (buffer);

		/// <summary>
		/// Gets the text buffer of a container created by <see cref="AsTextContainer"/>.
		/// </summary>
		public static ITextBuffer GetTextBuffer (this SourceTextContainer container)
			=> TryGetTextBuffer (container) ?? throw new ArgumentException ("The container is not backed by an editor text buffer", nameof (container));

		/// <summary>
		/// Gets the text buffer of a container created by <see cref="AsTextContainer"/>, or null.
		/// </summary>
		public static ITextBuffer TryGetTextBuffer (this SourceTextContainer container)
			=> (container as TextBufferContainer)?.TryFindEditorTextBuffer ();

		/// <summary>
		/// Gets the source text of a text snapshot (one per snapshot).
		/// </summary>
		public static SourceText AsText (this ITextSnapshot snapshot)
			=> snapshot == null ? null : SnapshotSourceText.From (snapshot);

		/// <summary>
		/// Gets the editor snapshot a source text was created from, or null.
		/// </summary>
		public static ITextSnapshot FindCorrespondingEditorTextSnapshot (this SourceText text)
			=> (text as SnapshotSourceText)?.Snapshot;

		public static Document GetOpenDocumentInCurrentContextWithChanges (this ITextSnapshot snapshot)
			=> snapshot.AsText ().GetOpenDocumentInCurrentContextWithChanges ();

		public static ImmutableArray<Document> GetRelatedDocumentsWithChanges (this ITextSnapshot snapshot)
			=> snapshot.AsText ().GetRelatedDocumentsWithChanges ();

		sealed class TextBufferContainer : SourceTextContainer
		{
			static readonly ConditionalWeakTable<ITextBuffer, TextBufferContainer> containers = new ConditionalWeakTable<ITextBuffer, TextBufferContainer> ();

			readonly WeakReference<ITextBuffer> weakBuffer;
			readonly object gate = new object ();
			readonly SourceText lastKnownText;
			EventHandler<TextChangeEventArgs> textChanged;

			TextBufferContainer (ITextBuffer buffer)
			{
				weakBuffer = new WeakReference<ITextBuffer> (buffer);
				lastKnownText = SourceText.From (string.Empty);
			}

			public static TextBufferContainer From (ITextBuffer buffer)
			{
				ArgumentNullException.ThrowIfNull (buffer);
				return containers.GetValue (buffer, b => new TextBufferContainer (b));
			}

			public ITextBuffer TryFindEditorTextBuffer ()
				=> weakBuffer.TryGetTarget (out var buffer) ? buffer : null;

			public override SourceText CurrentText
				=> TryFindEditorTextBuffer ()?.CurrentSnapshot.AsText () ?? lastKnownText;

			public override event EventHandler<TextChangeEventArgs> TextChanged {
				add {
					lock (gate) {
						var buffer = TryFindEditorTextBuffer ();
						if (textChanged == null && buffer != null)
							buffer.ChangedHighPriority += OnTextContentChanged;
						textChanged += value;
					}
				}
				remove {
					lock (gate) {
						textChanged -= value;
						var buffer = TryFindEditorTextBuffer ();
						if (textChanged == null && buffer != null)
							buffer.ChangedHighPriority -= OnTextContentChanged;
					}
				}
			}

			void OnTextContentChanged (object sender, TextContentChangedEventArgs args)
			{
				var handler = textChanged;
				if (handler == null || args.Changes.Count == 0)
					return;

				var ranges = args.Changes.Select (c => new TextChangeRange (new TextSpan (c.OldSpan.Start, c.OldSpan.Length), c.NewLength)).ToList ();
				handler (this, new TextChangeEventArgs (args.Before.AsText (), args.After.AsText (), ranges));
			}
		}

		sealed class SnapshotSourceText : SourceText
		{
			static readonly ConditionalWeakTable<ITextSnapshot, SnapshotSourceText> texts = new ConditionalWeakTable<ITextSnapshot, SnapshotSourceText> ();

			readonly TextBufferContainer container;
			readonly Encoding encoding;

			SnapshotSourceText (ITextSnapshot snapshot, TextBufferContainer container)
			{
				Snapshot = snapshot;
				this.container = container;
				if (snapshot.TextBuffer.Properties.TryGetProperty (typeof (ITextDocument), out ITextDocument document))
					encoding = document.Encoding;
			}

			public static SnapshotSourceText From (ITextSnapshot snapshot)
				=> texts.GetValue (snapshot, s => new SnapshotSourceText (s, TextBufferContainer.From (s.TextBuffer)));

			public ITextSnapshot Snapshot { get; }

			public override Encoding Encoding => encoding;

			public override int Length => Snapshot.Length;

			public override char this[int position] => Snapshot[position];

			public override SourceTextContainer Container => container;

			public override void CopyTo (int sourceIndex, char[] destination, int destinationIndex, int count)
				=> Snapshot.CopyTo (sourceIndex, destination, destinationIndex, count);

			public override string ToString (TextSpan span)
				=> Snapshot.GetText (span.Start, span.Length);

			public override string ToString ()
				=> Snapshot.GetText ();

			public override TextLineCollection GetLinesCore () // public: Roslyn is publicized (Roslyn 5.9)
				=> new LineCollection (this);

			public override IReadOnlyList<TextChangeRange> GetChangeRanges (SourceText oldText)
			{
				if (oldText is SnapshotSourceText old && old.Snapshot.TextBuffer == Snapshot.TextBuffer) {
					var oldVersion = old.Snapshot.Version;
					var newVersion = Snapshot.Version;
					if (oldVersion.VersionNumber == newVersion.VersionNumber)
						return ImmutableArray<TextChangeRange>.Empty;
					if (oldVersion.VersionNumber < newVersion.VersionNumber) {
						TextChangeRange? range = null;
						for (var version = oldVersion; version != null && version.VersionNumber < newVersion.VersionNumber; version = version.Next) {
							if (version.Changes == null || version.Changes.Count == 0)
								continue;
							var versionRange = TextChangeRange.Collapse (version.Changes.Select (c => new TextChangeRange (new TextSpan (c.OldPosition, c.OldLength), c.NewLength)));
							range = range.HasValue ? Compose (range.Value, versionRange) : versionRange;
						}
						return range.HasValue ? ImmutableArray.Create (range.Value) : ImmutableArray<TextChangeRange>.Empty;
					}
				}
				return base.GetChangeRanges (oldText);
			}

			// Composes two sequential changes into one range relative to the text before the first change.
			static TextChangeRange Compose (TextChangeRange first, TextChangeRange second)
			{
				int start = Math.Min (first.Span.Start, second.Span.Start);
				int firstNewEnd = first.Span.Start + first.NewLength;
				int end = Math.Max (firstNewEnd, second.Span.End);
				int oldEnd = end - first.NewLength + first.Span.Length;
				int newEnd = end + second.NewLength - second.Span.Length;
				return new TextChangeRange (TextSpan.FromBounds (start, oldEnd), newEnd - start);
			}

			sealed class LineCollection : TextLineCollection
			{
				readonly SnapshotSourceText text;

				public LineCollection (SnapshotSourceText text)
				{
					this.text = text;
				}

				public override int Count => text.Snapshot.LineCount;

				public override TextLine this[int index] {
					get {
						var line = text.Snapshot.GetLineFromLineNumber (index);
						return TextLine.FromSpan (text, TextSpan.FromBounds (line.Start, line.End));
					}
				}

				public override int IndexOf (int position)
					=> text.Snapshot.GetLineNumberFromPosition (position);

				public override TextLine GetLineFromPosition (int position)
					=> this[IndexOf (position)];

				public override LinePosition GetLinePosition (int position)
				{
					var line = text.Snapshot.GetLineFromPosition (position);
					return new LinePosition (line.LineNumber, position - line.Start);
				}
			}
		}
	}
}
