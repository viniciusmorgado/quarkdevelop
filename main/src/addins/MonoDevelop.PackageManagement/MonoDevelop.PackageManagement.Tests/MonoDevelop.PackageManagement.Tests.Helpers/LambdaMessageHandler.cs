// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.
//
// From: https://github.com/NuGet/NuGet.Client
// test/NuGet.Core.Tests/NuGet.Protocol.Tests/HttpSource/LambdaMessageHandler.cs

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.PackageManagement.Tests
{
	internal sealed class LambdaMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _delegate;

		public LambdaMessageHandler (Func<HttpRequestMessage, HttpResponseMessage> @delegate)
		{
			ArgumentNullException.ThrowIfNull (@delegate);

			_delegate = @delegate;
		}

		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult (_delegate (request));
		}
	}
}