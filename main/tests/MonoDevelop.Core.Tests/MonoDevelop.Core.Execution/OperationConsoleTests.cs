//
// OperationConsoleTests.cs
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
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoDevelop.Core.Execution
{
	/// <summary>
	/// Task T056: <see cref="LocalConsole"/>, whose writers feed in-memory readers (the NUnit runner reads
	/// its XML output through it).
	/// </summary>
	[TestFixture]
	public class LocalConsoleTests
	{
		[Test]
		public void OutputIsReadBack ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("hello");
				console.Out.Write (' ');
				console.Out.Write (new[] { '-', 'w', 'o', 'r', 'l', 'd', '-' }, 1, 5);
				console.Out.Write ("");
				console.Error.Write ("oops");
				console.Log.Write ("log");
				console.SetDone ();

				Assert.AreEqual ("hello world", console.OutReader.ReadToEnd ());
				Assert.AreEqual ("oops", console.ErrorReader.ReadToEnd ());
				Assert.AreEqual ("log", console.LogReader.ReadToEnd ());
				Assert.AreEqual ("", console.OutReader.ReadToEnd (), "the data has been consumed");
				Assert.AreEqual (Encoding.UTF8, console.Out.Encoding);
			}
		}

		[Test]
		public void InputWriterFeedsTheInReader ()
		{
			using (var console = new LocalConsole ()) {
				console.InWriter.Write ("typed input");
				console.SetDone ();
				Assert.AreEqual ("typed input", console.In.ReadToEnd ());
			}
		}

		[Test]
		public void ReadLineHandlesAllLineEndingsAndChunks ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("one\ntwo\r\nthree\rfo");
				console.Out.Write ("ur\nfi");
				console.Out.Write ("ve");
				console.SetDone ();

				var reader = console.OutReader;
				Assert.AreEqual ("one", reader.ReadLine ());
				Assert.AreEqual ("two", reader.ReadLine ());
				Assert.AreEqual ("three", reader.ReadLine ());
				Assert.AreEqual ("four", reader.ReadLine ());
				Assert.AreEqual ("five", reader.ReadLine ());
				Assert.IsNull (reader.ReadLine (), "end of data");
			}
		}

		[Test]
		public void CarriageReturnLineFeedSplitAcrossWrites ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("a\r");
				console.Out.Write ("\nb");
				console.SetDone ();
				Assert.AreEqual ("a", console.OutReader.ReadLine ());
				Assert.AreEqual ("b", console.OutReader.ReadLine ());
				Assert.IsNull (console.OutReader.ReadLine ());
			}
		}

		[Test]
		public void CharacterReads ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("xy");
				console.Out.Write ("z");
				console.SetDone ();

				var reader = console.OutReader;
				Assert.AreEqual ('x', reader.Peek ());
				Assert.AreEqual ('x', reader.Read ());
				Assert.AreEqual ('y', reader.Peek ());
				Assert.AreEqual ('y', reader.Read ());
				Assert.AreEqual ('z', reader.Read ());
				Assert.AreEqual (-1, reader.Read ());
				Assert.AreEqual (-1, reader.Peek ());
			}
		}

		[Test]
		public void BufferReadsSpanChunksWithoutBlocking ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("hello");
				console.Out.Write (" world");

				var buffer = new char[32];
				int n = console.OutReader.Read (buffer, 0, buffer.Length);
				Assert.AreEqual ("hello world", new string (buffer, 0, n));

				// A partial read leaves the rest of the chunk for the next call.
				console.Out.Write ("abcdef");
				n = console.OutReader.Read (buffer, 0, 4);
				Assert.AreEqual ("abcd", new string (buffer, 0, n));
				console.SetDone ();
				n = console.OutReader.Read (buffer, 0, buffer.Length);
				Assert.AreEqual ("ef", new string (buffer, 0, n));
				Assert.AreEqual (0, console.OutReader.Read (buffer, 0, buffer.Length));
			}
		}

		[Test]
		public void ReadLineWaitsForData ()
		{
			using (var console = new LocalConsole ()) {
				var pending = Task.Run (() => console.OutReader.ReadLine ());
				Assert.IsFalse (pending.Wait (100), "ReadLine blocks until a line is available");
				console.Out.Write ("late line\n");
				Assert.IsTrue (pending.Wait (TimeSpan.FromSeconds (10)));
				Assert.AreEqual ("late line", pending.Result);
			}
		}

		[Test]
		public void DisposedReaderDropsData ()
		{
			using (var console = new LocalConsole ()) {
				console.Out.Write ("pending");
				console.OutReader.Dispose ();
				console.Out.Write ("ignored");
				Assert.AreEqual (-1, console.OutReader.Peek ());
				Assert.AreEqual ("", console.OutReader.ReadToEnd ());
			}
		}

		[Test]
		public void DefaultDebugWritesToTheLog ()
		{
			using (var console = new LocalConsole ()) {
				console.Debug (0, "", "plain ");
				console.Debug (2, "cat", "message");
				console.SetDone ();
				Assert.AreEqual ("plain [2:cat] message", console.LogReader.ReadToEnd ());
			}
		}

		[Test]
		public void DisposeEndsTheStreams ()
		{
			var console = new LocalConsole ();
			console.Out.Write ("last");
			console.Dispose ();
			Assert.AreEqual ("last", console.OutReader.ReadToEnd ());
			Assert.IsNull (console.ErrorReader.ReadLine ());
		}
	}

	/// <summary>
	/// Task T056: <see cref="MultipleOperationConsoles"/> fans every write out to all of its consoles.
	/// </summary>
	[TestFixture]
	public class MultipleOperationConsolesTests
	{
		sealed class RecordingConsole : OperationConsole
		{
			readonly StringWriter output = new StringWriter (CultureInfo.InvariantCulture);
			readonly StringWriter error = new StringWriter (CultureInfo.InvariantCulture);
			readonly StringWriter log = new StringWriter (CultureInfo.InvariantCulture);
			readonly StringReader input;

			public RecordingConsole (string input = "")
			{
				this.input = new StringReader (input);
			}

			public List<string> DebugMessages { get; } = new List<string> ();
			public bool IsDisposed { get; private set; }

			public override TextReader In => input;
			public override TextWriter Out => output;
			public override TextWriter Error => error;
			public override TextWriter Log => log;

			public override void Debug (int level, string category, string message)
			{
				DebugMessages.Add (level + "/" + category + "/" + message);
			}

			public override void Dispose ()
			{
				IsDisposed = true;
				base.Dispose ();
			}
		}

		static async Task WriteEverything (TextWriter w)
		{
			w.NewLine = "|";
			w.Write (true);
			w.Write ('c');
			w.Write (new[] { 'a', 'b' });
			w.Write (new[] { 'x', 'y', 'z' }, 1, 1);
			w.Write (1.5m);
			w.Write (2.25d);
			w.Write (3.5f);
			w.Write (-4);
			w.Write (5L);
			w.Write ((object)"obj");
			w.Write ("{0}", 1);
			w.Write ("{0}{1}", 1, 2);
			w.Write ("{0}{1}{2}", 1, 2, 3);
			w.Write ("{0}{1}{2}{3}", new object[] { 1, 2, 3, 4 });
			w.Write ("str");
			w.Write (6u);
			w.Write (7ul);
			await w.WriteAsync ('!');
			await w.WriteAsync (new[] { 'p', 'q' }, 0, 2);
			await w.WriteAsync ("async");
			await w.WriteLineAsync ();
			w.WriteLine ();
			w.WriteLine (false);
			w.WriteLine ('d');
			w.WriteLine (new[] { 'e', 'f' });
			w.WriteLine (new[] { 'g', 'h', 'i' }, 1, 2);
			w.WriteLine (0.5m);
			w.WriteLine (0.25d);
			w.WriteLine (0.75f);
			w.WriteLine (8);
			w.WriteLine (9L);
			w.WriteLine ((object)null);
			w.WriteLine ("{0}", "a");
			w.WriteLine ("{0}{1}", "a", "b");
			w.WriteLine ("{0}{1}{2}", "a", "b", "c");
			w.WriteLine ("{0}{1}{2}{3}", new object[] { "a", "b", "c", "d" });
			w.WriteLine ("line");
			w.WriteLine (10u);
			w.WriteLine (11ul);
			await w.WriteLineAsync (new[] { 'j', 'k', 'l' }, 0, 3);
			await w.WriteLineAsync ('m');
			await w.WriteLineAsync ("n");
			w.Flush ();
			await w.FlushAsync ();
		}

		[Test]
		public async Task AllWritesReachEveryConsole ()
		{
			var expected = new StringWriter (CultureInfo.InvariantCulture);
			await WriteEverything (expected);

			var first = new RecordingConsole ();
			var second = new RecordingConsole ();
			var multi = new MultipleOperationConsoles (first, second);

			await WriteEverything (multi.Out);
			multi.Error.Write ("err");
			multi.Log.Write ("log");

			Assert.AreEqual (expected.ToString (), first.Out.ToString ());
			Assert.AreEqual (expected.ToString (), second.Out.ToString ());
			Assert.AreEqual ("|", multi.Out.NewLine);
			Assert.AreEqual ("err", first.Error.ToString ());
			Assert.AreEqual ("err", second.Error.ToString ());
			Assert.AreEqual ("log", first.Log.ToString ());
			Assert.AreEqual ("log", second.Log.ToString ());
		}

		[Test]
		public void WriterPropertiesComeFromTheFirstConsole ()
		{
			var first = new RecordingConsole ();
			var multi = new MultipleOperationConsoles (first, new RecordingConsole ());
			Assert.AreEqual (first.Out.Encoding, multi.Out.Encoding);
			Assert.AreSame (CultureInfo.InvariantCulture, multi.Out.FormatProvider);
		}

		[Test]
		public void InputComesFromTheFirstConsole ()
		{
			var multi = new MultipleOperationConsoles (new RecordingConsole ("from first"), new RecordingConsole ("from second"));
			Assert.AreEqual ("from first", multi.In.ReadToEnd ());
		}

		[Test]
		public void DebugAndDisposeAreForwarded ()
		{
			var first = new RecordingConsole ();
			var second = new RecordingConsole ();
			var multi = new MultipleOperationConsoles (first, second);
			multi.Debug (1, "cat", "msg");
			CollectionAssert.AreEqual (new[] { "1/cat/msg" }, first.DebugMessages);
			CollectionAssert.AreEqual (new[] { "1/cat/msg" }, second.DebugMessages);

			multi.Dispose ();
			Assert.IsTrue (first.IsDisposed);
			Assert.IsTrue (second.IsDisposed);
		}

		[Test]
		public void CloseAndDisposeCloseTheUnderlyingWriters ()
		{
			var first = new RecordingConsole ();
			var second = new RecordingConsole ();
			var multi = new MultipleOperationConsoles (first, second);

			multi.Out.Close ();
			Assert.Throws<ObjectDisposedException> (() => first.Out.Write ("x"));
			Assert.Throws<ObjectDisposedException> (() => second.Out.Write ("x"));

			multi.Error.Dispose ();
			Assert.Throws<ObjectDisposedException> (() => first.Error.Write ("x"));
			Assert.Throws<ObjectDisposedException> (() => second.Error.Write ("x"));
		}

		[Test]
		public void InvalidArguments ()
		{
			Assert.Throws<ArgumentNullException> (() => new MultipleOperationConsoles (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MultipleOperationConsoles ());
		}
	}

	/// <summary>
	/// Task T056: <see cref="ExecutionTarget"/> identity and <see cref="ExecutionTargetGroup"/> parent tracking.
	/// </summary>
	[TestFixture]
	public class ExecutionTargetTests
	{
		sealed class Target : ExecutionTarget
		{
			readonly string id, name;

			public Target (string id, string name = null)
			{
				this.id = id;
				this.name = name ?? id;
			}

			public override string Id => id;
			public override string Name => name;
		}

		[Test]
		public void IdentityIsTheId ()
		{
			var a = new Target ("dev1", "Device");
			Assert.AreEqual ("Device", a.FullName);
			Assert.IsTrue (a.Enabled);
			Assert.IsFalse (a.Notable);
			Assert.AreEqual (new Target ("dev1", "Other name"), a);
			Assert.AreNotEqual (new Target ("dev2", "Device"), a);
			Assert.IsFalse (a.Equals ("dev1"));
			Assert.AreEqual ("dev1".GetHashCode (), a.GetHashCode ());
			Assert.AreEqual ("[ExecutionTarget: Name=Device, FullName=Device, Id=dev1]", a.ToString ());

			a.Enabled = false;
			a.Notable = true;
			a.Image = "img";
			a.Tooltip = "tip";
			Assert.IsFalse (a.Enabled);
			Assert.IsTrue (a.Notable);
			Assert.AreEqual ("img", a.Image);
			Assert.AreEqual ("tip", a.Tooltip);
		}

		[Test]
		public void GroupTracksParent ()
		{
			var group = new ExecutionTargetGroup ("Simulators", "sims");
			Assert.AreEqual ("Simulators", group.Name);
			Assert.AreEqual ("sims", group.Id);
			Assert.IsFalse (group.IsReadOnly);

			var a = new Target ("a");
			var b = new Target ("b");
			var c = new Target ("c");
			group.Add (a);
			group.Insert (0, b);
			Assert.AreEqual (2, group.Count);
			Assert.AreSame (b, group[0]);
			Assert.AreSame (group, a.ParentGroup);
			Assert.AreSame (group, b.ParentGroup);
			Assert.AreEqual (1, group.IndexOf (a));
			Assert.IsTrue (group.Contains (b));

			group[0] = c;
			Assert.IsNull (b.ParentGroup);
			Assert.AreSame (group, c.ParentGroup);

			var array = new ExecutionTarget[3];
			group.CopyTo (array, 1);
			CollectionAssert.AreEqual (new ExecutionTarget[] { null, c, a }, array);

			var seen = new List<ExecutionTarget> ();
			foreach (var t in group)
				seen.Add (t);
			CollectionAssert.AreEqual (new[] { c, a }, seen);
			CollectionAssert.AreEqual (new[] { c, a }, (IEnumerable<ExecutionTarget>)group);

			Assert.IsTrue (group.Remove (a));
			Assert.IsNull (a.ParentGroup);
			group.RemoveAt (0);
			Assert.IsNull (c.ParentGroup);
			Assert.AreEqual (0, group.Count);

			group.AddRange (new[] { a, b });
			group.InsertRange (1, new[] { c });
			CollectionAssert.AreEqual (new[] { a, c, b }, (System.Collections.IEnumerable)group);

			group.Add (new Target ("d"));
			var d = group[3];
			group.Clear ();
			Assert.AreEqual (0, group.Count);
			Assert.IsNull (d.ParentGroup);
		}
	}
}
