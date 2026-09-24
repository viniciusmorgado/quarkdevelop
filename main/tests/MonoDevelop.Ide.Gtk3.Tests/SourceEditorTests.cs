//
// SourceEditorTests.cs
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
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Mono.TextEditor;
using MonoDevelop.Ide.Composition;
using MonoDevelop.SourceEditor;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Tasks T085/T088: the GTK 3 source editor (SourceEditor2 / Mono.TextEditor) composes its editor platform
	/// services, creates its text area inside a scrolled window and renders a document.
	/// </summary>
	[TestFixture]
	public class SourceEditorTests
	{
		[OneTimeSetUp]
		public void InitializeEditorEnvironment ()
		{
			GtkFixture.Require ();
			EditorTestEnvironment.EnsureInitialized ();
		}

		static void Pump ()
		{
			while (Gtk.Application.EventsPending ())
				Gtk.Application.RunIteration ();
		}

		[Test]
		public void EditorPlatformServicesAreExported ()
		{
			var exports = CompositionManager.Instance.ExportProvider;
			// Upstream these came from the closed-source VS editor; SourceEditor2 exports them on Linux.
			Assert.IsNotNull (exports.GetExportedValue<ISmartIndentationService> ());
			Assert.IsNotNull (exports.GetExportedValue<IToolTipService> ());
			Assert.IsInstanceOf<EditorObscuringTipManager> (exports.GetExportedValue<IObscuringTipManager> ());
			Assert.IsInstanceOf<EditorLoggingService> (exports.GetExportedValue<ILoggingServiceInternal> ());
			// They complete the vendored implementations that import them.
			Assert.IsNotNull (exports.GetExportedValue<IEditorOperationsFactoryService> ());
			Assert.IsNotNull (exports.GetExportedValue<IMultiSelectionBrokerFactory> ());
			var options = exports.GetExportedValue<IEditorOptionsFactoryService> ().GlobalOptions;
			Assert.IsTrue (options.GetOptionValue (DefaultTextViewOptions.BraceCompletionEnabledOptionId));
		}

		[Test]
		public void EditorLoggingServiceDropsTelemetry ()
		{
			var logging = new EditorLoggingService ();
			Assert.DoesNotThrow (() => {
				logging.PostEvent ("key", "name", 1);
				logging.PostEvent (TelemetryEventType.Operation, "event", TelemetryResult.Success, ("name", (object)1));
				logging.AdjustCounter ("key", "name");
				logging.PostCounters ();
			});
			Assert.IsNull (logging.CreateTelemetryOperationEventScope ("event", TelemetrySeverity.Normal, null, null));
		}

		[Test]
		public void TextAreaRendersDocumentInOffscreenWindow ()
		{
			GtkFixture.Require ();
			var errors = new List<Exception> ();
			GLib.UnhandledExceptionHandler handler = args => errors.Add (args.ExceptionObject as Exception);
			GLib.ExceptionManager.UnhandledException += handler;
			try {
				var doc = new TextDocument ("class C\n{\n\tint x = 42;\n}\n", "Test.cs", "text/x-csharp");
				var opts = new TextEditorOptions (zoomOverride: true);
				var editor = new MonoTextEditor (doc, opts, new SimpleEditMode ());
				var scrolled = new Gtk.ScrolledWindow ();
				scrolled.Add (editor);
				var window = new Gtk.OffscreenWindow ();
				window.Add (scrolled);
				window.SetSizeRequest (400, 200);
				window.ShowAll ();
				Pump ();

				// GTK 3 scrolled windows hand their adjustments to Gtk.IScrollable children (no viewport is added).
				Assert.AreSame (editor, scrolled.Child);
				Assert.AreSame (scrolled.Vadjustment, editor.Vadjustment);
				Assert.AreSame (scrolled.Hadjustment, editor.Hadjustment);
				var area = editor.TextArea;
				Assert.IsTrue (area.IsRealized);
				Assert.Greater (area.Allocation.Width, 300);
				Assert.Greater (area.Allocation.Height, 150);
				Assert.AreEqual (area.Allocation.Height, (int)editor.Vadjustment.PageSize);

				int width = area.Allocation.Width, height = area.Allocation.Height;
				using (var surface = new Cairo.ImageSurface (Cairo.Format.Rgb24, width, height))
				using (var cr = new Cairo.Context (surface)) {
					cr.SetSourceRGB (1, 0, 1);
					cr.Paint ();
					area.Draw (cr);
					surface.Flush ();
					var colors = CountColors (surface, 0, width);
					// Painted over the magenta fill: backgrounds, line numbers and text.
					Assert.IsFalse (colors.ContainsKey (Magenta), "part of the text area was not painted");
					Assert.Greater (colors.Count, 3, "no text was rendered");
				}

				// The margins and the text are drawn on their own contexts (TextArea.CreateIndependentContext):
				// they keep the clip of the context GTK passes, so nothing is painted outside of it.
				using (var surface = new Cairo.ImageSurface (Cairo.Format.Rgb24, width, height))
				using (var cr = new Cairo.Context (surface)) {
					cr.SetSourceRGB (1, 0, 1);
					cr.Paint ();
					cr.Rectangle (0, 0, width / 2, height);
					cr.Clip ();
					area.Draw (cr);
					surface.Flush ();
					Assert.IsFalse (CountColors (surface, 0, width / 2).ContainsKey (Magenta), "the clipped area was not painted");
					var outside = CountColors (surface, width / 2, width);
					Assert.AreEqual (1, outside.Count, "painted outside of the clip");
					Assert.IsTrue (outside.ContainsKey (Magenta));
				}

				// Typing goes through the edit mode into the document and is drawn.
				area.SimulateKeyPress (Gdk.Key.a, 'a', Gdk.ModifierType.None);
				Assert.AreEqual ("aclass C", doc.GetLineText (1));
				Assert.AreEqual (1, area.Caret.Offset);
				Pump ();
				using (var surface = new Cairo.ImageSurface (Cairo.Format.Rgb24, width, height))
				using (var cr = new Cairo.Context (surface))
					area.Draw (cr);
				window.Destroy ();
				Pump ();
			} finally {
				GLib.ExceptionManager.UnhandledException -= handler;
			}
			Assert.IsEmpty (errors, string.Join (Environment.NewLine, errors));
		}

		const int Magenta = 0xFF00FF;

		/// <summary>The colors of the columns [x0, x1) of an RGB24 surface and how many pixels have them.</summary>
		static unsafe Dictionary<int, int> CountColors (Cairo.ImageSurface surface, int x0, int x1)
		{
			var colors = new Dictionary<int, int> ();
			byte* data = (byte*)surface.DataPtr;
			for (int y = 0; y < surface.Height; y++) {
				int* row = (int*)(data + y * surface.Stride);
				for (int x = x0; x < x1; x++) {
					int c = row[x] & 0xFFFFFF;
					colors.TryGetValue (c, out int n);
					colors[c] = n + 1;
				}
			}
			return colors;
		}
	}
}
