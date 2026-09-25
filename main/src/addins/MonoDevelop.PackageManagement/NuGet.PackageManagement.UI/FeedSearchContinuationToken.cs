// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
	/// <summary>
	/// Most commonly used continuation token for plain package feeds.
	/// </summary>
	internal class FeedSearchContinuationToken : ContinuationToken
	{
		public int StartIndex { get; set; }
		public string SearchString { get; set; }
		public SearchFilter SearchFilter { get; set; }
	}
}