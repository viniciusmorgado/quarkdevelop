//
// TargetFrameworkMonikerTests.cs
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
using System.IO;
using System.Linq;
using System.Xml;
using MonoDevelop.Core.Serialization;
using NUnit.Framework;

namespace MonoDevelop.Core.Assemblies
{
	/// <summary>
	/// Task T056: parsing, formatting, ordering and identity of <see cref="TargetFrameworkMoniker"/>.
	/// </summary>
	[TestFixture]
	public class TargetFrameworkMonikerTests
	{
		[Test]
		public void Constructors ()
		{
			var fx = new TargetFrameworkMoniker ("v4.5");
			Assert.AreEqual (TargetFrameworkMoniker.ID_NET_FRAMEWORK, fx.Identifier);
			Assert.AreEqual ("4.5", fx.Version);
			Assert.IsNull (fx.Profile);

			fx = new TargetFrameworkMoniker (".NETPortable", "4.0", "");
			Assert.IsNull (fx.Profile, "an empty profile means no profile");

			fx = new TargetFrameworkMoniker ("MonoAndroid", "v9.0");
			Assert.AreEqual ("MonoAndroid", fx.Identifier);
			Assert.AreEqual ("9.0", fx.Version);

			Assert.Throws<ArgumentException> (() => new TargetFrameworkMoniker (""));
			Assert.Throws<ArgumentException> (() => new TargetFrameworkMoniker ("v"));
			Assert.Throws<ArgumentException> (() => new TargetFrameworkMoniker ("x", null));
			Assert.Throws<ArgumentException> (() => new TargetFrameworkMoniker (null, "1.0"));
			Assert.Throws<ArgumentException> (() => new TargetFrameworkMoniker ("", "1.0"));
		}

		[TestCase (".NETFramework,Version=v4.7.2", ".NETFramework", "4.7.2", null)]
		[TestCase (".NETPortable,Version=v4.0,Profile=Profile7", ".NETPortable", "4.0", "Profile7")]
		[TestCase (".NETCoreApp,Version=v3.1", ".NETCoreApp", "3.1", null)]
		[TestCase ("SL2.0", "Silverlight", "2.0", null)]
		[TestCase ("SL3.0", "Silverlight", "3.0", null)]
		[TestCase ("IPhone", "MonoTouch", "1.0", null)]
		[TestCase ("v3.5", ".NETFramework", "3.5", null)]
		[TestCase ("4.0", ".NETFramework", "4.0", null)]
		public void Parse (string value, string identifier, string version, string profile)
		{
			var fx = TargetFrameworkMoniker.Parse (value);
			Assert.AreEqual (identifier, fx.Identifier);
			Assert.AreEqual (version, fx.Version);
			Assert.AreEqual (profile, fx.Profile);

			Assert.IsTrue (TargetFrameworkMoniker.TryParse (fx.ToString (), out var reparsed));
			Assert.AreEqual (fx, reparsed);
		}

		[TestCase ("abc")]
		[TestCase (".NETFramework,Ver=4.0")]
		[TestCase (".NETFramework,Versio=v4.0.0.0")]
		[TestCase (".NETFramework,Version=vX")]
		[TestCase (",Version=v4.0")]
		public void InvalidMonikers (string value)
		{
			Assert.IsFalse (TargetFrameworkMoniker.TryParse (value, out var fx));
			Assert.IsNull (fx);
			var ex = Assert.Throws<FormatException> (() => TargetFrameworkMoniker.Parse (value));
			StringAssert.Contains ("Invalid framework moniker", ex.Message);
		}

		[Test]
		public void ToStringIncludesTheProfile ()
		{
			Assert.AreEqual (".NETFramework,Version=v4.5", TargetFrameworkMoniker.NET_4_5.ToString ());
			Assert.AreEqual (".NETPortable,Version=v4.0,Profile=Profile1", TargetFrameworkMoniker.PORTABLE_4_0.ToString ());
		}

		[Test]
		public void LegacyIds ()
		{
			Assert.AreEqual ("SL2.0", TargetFrameworkMoniker.SL_2_0.ToLegacyIdString ());
			Assert.AreEqual ("SL3.0", TargetFrameworkMoniker.SL_3_0.ToLegacyIdString ());
			Assert.AreEqual ("Silverlight,Version=v4.0", TargetFrameworkMoniker.SL_4_0.ToLegacyIdString ());
			Assert.AreEqual ("IPhone", TargetFrameworkMoniker.MONOTOUCH_1_0.ToLegacyIdString ());
			Assert.AreEqual ("4.5", TargetFrameworkMoniker.NET_4_5.ToLegacyIdString ());
			Assert.AreEqual ("MonoTouch,Version=v2.0", new TargetFrameworkMoniker ("MonoTouch", "2.0").ToLegacyIdString ());

			foreach (var fx in new[] { TargetFrameworkMoniker.SL_2_0, TargetFrameworkMoniker.MONOTOUCH_1_0, TargetFrameworkMoniker.NET_4_0 })
				Assert.AreEqual (fx, TargetFrameworkMoniker.Parse (fx.ToLegacyIdString ()));
		}

		[Test]
		public void AssemblyDirectoryName ()
		{
			var fx = TargetFrameworkMoniker.NET_4_5;
			Assert.AreEqual (Path.Combine (".NETFramework", "v4.5"), fx.GetAssemblyDirectoryName ());
			Assert.AreSame (fx.GetAssemblyDirectoryName (), fx.GetAssemblyDirectoryName (), "the name is cached");

			Assert.AreEqual (Path.Combine (".NETPortable", "v4.0", "Profile", "Profile1"), TargetFrameworkMoniker.PORTABLE_4_0.GetAssemblyDirectoryName ());
		}

		[Test]
		public void Equality ()
		{
			var a = TargetFrameworkMoniker.Parse (".NETPortable,Version=v4.0,Profile=Profile7");
			var b = new TargetFrameworkMoniker (".NETPortable", "4.0", "Profile7");
			var c = new TargetFrameworkMoniker (".NETPortable", "4.0", "Profile78");
			TargetFrameworkMoniker none = null;

			Assert.IsTrue (a == b);
			Assert.IsFalse (a != b);
			Assert.IsTrue (a != c);
			Assert.IsFalse (a == c);
			Assert.IsTrue (a.Equals ((object)b));
			Assert.IsFalse (a.Equals ((object)"4.0"));
			Assert.IsFalse (a.Equals (none));
			Assert.AreEqual (a.GetHashCode (), b.GetHashCode ());

			Assert.IsTrue (none == null);
			Assert.IsFalse (none != null);
			Assert.IsFalse (none == a);
			Assert.IsTrue (none != a);
			Assert.IsFalse (a == none);
		}

		[Test]
		public void ShortNames ()
		{
			Assert.AreEqual ("net472", TargetFrameworkMoniker.NET_4_7_2.ShortName);
			Assert.AreEqual ("net40", TargetFrameworkMoniker.NET_4_0.ShortName);
			Assert.AreEqual ("netcoreapp3.1", new TargetFrameworkMoniker (".NETCoreApp", "3.1").ShortName);
			Assert.AreEqual ("net5.0", new TargetFrameworkMoniker (".NETCoreApp", "5.0").ShortName);
			Assert.AreEqual ("net10.0", new TargetFrameworkMoniker (".NETCoreApp", "10.0").ShortName);
			Assert.AreEqual ("netstandard2.0", new TargetFrameworkMoniker (".NETStandard", "2.0").ShortName);
			Assert.AreEqual ("monoandroid9.0", new TargetFrameworkMoniker ("MonoAndroid", "9.0").ShortName);

			var custom = TargetFrameworkMoniker.NET_4_7_2.WithShortName ("custom");
			Assert.AreEqual ("custom", custom.ShortName);
			Assert.AreEqual (TargetFrameworkMoniker.NET_4_7_2, custom);
		}

		[Test]
		public void Ordering ()
		{
			var net49 = new TargetFrameworkMoniker ("4.9");
			var net410 = new TargetFrameworkMoniker ("4.10");
			Assert.Less (net49.CompareTo (net410), 0, "versions are compared numerically");
			Assert.Greater (net410.CompareTo (net49), 0);
			Assert.AreEqual (0, net49.CompareTo (new TargetFrameworkMoniker (".netframework", "4.9")), "identifiers are case insensitive");

			Assert.Less (new TargetFrameworkMoniker ("A", "9.0").CompareTo (new TargetFrameworkMoniker ("B", "1.0")), 0);

			var p1 = new TargetFrameworkMoniker (".NETPortable", "4.0", "Profile1");
			var p2 = new TargetFrameworkMoniker (".NETPortable", "4.0", "Profile2");
			var noProfile = new TargetFrameworkMoniker (".NETPortable", "4.0");
			Assert.Less (p1.CompareTo (p2), 0);
			Assert.Less (noProfile.CompareTo (p1), 0);

			// Versions that are not System.Version compatible fall back to string comparison.
			var odd1 = new TargetFrameworkMoniker ("X", "1.0-alpha");
			var odd2 = new TargetFrameworkMoniker ("X", "1.0-beta");
			Assert.Less (odd1.CompareTo (odd2), 0);
			Assert.AreEqual (0, odd1.CompareTo (new TargetFrameworkMoniker ("X", "1.0-ALPHA")));

			var sorted = new[] { TargetFrameworkMoniker.NET_4_7_2, TargetFrameworkMoniker.NET_2_0, TargetFrameworkMoniker.NET_4_6_1 }
				.OrderBy (f => f).Select (f => f.Version).ToArray ();
			CollectionAssert.AreEqual (new[] { "2.0", "4.6.1", "4.7.2" }, sorted);
		}

		[Test]
		public void WellKnownMonikers ()
		{
			Assert.AreEqual (TargetFrameworkMoniker.NET_1_1, TargetFrameworkMoniker.Default);
			var expected = new[] { "1.1", "2.0", "3.0", "3.5", "4.0", "4.5", "4.6", "4.6.1", "4.6.2", "4.7", "4.7.1", "4.7.2" };
			var actual = new[] {
				TargetFrameworkMoniker.NET_1_1, TargetFrameworkMoniker.NET_2_0, TargetFrameworkMoniker.NET_3_0,
				TargetFrameworkMoniker.NET_3_5, TargetFrameworkMoniker.NET_4_0, TargetFrameworkMoniker.NET_4_5,
				TargetFrameworkMoniker.NET_4_6, TargetFrameworkMoniker.NET_4_6_1, TargetFrameworkMoniker.NET_4_6_2,
				TargetFrameworkMoniker.NET_4_7, TargetFrameworkMoniker.NET_4_7_1, TargetFrameworkMoniker.NET_4_7_2
			};
			CollectionAssert.AreEqual (expected, actual.Select (f => f.Version));
			Assert.IsTrue (actual.All (f => f.Identifier == TargetFrameworkMoniker.ID_NET_FRAMEWORK));
			Assert.AreEqual ("Unknown,Version=v0.0", TargetFrameworkMoniker.UNKNOWN.ToString ());
		}

		[Test]
		public void DataTypeRoundTrip ()
		{
			var dataType = new TargetFrameworkMonikerDataType (typeof (TargetFrameworkMoniker));
			Assert.IsTrue (dataType.IsSimpleType);
			Assert.IsTrue (dataType.CanCreateInstance);
			Assert.IsFalse (dataType.CanReuseInstance);

			var node = (DataValue)dataType.OnSerialize (null, null, TargetFrameworkMoniker.PORTABLE_4_0);
			Assert.AreEqual ("TargetFrameworkMoniker", node.Name);
			Assert.AreEqual (".NETPortable,Version=v4.0,Profile=Profile1", node.Value);
			Assert.AreEqual (TargetFrameworkMoniker.PORTABLE_4_0, dataType.OnDeserialize (null, null, node));
		}
	}

	/// <summary>
	/// Task T056: loading, saving and comparing <see cref="SupportedFramework"/> descriptions (the XML used by
	/// portable library profiles).
	/// </summary>
	[TestFixture]
	public class SupportedFrameworkTests
	{
		const string FullFramework =
			"<Framework Identifier=\".NETFramework\" Profile=\"Client\" MinimumVersion=\"4.0.3\" MaximumVersion=\"*\" " +
			"DisplayName=\".NET Framework\" MinimumVersionDisplayName=\"4.0.3\" MonoSpecificVersion=\"4.5\" " +
			"MonoSpecificVersionDisplayName=\"Mono 4.5\" Unknown=\"ignored\" />";

		static SupportedFramework LoadFromString (string xml)
		{
			using (var reader = XmlReader.Create (new StringReader (xml))) {
				reader.MoveToContent ();
				return SupportedFramework.LoadFromAttributes (reader);
			}
		}

		static string Save (SupportedFramework fx)
		{
			var sw = new StringWriter ();
			using (var writer = XmlWriter.Create (sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
				fx.SaveAsElement (writer);
			return sw.ToString ();
		}

		[Test]
		public void LoadAllAttributes ()
		{
			var fx = LoadFromString (FullFramework);
			Assert.AreEqual (".NETFramework", fx.Identifier);
			Assert.AreEqual ("Client", fx.Profile);
			Assert.AreEqual (new Version (4, 0, 3), fx.MinimumVersion);
			Assert.AreEqual (SupportedFramework.NoMaximumVersion, fx.MaximumVersion);
			Assert.AreEqual (".NET Framework", fx.DisplayName);
			Assert.AreEqual ("4.0.3", fx.MinimumVersionDisplayName);
			Assert.AreEqual ("4.5", fx.MonoSpecificVersion);
			Assert.AreEqual ("Mono 4.5", fx.MonoSpecificVersionDisplayName);
		}

		[Test]
		public void Defaults ()
		{
			var fx = LoadFromString ("<Framework Identifier=\"Xamarin.iOS\" MinimumVersion=\"*\" MaximumVersion=\"2.0\" />");
			Assert.AreEqual (SupportedFramework.NoMinimumVersion, fx.MinimumVersion);
			Assert.AreEqual (new Version (2, 0), fx.MaximumVersion);
			Assert.AreEqual ("", fx.Profile);
			Assert.AreEqual ("", fx.DisplayName);
			Assert.IsNull (fx.MonoSpecificVersion);
		}

		[TestCase ("<Framework />")]
		[TestCase ("<Framework Profile=\"x\" />")]
		public void InvalidElements (string xml)
		{
			Assert.Throws<Exception> (() => LoadFromString (xml));
		}

		[Test]
		public void SaveAndReload ()
		{
			var fx = LoadFromString (FullFramework);
			var xml = Save (fx);
			StringAssert.StartsWith ("<SupportedFramework ", xml);
			StringAssert.DoesNotContain ("MaximumVersion", xml, "the unbounded maximum is not written");
			StringAssert.DoesNotContain ("Unknown", xml);

			var reloaded = LoadFromString (xml);
			Assert.AreEqual (fx, reloaded);
			Assert.AreEqual (fx.GetHashCode (), reloaded.GetHashCode ());
			Assert.IsTrue (SupportedFramework.EqualityComparer.Equals (fx, reloaded));
			Assert.AreEqual (fx.GetHashCode (), SupportedFramework.EqualityComparer.GetHashCode (reloaded));

			var bounded = LoadFromString ("<Framework Identifier=\"X\" MinimumVersion=\"1.0\" MaximumVersion=\"2.0\" />");
			var boundedXml = Save (bounded);
			StringAssert.Contains ("MaximumVersion=\"2.0\"", boundedXml);
			StringAssert.Contains ("MinimumVersion=\"1.0\"", boundedXml);
			Assert.AreEqual (bounded, LoadFromString (boundedXml));
		}

		[Test]
		public void LoadFromFileUsesTheFileNameAsDefaultDisplayName ()
		{
			var dir = Path.Combine (Path.GetTempPath (), "md-supportedfx-" + Guid.NewGuid ());
			Directory.CreateDirectory (dir);
			try {
				var file = Path.Combine (dir, "Windows Phone 8.xml");
				File.WriteAllText (file, "<Root><Framework Identifier=\"WindowsPhone\" MinimumVersion=\"8.0\" /></Root>");
				var fx = SupportedFramework.Load (file);
				Assert.AreEqual ("WindowsPhone", fx.Identifier);
				Assert.AreEqual ("Windows Phone 8", fx.DisplayName);

				File.WriteAllText (file, "<Framework Identifier=\"A\" DisplayName=\"Named\" />");
				Assert.AreEqual ("Named", SupportedFramework.Load (file).DisplayName);

				File.WriteAllText (file, "<Root><Other /></Root>");
				Assert.Throws<Exception> (() => SupportedFramework.Load (file));
			} finally {
				Directory.Delete (dir, true);
			}
		}

		[Test]
		public void EqualityComparesEveryField ()
		{
			var fx = new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.5");
			Assert.AreEqual (".NETFramework", fx.Identifier);
			Assert.AreEqual ("Display", fx.DisplayName);
			Assert.AreEqual ("Profile", fx.Profile);
			Assert.AreEqual (new Version (4, 5), fx.MinimumVersion);
			Assert.AreEqual ("4.5", fx.MinimumVersionDisplayName);
			Assert.AreEqual (SupportedFramework.NoMaximumVersion, fx.MaximumVersion);

			Assert.AreEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.5"));
			Assert.AreNotEqual (fx, new SupportedFramework ("Other", "Display", "Profile", new Version (4, 5), "4.5"));
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Other", "Profile", new Version (4, 5), "4.5"));
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Other", new Version (4, 5), "4.5"));
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 6), "4.5"));
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.6"));
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.5") { MaximumVersion = new Version (5, 0) });
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.5") { MonoSpecificVersion = "1" });
			Assert.AreNotEqual (fx, new SupportedFramework (".NETFramework", "Display", "Profile", new Version (4, 5), "4.5") { MonoSpecificVersionDisplayName = "1" });
			Assert.IsFalse (fx.Equals ("Display"));
		}
	}
}
