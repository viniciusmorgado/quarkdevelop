//
// AssemblyBrowserTests.cs
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
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;
using MonoDevelop.AssemblyBrowser;
using MonoDevelop.Ide.Editor;
using NUnit.Framework;

// The assembly browser writes into the legacy (pre-VS-editor) TextEditor API.
#pragma warning disable CS0618

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T093: the assembly browser on ICSharpCode.Decompiler 11 (metadata based, no Mono.Cecil) decompiles and
	/// disassembles a type of the shared framework into its text editor.
	/// </summary>
	[TestFixture]
	public class AssemblyBrowserTests
	{
		[OneTimeSetUp]
		public void InitializeEditorEnvironment ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
			var documentManager = MonoDevelop.Core.Runtime.GetService<MonoDevelop.Ide.Gui.Documents.DocumentManager> ();
			while (!documentManager.IsCompleted) {
				if (!GLib.MainContext.Iteration (false))
					Thread.Sleep (10);
			}
		}

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

		/// <summary>
		/// Runs <paramref name="test"/> with an assembly browser that has System.Console.dll loaded. The widget and the
		/// editors check that they are used from the thread that created them: everything runs on the test's thread,
		/// made the runtime's main thread (NUnit runs each test on its own thread).
		/// </summary>
		static void WithSystemConsole (Action<AssemblyLoader, ITypeDefinition> test)
		{
			MonoDevelop.Core.Runtime.MainSynchronizationContext = MonoDevelop.Core.Runtime.MainSynchronizationContext;
			// With the default search mode (types and members) the widget starts a search while it is built, which
			// reports to the workbench status bar; the test host has no workbench.
			MonoDevelop.Core.PropertyService.Set ("AssemblyBrowser.SearchMemberState", SearchMemberState.Types);
			var widget = new AssemblyBrowserWidget ();
			try {
				var loader = widget.AddReferenceByFileName (typeof (Console).Assembly.Location);
				Assert.IsNotNull (loader, "System.Console.dll not found");
				Assert.IsNotNull (Wait (loader.LoadingTask), "System.Console.dll was not read");
				var console = loader.DecompilerTypeSystem.MainModule.TypeDefinitions.Single (t => t.FullName == "System.Console");
				test (loader, console);
			} finally {
				// Let the widget finish what it posted to the main loop (tree updates, its own output) before it goes.
				while (GLib.MainContext.Iteration (false)) {
				}
				widget.Destroy ();
			}
		}

		static TextEditor CreateEditor (string mimeType)
		{
			var editor = TextEditorFactory.CreateNewEditor ();
			editor.MimeType = mimeType;
			return editor;
		}

		[Test]
		public void DecompilesSystemConsoleToCSharp ()
		{
			WithSystemConsole ((loader, console) => {
				var editor = CreateEditor ("text/x-csharp");
				var flags = new DecompileFlags { PublicOnly = true, MethodBodies = true };

				var references = Wait (MethodDefinitionNodeBuilder.DecompileAsync (editor, loader, d => d.Decompile (console.MetadataToken), flags: flags));

				StringAssert.Contains ("public static class Console", editor.Text);
				// The assembly browser formats with the Mono style (a space before parentheses).
				StringAssert.Contains ("public static void WriteLine (string? value)", editor.Text);
				StringAssert.DoesNotContain ("decompilation failed", editor.Text);
				// Only the type: the assembly attributes the widget decompiles meanwhile for the assembly node stay out.
				StringAssert.DoesNotContain ("[assembly:", editor.Text);
				// Types and members in the output are links (ColoredCSharpFormatter reference segments).
				Assert.IsTrue (references.Any (r => r.Reference is IMember member && member.Name == "WriteLine"), "no reference to WriteLine");
			});
		}

		[Test]
		public void DisassemblesSystemConsoleToIL ()
		{
			WithSystemConsole ((loader, console) => {
				var editor = CreateEditor ("text/x-ilasm");

				Wait (MethodDefinitionNodeBuilder.DisassembleAsync (editor, rd => rd.DisassembleType (console.ParentModule.MetadataFile, (TypeDefinitionHandle)console.MetadataToken)));

				StringAssert.Contains (".class public auto ansi abstract sealed beforefieldinit System.Console", editor.Text);
				StringAssert.Contains ("WriteLine", editor.Text);
			});
		}
	}
}
