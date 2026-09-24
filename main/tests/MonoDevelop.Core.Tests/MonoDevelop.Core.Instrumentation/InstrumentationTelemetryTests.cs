//
// InstrumentationTelemetryTests.cs
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using NUnit.Framework;

namespace MonoDevelop.Core.Instrumentation
{
	/// <summary>The instrumentation counters seen through System.Diagnostics.Metrics and ActivitySource (T126).</summary>
	[TestFixture]
	public class InstrumentationTelemetryTests
	{
		MeterListener meterListener;
		ActivityListener activityListener;
		List<(string Instrument, double Value, string Counter)> measurements;
		List<Activity> activities;

		[SetUp]
		public void SetUp ()
		{
			measurements = new List<(string, double, string)> ();
			activities = new List<Activity> ();

			meterListener = new MeterListener ();
			meterListener.InstrumentPublished = (instrument, listener) => {
				if (instrument.Meter.Name == InstrumentationTelemetry.Name)
					listener.EnableMeasurementEvents (instrument);
			};
			meterListener.SetMeasurementEventCallback<long> ((instrument, value, tags, state) => Record (instrument, value, tags));
			meterListener.SetMeasurementEventCallback<double> ((instrument, value, tags, state) => Record (instrument, value, tags));
			meterListener.Start ();

			activityListener = new ActivityListener {
				ShouldListenTo = source => source.Name == InstrumentationTelemetry.Name,
				Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
				ActivityStopped = activity => { lock (activities) activities.Add (activity); },
			};
			ActivitySource.AddActivityListener (activityListener);
		}

		[TearDown]
		public void TearDown ()
		{
			meterListener.Dispose ();
			activityListener.Dispose ();
		}

		void Record (Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
		{
			string counter = null;
			foreach (var tag in tags)
				if (tag.Key == "counter")
					counter = tag.Value as string;
			lock (measurements)
				measurements.Add ((instrument.Name, value, counter));
		}

		static string UniqueName (string prefix) => prefix + " " + Guid.NewGuid ().ToString ("N");

		[Test]
		public void InstrumentsArePublishedOnTheMonoDevelopMeter ()
		{
			Assert.That (InstrumentationTelemetry.Meter.Name, Is.EqualTo ("MonoDevelop"));
			Assert.That (InstrumentationTelemetry.CounterChanges.Enabled, Is.True, "the listener enabled the counter");
			Assert.That (InstrumentationTelemetry.TimerDuration.Enabled, Is.True, "the listener enabled the histogram");
			Assert.That (InstrumentationTelemetry.TimerDuration.Unit, Is.EqualTo ("ms"));
		}

		[Test]
		public void CounterIncAndDec_AreRecordedWithTheCounterName ()
		{
			string name = UniqueName ("telemetry counter");
			var counter = InstrumentationService.CreateCounter (name);

			counter.Inc ();
			counter.Inc (3);
			counter.Dec ();

			var changes = measurements.Where (m => m.Counter == name && m.Instrument == "monodevelop.counter.changes").Select (m => m.Value).ToList ();
			Assert.That (changes, Is.EqualTo (new double[] { 1, 3, -1 }));
		}

		[Test]
		public void TimerCounter_RecordsDurationAndActivity ()
		{
			string name = UniqueName ("telemetry timer");
			var timer = InstrumentationService.CreateTimerCounter (name);

			using (var tracker = timer.BeginTiming ())
				System.Threading.Thread.Sleep (20);

			var durations = measurements.Where (m => m.Counter == name && m.Instrument == "monodevelop.timer.duration").ToList ();
			Assert.That (durations, Has.Count.EqualTo (1));
			Assert.That (durations[0].Value, Is.GreaterThanOrEqualTo (15));
			Assert.That (measurements.Count (m => m.Counter == name && m.Instrument == "monodevelop.counter.changes"), Is.EqualTo (1));

			var activity = activities.Single (a => a.OperationName == name);
			Assert.That (activity.Source.Name, Is.EqualTo ("MonoDevelop"));
			Assert.That (activity.Duration, Is.GreaterThan (TimeSpan.Zero));
		}

		[Test]
		public void TimerCounter_WithoutListeners_StillMeasuresDuration ()
		{
			meterListener.Dispose ();
			activityListener.Dispose ();
			string name = UniqueName ("telemetry timer without listeners");
			var timer = InstrumentationService.CreateTimerCounter (name);

			using (var tracker = timer.BeginTiming ())
				System.Threading.Thread.Sleep (5);

			Assert.That (measurements.Any (m => m.Counter == name), Is.False);
			Assert.That (activities.Any (a => a.OperationName == name), Is.False);
		}
	}
}
