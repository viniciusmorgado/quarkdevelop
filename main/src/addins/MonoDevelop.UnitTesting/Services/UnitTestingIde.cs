//
// UnitTestingIde.cs
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

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;

namespace MonoDevelop.UnitTesting
{
	/// <summary>
	/// The IDE state the test service reads. The service, the VSTest provider and the test tree also run without the
	/// workbench (tests, headless hosts): reading IdeApp there would run IdeApp's static constructor, which creates GTK
	/// objects, and the workspace, build and type system services are not started.
	/// </summary>
	static class UnitTestingIde
	{
		/// <summary>IdeApp.IsInitialized without running IdeApp's static constructor.</summary>
		public static bool IsInitialized => Runtime.PeekService<RootWorkspace> () != null && IdeApp.IsInitialized;

		/// <summary>The workspace of the IDE, or null without the workbench.</summary>
		public static RootWorkspace Workspace => Runtime.PeekService<RootWorkspace> ();

		/// <summary>The build and run operations of the IDE, or null without the workbench.</summary>
		public static ProjectOperations ProjectOperations => Runtime.PeekService<ProjectOperations> ();

		/// <summary>The type system of the IDE, or null when it is not started (without the workbench).</summary>
		public static TypeSystemService TypeSystemService => Runtime.PeekService<TypeSystemService> ();

		/// <summary>The active configuration of the IDE, or the default configuration without the workbench.</summary>
		public static ConfigurationSelector ActiveConfiguration => Workspace?.ActiveConfiguration ?? ConfigurationSelector.Default;
	}
}
