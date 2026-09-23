//
// CoreUtilitiesTests.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core.Web;
using MonoDevelop.Projects.Utility;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Task T056: nested task bookkeeping of <see cref="ProgressTracker"/> (used by progress monitors).
	/// </summary>
	[TestFixture]
	public class ProgressTrackerTests
	{
		[Test]
		public void EmptyTracker ()
		{
			var t = new ProgressTracker ();
			Assert.IsNull (t.CurrentTask);
			Assert.AreEqual (0, t.CurrentTaskWork);
			Assert.AreEqual (0, t.GlobalWork);
			Assert.IsFalse (t.UnknownWork);
			Assert.IsTrue (t.InProgress);
			t.Step (1);
			t.EndTask ();
			Assert.AreEqual (0, t.GlobalWork);
		}

		[Test]
		public void SingleTask ()
		{
			var t = new ProgressTracker ();
			t.BeginTask ("Build", 4);
			Assert.AreEqual ("Build", t.CurrentTask);
			t.Step (1);
			Assert.AreEqual (0.25, t.CurrentTaskWork);
			Assert.AreEqual (0.25, t.GlobalWork);
			t.Step (10);
			Assert.AreEqual (1.0, t.CurrentTaskWork, "work is clamped to the total");
			t.EndTask ();
			Assert.IsNull (t.CurrentTask);

			t.BeginTask ("Nothing to do", 0);
			Assert.AreEqual (0, t.CurrentTaskWork);
		}

		[Test]
		public void NestedStepTask ()
		{
			var t = new ProgressTracker ();
			t.BeginTask ("Outer", 10);
			t.BeginStepTask ("Inner", 4, 5);
			Assert.AreEqual ("Inner", t.CurrentTask);
			t.Step (2);
			Assert.AreEqual (0.5, t.CurrentTaskWork);
			Assert.AreEqual (0.25, t.GlobalWork, 1e-9, "half of an inner task worth 5 of 10 outer units");

			t.EndTask ();
			Assert.AreEqual ("Outer", t.CurrentTask);
			Assert.AreEqual (0.5, t.GlobalWork, 1e-9, "ending a step task advances the parent by its step size");

			t.Done ();
			Assert.IsFalse (t.InProgress);
			Assert.AreEqual (1.0, t.GlobalWork);
			Assert.IsNull (t.CurrentTask);

			t.Reset ();
			Assert.IsTrue (t.InProgress);
			Assert.AreEqual (0, t.GlobalWork);
		}

		[Test]
		public void UnknownWork ()
		{
			var t = new ProgressTracker ();
			t.BeginTask ("a", 1);
			t.BeginTask ("b", 1);
			Assert.IsTrue (t.UnknownWork, "tasks with a total of 1 have no measurable progress");
			t.BeginTask ("c", 3);
			Assert.IsFalse (t.UnknownWork);
		}
	}

	/// <summary>
	/// Task T056: list differencing and comparison helpers of <see cref="DiffUtility"/>.
	/// </summary>
	[TestFixture]
	public class DiffUtilityTests
	{
		[Test]
		public void AddedAndRemovedItems ()
		{
			var original = new[] { 1, 2, 3 };
			var changed = new[] { 2, 3, 4, 5 };

			var added = new List<int> ();
			Assert.AreEqual (2, DiffUtility.GetAddedItems (original, changed, added));
			CollectionAssert.AreEqual (new[] { 4, 5 }, added);

			var removed = new List<int> ();
			Assert.AreEqual (1, DiffUtility.GetRemovedItems (original, changed, removed));
			CollectionAssert.AreEqual (new[] { 1 }, removed);

			var all = new List<int> ();
			Assert.AreEqual (4, DiffUtility.GetAddedItems (null, changed, all));
			CollectionAssert.AreEqual (changed, all);

			Assert.AreEqual (0, DiffUtility.GetAddedItems (original, null, new List<int> ()));
			Assert.AreEqual (0, DiffUtility.GetAddedItems (original, changed, null));
		}

		[Test]
		public void CustomComparer ()
		{
			var original = new[] { "Alpha", "Beta" };
			var changed = new[] { "ALPHA", "gamma" };
			var added = new ArrayList ();
			Assert.AreEqual (1, DiffUtility.GetAddedItems (original, changed, added, CaseInsensitiveComparer.DefaultInvariant));
			CollectionAssert.AreEqual (new[] { "gamma" }, added);

			var removed = new ArrayList ();
			Assert.AreEqual (1, DiffUtility.GetRemovedItems (original, changed, removed, CaseInsensitiveComparer.DefaultInvariant));
			CollectionAssert.AreEqual (new[] { "Beta" }, removed);
		}

		[Test]
		public void CompareLists ()
		{
			Assert.AreEqual (0, DiffUtility.Compare ((IList)null, null));
			Assert.AreEqual (-1, DiffUtility.Compare (null, new[] { 1 }));
			Assert.AreEqual (1, DiffUtility.Compare (new[] { 1 }, null));
			Assert.AreEqual (0, DiffUtility.Compare (new[] { 1, 2 }, new[] { 1, 2 }));
			Assert.Less (DiffUtility.Compare (new[] { 1, 2 }, new[] { 1, 3 }), 0);
			Assert.Greater (DiffUtility.Compare (new[] { 2 }, new[] { 1, 3 }), 0);
			Assert.AreEqual (-1, DiffUtility.Compare (new[] { 1, 2 }, new[] { 1, 2, 3 }), "a prefix sorts first");

			// Items that are not IComparable are skipped.
			var o = new object ();
			Assert.AreEqual (0, DiffUtility.Compare (new[] { o, 1 }, new[] { new object (), (object)1 }));
		}

		[Test]
		public void CompareSortedLists ()
		{
			var a = new SortedList { { "a", 1 }, { "b", 2 } };
			var b = new SortedList { { "a", 1 }, { "b", 3 } };
			var c = new SortedList { { "a", 1 } };

			Assert.AreEqual (0, DiffUtility.Compare ((SortedList)null, null));
			Assert.AreEqual (-1, DiffUtility.Compare (null, a));
			Assert.AreEqual (1, DiffUtility.Compare (a, (SortedList)null));
			Assert.AreEqual (0, DiffUtility.Compare (a, new SortedList (a)));
			Assert.Less (DiffUtility.Compare (a, b), 0);
			Assert.AreEqual (1, DiffUtility.Compare (a, c));
		}
	}

	/// <summary>
	/// Task T056: the NuGet derived <see cref="MemoryCache"/> used by the web request helpers.
	/// </summary>
	[TestFixture]
	public class MemoryCacheTests
	{
		[Test]
		public void FactoryRunsOncePerKey ()
		{
			using (var cache = new MemoryCache ()) {
				int calls = 0;
				Func<string> factory = () => { calls++; return "value" + calls; };

				Assert.AreEqual ("value1", cache.GetOrAdd ("key", factory, TimeSpan.FromMinutes (1)));
				Assert.AreEqual ("value1", cache.GetOrAdd ("key", factory, TimeSpan.FromMinutes (1)));
				Assert.AreEqual (1, calls);

				Assert.IsTrue (cache.TryGetValue ("key", out string cached));
				Assert.AreEqual ("value1", cached);
				Assert.IsFalse (cache.TryGetValue ("KEY", out string missing), "keys are case sensitive");
				Assert.IsNull (missing);

				cache.Remove ("key");
				Assert.IsFalse (cache.TryGetValue ("key", out cached));
				Assert.AreEqual ("value2", cache.GetOrAdd ("key", factory, TimeSpan.FromMinutes (1)));
			}
		}

		[Test]
		public void ExpiredEntriesAreRemovedByTheCleanup ()
		{
			using (var cache = new MemoryCache ()) {
				cache.GetOrAdd ("old", () => "old", TimeSpan.FromMinutes (-1), absoluteExpiration: true);
				cache.GetOrAdd ("sliding", () => "sliding", TimeSpan.FromMinutes (-1));
				cache.GetOrAdd ("fresh", () => "fresh", TimeSpan.FromMinutes (10));

				// The cleanup normally runs from a 10 second timer; run it now.
				typeof (MemoryCache).GetMethod ("RemoveExpiredEntries", BindingFlags.NonPublic | BindingFlags.Instance)
					.Invoke (cache, new object[] { null });

				Assert.IsFalse (cache.TryGetValue ("old", out string value));
				Assert.IsFalse (cache.TryGetValue ("sliding", out value));
				Assert.IsTrue (cache.TryGetValue ("fresh", out value));
				Assert.AreEqual ("fresh", value);
			}
		}

		[Test]
		public void SharedInstance ()
		{
			Assert.IsNotNull (MemoryCache.Instance);
			Assert.AreSame (MemoryCache.Instance, MemoryCache.Instance);
		}
	}

	/// <summary>
	/// Task T056: exception isolation of the SafeInvoke/TimeInvoke event helpers in CoreExtensions.
	/// </summary>
	[TestFixture]
	public class EventHandlerExtensionsTests
	{
		[Test]
		public void SafeInvokeRunsEveryHandler ()
		{
			var calls = new List<string> ();
			EventHandler handler = (s, e) => calls.Add ("first");
			handler += (s, e) => throw new InvalidOperationException ("handler failure");
			handler += (s, e) => calls.Add ("third:" + s);

			handler.SafeInvoke ("sender", EventArgs.Empty);
			CollectionAssert.AreEqual (new[] { "first", "third:sender" }, calls);
		}

		[Test]
		public void GenericSafeInvokeRunsEveryHandler ()
		{
			var calls = new List<string> ();
			EventHandler<string> handler = (s, e) => throw new InvalidOperationException ("handler failure");
			handler += (s, e) => calls.Add (e);

			handler.SafeInvoke (this, "payload");
			CollectionAssert.AreEqual (new[] { "payload" }, calls);
		}

		sealed class CountArgs : EventArgs
		{
			public int Count;
		}

		[Test]
		public void TimeInvokeRunsEveryHandler ()
		{
			var args = new CountArgs ();
			EventHandler<CountArgs> generic = (s, e) => e.Count++;
			generic += (s, e) => throw new InvalidOperationException ("handler failure");
			generic += (s, e) => e.Count += 10;
			generic.TimeInvoke (this, args);
			generic.TimeInvoke (this, args, groupId: "group");
			Assert.AreEqual (22, args.Count);

			int plain = 0;
			EventHandler handler = (s, e) => plain++;
			handler += (s, e) => throw new InvalidOperationException ("handler failure");
			handler.TimeInvoke (this, EventArgs.Empty);
			Assert.AreEqual (1, plain);

			Assert.DoesNotThrow (CoreExtensions.TimingsReport);
		}
	}
}
