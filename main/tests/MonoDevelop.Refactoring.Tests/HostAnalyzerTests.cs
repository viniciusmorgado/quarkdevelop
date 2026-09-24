//
// HostAnalyzerTests.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.AnalysisCore;
using MonoDevelop.CodeActions;
using NUnit.Framework;

namespace MonoDevelop.Refactoring.Tests
{
	/// <summary>
	/// Task T090: Roslyn 4+ has no IWorkspaceDiagnosticAnalyzerProviderService nor push-based IDiagnosticService; the
	/// Refactoring add-in adds its host analyzers to the solution and pulls diagnostics from IDiagnosticAnalyzerService.
	/// </summary>
	[TestFixture]
	public class HostAnalyzerTests
	{
		static (AdhocWorkspace Workspace, Document Document) CreateDocument (string text)
		{
			var workspace = new AdhocWorkspace (MefHostServices.DefaultHost);
			var trustedAssemblies = ((string)AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")).Split (Path.PathSeparator);
			var references = trustedAssemblies
				.Where (path => Path.GetFileName (path) is "System.Private.CoreLib.dll" or "System.Runtime.dll")
				.Select (path => MetadataReference.CreateFromFile (path))
				.ToList ();
			var projectInfo = ProjectInfo.Create (
				ProjectId.CreateNewId (), VersionStamp.Create (), "Test", "Test", LanguageNames.CSharp,
				compilationOptions: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary),
				metadataReferences: references);
			var project = workspace.AddProject (projectInfo);
			var document = workspace.AddDocument (project.Id, "a.cs", SourceText.From (text));
			return (workspace, document);
		}

		[Test]
		public async Task HostAnalyzersIncludeTheCSharpCompilerAnalyzerAsync ()
		{
			// Loaded like in the IDE, where the MEF composition loads the Roslyn assemblies before analysis.
			GC.KeepAlive (typeof (CSharpCompilation));
			GC.KeepAlive (MefHostServices.DefaultHost);
			var provider = new MonoDevelopWorkspaceDiagnosticAnalyzerProviderService ();

			var references = await provider.GetHostAnalyzerReferencesAsync ();

			var names = references.Select (r => Path.GetFileNameWithoutExtension (r.FullPath)).ToList ();
			CollectionAssert.Contains (names, "Microsoft.CodeAnalysis.CSharp");
			CollectionAssert.Contains (names, "Microsoft.CodeAnalysis.CSharp.Features");
		}

		[Test]
		public async Task CompilerErrorsComeFromTheDiagnosticAnalyzerServiceAsync ()
		{
			var (workspace, document) = CreateDocument ("class C { void M () { undefinedName (); } }");
			using (workspace) {
				var provider = new MonoDevelopWorkspaceDiagnosticAnalyzerProviderService ();

				await provider.EnsureHostAnalyzersAsync (workspace);

				Assert.IsNotEmpty (workspace.CurrentSolution.AnalyzerReferences);
				var service = workspace.Services.GetService<IDiagnosticAnalyzerService> ();
#pragma warning disable VSTHRD103 // Solution.GetDocument does not block
				var current = workspace.CurrentSolution.GetDocument (document.Id);
#pragma warning restore VSTHRD103
				var diagnostics = await service.GetDiagnosticsForSpanAsync (
					current, null, DiagnosticIdFilter.All, null, DiagnosticKind.All, CancellationToken.None);
				CollectionAssert.Contains (diagnostics.Select (d => d.Id).ToList (), "CS0103");
			}
		}

		[Test]
		public async Task HostAnalyzersAreAddedAgainWhenTheSolutionIsReplacedAsync ()
		{
			var (workspace, _) = CreateDocument ("class C { }");
			using (workspace) {
				var provider = new MonoDevelopWorkspaceDiagnosticAnalyzerProviderService ();
				await provider.EnsureHostAnalyzersAsync (workspace);

				workspace.ClearSolution ();
				// The workspace raises its change events asynchronously.
				for (int i = 0; i < 100 && workspace.CurrentSolution.AnalyzerReferences.Count == 0; i++)
					await Task.Delay (50);

				Assert.IsNotEmpty (workspace.CurrentSolution.AnalyzerReferences);
			}
		}

		[Test]
		public void ProgressTrackerCountsItems ()
		{
			var tracker = new RoslynProgressTracker ();

			tracker.Report (CodeAnalysisProgress.AddIncompleteItems (3, "work"));
			tracker.Report (CodeAnalysisProgress.AddCompleteItems (2, null));

			Assert.AreEqual (3, tracker.TotalItems);
			Assert.AreEqual (2, tracker.CompletedItems);
			Assert.AreEqual ("work", tracker.Description);

			tracker.Report (CodeAnalysisProgress.Clear ());
			Assert.AreEqual (0, tracker.TotalItems);
		}
	}
}
