//
// TestHost.cs
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
using System.Threading;
using MonoDevelop.Core;

namespace UnitTests
{
	/// <summary>
	/// Initializes the MonoDevelop runtime once per test process. Under <c>dotnet test</c> this replaces
	/// what <c>mdtool run-md-tests</c> + GuiUnit did on .NET Framework (ADR 0015): a dedicated
	/// "main" thread with a message loop becomes <see cref="Runtime.MainSynchronizationContext"/>, and
	/// the add-in engine is initialized with an isolated profile under <c>main/tests/config</c>.
	/// </summary>
	public static class TestHost
	{
		static readonly object initLock = new object ();
		static MainLoopSynchronizationContext mainContext;
		static Exception initializationError;

		/// <summary>The synchronization context of the emulated UI thread.</summary>
		public static SynchronizationContext MainSynchronizationContext => mainContext;

		public static void EnsureInitialized ()
		{
			lock (initLock) {
				if (initializationError != null)
					throw new InvalidOperationException ("MonoDevelop runtime initialization failed", initializationError);
				if (mainContext != null)
					return;

				var configRoot = Path.Combine (Util.TestsRootDir, "config");
				Directory.CreateDirectory (configRoot);
				// Isolated user profile and add-in registry for the test run.
				Environment.SetEnvironmentVariable ("MONODEVELOP_PROFILE", configRoot);
				Environment.SetEnvironmentVariable ("MONO_ADDINS_REGISTRY", configRoot);
				Environment.SetEnvironmentVariable ("XDG_CONFIG_HOME", configRoot);

				mainContext = new MainLoopSynchronizationContext ();
				try {
					mainContext.Send (_ => {
						Runtime.MainSynchronizationContext = mainContext;
						Runtime.Initialize (true);
						Runtime.Preferences.EnableUpdaterForCurrentSession = false;
					}, null);
				} catch (Exception ex) {
					initializationError = ex;
					throw;
				}
			}
		}

		/// <summary>
		/// A basic message loop on a dedicated thread, emulating the UI loop that GuiUnit provided.
		/// </summary>
		sealed class MainLoopSynchronizationContext : SynchronizationContext
		{
			readonly Queue<(SendOrPostCallback callback, object state)> work = new Queue<(SendOrPostCallback, object)> ();
			readonly Thread thread;

			public MainLoopSynchronizationContext ()
			{
				thread = new Thread (Run) {
					IsBackground = true,
					Name = "MonoDevelop test main loop"
				};
				thread.Start ();
			}

			void Run ()
			{
				SetSynchronizationContext (this);
				while (true) {
					(SendOrPostCallback callback, object state) item;
					lock (work) {
						while (work.Count == 0)
							Monitor.Wait (work);
						item = work.Dequeue ();
					}
					try {
						item.callback (item.state);
					} catch (Exception ex) {
						LoggingService.LogError ("Unhandled exception in the test main loop", ex);
					}
				}
			}

			public override void Post (SendOrPostCallback d, object state)
			{
				lock (work) {
					work.Enqueue ((d, state));
					Monitor.Pulse (work);
				}
			}

			public override void Send (SendOrPostCallback d, object state)
			{
				if (Thread.CurrentThread == thread) {
					d (state);
					return;
				}
				Exception error = null;
				using (var done = new ManualResetEventSlim (false)) {
					Post (_ => {
						try {
							d (state);
						} catch (Exception ex) {
							error = ex;
						} finally {
							done.Set ();
						}
					}, null);
					done.Wait ();
				}
				if (error != null)
					throw new AggregateException (error);
			}

			public override SynchronizationContext CreateCopy () => this;
		}
	}
}
