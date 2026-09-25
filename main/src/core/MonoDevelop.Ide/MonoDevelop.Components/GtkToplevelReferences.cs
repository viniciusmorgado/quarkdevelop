//
// GtkToplevelReferences.cs
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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoDevelop.Core;

namespace MonoDevelop.Components
{
	/// <summary>
	/// Keeps GTK toplevels and GDK windows created from C# alive until their GtkSharp wrapper releases them (ADR 0024, T153).
	/// </summary>
	/// <remarks>
	/// Some instances own the reference their constructor returns and drop it when they are destroyed: gtk_window_init
	/// and gtk_invisible_init sink the floating reference and keep it for GTK ("has_user_ref_count", dropped by
	/// gtk_widget_destroy); gdk_window_destroy drops the reference returned by gdk_window_new (widgets that create their
	/// GdkWindow in OnRealized destroy it when they are unrealized). GtkSharp 3.24.24 (GLib.Object.Raw) assumes that a
	/// constructor returned a reference for the wrapper: its ToggleRef adds the toggle reference and unrefs the reference
	/// it believes it owns. Destroying the instance then frees it while the wrapper still holds the toggle reference, and
	/// disposing or collecting the wrapper later removes that toggle reference from freed memory
	/// ("g_object_remove_toggle_ref: assertion 'G_IS_OBJECT (object)' failed", crashes). Wrappers made by
	/// GLib.Object.GetObject take a reference of their own and are not affected.
	///
	/// <see cref="Install"/> hooks the constructed virtual function of GtkWindow, GtkInvisible and GdkWindow (the types
	/// whose wrappers MD_GTK_REFERENCE_DIAGNOSTICS=1 found repaired in the tests): every instance gets a
	/// weak reference, notified at the end of each dispose. If only the reference held by that dispose is left while
	/// GtkSharp still maps the instance to a wrapper, the wrapper's toggle reference was not counted (GtkSharp unregisters
	/// a wrapper before it drops its toggle reference, and a counted toggle reference would be a second one). The weak
	/// notification then adds that reference back: the destroyed instance stays alive until the wrapper is disposed or
	/// collected, as it would if GtkSharp had counted its toggle reference.
	///
	/// That repair comes too late for a GdkWindow (T153): nothing but the toggle reference keeps the wrapper of the
	/// Gdk.Window constructor alive once OnRealized returns, so it is collected while the widget is still realized, and
	/// releasing it frees the window the widget uses (or drops a reference someone else counts on). A GdkWindow therefore
	/// gets an extra, floating reference when it is constructed: GtkSharp sinks a floating reference instead of adding
	/// one, both when its constructor sets GLib.Object.Raw and when GLib.Object.GetObject wraps an instance, so the
	/// wrapper's toggle reference is counted from the start. The floating reference of a window no wrapper claimed is
	/// released on the next iteration of the main loop.
	/// </remarks>
	public static unsafe class GtkToplevelReferences
	{
		// GObjectClass: g_type, construct_properties, constructor, set_property, get_property, dispose, finalize,
		// dispatch_properties_changed, notify, constructed.
		static readonly int ConstructedOffset = 9 * IntPtr.Size;

		// The patched types with the constructed function they had before.
		static (IntPtr Type, IntPtr Constructed)[] roots = Array.Empty<(IntPtr, IntPtr)> ();

		// MD_GTK_REFERENCE_DIAGNOSTICS=1: hook every GObject type instead, and log the types that needed a repair.
		static bool diagnostics;

		// GdkWindows constructed since the last ReleaseUnclaimed, each holding a floating reference for its wrapper and
		// a reference of this class that keeps it valid until then.
		static IntPtr gdkWindowType;
		static readonly List<IntPtr> unclaimed = new List<IntPtr> ();
		static bool releaseQueued;

		public static bool IsInstalled => roots.Length > 0;

		/// <summary>
		/// Installs the workaround. Call it on the GTK thread after Gtk.Application.Init, before creating windows (windows
		/// constructed earlier are not covered). Calling it again does nothing.
		/// </summary>
		public static void Install ()
		{
			if (IsInstalled)
				return;

			// The types must not derive from each other: the hook of a subclass would chain up into itself.
			diagnostics = Environment.GetEnvironmentVariable ("MD_GTK_REFERENCE_DIAGNOSTICS") == "1";
			gdkWindowType = Gdk.Window.GType.Val;
			var types = diagnostics
				? new[] { GLib.Object.GType.Val }
				: new[] { Gtk.Window.GType.Val, Gtk.Invisible.GType.Val, Gdk.Window.GType.Val };
			var installed = new (IntPtr Type, IntPtr Constructed)[types.Length];
			for (int i = 0; i < types.Length; i++) {
				// The class reference is never released: the class stays loaded, and patched.
				IntPtr klass = g_type_class_ref (types[i]);
				installed[i] = (types[i], Marshal.ReadIntPtr (klass, ConstructedOffset));
			}
			// Before patching: Constructed looks up the original function here.
			roots = installed;

			IntPtr hook = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&Constructed;
			foreach (var root in installed)
				Patch (root.Type, root.Constructed, hook);
		}

		// Subclasses copy the class structure of their parent when they are initialized: classes initialized from now on
		// inherit the hook, those initialized already (g_type_class_peek) get it here unless they override constructed
		// (their override chains up to the hooked parent).
		static void Patch (IntPtr type, IntPtr original, IntPtr hook)
		{
			IntPtr klass = g_type_class_peek (type);
			if (klass != IntPtr.Zero && Marshal.ReadIntPtr (klass, ConstructedOffset) == original)
				Marshal.WriteIntPtr (klass, ConstructedOffset, hook);

			IntPtr children = g_type_children (type, out uint count);
			for (int i = 0; i < count; i++)
				Patch (Marshal.ReadIntPtr (children, i * IntPtr.Size), original, hook);
			g_free (children);
		}

		[UnmanagedCallersOnly (CallConvs = new[] { typeof (CallConvCdecl) })]
		static void Constructed (IntPtr instance)
		{
			foreach (var root in roots) {
				if (g_type_check_instance_is_a (instance, root.Type) != 0) {
					if (root.Constructed != IntPtr.Zero)
						((delegate* unmanaged[Cdecl]<IntPtr, void>)root.Constructed) (instance);
					break;
				}
			}
			if (g_type_check_instance_is_a (instance, gdkWindowType) != 0)
				AddFloatingReference (instance);
			g_object_weak_ref (instance, &Disposing, IntPtr.Zero);
		}

		// The reference returned by gdk_window_new is GDK's: gdk_window_destroy drops it. A wrapper made from C# right
		// after (Gdk.Window constructor) or later (GetObject, e.g. Gtk.Widget.Window) sinks the floating reference added
		// here and releases it with its toggle reference.
		static void AddFloatingReference (IntPtr instance)
		{
			g_object_ref (instance);
			g_object_force_floating (instance);
			g_object_ref (instance);
			lock (unclaimed) {
				unclaimed.Add (instance);
				if (!releaseQueued) {
					releaseQueued = true;
					g_idle_add_full (GPriorityHigh, &ReleaseUnclaimed, IntPtr.Zero, IntPtr.Zero);
				}
			}
		}

		const int GPriorityHigh = -100;

		// Idle handler: GdkWindows still floating were created by GTK and not wrapped; sinking clears the flag without
		// adding a reference (GObject logs a critical when a floating object is finalized). Then the reference that kept
		// each window valid is released.
		[UnmanagedCallersOnly (CallConvs = new[] { typeof (CallConvCdecl) })]
		static int ReleaseUnclaimed (IntPtr data)
		{
			IntPtr[] instances;
			lock (unclaimed) {
				instances = unclaimed.ToArray ();
				unclaimed.Clear ();
				releaseQueued = false;
			}
			foreach (var instance in instances) {
				if (g_object_is_floating (instance) != 0) {
					g_object_ref_sink (instance);
					g_object_unref (instance);
				}
				g_object_unref (instance);
			}
			// G_SOURCE_REMOVE
			return 0;
		}

		// Gtk.Widget.Dispose () of a toplevel it believes undestroyed takes a reference and calls gtk_widget_destroy,
		// expecting GTK to drop its own. GTK has dropped it already when the instance was resurrected: that reference
		// would leak it.
		static readonly FieldInfo widgetDestroyed = typeof (Gtk.Widget).GetField ("destroyed", BindingFlags.Instance | BindingFlags.NonPublic);

		// Called at the end of each dispose (g_object_run_dispose in gtk_widget_destroy, and the one before finalization),
		// which holds one reference: a reference added here resurrects the instance.
		[UnmanagedCallersOnly (CallConvs = new[] { typeof (CallConvCdecl) })]
		static void Disposing (IntPtr data, IntPtr instance)
		{
			try {
				var wrapper = GLib.Object.TryGetObject (instance);
				if (wrapper == null)
					return;
				// GObject.ref_count follows the GTypeInstance pointer.
				if (Marshal.ReadInt32 (instance, IntPtr.Size) > 1) {
					// The instance survives this dispose: the wrapper's reference is counted, or someone else (a signal
					// emission on the window, an event) still holds one. Check again at the next dispose.
					g_object_weak_ref (instance, &Disposing, IntPtr.Zero);
					return;
				}
				// Only the reference of this dispose is left, yet the wrapper still holds its toggle reference: the
				// wrapper had taken over GTK's reference and GTK has just dropped it. Give it back.
				g_object_ref (instance);
				if (diagnostics)
					LoggingService.LogWarning ("GtkToplevelReferences: repaired the reference of a {0} ({1})", wrapper.NativeType, wrapper.GetType ());
				if (wrapper is Gtk.Widget)
					widgetDestroyed?.SetValue (wrapper, true);
			} catch (Exception e) {
				// An exception must not unwind into GObject.
				LoggingService.LogInternalError ("GtkToplevelReferences: weak notification failed", e);
			}
		}

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_type_class_ref (IntPtr type);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_type_class_peek (IntPtr type);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_type_children (IntPtr type, out uint n_children);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern int g_type_check_instance_is_a (IntPtr instance, IntPtr type);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern void g_object_weak_ref (IntPtr instance, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> notify, IntPtr data);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_object_ref (IntPtr instance);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_object_ref_sink (IntPtr instance);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern void g_object_unref (IntPtr instance);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern void g_object_force_floating (IntPtr instance);

		[DllImport (PangoUtil.LIBGOBJECT, CallingConvention = CallingConvention.Cdecl)]
		static extern int g_object_is_floating (IntPtr instance);

		// Returns the source id, which is not needed: the handler removes itself.
		[DllImport (PangoUtil.LIBGLIB, CallingConvention = CallingConvention.Cdecl)]
		static extern void g_idle_add_full (int priority, delegate* unmanaged[Cdecl]<IntPtr, int> function, IntPtr data, IntPtr notify);

		[DllImport (PangoUtil.LIBGLIB, CallingConvention = CallingConvention.Cdecl)]
		static extern void g_free (IntPtr mem);
	}
}
