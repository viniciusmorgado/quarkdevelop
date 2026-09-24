//
// TextEditorOverviewTests.cs
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
using System.Threading;
using MonoDevelop.Ide.Editor;
using NUnit.Framework;

// Embedded editors use the legacy (pre-VS-editor) TextEditor API.
#pragma warning disable CS0618

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// An IDE text editor embedded in another widget (as the assembly browser and the Gettext catalog editor do) redraws
	/// the overview of its scroll bar after its options change. The overview drew into a surface made from a cairo
	/// context of the IDE window, which lost references of that window on GTK 3 (the IDE crashed).
	/// </summary>
	[TestFixture]
	public class TextEditorOverviewTests
	{
		static void Iterate (int milliseconds)
		{
			var end = DateTime.UtcNow.AddMilliseconds (milliseconds);
			while (DateTime.UtcNow < end) {
				while (GLib.MainContext.Iteration (false)) {
				}
				Thread.Sleep (10);
			}
		}

		[Test]
		public void EmbeddedEditorRedrawsItsOverviewAfterAnOptionsChange ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
			var documentManager = MonoDevelop.Core.Runtime.GetService<MonoDevelop.Ide.Gui.Documents.DocumentManager> ();
			while (!documentManager.IsCompleted)
				Iterate (10);
			// The editor checks that it is used from the thread that created it (NUnit runs each test on its own thread).
			MonoDevelop.Core.Runtime.MainSynchronizationContext = MonoDevelop.Core.Runtime.MainSynchronizationContext;

			var editor = TextEditorFactory.CreateNewEditor ();
			var window = new Gtk.Window (Gtk.WindowType.Toplevel);
			try {
				window.SetDefaultSize (400, 300);
				var frame = new Gtk.Frame ();
				frame.Add (editor);
				window.Add (frame);
				window.ShowAll ();
				Iterate (300);

				editor.Options = DefaultSourceEditorOptions.PlainEditor;
				editor.Text = string.Join ("\n", Enumerable.Range (0, 200).Select (i => "line " + i));
				Iterate (1000);

				Gtk.Widget widget = editor;
				Assert.IsTrue (widget.IsRealized);
				Assert.IsTrue (window.GdkWindow.IsVisible);
				Assert.AreEqual (200, editor.LineCount);
			} finally {
				window.Destroy ();
				Iterate (100);
			}
		}
	}
}
