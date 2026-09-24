//
// StructuredLoggingTests.cs
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
using System.Text.Json;
using MonoDevelop.Core.Logging;
using NUnit.Framework;

namespace MonoDevelop.Core
{
	/// <summary>MD_LOG_LEVEL, MD_LOG_FORMAT=json and the start-up record (T125, ADR 0023).</summary>
	[TestFixture]
	public class StructuredLoggingTests
	{
		[TestCase ("fatal", EnabledLoggingLevel.UpToFatal)]
		[TestCase ("Error", EnabledLoggingLevel.UpToError)]
		[TestCase ("warn", EnabledLoggingLevel.UpToWarn)]
		[TestCase ("warning", EnabledLoggingLevel.UpToWarn)]
		[TestCase ("info", EnabledLoggingLevel.UpToInfo)]
		[TestCase (" DEBUG ", EnabledLoggingLevel.UpToDebug)]
		[TestCase ("none", EnabledLoggingLevel.None)]
		[TestCase ("UpToWarn", EnabledLoggingLevel.UpToWarn)]
		public void TryParseLevel_KnownNames (string value, EnabledLoggingLevel expected)
		{
			Assert.That (LoggingService.TryParseLevel (value, out var level), Is.True);
			Assert.That (level, Is.EqualTo (expected));
		}

		[TestCase ("verbose")]
		[TestCase ("")]
		[TestCase (null)]
		public void TryParseLevel_UnknownNames (string value)
		{
			Assert.That (LoggingService.TryParseLevel (value, out _), Is.False);
		}

		[Test]
		public void FormatJson_WritesTimestampLevelMessageAndProperties ()
		{
			var properties = new[] {
				new KeyValuePair<string, object> ("version", "8.6"),
				new KeyValuePair<string, object> ("count", 3),
				new KeyValuePair<string, object> ("ok", true),
				new KeyValuePair<string, object> ("missing", null),
				new KeyValuePair<string, object> ("message", "ignored: reserved name"),
			};
			var timestamp = new DateTimeOffset (2026, 9, 24, 12, 30, 0, TimeSpan.Zero);

			string line = ConsoleLogger.FormatJson (timestamp, LogLevel.Warn, "a \"quoted\" message\nwith a new line", properties);

			Assert.That (line, Does.Not.Contain ("\n"), "one record per line");
			using var json = JsonDocument.Parse (line);
			var root = json.RootElement;
			Assert.That (root.GetProperty ("timestamp").GetDateTimeOffset (), Is.EqualTo (timestamp));
			Assert.That (root.GetProperty ("level").GetString (), Is.EqualTo ("warn"));
			Assert.That (root.GetProperty ("message").GetString (), Is.EqualTo ("a \"quoted\" message\nwith a new line"));
			Assert.That (root.GetProperty ("version").GetString (), Is.EqualTo ("8.6"));
			Assert.That (root.GetProperty ("count").GetInt32 (), Is.EqualTo (3));
			Assert.That (root.GetProperty ("ok").GetBoolean (), Is.True);
			Assert.That (root.GetProperty ("missing").ValueKind, Is.EqualTo (JsonValueKind.Null));
			Assert.That (root.EnumerateObject ().Count (p => p.Name == "message"), Is.EqualTo (1));
		}

		[Test]
		public void ConsoleLogger_JsonFormat_WritesOneObjectPerRecord ()
		{
			var logger = new ConsoleLogger { Format = LogFormat.Json };
			var output = new StringWriter ();
			var previous = Console.Out;
			Console.SetOut (output);
			try {
				logger.Log (LogLevel.Info, "first");
				logger.Log (LogLevel.Error, "second", new[] { new KeyValuePair<string, object> ("event", "test") });
			} finally {
				Console.SetOut (previous);
			}

			var lines = output.ToString ().Split (new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			Assert.That (lines, Has.Length.EqualTo (2));
			using var first = JsonDocument.Parse (lines[0]);
			using var second = JsonDocument.Parse (lines[1]);
			Assert.That (first.RootElement.GetProperty ("level").GetString (), Is.EqualTo ("info"));
			Assert.That (second.RootElement.GetProperty ("message").GetString (), Is.EqualTo ("second"));
			Assert.That (second.RootElement.GetProperty ("event").GetString (), Is.EqualTo ("test"));
		}

		[Test]
		public void ConsoleLogger_TextFormat_IgnoresProperties ()
		{
			var logger = new ConsoleLogger ();
			var output = new StringWriter ();
			var previous = Console.Out;
			Console.SetOut (output);
			try {
				logger.Log (LogLevel.Warn, "text record", new[] { new KeyValuePair<string, object> ("event", "test") });
			} finally {
				Console.SetOut (previous);
			}

			Assert.That (output.ToString (), Does.StartWith ("WARNING ["));
			Assert.That (output.ToString (), Does.Contain ("text record"));
			Assert.That (output.ToString (), Does.Not.Contain ("event"));
		}

		[Test]
		public void Log_WithProperties_ReachesStructuredLoggersAndPlainLoggers ()
		{
			var structured = new RecordingLogger ("StructuredLoggingTests.structured");
			var plain = new PlainLogger ();
			LoggingService.AddLogger (structured);
			LoggingService.AddLogger (plain);
			try {
				LoggingService.Log (LogLevel.Info, "hello", new[] { new KeyValuePair<string, object> ("key", "value") });
			} finally {
				LoggingService.RemoveLogger (structured.Name);
				LoggingService.RemoveLogger (plain.Name);
			}

			Assert.That (structured.Records.Single (r => r.Message == "hello").Properties.Single ().Value, Is.EqualTo ("value"));
			Assert.That (plain.Messages, Does.Contain ("hello"));
		}

		[Test]
		public void StartupProperties_IncludeVersionsAndPlatform ()
		{
			var properties = LoggingService.GetStartupProperties ().ToDictionary (p => p.Key, p => p.Value);

			Assert.That (properties["event"], Is.EqualTo ("startup"));
			Assert.That (properties["version"], Is.EqualTo (BuildInfo.Version));
			Assert.That ((string)properties["runtime"], Does.StartWith (".NET 10"));
			Assert.That ((string)properties["os"], Is.Not.Empty);
			Assert.That (properties.ContainsKey ("sdk"), Is.True);
			Assert.That (properties.ContainsKey ("host"), Is.True);
		}

		class RecordingLogger : IStructuredLogger
		{
			public RecordingLogger (string name) => Name = name;

			public List<(string Message, IReadOnlyList<KeyValuePair<string, object>> Properties)> Records { get; } = new ();

			public EnabledLoggingLevel EnabledLevel { get; set; } = EnabledLoggingLevel.All;

			public string Name { get; }

			public void Log (LogLevel level, string message) => Log (level, message, null);

			public void Log (LogLevel level, string message, IReadOnlyList<KeyValuePair<string, object>> properties)
			{
				lock (Records)
					Records.Add ((message, properties));
			}
		}

		class PlainLogger : ILogger
		{
			public List<string> Messages { get; } = new ();

			public EnabledLoggingLevel EnabledLevel { get; set; } = EnabledLoggingLevel.All;

			public string Name => "StructuredLoggingTests.plain";

			public void Log (LogLevel level, string message)
			{
				lock (Messages)
					Messages.Add (message);
			}
		}
	}
}
