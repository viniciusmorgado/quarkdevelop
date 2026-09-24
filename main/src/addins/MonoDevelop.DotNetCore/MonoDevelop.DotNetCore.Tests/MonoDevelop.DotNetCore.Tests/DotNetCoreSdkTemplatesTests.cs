//
// DotNetCoreSdkTemplatesTests.cs
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

using System.Linq;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Templates;
using NUnit.Framework;

namespace MonoDevelop.DotNetCore.Tests
{
	/// <summary>
	/// The New Project dialog lists the .NET templates of the installed SDK (dotnet/templates/10.0.*), registered by the
	/// add-in's manifest for SDK 10 (T099).
	/// </summary>
	[TestFixture]
	// Before the fixtures that fake the installed SDKs: the template engine keeps the templates it scanned first.
	[Order (1)]
	public class DotNetCoreSdkTemplatesTests
	{
		[TestCase ("Microsoft.Common.Console.CSharp", "netcore/app/general")]
		[TestCase ("Microsoft.Common.Library.CSharp-netcoreapp", "netcore/library/general")]
		[TestCase ("Microsoft.Test.xUnit.CSharp", "netcore/test/general")]
		[TestCase ("NUnit3.DotNetNew.Template.CSharp", "netcore/test/general")]
		public async Task InstalledSdkTemplateIsListedAsync (string id, string category)
		{
			if (!DotNetCoreSdk.IsInstalled || !DotNetCoreSdk.Versions.Any (version => version.Major == 10)) {
				Assert.Ignore (".NET 10 SDK is not installed.");
			}

			// The template engine runs on the main thread.
			var template = await Runtime.RunInMainThread (() => IdeServices.TemplatingService.GetSolutionTemplate (id));

			Assert.IsNotNull (template, id);
			Assert.IsInstanceOf<MicrosoftTemplateEngineSolutionTemplate> (template);
			Assert.AreEqual (category, template.Category);
			Assert.AreEqual ("UseNetCore100=true", template.Condition);
		}
	}
}
