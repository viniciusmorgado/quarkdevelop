//
// AssertLoggingTraceListenerTests.cs
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
using System.Linq;
using System.Reflection;
using MonoDevelop.Core.Logging;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core
{
	[TestFixture]
	public class AssertLoggingTraceListenerTests
	{
		// .NET: reflection invokes methods through emitted stubs (InvokeStub_...), whose stack frames have no declaring
		// type; a failed assertion reached through one threw a NullReferenceException from Debug.Fail instead of being
		// logged (T107: MonoDevelop.Ide.Tests, editor tests invoked by NUnit).
		[Test]
		public void FailReachedThroughReflectionIsLogged ()
		{
			var listener = new AssertLoggingTraceListener ();
			var callFail = typeof (AssertLoggingTraceListenerTests).GetMethod (nameof (CallFail), BindingFlags.Static | BindingFlags.NonPublic);
			var logger = new CapturingLogger ();
			LoggingService.AddLogger (logger);
			try {
				// the first calls are interpreted; later ones go through an emitted invoke stub
				for (int i = 0; i < 5; i++)
					callFail.Invoke (null, new object[] { listener, i });
			} finally {
				LoggingService.RemoveLogger (logger.Name);
			}

			var errors = logger.LogMessages.Where (m => m.Level == LogLevel.Error).Select (m => m.Message).ToArray ();
			Assert.AreEqual (5, errors.Length);
			Assert.That (errors, Has.All.StartWith ("Failed assertion: from reflection"));
		}

		static void CallFail (AssertLoggingTraceListener listener, int call)
		{
			listener.Fail ("from reflection", null);
		}
	}
}
