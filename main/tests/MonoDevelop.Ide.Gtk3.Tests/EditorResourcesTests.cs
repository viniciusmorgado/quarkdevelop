//
// EditorResourcesTests.cs
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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// The string resources of the vendored vs-editor-api assemblies are found under the names their generated Strings
	/// classes use (an undo transaction created for a text change outside of one read a missing resource).
	/// </summary>
	[TestFixture]
	public class EditorResourcesTests
	{
		[TestCase ("Microsoft.VisualStudio.CoreUtilityImplementation", "Microsoft.VisualStudio.CoreUtilityImplementation.ContentType.Strings")]
		[TestCase ("Microsoft.VisualStudio.Logic.Text.BufferUndoManager.Implementation", "Microsoft.VisualStudio.Logic.Text.BufferUndoManager.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.UI.Text.EditorOperations.Implementation", "Microsoft.VisualStudio.UI.Text.EditorOperations.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.Text.UI.Utilities", "Microsoft.VisualStudio.Text.UI.Utilities.Strings")]
		[TestCase ("Microsoft.VisualStudio.Text.Data", "Microsoft.VisualStudio.Text.Strings")]
		[TestCase ("Microsoft.VisualStudio.Language.Implementation", "Microsoft.VisualStudio.Language.Intellisense.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.Text.Model.Implementation", "Microsoft.VisualStudio.Text.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.UI.Text.EditorPrimitives.Implementation", "Microsoft.VisualStudio.UI.Text.EditorPrimitives.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.UI.Text.Commanding.Implementation", "Microsoft.VisualStudio.UI.Text.Commanding.Implementation.CommandingStrings")]
		[TestCase ("Microsoft.VisualStudio.Logic.Text.Classification.LookUp.Implementation", "Microsoft.VisualStudio.Logic.Text.Classification.LookUp.Implementation.Strings")]
		[TestCase ("Microsoft.VisualStudio.Text.MultiCaret.Implementation", "Microsoft.VisualStudio.Text.MultiSelection.Implementation.Strings")]
		public void StringResourcesAreFound (string assemblyName, string baseName)
		{
			// The editor implementation assemblies are in main/build/bin, next to the IDE (the IDE composes them with MEF).
			var binDir = Path.GetFullPath (Path.Combine (AppContext.BaseDirectory, "..", "..", "bin"));
			var assembly = Assembly.LoadFrom (Path.Combine (binDir, assemblyName + ".dll"));
			var resources = new ResourceManager (baseName, assembly);

			var set = resources.GetResourceSet (CultureInfo.InvariantCulture, true, true);

			Assert.IsNotNull (set, string.Join (", ", assembly.GetManifestResourceNames ()));
		}
	}
}
