//
// AddinInfo.cs
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

using System.Reflection;
using Mono.Addins;

[assembly: Addin ("CSharpBinding.Core",
	Namespace = "MonoDevelop",
	Version = MonoDevelop.BuildInfo.Version,
	Category = "Language bindings")]

[assembly: AddinName ("C# Project Support")]
[assembly: AddinDescription ("C# project model and compiler settings, without the IDE")]
[assembly: AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]

[assembly: AssemblyTitle ("MonoDevelop.CSharpBinding.Core")]
[assembly: AssemblyVersion ("2.6.0")]
