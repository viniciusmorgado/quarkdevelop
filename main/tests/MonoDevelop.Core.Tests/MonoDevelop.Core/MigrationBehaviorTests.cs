//
// MigrationBehaviorTests.cs
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
using System.Reflection;
using System.Threading.Tasks;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Projects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Behaviour changed while porting the core to .NET 10 (tasks T034–T036, ADR 0009) and the CA5369 fix.
	/// </summary>
	[TestFixture]
	public class MigrationBehaviorTests : TestBase
	{
		sealed class DisposableStub : IDisposable
		{
			public void Dispose ()
			{
			}
		}

		[Test]
		public void ExternalProcessObjectsAreNotSupported ()
		{
			// mdhost relied on .NET Remoting, which does not exist on .NET.
			Assert.Throws<NotSupportedException> (() => Runtime.ProcessService.CreateExternalProcessObject (typeof (DisposableStub)));
		}

		[Test]
		public void ExternalProcessObjectStillValidatesTheType ()
		{
			Assert.Throws<ArgumentException> (() => Runtime.ProcessService.CreateExternalProcessObject (typeof (object)));
		}

		[Test]
		public void RemoteHostingIsNeverValid ()
		{
			Assert.IsFalse (Runtime.ProcessService.IsValidForRemoteHosting (new MonoDevelop.Core.Execution.NativePlatformExecutionHandler ()));
		}

		[Test]
		public void InstrumentationDataIsSavedAsJson ()
		{
			var file = Path.Combine (Path.GetTempPath (), "md-instrumentation-" + Guid.NewGuid () + ".json");
			try {
				InstrumentationService.SaveJson (file);
				var json = JObject.Parse (File.ReadAllText (file));
				Assert.IsNotNull (json["StartTime"], "the saved data contains the service start time");
			} finally {
				File.Delete (file);
			}
		}

		[Test]
		public void LoadingBinaryInstrumentationDataIsNotSupported ()
		{
			Assert.Throws<PlatformNotSupportedException> (() => InstrumentationService.LoadServiceDataFromFile ("unused.bin"));
		}

		[Test]
		public void DelayedItemInitializationIsScopedToTheCall ()
		{
			Assert.IsFalse (ItemInitializationContext.DelayItemInitialization);
			var seenInside = ItemInitializationContext.CreateUninitialized (() => ItemInitializationContext.DelayItemInitialization);
			Assert.IsTrue (seenInside);
			Assert.IsFalse (ItemInitializationContext.DelayItemInitialization);
		}

		[Test]
		public async Task DelayedItemInitializationFlowsAcrossAwaits ()
		{
			var flowed = await ItemInitializationContext.CreateUninitialized (async () => {
				await Task.Yield ();
				return ItemInitializationContext.DelayItemInitialization;
			});
			Assert.IsTrue (flowed, "the flag must flow to async continuations (CallContext.LogicalSetData semantics)");
		}

		[Test]
		public async Task DelayedItemInitializationDoesNotLeakToOtherFlows ()
		{
			var otherFlow = Task.Run (() => ItemInitializationContext.DelayItemInitialization);
			var inner = ItemInitializationContext.CreateUninitialized (() => otherFlow);
			Assert.IsFalse (await inner);
		}

		[Test]
		public void StoredPropertiesRejectDtds ()
		{
			// CA5369: stored property XML is deserialized without DTD processing.
			var deserializerType = typeof (Properties).GetNestedType ("LazyXmlDeserializer", BindingFlags.NonPublic);
			Assert.IsNotNull (deserializerType);
			const string xml = "<?xml version=\"1.0\"?><!DOCTYPE string [<!ENTITY x \"expanded\">]><string>&x;</string>";
			var deserializer = Activator.CreateInstance (deserializerType, xml);
			var result = deserializerType.GetMethod ("Deserialize").Invoke (deserializer, new object[] { typeof (string) });
			Assert.IsNull (result, "a document with a DTD must not be deserialized");
		}

		[Test]
		public void StoredPropertiesStillDeserializeRegularXml ()
		{
			var deserializerType = typeof (Properties).GetNestedType ("LazyXmlDeserializer", BindingFlags.NonPublic);
			var deserializer = Activator.CreateInstance (deserializerType, "<string>value</string>");
			var result = deserializerType.GetMethod ("Deserialize").Invoke (deserializer, new object[] { typeof (string) });
			Assert.AreEqual ("value", result);
		}
	}
}
