//
// ToplevelReferenceTests.cs
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// GtkSharp 3.24.24 wraps a toplevel created from C# (gtk_window_new, g_object_new of a GtkWindow subclass) as if
	/// the constructor returned a reference for the wrapper, but GTK keeps that reference for itself and drops it in
	/// gtk_widget_destroy. Destroy () freed the window while the wrapper still held a toggle reference on it; when the
	/// wrapper was disposed or collected later, GObject logged "g_object_remove_toggle_ref: assertion 'G_IS_OBJECT
	/// (object)' failed" or the test host crashed. MonoDevelop.Components.GtkToplevelReferences (ADR 0024) keeps such
	/// windows alive until their wrapper lets go of them.
	/// </summary>
	[TestFixture]
	public class ToplevelReferenceTests
	{
		const string LibGObject = "libgobject-2.0.so.0";
		const string LibGtk = "libgtk-3.so.0";

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void DestroyNotify (IntPtr data);

		// Called from GObject's finalize (g_datalist_clear), after dispose: set only when the instance is freed.
		static readonly DestroyNotify finalizedNotify = OnFinalized;
		static readonly HashSet<IntPtr> finalized = new HashSet<IntPtr> ();
		static int nextToken;

		readonly List<string> criticals = new List<string> ();
		uint logHandler;

		static void OnFinalized (IntPtr data)
		{
			finalized.Add (data);
		}

		[SetUp]
		public void CaptureGObjectCriticals ()
		{
			GtkFixture.Require ();
			criticals.Clear ();
			logHandler = GLib.Log.SetLogHandler ("GLib-GObject", GLib.LogLevelFlags.Critical | GLib.LogLevelFlags.Warning,
				(domain, level, message) => criticals.Add (message));
		}

		[TearDown]
		public void StopCapturing ()
		{
			if (logHandler != 0)
				GLib.Log.RemoveLogHandler ("GLib-GObject", logHandler);
			logHandler = 0;
		}

		/// <summary>Returns a token that is reported as finalized once GObject frees <paramref name="handle"/>.</summary>
		static IntPtr WatchFinalization (IntPtr handle)
		{
			var token = new IntPtr (++nextToken);
			IntPtr name = GLib.Marshaller.StringToPtrGStrdup ("md-test-finalized-" + nextToken);
			g_object_set_qdata_full (handle, g_quark_from_string (name), token, finalizedNotify);
			GLib.Marshaller.Free (name);
			return token;
		}

		static void Iterate (int milliseconds)
		{
			var end = DateTime.UtcNow.AddMilliseconds (milliseconds);
			while (DateTime.UtcNow < end) {
				while (GLib.MainContext.Iteration (false)) {
				}
				Thread.Sleep (10);
			}
		}

		// The toggle references of collected wrappers are released by a 50 ms GLib timeout of GtkSharp.
		static void CollectWrappers ()
		{
			for (int i = 0; i < 3; i++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Iterate (100);
			}
		}

		// GTK may hold a reference until its pending idle handlers ran.
		static void AssertFreed (IntPtr token, string message)
		{
			CollectWrappers ();
			Assert.IsTrue (finalized.Contains (token), message);
		}

		void AssertNoCriticals ()
		{
			CollectWrappers ();
			Assert.IsEmpty (criticals, "GObject criticals: " + string.Join (" | ", criticals));
		}

		static IEnumerable<TestCaseData> Toplevels ()
		{
			yield return new TestCaseData (new Func<Gtk.Window> (() => new Gtk.Window (Gtk.WindowType.Toplevel))).SetName ("{m}(Window)");
			yield return new TestCaseData (new Func<Gtk.Window> (() => new Gtk.Window (Gtk.WindowType.Popup))).SetName ("{m}(PopupWindow)");
			yield return new TestCaseData (new Func<Gtk.Window> (() => new Gtk.OffscreenWindow ())).SetName ("{m}(OffscreenWindow)");
			yield return new TestCaseData (new Func<Gtk.Window> (() => new Gtk.Dialog ())).SetName ("{m}(Dialog)");
			yield return new TestCaseData (new Func<Gtk.Window> (() => new ManagedWindow ())).SetName ("{m}(ManagedSubclass)");
		}

		// A managed subclass is created with g_object_newv (GLib.Object.CreateNativeObject) instead of gtk_window_new.
		sealed class ManagedWindow : Gtk.Window
		{
			public ManagedWindow () : base ("managed")
			{
			}
		}

		[TestCaseSource (nameof (Toplevels))]
		public void DestroyedToplevelLivesUntilItsWrapperIsDisposed (Func<Gtk.Window> create)
		{
			var window = create ();
			var token = WatchFinalization (window.Handle);
			if (window.Child == null)
				window.Add (new Gtk.Label ("label"));
			window.ShowAll ();
			Iterate (50);

			window.Destroy ();
			Iterate (50);
			Assert.IsFalse (finalized.Contains (token), "the toplevel was freed while its wrapper still referenced it");

			window.Dispose ();
			AssertFreed (token, "the toplevel was not freed with its wrapper");
			AssertNoCriticals ();
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static IntPtr CreateAndDestroy (Func<Gtk.Window> create)
		{
			var window = create ();
			var token = WatchFinalization (window.Handle);
			window.Destroy ();
			return token;
		}

		[TestCaseSource (nameof (Toplevels))]
		public void DestroyedToplevelIsFreedWhenItsWrapperIsCollected (Func<Gtk.Window> create)
		{
			var token = CreateAndDestroy (create);
			Assert.IsFalse (finalized.Contains (token), "the toplevel was freed while its wrapper still referenced it");

			AssertFreed (token, "the toplevel was not freed after its wrapper was collected");
			AssertNoCriticals ();
		}

		[Test]
		public void UndestroyedToplevelIsFreedByDispose ()
		{
			var window = new Gtk.Window (Gtk.WindowType.Toplevel);
			var token = WatchFinalization (window.Handle);
			window.Dispose ();
			AssertFreed (token, "the toplevel leaked");
			AssertNoCriticals ();
		}

		[Test]
		public void ToplevelDestroyedByGtkLivesUntilItsWrapperIsCollected ()
		{
			// As when the window manager closes a window: GTK destroys it without Gtk.Widget.Destroy ().
			var token = DestroyNatively ();
			Assert.IsFalse (finalized.Contains (token), "the toplevel was freed while its wrapper still referenced it");
			AssertNoCriticals ();
		}

		[Test]
		public void ToplevelDestroyedByGtkIsFreedByDispose ()
		{
			var window = new Gtk.Window (Gtk.WindowType.Toplevel);
			var token = WatchFinalization (window.Handle);
			gtk_widget_destroy (window.Handle);
			Assert.IsFalse (finalized.Contains (token), "the toplevel was freed while its wrapper still referenced it");
			window.Dispose ();
			AssertFreed (token, "the toplevel leaked");
			AssertNoCriticals ();
		}

		[Test]
		public void ToplevelDestroyedInItsOwnSignalHandlerLivesUntilItsWrapperIsDisposed ()
		{
			// Dialogs destroy themselves in their Response handler: the emission holds a reference on the dialog, so
			// the reference GTK drops in gtk_widget_destroy is not the last one yet.
			var dialog = new Gtk.Dialog ();
			var token = WatchFinalization (dialog.Handle);
			dialog.Response += (o, args) => ((Gtk.Dialog)o).Destroy ();
			dialog.Respond (Gtk.ResponseType.Ok);
			Assert.IsFalse (finalized.Contains (token), "the dialog was freed while its wrapper still referenced it");
			dialog.Dispose ();
			AssertFreed (token, "the dialog leaked");
			AssertNoCriticals ();
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static IntPtr DestroyNatively ()
		{
			var window = new Gtk.Window (Gtk.WindowType.Toplevel);
			var token = WatchFinalization (window.Handle);
			gtk_widget_destroy (window.Handle);
			return token;
		}

		[Test]
		public void ToplevelCreatedByGtkAndWrappedLaterIsFreed ()
		{
			// GLib.Object.GetObject takes a reference of its own for the wrapper: nothing to repair there.
			IntPtr handle = gtk_window_new (Gtk.WindowType.Toplevel);
			var token = WatchFinalization (handle);
			var window = (Gtk.Window)GLib.Object.GetObject (handle);
			window.Destroy ();
			Assert.IsFalse (finalized.Contains (token));
			window.Dispose ();
			AssertFreed (token, "the toplevel leaked");
			AssertNoCriticals ();
		}

		[Test]
		public void UnwrappedToplevelIsFreedByDestroy ()
		{
			IntPtr handle = gtk_window_new (Gtk.WindowType.Toplevel);
			var token = WatchFinalization (handle);
			gtk_widget_destroy (handle);
			AssertFreed (token, "the toplevel leaked");
			AssertNoCriticals ();
		}

		[Test]
		public void DestroyedInvisibleLivesUntilItsWrapperIsDisposed ()
		{
			// GtkInvisible keeps a reference of its own like GtkWindow (the Stetic GUI designer creates them).
			var invisible = new Gtk.Invisible ();
			var token = WatchFinalization (invisible.Handle);
			invisible.Destroy ();
			Assert.IsFalse (finalized.Contains (token), "the widget was freed while its wrapper still referenced it");
			invisible.Dispose ();
			AssertFreed (token, "the widget leaked");
			AssertNoCriticals ();
		}

		// A widget with a window of its own, created in OnRealized like Mono.TextEditor.TextArea does.
		sealed class WindowedWidget : Gtk.Widget
		{
			public WindowedWidget ()
			{
				HasWindow = true;
			}

			protected override void OnRealized ()
			{
				IsRealized = true;
				var attributes = new Gdk.WindowAttr {
					WindowType = Gdk.WindowType.Child,
					X = Allocation.X,
					Y = Allocation.Y,
					Width = Allocation.Width,
					Height = Allocation.Height,
					Wclass = Gdk.WindowWindowClass.InputOutput,
					Visual = Visual,
					EventMask = (int)(Events | Gdk.EventMask.ExposureMask),
				};
				Window = new Gdk.Window (ParentWindow, attributes, Gdk.WindowAttributesType.X | Gdk.WindowAttributesType.Y | Gdk.WindowAttributesType.Visual);
				RegisterWindow (Window);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static IntPtr RealizeAndDestroy ()
		{
			var window = new Gtk.Window (Gtk.WindowType.Toplevel);
			var widget = new WindowedWidget ();
			window.Add (widget);
			window.ShowAll ();
			Iterate (50);
			Assert.IsTrue (widget.IsRealized);
			var token = WatchFinalization (widget.Window.Handle);
			// gtk_widget_unrealize destroys the GdkWindow, and gdk_window_destroy drops the reference returned by gdk_window_new.
			window.Destroy ();
			return token;
		}

		[Test]
		public void GdkWindowOfAnUnrealizedWidgetLivesUntilItsWrapperIsCollected ()
		{
			var token = RealizeAndDestroy ();
			Assert.IsFalse (finalized.Contains (token), "the GdkWindow was freed while its wrapper still referenced it");
			AssertFreed (token, "the GdkWindow was not freed after its wrapper was collected");
			AssertNoCriticals ();
		}

		[DllImport (LibGObject)]
		static extern void g_object_set_qdata_full (IntPtr obj, uint quark, IntPtr data, DestroyNotify destroy);

		[DllImport ("libglib-2.0.so.0")]
		static extern uint g_quark_from_string (IntPtr name);

		[DllImport (LibGtk)]
		static extern IntPtr gtk_window_new (Gtk.WindowType type);

		[DllImport (LibGtk)]
		static extern void gtk_widget_destroy (IntPtr widget);
	}
}
