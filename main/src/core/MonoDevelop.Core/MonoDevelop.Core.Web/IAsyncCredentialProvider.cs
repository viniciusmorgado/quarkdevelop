// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.
//
// Based on ICredentialProvider
// From: https://github.com/NuGet/NuGet.Client

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.Core.Web
{
	interface IAsyncCredentialProvider
	{
		string Id { get; }

		Task<CredentialResponse> GetAsync(
			Uri uri,
			IWebProxy proxy,
			CredentialType type,
			bool isRetry,
			CancellationToken cancellationToken);
	}
}
