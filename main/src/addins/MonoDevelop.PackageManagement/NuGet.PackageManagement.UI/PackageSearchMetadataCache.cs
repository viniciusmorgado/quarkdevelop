// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.

using System.Collections.Generic;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
	class PackageSearchMetadataCache
	{
		// Cached Package Metadata
		public IReadOnlyList<IPackageSearchMetadata> Packages { get; set; }

		// Remember the IncludePrerelease setting corresponding to the Cached Metadata
		public bool IncludePrerelease { get; set; }
	}
}