// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.
//
// From: https://github.com/NuGet/NuGet.Client
// test/NuGet.Core.Tests/NuGet.Protocol.Tests/HttpSource/TestProxy.cs

using System;
using System.Net;

namespace MonoDevelop.PackageManagement.Tests
{
	internal sealed class TestProxy : IWebProxy
	{
		readonly Uri proxyAddress;

		public TestProxy (Uri proxyAddress)
		{
			this.proxyAddress = proxyAddress;
		}

		public ICredentials Credentials { get; set; }

		public Uri GetProxy (Uri destination) => proxyAddress;

		public bool IsBypassed (Uri host) => false;
	}
}