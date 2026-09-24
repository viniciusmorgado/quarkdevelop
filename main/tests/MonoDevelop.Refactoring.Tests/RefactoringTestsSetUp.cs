//
// RefactoringTestsSetUp.cs
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

using NUnit.Framework;

[assembly: IdeUnitTests.GuiTestContext]

/// <summary>
/// Initializes the IDE test host (GTK, the runtime, Xwt; IdeUnitTests.GuiTestHost) once, on the test thread, before
/// any fixture of the assembly (global namespace). GuiUnit did this before NUnit 3 (ADR 0015).
/// </summary>
[SetUpFixture]
#pragma warning disable CA1050 // in the global namespace, NUnit applies the set-up fixture to the whole assembly
public class RefactoringTestsSetUp
#pragma warning restore CA1050
{
	[OneTimeSetUp]
	public void InitializeIdeTestHost ()
	{
		IdeUnitTests.GuiTestHost.EnsureInitialized ();
	}
}
