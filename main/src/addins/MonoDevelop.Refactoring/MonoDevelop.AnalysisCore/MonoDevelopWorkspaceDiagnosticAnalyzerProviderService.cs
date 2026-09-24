//
// MonoDevelopWorkspaceDiagnosticAnalyzerProviderService.cs
//
// Author:
//       therzok <marius.ungureanu@xamarin.com>
//
// Copyright (c) 2017 (c) Marius Ungureanu
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
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Core.AddIns;

namespace MonoDevelop.AnalysisCore
{
	// Roslyn 4+ removed IWorkspaceDiagnosticAnalyzerProviderService: host analyzers are solution analyzer references.
	// This service computes them as before and adds them to each workspace whose documents are analyzed
	// (EnsureHostAnalyzers); callers import it by its own type.
	[Export (typeof (MonoDevelopWorkspaceDiagnosticAnalyzerProviderService))]
	partial class MonoDevelopWorkspaceDiagnosticAnalyzerProviderService
	{
		static readonly AnalyzerAssemblyLoader analyzerAssemblyLoader = new AnalyzerAssemblyLoader ();
		readonly static string diagnosticAnalyzerAssembly = typeof (DiagnosticAnalyzerAttribute).Assembly.GetName ().Name;

		private TaskCompletionSource<OptionsTable> optionsCompletionSource = new TaskCompletionSource<OptionsTable> ();
		internal Task<OptionsTable> GetOptionsAsync () => optionsCompletionSource.Task;
		readonly Task<ImmutableArray<AnalyzerReference>> hostDiagnosticAnalyzerInfoTask;
		readonly HashSet<Workspace> trackedWorkspaces = new HashSet<Workspace> ();

		const string extensionPath = "/MonoDevelop/Refactoring/AnalyzerAssemblies";
		string [] RuntimeEnabledAssemblies;
		public MonoDevelopWorkspaceDiagnosticAnalyzerProviderService ()
		{
			hostDiagnosticAnalyzerInfoTask = Task.Run (() => CreateHostDiagnosticAnalyzerPackages ());
		}

		void LoadAnalyzerAssemblies()
		{
			// Without the add-in engine (tests) only the assemblies that reference Roslyn are host analyzers.
			RuntimeEnabledAssemblies = AddinManager.IsInitialized
				? AddinManager.GetExtensionNodes<AssemblyExtensionNode> (extensionPath).Select (b => b.FileName).ToArray ()
				: Array.Empty<string> ();
		}

		public IAnalyzerAssemblyLoader GetAnalyzerAssemblyLoader ()
		{
			return analyzerAssemblyLoader;
		}

		public Task<ImmutableArray<AnalyzerReference>> GetHostAnalyzerReferencesAsync () => hostDiagnosticAnalyzerInfoTask;

		/// <summary>
		/// Adds the host analyzer references to the solution of the workspace, now and whenever the workspace
		/// replaces its solution (a solution load or reload creates it without them).
		/// </summary>
		public async Task EnsureHostAnalyzersAsync (Workspace workspace)
		{
			if (workspace == null)
				return;
			var references = await hostDiagnosticAnalyzerInfoTask.ConfigureAwait (false);
			lock (trackedWorkspaces) {
				if (trackedWorkspaces.Add (workspace))
					workspace.WorkspaceChanged += (sender, e) => {
						if (e.Kind == WorkspaceChangeKind.SolutionAdded || e.Kind == WorkspaceChangeKind.SolutionReloaded || e.Kind == WorkspaceChangeKind.SolutionCleared)
							AddHostAnalyzers (workspace, references);
					};
			}
			AddHostAnalyzers (workspace, references);
		}

		static void AddHostAnalyzers (Workspace workspace, ImmutableArray<AnalyzerReference> references)
		{
			workspace.SetCurrentSolution (
				solution => solution.AnalyzerReferences.Count > 0 ? solution : solution.WithAnalyzerReferences (references),
				WorkspaceChangeKind.SolutionChanged);
		}

		ImmutableArray<AnalyzerReference> CreateHostDiagnosticAnalyzerPackages ()
		{
			LoadAnalyzerAssemblies ();
			var assemblies = ImmutableArray.CreateBuilder<string> ();
			var options = new OptionsTable ();
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies ()) {
				try {
					var assemblyName = asm.GetName ().Name;
					if (Array.IndexOf (RuntimeEnabledAssemblies, assemblyName) == -1) {
						switch (assemblyName) {
						//blacklist
						case "FSharpBinding":
							continue;
						//addin assemblies that reference roslyn
						default:
							var refAsm = asm.GetReferencedAssemblies ();
							if (refAsm.Any (a => a.Name == diagnosticAnalyzerAssembly))
								break;
							continue;
						}
					}

					// Figure out a way to disable E&C analyzers.
					assemblies.Add (asm.Location);
					options.ProcessAssembly (asm);
				} catch (Exception e) {
					LoggingService.LogError ("Error while loading diagnostics in " + asm.FullName, e);
				}
			}
			optionsCompletionSource.SetResult (options);
			return assemblies.Select (path => (AnalyzerReference)new AnalyzerFileReference (path, analyzerAssemblyLoader)).ToImmutableArray ();
		}
	}
}
