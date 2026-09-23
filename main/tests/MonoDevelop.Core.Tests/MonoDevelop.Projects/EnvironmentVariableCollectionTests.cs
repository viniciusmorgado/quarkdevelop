//
// EnvironmentVariableCollectionTests.cs
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Core.Serialization;
using NUnit.Framework;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T056: <see cref="EnvironmentVariableCollection"/>, an insertion ordered string dictionary that is
	/// serialized as a list of Variable items.
	/// </summary>
	[TestFixture]
	public class EnvironmentVariableCollectionTests
	{
		[Test]
		public void IndexerKeepsInsertionOrder ()
		{
			var env = new EnvironmentVariableCollection ();
			env["B"] = "1";
			env["A"] = "2";
			env["B"] = "3";
			env.Add ("C", "4");

			Assert.AreEqual (3, env.Count);
			Assert.AreEqual ("3", env["B"]);
			CollectionAssert.AreEqual (new[] { "B", "A", "C" }, env.Keys);
			CollectionAssert.AreEqual (new[] { "3", "2", "4" }, env.Values);
			Assert.Throws<KeyNotFoundException> (() => _ = env["missing"]);
		}

		[Test]
		public void DictionaryOperations ()
		{
			var env = new EnvironmentVariableCollection (new Dictionary<string, string> { { "PATH", "/bin" }, { "HOME", "/home/u" } });
			IDictionary<string, string> dict = env;

			Assert.IsFalse (dict.IsReadOnly);
			Assert.IsTrue (env.ContainsKey ("PATH"));
			Assert.IsFalse (env.ContainsKey ("path"), "keys are case sensitive");
			Assert.IsTrue (dict.TryGetValue ("HOME", out var home));
			Assert.AreEqual ("/home/u", home);
			Assert.IsFalse (dict.TryGetValue ("NOPE", out var nope));
			Assert.IsNull (nope);

			dict.Add (new KeyValuePair<string, string> ("LANG", "C"));
			Assert.IsTrue (dict.Contains (new KeyValuePair<string, string> ("LANG", "C")));
			Assert.IsFalse (dict.Contains (new KeyValuePair<string, string> ("LANG", "en")));
			Assert.IsFalse (dict.Remove (new KeyValuePair<string, string> ("LANG", "en")));
			Assert.IsTrue (dict.Remove (new KeyValuePair<string, string> ("LANG", "C")));

			var array = new KeyValuePair<string, string>[3];
			dict.CopyTo (array, 1);
			Assert.AreEqual ("PATH", array[1].Key);
			Assert.AreEqual ("HOME", array[2].Key);

			Assert.IsTrue (env.Remove ("PATH"));
			Assert.IsFalse (env.Remove ("PATH"));
			Assert.AreEqual (1, env.Count);

			var seen = new List<string> ();
			foreach (var kv in env)
				seen.Add (kv.Key + "=" + kv.Value);
			foreach (KeyValuePair<string, string> kv in (IEnumerable)env)
				seen.Add (kv.Key);
			foreach (var kv in (IEnumerable<KeyValuePair<string, string>>)env)
				seen.Add (kv.Value);
			CollectionAssert.AreEqual (new[] { "HOME=/home/u", "HOME", "/home/u" }, seen);

			env.CopyFrom (new Dictionary<string, string> { { "X", "y" } });
			CollectionAssert.AreEqual (new[] { "X" }, env.Keys);
			env.Clear ();
			Assert.AreEqual (0, env.Count);
		}

		[Test]
		public void SerializationRoundTrip ()
		{
			var env = new EnvironmentVariableCollection ();
			env["ONE"] = "1";
			env["TWO"] = "two words";

			var data = ((ICustomDataItem)env).Serialize (null);
			Assert.AreEqual (2, data.Count);
			var first = (DataItem)data[0];
			Assert.AreEqual ("Variable", first.Name);
			Assert.AreEqual ("ONE", ((DataValue)first.ItemData["name"]).Value);
			Assert.AreEqual ("1", ((DataValue)first.ItemData["value"]).Value);
			Assert.IsTrue (((DataValue)first.ItemData["name"]).StoreAsAttribute);

			// Incomplete items are ignored when reading.
			var incomplete = new DataItem { Name = "Variable" };
			incomplete.ItemData.Add (new DataValue ("name", "ORPHAN"));
			data.Add (incomplete);

			var copy = new EnvironmentVariableCollection ();
			((ICustomDataItem)copy).Deserialize (null, data);
			CollectionAssert.AreEqual (env.ToList (), copy.ToList ());
		}
	}
}
