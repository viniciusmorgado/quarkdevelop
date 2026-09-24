//
// SourceGeneratedFiles.cs
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
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Documents;

namespace MonoDevelop.Ide.TypeSystem
{
	/// <summary>
	/// T147: a document produced by a source generator exists only in the Roslyn workspace. Its path is virtual and
	/// relative (<c>generator assembly/generator type/hint name</c>), so the editor, which opens files, cannot show it.
	/// Navigation writes its text to a read-only file in the cache directory and opens that file instead, in a read-only
	/// editor.
	/// </summary>
	static class SourceGeneratedFiles
	{
		internal static FilePath RootDirectory => UserProfile.Current.CacheDir.Combine ("SourceGenerated");

		static SourceGeneratedFiles ()
		{
			Runtime.ServiceProvider.WhenServiceInitialized<DocumentManager> (manager => manager.DocumentOpened += OnDocumentOpened);
		}

		// The file is a snapshot of the generator output: editing it would change nothing (the save asks first anyway,
		// the file being read-only).
		static void OnDocumentOpened (object sender, Gui.DocumentEventArgs e)
		{
			// the view, and with it the editor, is created after the document is opened
			if (e.Document != null && e.Document.FileName.IsChildPathOf (RootDirectory))
				e.Document.RunWhenContentAdded<Editor.TextEditor> (editor => editor.IsReadOnly = true);
		}

		/// <summary>
		/// The file to open for <paramref name="tree"/>: its own path, or, for a source-generated document of an IDE
		/// workspace, a read-only copy of its current text.
		/// </summary>
		public static FilePath GetFilePath (SyntaxTree tree)
		{
			if (tree == null)
				return FilePath.Null;
			if (Path.IsPathRooted (tree.FilePath))
				return tree.FilePath;

			foreach (var workspace in IdeServices.TypeSystemService.AllWorkspaces) {
				var path = GetFilePath (workspace.CurrentSolution, tree);
				if (path != tree.FilePath)
					return path;
			}
			return tree.FilePath;
		}

		/// <summary>
		/// As <see cref="GetFilePath(SyntaxTree)"/>, for a tree of <paramref name="solution"/>.
		/// </summary>
		public static FilePath GetFilePath (Solution solution, SyntaxTree tree)
		{
			if (tree == null)
				return FilePath.Null;
			if (Path.IsPathRooted (tree.FilePath) || !(solution?.GetDocument (tree) is SourceGeneratedDocument document))
				return tree.FilePath;

			try {
				var directory = GetCacheDirectory (document.Project);
				var file = directory.Combine (document.FilePath).FullPath;
				if (!file.IsChildPathOf (directory))
					return tree.FilePath;
				Write (file, tree.GetText ().ToString ());
				return file;
			} catch (Exception e) {
				LoggingService.LogError ($"Cannot write the source-generated file {document.FilePath}", e);
				return tree.FilePath;
			}
		}

		// One directory per project file, stable across sessions: SourceGenerated/<project>-<hash of its path>/.
		static FilePath GetCacheDirectory (Project project)
		{
			var key = project.FilePath ?? project.Name;
			var hash = SHA256.HashData (Encoding.UTF8.GetBytes (key));
			var name = Path.GetFileNameWithoutExtension (key) + "-" + Convert.ToHexString (hash, 0, 4).ToLowerInvariant ();
			return RootDirectory.Combine (name);
		}

		static void Write (FilePath file, string text)
		{
			if (File.Exists (file)) {
				if (File.ReadAllText (file) == text)
					return;
				File.SetAttributes (file, FileAttributes.Normal);
			} else {
				Directory.CreateDirectory (file.ParentDirectory);
			}
			File.WriteAllText (file, text);
			// read-only: the editor opens it as such (FileModel.CanWrite); edits belong in the generator input
			File.SetAttributes (file, FileAttributes.ReadOnly);
		}
	}
}
