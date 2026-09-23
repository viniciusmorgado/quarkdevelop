using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.Ide.RoslynServices
{
	[ExportWorkspaceServiceFactory (typeof (IDocumentNavigationService), ServiceLayer.Host), Shared]
	internal sealed class VisualStudioDocumentNavigationServiceFactory : IWorkspaceServiceFactory
	{
		private readonly IDocumentNavigationService _singleton;

		[Import]
		public IBufferGraphFactoryService BufferGraphFactoryService { get; set; }

		[ImportingConstructor]
		[Obsolete (MefConstruction.ImportingConstructorMessage, error: true)]
		private VisualStudioDocumentNavigationServiceFactory ()
		{
			_singleton = new MonoDevelopDocumentNavigationService (this);
		}

		public IWorkspaceService CreateService (HostWorkspaceServices workspaceServices)
		{
			return _singleton;
		}
	}

	class MonoDevelopDocumentNavigationService : IDocumentNavigationService
	{
		private VisualStudioDocumentNavigationServiceFactory factory;

		public MonoDevelopDocumentNavigationService (VisualStudioDocumentNavigationServiceFactory visualStudioDocumentNavigationServiceFactory)
		{
			this.factory = visualStudioDocumentNavigationServiceFactory;
		}

		// Roslyn 5.9 IDocumentNavigationService is asynchronous and returns navigable locations;
		// they wrap the synchronous MonoDevelop implementation below (navigation runs on the UI thread).
		public Task<bool> CanNavigateToSpanAsync (Workspace workspace, DocumentId documentId, TextSpan textSpan, bool allowInvalidSpan, CancellationToken cancellationToken)
			=> Task.FromResult (CanNavigateToSpan (workspace, documentId, textSpan));

		public Task<bool> CanNavigateToPositionAsync (Workspace workspace, DocumentId documentId, int position, int virtualSpace, bool allowInvalidPosition, CancellationToken cancellationToken)
			=> Task.FromResult (CanNavigateToPosition (workspace, documentId, position, virtualSpace));

		public Task<INavigableLocation> GetLocationForSpanAsync (Workspace workspace, DocumentId documentId, TextSpan textSpan, bool allowInvalidSpan, CancellationToken cancellationToken)
		{
			if (!CanNavigateToSpan (workspace, documentId, textSpan))
				return Task.FromResult<INavigableLocation> (null);
			return Task.FromResult<INavigableLocation> (new NavigableLocation ((options, token) =>
				Runtime.RunInMainThread (() => TryNavigateToSpan (workspace, documentId, textSpan, null))));
		}

		public Task<INavigableLocation> GetLocationForPositionAsync (Workspace workspace, DocumentId documentId, int position, int virtualSpace, bool allowInvalidPosition, CancellationToken cancellationToken)
		{
			if (!CanNavigateToPosition (workspace, documentId, position, virtualSpace))
				return Task.FromResult<INavigableLocation> (null);
			return Task.FromResult<INavigableLocation> (new NavigableLocation ((options, token) =>
				Runtime.RunInMainThread (() => TryNavigateToPosition (workspace, documentId, position, virtualSpace, null))));
		}

		public bool CanNavigateToSpan (Workspace workspace, DocumentId documentId, TextSpan textSpan)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);
			var document = workspace.CurrentSolution.GetDocument (documentId);

			if (!IsSecondaryBuffer (workspace, document)) {
				return true;
			}

			var text = document.GetTextSynchronously (CancellationToken.None);

			var boundedTextSpan = GetSpanWithinDocumentBounds (textSpan, text.Length);
			if (boundedTextSpan != textSpan) {
				throw new ArgumentOutOfRangeException ();
			}

			return CanMapFromSecondaryBufferToPrimaryBuffer (workspace, document, textSpan);
		}

		public bool CanNavigateToLineAndOffset (Workspace workspace, DocumentId documentId, int lineNumber, int offset)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);
			var document = workspace.CurrentSolution.GetDocument (documentId);

			if (!IsSecondaryBuffer (workspace, document)) {
				return true;
			}

			var text = document.GetTextSynchronously (CancellationToken.None);
			var textSpan = new TextSpan (text.Lines [lineNumber].Start + offset, 0);

			return CanMapFromSecondaryBufferToPrimaryBuffer (workspace, document, textSpan);
		}

		public bool CanNavigateToPosition (Workspace workspace, DocumentId documentId, int position, int virtualSpace = 0)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);
			var document = workspace.CurrentSolution.GetDocument (documentId);

			if (!IsSecondaryBuffer (workspace, document)) {
				return true;
			}

			var text = document.GetTextSynchronously (CancellationToken.None);

			var boundedPosition = GetPositionWithinDocumentBounds (position, text.Length);
			if (boundedPosition != position) {
				throw new ArgumentOutOfRangeException ();
			}

			var textSpan = new TextSpan (position+ virtualSpace,0);

			return CanMapFromSecondaryBufferToPrimaryBuffer (workspace, document, textSpan);
		}

		public bool TryNavigateToSpan (Workspace workspace, DocumentId documentId, TextSpan textSpan, OptionSet options)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);

			Runtime.AssertMainThread ();

			var document = workspace.CurrentSolution.GetDocument (documentId);
			if (document == null) {
				return false;
			}

			var text = document.GetTextSynchronously (CancellationToken.None);

			var boundedTextSpan = GetSpanWithinDocumentBounds (textSpan, text.Length);
			if (boundedTextSpan != textSpan) {
				throw new ArgumentOutOfRangeException ();
			}

			if (IsSecondaryBuffer (workspace, document) &&
				!TryMapSpanFromSecondaryBufferToPrimaryBuffer (textSpan, workspace, document, out textSpan)) {
				return false;
			}

			return NavigateTo (document, textSpan);
		}

		public bool TryNavigateToLineAndOffset (Workspace workspace, DocumentId documentId, int lineNumber, int offset, OptionSet options)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);

			Runtime.AssertMainThread ();

			var document = workspace.CurrentSolution.GetDocument (documentId);
			if (document == null) {
				return false;
			}

			var textSpan = new TextSpan (offset, 0);

			if (IsSecondaryBuffer (workspace, document) &&
				!TryMapSpanFromSecondaryBufferToPrimaryBuffer (textSpan, workspace, document, out textSpan)) {
				return false;
			}

			return NavigateTo (document, textSpan);
		}

		public bool TryNavigateToPosition (Workspace workspace, DocumentId documentId, int position, int virtualSpace, OptionSet options)
		{
			// Navigation should not change the context of linked files and Shared Projects.
			documentId = workspace.GetDocumentIdInCurrentContext (documentId);

			Runtime.AssertMainThread ();

			var document = workspace.CurrentSolution.GetDocument (documentId);
			if (document == null) {
				return false;
			}

			var textSpan = new TextSpan (position + virtualSpace, 0);

			if (IsSecondaryBuffer (workspace, document) &&
				!TryMapSpanFromSecondaryBufferToPrimaryBuffer (textSpan, workspace, document, out textSpan)) {
				return false;
			}

			return NavigateTo (document, textSpan);
		}

		/// <summary>
		/// It is unclear why, but we are sometimes asked to navigate to a position that is not
		/// inside the bounds of the associated <see cref="Document"/>. This method returns a
		/// position that is guaranteed to be inside the <see cref="Document"/> bounds. If the
		/// returned position is different from the given position, then the worst observable
		/// behavior is either no navigation or navigation to the end of the document. See the
		/// following bugs for more details:
		///     https://devdiv.visualstudio.com/DevDiv/_workitems?id=112211
		///     https://devdiv.visualstudio.com/DevDiv/_workitems?id=136895
		///     https://devdiv.visualstudio.com/DevDiv/_workitems?id=224318
		///     https://devdiv.visualstudio.com/DevDiv/_workitems?id=235409
		/// </summary>
		private static int GetPositionWithinDocumentBounds (int position, int documentLength)
		{
			return Math.Min (documentLength, Math.Max (position, 0));
		}

		/// <summary>
		/// It is unclear why, but we are sometimes asked to navigate to a <see cref="TextSpan"/>
		/// that is not inside the bounds of the associated <see cref="Document"/>. This method
		/// returns a span that is guaranteed to be inside the <see cref="Document"/> bounds. If
		/// the returned span is different from the given span, then the worst observable behavior
		/// is either no navigation or navigation to the end of the document.
		/// See https://github.com/dotnet/roslyn/issues/7660 for more details.
		/// </summary>
		private static TextSpan GetSpanWithinDocumentBounds (TextSpan span, int documentLength)
		{
			return TextSpan.FromBounds (GetPositionWithinDocumentBounds (span.Start, documentLength), GetPositionWithinDocumentBounds (span.End, documentLength));
		}


		private bool NavigateTo (Document document, TextSpan span)
		{
			string filePath = document.FilePath;
			filePath = GetActualFilePathToOpen (filePath);
			var proj = (document.Project.Solution.Workspace as MonoDevelopWorkspace)?.GetMonoProject (document.Project);
			var task = IdeApp.Workbench.OpenDocument (new Gui.FileOpenInformation (filePath, proj) {
				Offset = span.Start
			});
			return true;
		}

		/// <summary>
		/// Razor: Strip the .g.cs since we want to open the corresponding .cshtml or .razor document.
		///
		/// In Visual Studio for Windows the underlying C# buffer is added to the workspace with the
		/// .cshtml or .razor extension (without the .g.cs) part, so they don't have to worry about
		/// this. In our case we have an assumption somewhere that all C# documents in the workspace
		/// have the .cs extension, so we're adding the .g.cs part that we need to strip here.
		/// 
		/// This is not great to hardcode application-specific logic here, but we don't anticipate
		/// more scenarios where we want to open a different file than requested, so it doesn't
		/// warrant an extension point at this time.
		/// </summary>
		string GetActualFilePathToOpen (string filePath)
		{
			if (filePath == null) {
				return null;
			}

			if (filePath.EndsWith (".cshtml.g.cs", StringComparison.OrdinalIgnoreCase) ||
				filePath.EndsWith (".razor.g.cs", StringComparison.OrdinalIgnoreCase)) {
				filePath = filePath.Substring (0, filePath.Length - ".g.cs".Length);
			}

			return filePath;
		}

		private bool IsSecondaryBuffer (Workspace workspace, Document document)
		{
			var containedDocument = MonoDevelopHostDocumentRegistration.FromDocument (document);
			if (containedDocument == null) {
				return false;
			}

			return true;
		}

		public bool TryMapSpanFromSecondaryBufferToPrimaryBuffer (TextSpan spanInSecondaryBuffer, Microsoft.CodeAnalysis.Workspace workspace, Document document, out TextSpan spanInPrimaryBuffer)
		{
			spanInPrimaryBuffer = default;

			var containedDocument = MonoDevelopHostDocumentRegistration.FromDocument (document);
			if (containedDocument == null) {
				return false;
			}

			var projectionBuffer = containedDocument.TopBuffer;

			var bufferGraph = factory.BufferGraphFactoryService.CreateBufferGraph (projectionBuffer);

			if (document.TryGetText(out var sourceText) && sourceText.Container.TryGetTextBuffer() is ITextBuffer languageBuffer) {
				var secondarySnapshot = languageBuffer.CurrentSnapshot;
				var snapshotSpanInSecondaryBuffer = new SnapshotSpan (secondarySnapshot, new Span (spanInSecondaryBuffer.Start, spanInSecondaryBuffer.Length));
				var topBufferSnapshotSpan = bufferGraph.MapUpToSnapshot (
					snapshotSpanInSecondaryBuffer,
					SpanTrackingMode.EdgeExclusive,
					projectionBuffer.CurrentSnapshot).FirstOrDefault();
				if (topBufferSnapshotSpan != default) {
					spanInPrimaryBuffer = new TextSpan (topBufferSnapshotSpan.Start, topBufferSnapshotSpan.Length);
					return true;
				}
			}

			return false;
		}

		private bool CanMapFromSecondaryBufferToPrimaryBuffer (Workspace workspace, Document document, TextSpan spanInSecondaryBuffer)
		{
			return TryMapSpanFromSecondaryBufferToPrimaryBuffer (spanInSecondaryBuffer, workspace, document, out var spanInPrimaryBuffer);
		}
	}
}
