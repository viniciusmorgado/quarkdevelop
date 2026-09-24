//
// NUnitMessagePump.cs
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

using System.Collections.Generic;
using System.Reflection;
using System.Threading;

// Only the two WinForms type names NUnit looks up by name (see WindowsFormsSynchronizationContext); nothing else of
// WinForms is used or emulated.
namespace System.Windows.Forms
{
	/// <summary>
	/// NUnit 3.14 keeps the message loop of the test thread running while it waits for an async test, set-up or
	/// tear-down only when the synchronization context of the thread is the WinForms or the WPF one (its
	/// MessagePumpStrategy finds them by type name); with any other context it blocks the thread, and the
	/// continuations that IDE code posts to the main loop never run. GuiUnit ran a nested GTK main loop instead.
	/// This context has the WinForms type name: it posts to the GLib main loop (DispatchService), and the
	/// <see cref="Application.Run"/> and <see cref="Application.Exit"/> calls of NUnit run and leave a nested
	/// GLib main loop until the awaited task completes.
	/// </summary>
	sealed class WindowsFormsSynchronizationContext : SynchronizationContext
	{
		readonly SynchronizationContext glibContext;

		public WindowsFormsSynchronizationContext (SynchronizationContext glibContext)
		{
			this.glibContext = glibContext;
		}

#pragma warning disable VSTHRD001 // this is the main thread's synchronization context itself
		public override void Post (SendOrPostCallback d, object state)
		{
			// NUnit posts the awaiter of the task it waits for just before it calls Application.Run.
			Application.OnPost (state);
			glibContext.Post (d, state);
		}

		public override void Send (SendOrPostCallback d, object state) => glibContext.Send (d, state);
#pragma warning restore VSTHRD001

		public override SynchronizationContext CreateCopy () => this;
	}

	/// <summary>The message loop NUnit runs for <see cref="WindowsFormsSynchronizationContext"/>.</summary>
	static class Application
	{
		// The NUnit DefaultTimeout of the other test hosts (Test.targets): the single-threaded IDE hosts cannot use NUnit
		// timeouts, which run the test on another thread. A hung test fails instead of stopping the run.
		const uint TimeoutMilliseconds = 120000;

		sealed class Loop
		{
			public bool Exited;
			public bool TimedOut;
			public Func<bool> IsCompleted;

			// A loop that knows its task ends when the task completes: the Exit of a test that timed out earlier and
			// completes later must not end the loop of the current test.
			public bool Done => IsCompleted != null ? IsCompleted () : Exited;
		}

		static readonly Stack<Loop> loops = new Stack<Loop> ();
		static Func<bool> postedAwaiter;

		internal static void OnPost (object state)
		{
			// NUnit.Framework.Internal.AwaitAdapter (internal): IsCompleted tells whether the awaited task completed.
			for (var type = state?.GetType (); type != null; type = type.BaseType) {
				if (type.FullName == "NUnit.Framework.Internal.AwaitAdapter") {
					var property = type.GetProperty ("IsCompleted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (property != null)
						postedAwaiter = () => (bool)property.GetValue (state);
					return;
				}
			}
		}

		/// <summary>Runs the GLib main loop until the task NUnit waits for completes.</summary>
		public static void Run ()
		{
			var loop = new Loop { IsCompleted = postedAwaiter };
			postedAwaiter = null;
			loops.Push (loop);
			var timeoutId = GLib.Timeout.Add (TimeoutMilliseconds, () => {
				loop.TimedOut = true;
				return false;
			});
			try {
				while (!loop.Done && !loop.TimedOut)
					GLib.MainContext.Iteration (true);
			} finally {
				loops.Pop ();
				if (!loop.TimedOut)
					GLib.Source.Remove (timeoutId);
			}
			if (!loop.Done)
				throw new TimeoutException ($"The test did not complete within {TimeoutMilliseconds} ms (IdeUnitTests message loop)");
		}

		/// <summary>Called by NUnit when the task completed: leaves the innermost <see cref="Run"/>.</summary>
		public static void Exit ()
		{
			if (loops.Count > 0)
				loops.Peek ().Exited = true;
		}
	}
}
