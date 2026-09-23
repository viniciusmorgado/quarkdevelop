//
// SyncContextTests.cs
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
using System.Threading;
using MonoDevelop.Ide.Gui;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T136: SyncContext.AsyncDispatch no longer uses Delegate.BeginInvoke (not supported on .NET);
	/// it still runs the handler asynchronously, with its state, on another thread.
	/// </summary>
	[TestFixture]
	public class SyncContextTests
	{
		[Test]
		public void AsyncDispatchRunsTheHandlerOnAnotherThread ()
		{
			var done = new ManualResetEventSlim ();
			object received = null;
			int handlerThread = -1;
			var state = new object ();
			new SyncContext ().AsyncDispatch (s => {
				received = s;
				handlerThread = Environment.CurrentManagedThreadId;
				done.Set ();
			}, state);
			Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)), "handler did not run");
			Assert.AreSame (state, received);
			Assert.AreNotEqual (Environment.CurrentManagedThreadId, handlerThread);
		}

		[Test]
		public void AsyncDispatchDoesNotBlockTheCaller ()
		{
			var release = new ManualResetEventSlim ();
			var done = new ManualResetEventSlim ();
			new SyncContext ().AsyncDispatch (s => {
				release.Wait (TimeSpan.FromSeconds (10));
				done.Set ();
			}, null);
			// Returned before the handler could finish.
			Assert.IsFalse (done.IsSet);
			release.Set ();
			Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)));
		}

		[Test]
		public void DispatchRunsSynchronously ()
		{
			int handlerThread = -1;
			new SyncContext ().Dispatch (s => handlerThread = Environment.CurrentManagedThreadId, null);
			Assert.AreEqual (Environment.CurrentManagedThreadId, handlerThread);
		}
	}
}
