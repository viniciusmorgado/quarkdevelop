// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSES/Apache-2.0.txt in the repository root for license information.

namespace NuGet.PackageManagement.UI
{
	public interface INuGetUILogger
	{
		void Log(ProjectManagement.MessageLevel level, string message, params object[] args);
	}
}
