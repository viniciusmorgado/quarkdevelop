//
// MonoOptionsTests.cs
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
using Mono.Options;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Task T056: the command-line parser shipped in MonoDevelop.Core (Mono.Options.cs), used for the IDE
	/// and tool options, behaves on .NET 10 as documented upstream.
	/// </summary>
	[TestFixture]
	public class MonoOptionsTests
	{
		[Test]
		public void FlagsValuesAndExtraArguments ()
		{
			bool verbose = false;
			string name = null;
			var extra = new OptionSet {
				{ "v|verbose", "be verbose", v => verbose = v != null },
				{ "n|name=", "the name", v => name = v },
			}.Parse (new [] { "-v", "--name=hello", "file.sln", "other" });

			Assert.IsTrue (verbose);
			Assert.AreEqual ("hello", name);
			CollectionAssert.AreEqual (new [] { "file.sln", "other" }, extra);
		}

		[TestCase ("--name=value")]
		[TestCase ("--name:value")]
		[TestCase ("-name=value")]
		[TestCase ("/name:value")]
		public void ValueSeparatorsAndPrefixes (string argument)
		{
			string name = null;
			new OptionSet { { "name=", v => name = v } }.Parse (new [] { argument });
			Assert.AreEqual ("value", name);
		}

		[Test]
		public void RequiredValueFromNextArgument ()
		{
			string name = null;
			new OptionSet { { "n|name=", v => name = v } }.Parse (new [] { "-n", "next" });
			Assert.AreEqual ("next", name);
		}

		[Test]
		public void MissingRequiredValueThrows ()
		{
			var set = new OptionSet { { "n|name=", v => { } } };
			var ex = Assert.Throws<OptionException> (() => set.Parse (new [] { "-n" }));
			Assert.AreEqual ("-n", ex.OptionName);
			StringAssert.Contains ("Missing required value", ex.Message);
		}

		[Test]
		public void OptionalValue ()
		{
			var values = new List<string> ();
			var set = new OptionSet { { "o|opt:", v => values.Add (v) } };
			set.Parse (new [] { "-o", "--opt=x" });
			CollectionAssert.AreEqual (new string [] { null, "x" }, values);
		}

		[Test]
		public void BundledBooleanFlags ()
		{
			bool a = false, b = false, c = false;
			var extra = new OptionSet {
				{ "a", v => a = v != null },
				{ "b", v => b = v != null },
				{ "c", v => c = v != null },
			}.Parse (new [] { "-abc" });
			Assert.IsTrue (a && b && c);
			Assert.IsEmpty (extra);
		}

		[Test]
		public void BooleanFlagsCanBeTurnedOff ()
		{
			bool? debug = null;
			var set = new OptionSet { { "d|debug", v => debug = v != null } };
			set.Parse (new [] { "-d-" });
			Assert.AreEqual (false, debug);
			set.Parse (new [] { "-d+" });
			Assert.AreEqual (true, debug);
		}

		[Test]
		public void TypedValuesAreConverted ()
		{
			int level = 0;
			new OptionSet { { "level=", (int v) => level = v } }.Parse (new [] { "--level=42" });
			Assert.AreEqual (42, level);

			var set = new OptionSet { { "level=", (int v) => level = v } };
			var ex = Assert.Throws<OptionException> (() => set.Parse (new [] { "--level=many" }));
			StringAssert.Contains ("Could not convert", ex.Message);
		}

		[Test]
		public void KeyValueOptions ()
		{
			var defines = new Dictionary<string, string> ();
			new OptionSet { { "D:", (k, v) => defines [k] = v } }
				.Parse (new [] { "-DDEBUG", "-DLEVEL=3" });
			Assert.IsTrue (defines.ContainsKey ("DEBUG"));
			Assert.IsNull (defines ["DEBUG"]);
			Assert.AreEqual ("3", defines ["LEVEL"]);
		}

		[Test]
		public void DoubleDashStopsOptionProcessing ()
		{
			bool verbose = false;
			var extra = new OptionSet { { "v", v => verbose = true } }.Parse (new [] { "--", "-v" });
			Assert.IsFalse (verbose);
			CollectionAssert.AreEqual (new [] { "-v" }, extra);
		}

		[Test]
		public void DefaultHandlerReceivesUnknownArguments ()
		{
			var unknown = new List<string> ();
			var extra = new OptionSet { { "<>", v => unknown.Add (v) } }.Parse (new [] { "a", "b" });
			CollectionAssert.AreEqual (new [] { "a", "b" }, unknown);
			Assert.IsEmpty (extra);
		}

		[Test]
		public void InvalidPrototypesAreRejected ()
		{
			var set = new OptionSet ();
			Assert.Throws<ArgumentNullException> (() => set.Add ((string)null, v => { }));
			Assert.Throws<ArgumentException> (() => set.Add ("", v => { }));
			Assert.Throws<ArgumentException> (() => set.Add ("a=|b:", v => { }));
			set.Add ("x", v => { });
			Assert.Throws<ArgumentException> (() => set.Add ("x", v => { }));
		}

		[Test]
		public void OptionsAreLookedUpByAnyName ()
		{
			var set = new OptionSet { { "v|verbose|loud", "chatty", v => { } } };
			Assert.IsTrue (set.Contains ("verbose"));
			Assert.IsTrue (set.Contains ("loud"));
			var option = set ["v"];
			CollectionAssert.AreEqual (new [] { "v", "verbose", "loud" }, option.GetNames ());
			Assert.AreEqual ("chatty", option.Description);
			Assert.AreEqual (OptionValueType.None, option.OptionValueType);
		}

		[Test]
		public void HelpTextListsEveryOption ()
		{
			var set = new OptionSet {
				{ "v|verbose", "be verbose", v => { } },
				{ "n|name=", "the {NAME} to use", v => { } },
				{ "o|opt:", "optional value", v => { } },
				{ "D:", "define a {0:NAME}={1:VALUE} pair", (k, v) => { } },
			};
			var writer = new StringWriter ();
			set.WriteOptionDescriptions (writer);
			var help = writer.ToString ();
			StringAssert.Contains ("-v, --verbose", help);
			StringAssert.Contains ("--name=NAME", help);
			StringAssert.Contains ("be verbose", help);
			StringAssert.Contains ("optional value", help);
			StringAssert.Contains ("-D", help);
		}

		[Test]
		public void MessagesGoThroughTheLocalizer ()
		{
			var set = new OptionSet (s => "[" + s + "]") { { "n=", v => { } } };
			var ex = Assert.Throws<OptionException> (() => set.Parse (new [] { "-n" }));
			StringAssert.StartsWith ("[", ex.Message);
		}

		[Test]
		public void ValueCollectionBehavesLikeAList ()
		{
			OptionValueCollection seen = null;
			var set = new OptionSet { { "D:", (k, v) => { } } };
			var option = set ["D"];
			Assert.AreEqual (2, option.MaxValueCount);
			CollectionAssert.AreEquivalent (new [] { ":", "=" }, option.GetValueSeparators ());
			var context = new OptionContext (set) { Option = option };
			seen = context.OptionValues;
			seen.Add ("a");
			seen.Insert (0, "b");
			Assert.AreEqual (2, seen.Count);
			Assert.AreEqual ("b", seen [0]);
			Assert.IsTrue (seen.Contains ("a"));
			Assert.AreEqual (1, seen.IndexOf ("a"));
			CollectionAssert.AreEqual (new [] { "b", "a" }, seen.ToArray ());
			Assert.AreEqual ("b, a", seen.ToString ());
			seen.RemoveAt (0);
			Assert.IsTrue (seen.Remove ("a"));
			Assert.AreEqual (0, seen.ToList ().Count);
			seen.Clear ();
		}
	}
}
