//
// RoslynClassificationTaggerProvider.cs
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
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.CSharp.Highlighting
{
	/// <summary>
	/// Content types of Roslyn languages. Upstream they came from Roslyn EditorFeatures, which is not used on Linux
	/// (T089); the C# MIME type maps to the "CSharp" content type (CSharpBinding.addin.xml).
	/// </summary>
	static class RoslynContentTypeDefinitions
	{
		public const string RoslynContentType = "Roslyn Languages";
		public const string CSharpContentType = "CSharp";

		[Export]
		[Name (RoslynContentType)]
		[BaseDefinition ("code")]
		internal static ContentTypeDefinition RoslynContentTypeDefinition = null;

		[Export]
		[Name (CSharpContentType)]
		[BaseDefinition (RoslynContentType)]
		internal static ContentTypeDefinition CSharpContentTypeDefinition = null;
	}

	/// <summary>
	/// Syntactic and semantic classification of C# documents for the editor (TagBasedSyntaxHighlighting), computed
	/// with Roslyn's public Classifier. It replaces the classification taggers of Roslyn EditorFeatures (T089).
	/// </summary>
	[Export (typeof (ITaggerProvider))]
	[ContentType (RoslynContentTypeDefinitions.CSharpContentType)]
	[TagType (typeof (IClassificationTag))]
	sealed class RoslynClassificationTaggerProvider : ITaggerProvider
	{
		readonly IClassificationTypeRegistryService registry;
		readonly object classificationTypesLock = new object ();
		readonly Dictionary<string, IClassificationTag> tags = new Dictionary<string, IClassificationTag> ();

		[ImportingConstructor]
		public RoslynClassificationTaggerProvider (IClassificationTypeRegistryService registry)
		{
			this.registry = registry;
		}

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
		{
			return buffer.Properties.GetOrCreateSingletonProperty (() => new RoslynClassificationTagger (buffer, this)) as ITagger<T>;
		}

		/// <summary>
		/// The tag of a Roslyn classification. Classification types that the editor platform does not define (Roslyn's
		/// definitions were part of EditorFeatures) are created in the registry on first use.
		/// </summary>
		internal IClassificationTag GetTag (string classificationTypeName)
		{
			lock (classificationTypesLock) {
				if (tags.TryGetValue (classificationTypeName, out var tag))
					return tag;
				var type = registry.GetClassificationType (classificationTypeName);
				if (type == null) {
					// Names of symbols derive from "identifier" when the platform defines it (the highlighting maps
					// Roslyn's names to theme scopes by name, TagBasedSyntaxHighlighting).
					var baseType = classificationTypeName.EndsWith (" name", StringComparison.Ordinal)
						? registry.GetClassificationType (PredefinedClassificationTypeNames.Identifier)
						: null;
					type = registry.CreateClassificationType (classificationTypeName, baseType != null ? new[] { baseType } : Array.Empty<IClassificationType> ());
				}
				tag = type != null ? new ClassificationTag (type) : null;
				tags[classificationTypeName] = tag;
				return tag;
			}
		}
	}

	sealed class RoslynClassificationTagger : ITagger<IClassificationTag>
	{
		static readonly ImmutableHashSet<string> additiveTypeNames = ClassificationTypeNames.AdditiveTypeNames.ToImmutableHashSet ();

		readonly ITextBuffer buffer;
		readonly RoslynClassificationTaggerProvider provider;
		readonly object gate = new object ();
		ITextSnapshot classifiedSnapshot;
		IReadOnlyList<ClassifiedSpan> classifiedSpans = Array.Empty<ClassifiedSpan> ();
		CancellationTokenSource cancellation;

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public RoslynClassificationTagger (ITextBuffer buffer, RoslynClassificationTaggerProvider provider)
		{
			this.buffer = buffer;
			this.provider = provider;
			buffer.Changed += (sender, e) => ScheduleClassification ();
			// Semantic classifications change with the workspace (the document is opened in it after the buffer
			// is created, other documents and references change): classify again when it changes.
			var registration = Microsoft.CodeAnalysis.Workspace.GetWorkspaceRegistration (buffer.AsTextContainer ());
			// Publicizer exposes the event's backing field under the same name: subscribe through reflection.
			typeof (Microsoft.CodeAnalysis.WorkspaceRegistration).GetEvent ("WorkspaceChanged").AddEventHandler (registration, new EventHandler ((sender, e) => {
				SubscribeTo (registration.Workspace);
				ScheduleClassification ();
			}));
			SubscribeTo (registration.Workspace);
			ScheduleClassification ();
		}

		Microsoft.CodeAnalysis.Workspace subscribedWorkspace;

		void SubscribeTo (Microsoft.CodeAnalysis.Workspace workspace)
		{
			lock (gate) {
				if (workspace == null || workspace == subscribedWorkspace)
					return;
				subscribedWorkspace = workspace;
			}
			workspace.WorkspaceChanged += (sender, e) => {
				if (sender == subscribedWorkspace)
					ScheduleClassification ();
			};
		}

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags (NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0)
				return Array.Empty<ITagSpan<IClassificationTag>> ();
			ITextSnapshot snapshot;
			IReadOnlyList<ClassifiedSpan> classified;
			lock (gate) {
				snapshot = classifiedSnapshot;
				classified = classifiedSpans;
			}
			if (snapshot == null)
				return Array.Empty<ITagSpan<IClassificationTag>> ();
			var requested = spans[0].Snapshot;
			var result = new List<ITagSpan<IClassificationTag>> ();
			foreach (var classifiedSpan in classified) {
				if (additiveTypeNames.Contains (classifiedSpan.ClassificationType))
					continue;
				var tag = provider.GetTag (classifiedSpan.ClassificationType);
				if (tag == null)
					continue;
				var span = new SnapshotSpan (snapshot, classifiedSpan.TextSpan.Start, classifiedSpan.TextSpan.Length);
				// Classifications of an older snapshot are shown at their tracked position until the new ones arrive.
				if (snapshot != requested)
					span = span.TranslateTo (requested, SpanTrackingMode.EdgeExclusive);
				if (spans.IntersectsWith (span))
					result.Add (new TagSpan<IClassificationTag> (span, tag));
			}
			return result;
		}

		void ScheduleClassification ()
		{
			CancellationToken token;
			lock (gate) {
				cancellation?.Cancel ();
				cancellation = new CancellationTokenSource ();
				token = cancellation.Token;
			}
			var snapshot = buffer.CurrentSnapshot;
			Task.Run (() => ClassifyAsync (snapshot, token), token);
		}

		async Task ClassifyAsync (ITextSnapshot snapshot, CancellationToken token)
		{
			try {
				// Coalesce bursts of typing.
				await Task.Delay (150, token).ConfigureAwait (false);
				var document = snapshot.GetOpenDocumentInCurrentContextWithChanges ();
				if (document == null)
					return;
				var spans = await Classifier.GetClassifiedSpansAsync (document, new TextSpan (0, snapshot.Length), token).ConfigureAwait (false);
				var list = spans.ToList ();
				lock (gate) {
					if (token.IsCancellationRequested)
						return;
					classifiedSnapshot = snapshot;
					classifiedSpans = list;
				}
				await Runtime.RunInMainThread (() => {
					var current = buffer.CurrentSnapshot;
					TagsChanged?.Invoke (this, new SnapshotSpanEventArgs (new SnapshotSpan (current, 0, current.Length)));
				}).ConfigureAwait (false);
			} catch (OperationCanceledException) {
			} catch (Exception e) {
				LoggingService.LogError ("C# classification failed", e);
			}
		}
	}
}
