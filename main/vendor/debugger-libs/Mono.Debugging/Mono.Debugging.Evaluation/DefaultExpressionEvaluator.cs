//
// DefaultExpressionEvaluator.cs
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

using Mono.Debugging.Client;

namespace Mono.Debugging.Evaluation
{
	/// <summary>
	/// MonoDevelop local patch: default evaluator of <see cref="DebuggerSession"/>, replacing the
	/// NRefactory 5 based NRefactoryExpressionEvaluator (not vendored, ADR 0019). It resolves plain
	/// local, parameter and member names (the base implementation) and leaves expressions unresolved.
	/// Debuggers that evaluate expressions themselves (DAP adapters such as netcoredbg) do not use it.
	/// </summary>
	sealed class DefaultExpressionEvaluator : ExpressionEvaluator
	{
		public override string Resolve (DebuggerSession session, SourceLocation location, string expression)
		{
			return expression;
		}
	}
}
