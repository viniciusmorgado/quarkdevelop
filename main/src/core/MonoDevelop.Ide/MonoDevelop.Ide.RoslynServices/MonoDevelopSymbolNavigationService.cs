using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.Ide.RoslynServices
{
	[ExportWorkspaceServiceFactory (typeof (ISymbolNavigationService), ServiceLayer.Host), Shared]
	internal class VisualStudioSymbolNavigationServiceFactory : IWorkspaceServiceFactory
	{
		private readonly ISymbolNavigationService _singleton;

		[ImportingConstructor]
		private VisualStudioSymbolNavigationServiceFactory ()
		{
			_singleton = new MonoDevelopSymbolNavigationService ();
		}

		public IWorkspaceService CreateService (HostWorkspaceServices workspaceServices)
		{
			return _singleton;
		}
	}

	class MonoDevelopSymbolNavigationService : ISymbolNavigationService
	{
		public bool TryNavigateToSymbol (ISymbol symbol, Project project, OptionSet options = null, CancellationToken cancellationToken = default)
		{
			IdeApp.ProjectOperations.JumpToDeclaration (symbol, ((MonoDevelopWorkspace)project.Solution.Workspace).GetMonoProject (project));
			return true;
		}

		public bool TrySymbolNavigationNotify (ISymbol symbol, Project project, CancellationToken cancellationToken)
		{
			IdeApp.ProjectOperations.JumpToDeclaration (symbol, ((MonoDevelopWorkspace)project.Solution.Workspace).GetMonoProject (project));
			return true;
		}

		public bool WouldNavigateToSymbol (DefinitionItem definitionItem, Solution solution, CancellationToken cancellationToken, out string filePath, out int lineNumber, out int charOffset)
		{
			filePath = null;
			lineNumber = -1;
			charOffset = -1;
			return true;
		}

		// Roslyn 5.9 ISymbolNavigationService is asynchronous; navigation is deferred to the returned location.
		public Task<INavigableLocation> GetNavigableLocationAsync (ISymbol symbol, Project project, CancellationToken cancellationToken)
		{
			return Task.FromResult<INavigableLocation> (new NavigableLocation ((options, token) =>
				Runtime.RunInMainThread (() => TryNavigateToSymbol (symbol, project, null, token))));
		}

		public Task<bool> TrySymbolNavigationNotifyAsync (ISymbol symbol, Project project, CancellationToken cancellationToken)
		{
			return Runtime.RunInMainThread (() => TrySymbolNavigationNotify (symbol, project, cancellationToken));
		}

		// No external (metadata-as-source) location is provided by MonoDevelop.
		public Task<(string filePath, LinePosition linePosition)?> GetExternalNavigationSymbolLocationAsync (DefinitionItem definitionItem, CancellationToken cancellationToken)
		{
			return Task.FromResult<(string filePath, LinePosition linePosition)?> (null);
		}
	}
}
