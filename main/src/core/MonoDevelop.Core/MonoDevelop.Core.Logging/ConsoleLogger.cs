// 
// ConsoleLogger.cs
// 
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MonoDevelop.Core.Logging
{
	
	public class ConsoleLogger : IStructuredLogger
	{
		EnabledLoggingLevel enabledLevel = EnabledLoggingLevel.UpToInfo;
		bool useColour;
		static readonly object writeLock = new object ();

		/// <summary>Text (default) or one JSON object per line (<c>MD_LOG_FORMAT=json</c>, ADR 0023).</summary>
		public LogFormat Format { get; set; }
		
		public ConsoleLogger ()
		{
		}

		public void Log (LogLevel level, string message)
		{
			Log (level, message, null);
		}

		public void Log (LogLevel level, string message, IReadOnlyList<KeyValuePair<string, object>> properties)
		{
			if (Format == LogFormat.Json) {
				string line = FormatJson (DateTimeOffset.UtcNow, level, message, properties);
				lock (writeLock)
					Console.Out.WriteLine (line);
				return;
			}
			LogText (level, message);
		}

		/// <summary>
		/// One JSON object: <c>timestamp</c> (ISO 8601, UTC), <c>level</c>, <c>message</c>, then the properties
		/// (a property named like one of these three is skipped).
		/// </summary>
		internal static string FormatJson (DateTimeOffset timestamp, LogLevel level, string message, IReadOnlyList<KeyValuePair<string, object>> properties)
		{
			var buffer = new ArrayBufferWriter<byte> ();
			using (var json = new Utf8JsonWriter (buffer)) {
				json.WriteStartObject ();
				json.WriteString ("timestamp", timestamp);
				json.WriteString ("level", level.ToString ().ToLowerInvariant ());
				json.WriteString ("message", message);
				if (properties != null) {
					foreach (var property in properties) {
						if (property.Key == "timestamp" || property.Key == "level" || property.Key == "message")
							continue;
						switch (property.Value) {
						case null:
							json.WriteNull (property.Key);
							break;
						case bool b:
							json.WriteBoolean (property.Key, b);
							break;
						case int i:
							json.WriteNumber (property.Key, i);
							break;
						case long l:
							json.WriteNumber (property.Key, l);
							break;
						case double d:
							json.WriteNumber (property.Key, d);
							break;
						default:
							json.WriteString (property.Key, property.Value.ToString ());
							break;
						}
					}
				}
				json.WriteEndObject ();
			}
			return Encoding.UTF8.GetString (buffer.WrittenSpan);
		}

		void LogText (LogLevel level, string message)
		{
			string header;
			
			switch (level) {
			case LogLevel.Fatal:
				header = "FATAL ERROR";
				if (useColour) {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Yellow;
					ConsoleCrayon.BackgroundColor = ConsoleColor.Red;
				}
				break;
			case LogLevel.Error:
				header = "ERROR";
				if (useColour) {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Red;
					ConsoleCrayon.BackgroundColor = ConsoleColor.Yellow;
				}
				break;
			case LogLevel.Warn:
				header = "WARNING";
				if (useColour) {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Red;
				}
				break;
			case LogLevel.Info:
				header = "INFO";
				if (useColour) {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Green;
				}
				break;
			case LogLevel.Debug:
				header = "DEBUG";
				if (useColour) {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Blue;
				}
				break;
			default:
				header = "LOG";
				break;
			}
			
			Console.Write ("{0} [{1}]:", header, DateTime.Now.ToString ("u"));
			if (useColour) ConsoleCrayon.ResetColor ();
			Console.WriteLine (" " + message);
		}
		
		public EnabledLoggingLevel EnabledLevel {
			get { return enabledLevel; }
			set { enabledLevel = value; }
		}

		public string Name {
			get { return "ConsoleLogger"; }
		}
		
		public bool UseColour {
			get { return useColour; }
			set {
				useColour = false;
				if (value) try {
					ConsoleCrayon.ForegroundColor = ConsoleColor.Red;
					ConsoleCrayon.ResetColor ();
					useColour = true;
				} catch { }
			}
		}
	}
}
