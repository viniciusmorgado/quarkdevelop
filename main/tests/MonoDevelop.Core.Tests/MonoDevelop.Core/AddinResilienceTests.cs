//
// AddinResilienceTests.cs
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
using Mono.Addins;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Task T050: with Mono.Addins 1.4.1 on CoreCLR (ADR 0006), an add-in whose dependency is missing
	/// is reported and skipped; the add-ins that do resolve keep contributing extensions. Runs on a
	/// private engine and registry so the test host's add-ins are not touched.
	/// </summary>
	[TestFixture]
	public class AddinResilienceTests
	{
		string root;
		AddinEngine engine;

		const string HostManifest = @"<Addin id=""Host"" namespace=""Resilience"" version=""1.0"" isroot=""true"">
	<ExtensionPoint path=""/Resilience/Items"">
		<ExtensionNode name=""Item"" />
	</ExtensionPoint>
</Addin>";

		const string GoodManifest = @"<Addin id=""Good"" namespace=""Resilience"" version=""1.0"">
	<Dependencies><Addin id=""Host"" version=""1.0"" /></Dependencies>
	<Extension path=""/Resilience/Items""><Item id=""FromGood"" /></Extension>
</Addin>";

		const string BrokenManifest = @"<Addin id=""Broken"" namespace=""Resilience"" version=""1.0"">
	<Dependencies>
		<Addin id=""Host"" version=""1.0"" />
		<Addin id=""NotInstalled"" version=""1.0"" />
	</Dependencies>
	<Extension path=""/Resilience/Items""><Item id=""FromBroken"" /></Extension>
</Addin>";

		[SetUp]
		public void SetUp ()
		{
			root = Path.Combine (Path.GetTempPath (), "md-addin-resilience-" + Guid.NewGuid ());
			var addins = Path.Combine (root, "addins");
			Directory.CreateDirectory (addins);
			File.WriteAllText (Path.Combine (root, "Host.addin.xml"), HostManifest);
			File.WriteAllText (Path.Combine (addins, "Good.addin.xml"), GoodManifest);
			File.WriteAllText (Path.Combine (addins, "Broken.addin.xml"), BrokenManifest);
			engine = new AddinEngine ();
		}

		[TearDown]
		public void TearDown ()
		{
			if (engine.IsInitialized)
				engine.Shutdown ();
			Directory.Delete (root, true);
		}

		[Test]
		public void AddinWithMissingDependencyIsSkippedAndReported ()
		{
			var errors = new List<string> ();
			// Static in Mono.Addins 1.4.1: filter to this test's add-ins and unsubscribe afterwards.
			AddinErrorEventHandler onError = (sender, args) => {
				if (args.AddinId != null && args.AddinId.StartsWith ("Resilience.", StringComparison.Ordinal))
					errors.Add (args.AddinId + ": " + args.Message);
			};
			AddinEngine.AddinLoadError += onError;
			try {
				Run (errors);
			} finally {
				AddinEngine.AddinLoadError -= onError;
			}
		}

		void Run (List<string> errors)
		{
			// Initialize scans the folders and reports problems on the console (MonoDevelop's log in the IDE).
			var console = new StringWriter ();
			var stdout = Console.Out;
			Console.SetOut (console);
			try {
				// (configDir, addinsDir, databaseDir, startupDirectory)
				engine.Initialize (Path.Combine (root, "config"), Path.Combine (root, "addins"), Path.Combine (root, "db"), root);
			} finally {
				Console.SetOut (stdout);
			}
			errors.Add (console.ToString ());
			var status = new RecordingStatus ();
			engine.Registry.Update (status);

			// Both manifests are registered; the broken one is not usable.
			var broken = engine.Registry.GetAddin ("Resilience.Broken");
			Assert.IsNotNull (broken, "broken add-in is registered");
			Assert.IsNotNull (engine.Registry.GetAddin ("Resilience.Good"));

			// The host is a root add-in (normally loaded with its assembly); load it explicitly.
			engine.LoadAddin (null, "Resilience.Host");
			var items = engine.GetExtensionNodes ("/Resilience/Items").Cast<ExtensionNode> ().Select (n => n.Id).ToList ();
			CollectionAssert.Contains (items, "FromGood");
			CollectionAssert.DoesNotContain (items, "FromBroken");

			Assert.IsFalse (engine.IsAddinLoaded ("Resilience.Broken"));
			// The missing dependency is reported, either while scanning or when loading.
			var report = string.Join ("\n", errors.Concat (status.Messages));
			StringAssert.Contains ("NotInstalled", report);
		}

		sealed class RecordingStatus : IProgressStatus
		{
			public List<string> Messages { get; } = new List<string> ();

			public int LogLevel => 4; // dependency problems are logged at the detailed levels
			public bool IsCanceled => false;
			public void SetMessage (string msg) { }
			public void SetProgress (double progress) { }
			public void Log (string msg) => Messages.Add (msg);
			public void ReportWarning (string message) => Messages.Add (message);
			public void ReportError (string message, Exception exception) => Messages.Add (message);
			public void Cancel () { }
		}
	}
}
