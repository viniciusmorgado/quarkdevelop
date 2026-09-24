//
// MonoDevelopNuGetResourceProviderFactory.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2019 Microsoft Corporation
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
using System.Collections.Generic;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.LocalRepositories;

namespace MonoDevelop.PackageManagement
{
	class MonoDevelopNuGetResourceProviderFactory : Repository.ProviderFactory
	{
		static MonoDevelopNuGetResourceProviderFactory ()
		{
			// Some parts of NuGet create new SourceRepository instances which bypasses the custom
			// MonoDevelopHttpHandlerResourceV3Provider used when SourceRepository instances are created by the NuGet
			// addin itself. To prevent this the default provider factory is replaced with a custom provider factory.
			Repository.Provider = new MonoDevelopNuGetResourceProviderFactory ();
		}

		public static IEnumerable<Lazy<INuGetResourceProvider>> GetProviders ()
		{
			return Repository.Provider.GetCoreV3 ();
		}

		/// <summary>
		/// Includes a custom HttpHandlerResourceV3Provider which can use native HttpMessageHandlers defined by MonoDevelop.
		/// </summary>
		public override IEnumerable<Lazy<INuGetResourceProvider>> GetCoreV3 ()
		{
			// NuGet's own list (it grows with NuGet, e.g. the NuGetAudit vulnerability resource of NuGet 6.8+) instead
			// of a copy of the NuGet 5 list, with MonoDevelop's HTTP handler and plugin manager.
			foreach (Lazy<INuGetResourceProvider> provider in base.GetCoreV3 ()) {
				if (provider.Value is HttpHandlerResourceV3Provider) {
					yield return new Lazy<INuGetResourceProvider> (() => new MonoDevelopHttpHandlerResourceV3Provider ());
				} else if (provider.Value is PluginResourceProvider) {
					yield return new Lazy<INuGetResourceProvider> (() => new PluginResourceProvider (PackageManagementServices.PluginManager));
				} else {
					yield return provider;
				}
			}
		}
	}
}
