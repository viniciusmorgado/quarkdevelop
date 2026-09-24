//
// ProxyCacheTests.cs
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
using System.Net;
using NUnit.Framework;

namespace MonoDevelop.Core.Web
{
	[TestFixture]
	public class ProxyCacheTests
	{
		/// <summary>
		/// On .NET the default proxy returns null for a uri that is not proxied (Mono returned the uri): the NuGet
		/// add-in's update check failed with a NullReferenceException for every solution with package references (T099).
		/// </summary>
		[Test]
		public void DefaultProxyWithoutAddressMeansNoProxy ()
		{
#pragma warning disable SYSLIB0014 // ProxyCache reads WebRequest.DefaultWebProxy (the system proxy settings).
			IWebProxy defaultProxy = WebRequest.DefaultWebProxy;
			try {
				WebRequest.DefaultWebProxy = new TestProxy (null);

				Assert.IsNull (new ProxyCache ().GetProxy (new Uri ("https://api.nuget.org/v3/index.json")));
			} finally {
				WebRequest.DefaultWebProxy = defaultProxy;
			}
#pragma warning restore SYSLIB0014
		}
	}
}
