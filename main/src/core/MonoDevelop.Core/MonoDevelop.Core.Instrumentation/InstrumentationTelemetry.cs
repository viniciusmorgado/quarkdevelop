//
// InstrumentationTelemetry.cs
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

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MonoDevelop.Core.Instrumentation
{
	/// <summary>
	/// Publishes the instrumentation counters through <see cref="System.Diagnostics.Metrics"/> and
	/// <see cref="System.Diagnostics.ActivitySource"/> (T126, ADR 0023), so that <c>dotnet-counters</c>,
	/// <c>dotnet-trace</c> or an OpenTelemetry exporter can observe the IDE without the old instrumentation monitor.
	/// Nothing is recorded unless a listener is attached.
	/// </summary>
	public static class InstrumentationTelemetry
	{
		/// <summary>The name of both the meter and the activity source.</summary>
		public const string Name = "MonoDevelop";

		public static readonly Meter Meter = new Meter (Name, BuildInfo.Version);

		/// <summary>One activity per <see cref="TimerCounter.BeginTiming()"/> (activity name = counter name).</summary>
		public static readonly ActivitySource ActivitySource = new ActivitySource (Name, BuildInfo.Version);

		/// <summary><see cref="Counter.Inc()"/> and <see cref="Counter.Dec()"/> amounts, tagged with the counter name.</summary>
		public static readonly UpDownCounter<long> CounterChanges = Meter.CreateUpDownCounter<long> (
			"monodevelop.counter.changes", description: "Increments and decrements of the instrumentation counters (tag: counter)");

		/// <summary>Durations of the timer counters, tagged with the counter name.</summary>
		public static readonly Histogram<double> TimerDuration = Meter.CreateHistogram<double> (
			"monodevelop.timer.duration", unit: "ms", description: "Durations measured by the instrumentation timer counters (tag: counter)");

		internal static bool IsTimingObserved => TimerDuration.Enabled || ActivitySource.HasListeners ();

		internal static void RecordChange (Counter counter, long delta)
		{
			if (CounterChanges.Enabled)
				CounterChanges.Add (delta, new KeyValuePair<string, object?> ("counter", counter.Name));
		}

		internal static void RecordDuration (Counter counter, double milliseconds)
		{
			if (TimerDuration.Enabled)
				TimerDuration.Record (milliseconds, new KeyValuePair<string, object?> ("counter", counter.Name));
		}
	}
}
