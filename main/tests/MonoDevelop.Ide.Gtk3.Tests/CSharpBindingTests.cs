//
// CSharpBindingTests.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.Core.Text;
using MonoDevelop.CSharp.Completion;
using MonoDevelop.CSharp.Formatting;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using NUnit.Framework;

// The C# binding's editor extensions are written against the legacy (pre-VS-editor) DocumentContext API.
#pragma warning disable CS0618, CS0612, CS0672

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T089: the GTK C# binding on Roslyn 5.9 without Roslyn EditorFeatures (completion, formatting and the
	/// NRefactory lexer replacement). The documents live in a Roslyn ad hoc workspace with the default MEF host
	/// (Roslyn Workspaces and Features), as the IDE's workspace does.
	/// </summary>
	[TestFixture]
	public class CSharpBindingTests
	{
		[OneTimeSetUp]
		public void InitializeEditorEnvironment ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
			// The IDE text editor subscribes to the document manager (active document changes).
			var documentManager = MonoDevelop.Core.Runtime.GetService<MonoDevelop.Ide.Gui.Documents.DocumentManager> ();
			while (!documentManager.IsCompleted) {
				if (!GLib.MainContext.Iteration (false))
					Thread.Sleep (10);
			}
		}

		/// <summary>
		/// Waits for a task on the thread that initialized the runtime, running the GLib main loop meanwhile: the
		/// editor code posts work to the main thread (Runtime.RunInMainThread) and NUnit runs every test on it.
		/// </summary>
		static T Wait<T> (Task<T> task)
		{
			while (!task.IsCompleted) {
				if (!GLib.MainContext.Iteration (false))
					Thread.Sleep (10);
			}
#pragma warning disable VSTHRD002 // the task is complete
			return task.GetAwaiter ().GetResult ();
#pragma warning restore VSTHRD002
		}

		static (AdhocWorkspace Workspace, Microsoft.CodeAnalysis.Document Document) CreateDocument (string text)
		{
			var workspace = new AdhocWorkspace (MefHostServices.DefaultHost);
			var trustedAssemblies = ((string)AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")).Split (Path.PathSeparator);
			var references = trustedAssemblies
				.Where (path => Path.GetFileName (path) is "System.Private.CoreLib.dll" or "System.Runtime.dll" or "System.Console.dll")
				.Select (path => MetadataReference.CreateFromFile (path))
				.ToList ();
			var projectInfo = ProjectInfo.Create (
				ProjectId.CreateNewId (), VersionStamp.Create (), "Test", "Test", LanguageNames.CSharp,
				compilationOptions: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary),
				metadataReferences: references);
			var project = workspace.AddProject (projectInfo);
			var document = workspace.AddDocument (project.Id, "a.cs", SourceText.From (text));
			// The completion extension uses the text of the document only when it is loaded (open documents in the IDE).
			Wait (document.GetTextAsync ());
			return (workspace, document);
		}

		[Test]
		public void CompletionAfterSystemConsoleOffersWriteLine ()
		{
			const string text = "class C\n{\n\tvoid M ()\n\t{\n\t\tSystem.Console.\n\t}\n}\n";
			int caret = text.IndexOf ("Console.", StringComparison.Ordinal) + "Console.".Length;
			var (workspace, document) = CreateDocument (text);
			using (workspace) {
				// NUnit runs each test on its own thread (per-test timeout); the IDE text editor asserts that it is used
				// from the runtime's main thread, which setting the main synchronization context moves to this thread.
				MonoDevelop.Core.Runtime.MainSynchronizationContext = MonoDevelop.Core.Runtime.MainSynchronizationContext;
				var editor = TextEditorFactory.CreateNewEditor (TextEditorFactory.CreateNewDocument (new StringTextSource (text), "a.cs", "text/x-csharp"));
				editor.CaretOffset = caret;
				var extension = new CSharpCompletionTextEditorExtension ();
				extension.Initialize (editor, new TestDocumentContext (document));
				var location = editor.OffsetToLocation (caret);
				var completionContext = new CodeCompletionContext {
					TriggerOffset = caret,
					TriggerLine = location.Line,
					TriggerLineOffset = location.Column - 1,
				};

				var list = Wait (extension.HandleCodeCompletionAsync (completionContext, new CompletionTriggerInfo (CompletionTriggerReason.CharTyped, '.'), CancellationToken.None));

				Assert.IsNotNull (list, "no completion list");
				var names = list.Select (data => data.DisplayText).ToList ();
				CollectionAssert.Contains (names, "WriteLine");
				CollectionAssert.Contains (names, "ReadLine");
				// Members of System.Console only: no keywords or types of the enclosing scope.
				CollectionAssert.DoesNotContain (names, "class");
			}
		}

		[Test]
		public void FormattingServiceFormatsDocument ()
		{
			var (workspace, document) = CreateDocument ("class C{void M(){int x=1;}}");
			using (workspace) {
				var changes = Wait (RoslynFormattingService.Instance.GetFormattingChangesAsync (document, null, CancellationToken.None));

				Assert.IsNotEmpty (changes);
				var formatted = Wait (document.GetTextAsync ()).WithChanges (changes).ToString ();
				StringAssert.Contains ("int x = 1;", formatted);
			}
		}

		[Test]
		public void FormattingServiceFormatsStatementOnSemicolon ()
		{
			const string text = "class C\n{\n\tvoid M ()\n\t{\n\t\tint   x=1;\n\t}\n}\n";
			int caret = text.IndexOf (';') + 1;
			var (workspace, document) = CreateDocument (text);
			using (workspace) {
				Assert.IsTrue (RoslynFormattingService.Instance.SupportsFormattingOnTypedCharacter (document, ';'));

				var changes = Wait (RoslynFormattingService.Instance.GetFormattingChangesAsync (document, ';', caret, CancellationToken.None));

				Assert.IsNotNull (changes);
				var formatted = Wait (document.GetTextAsync ()).WithChanges (changes).ToString ();
				StringAssert.Contains ("int x = 1;", formatted);
			}
		}

		[TestCase ("var s = \"abc", true, false, false)]
		[TestCase ("var s = \"abc\";", false, false, false)]
		[TestCase ("var s = @\"a\"\"b", false, true, false)]
		[TestCase ("int x; // comment", false, false, true)]
		[TestCase ("var s = \"// not a comment", true, false, false)]
		public void MiniLexerTracksStringsAndComments (string text, bool inString, bool inVerbatimString, bool inSingleComment)
		{
			var lexer = new CSharpMiniLexer (text);
			lexer.Parse ();

			Assert.AreEqual (inString, lexer.IsInString, nameof (lexer.IsInString));
			Assert.AreEqual (inVerbatimString, lexer.IsInVerbatimString, nameof (lexer.IsInVerbatimString));
			Assert.AreEqual (inSingleComment, lexer.IsInSingleComment, nameof (lexer.IsInSingleComment));
		}

		[Test]
		public void MiniLexerStopsWhenTheCallbackReturnsTrue ()
		{
			var lexer = new CSharpMiniLexer ("abc;def");
			var seen = new List<char> ();
			lexer.Parse ((ch, i) => {
				seen.Add (ch);
				return ch == ';';
			});

			Assert.AreEqual ("abc;", new string (seen.ToArray ()));
		}

		sealed class TestDocumentContext : DocumentContext
		{
			readonly Microsoft.CodeAnalysis.Document document;

			public TestDocumentContext (Microsoft.CodeAnalysis.Document document)
			{
				this.document = document;
				RoslynWorkspace = document.Project.Solution.Workspace;
			}

			public override string Name => document.Name;

			public override MonoDevelop.Projects.Project Project => null;

			public override Microsoft.CodeAnalysis.Document AnalysisDocument => document;

			public override MonoDevelop.Ide.TypeSystem.ParsedDocument ParsedDocument => null;

			public override void AttachToProject (MonoDevelop.Projects.Project project)
			{
			}

			public override void ReparseDocument ()
			{
			}

			public override Microsoft.CodeAnalysis.Options.OptionSet GetOptionSet () => null;

			public override Task<MonoDevelop.Ide.TypeSystem.ParsedDocument> UpdateParseDocument ()
			{
				return Task.FromResult<MonoDevelop.Ide.TypeSystem.ParsedDocument> (null);
			}
		}
	}
}
