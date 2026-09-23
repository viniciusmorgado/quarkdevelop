//
// MonoDevelopErrorReportingService.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2018 Microsoft Inc.
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
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Telemetry;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.RoslynServices
{
	[ExportWorkspaceServiceFactory (typeof (IErrorReportingService), ServiceLayer.Host), Shared]
	sealed class MonoDevelopErrorReportingServiceFactory : IWorkspaceServiceFactory
	{
		public IWorkspaceService CreateService (HostWorkspaceServices workspaceServices)
		{
			// Roslyn 5.9 has no IInfoBarService; the info bar helper is no longer a workspace service.
			return new MonoDevelopErrorReportingService (MonoDevelopInfoBarService.Instance);
		}

		sealed class MonoDevelopErrorReportingService : IErrorReportingService
		{
			readonly MonoDevelopInfoBarService _infoBarService;

			public MonoDevelopErrorReportingService (MonoDevelopInfoBarService infoBarService)
			{
				_infoBarService = infoBarService;
			}

			public string HostDisplayName => BrandingService.ApplicationName;

			public void ShowErrorInfoInActiveView (string message, params InfoBarUI [] items) =>
				_infoBarService.ShowInfoBarInActiveView (message, items);

			public void ShowGlobalErrorInfo (string message, TelemetryFeatureName featureName, Exception exception, params InfoBarUI [] items)
			{
				if (exception != null)
					LoggingService.LogError (message, exception);
				_infoBarService.ShowInfoBarInGlobalView (message, items);
			}

			public void ShowFeatureNotAvailableErrorInfo (string message, TelemetryFeatureName featureName, Exception exception)
			{
				if (exception != null)
					LoggingService.LogError (message, exception);
				_infoBarService.ShowInfoBarInGlobalView (message);
			}

			// These are usually analyzers which would crash the process.
			public void ShowDetailedErrorInfo (Exception exception)
			{
				LoggingService.LogError("Roslyn reported an exception to the user", exception);
				
				var logFile = (string)typeof (LoggingService).InvokeMember ("logFile", BindingFlags.GetField | BindingFlags.Static | BindingFlags.NonPublic, null, null, null);

				// If the output is redirected, open the log file, otherwise do not do anything.
				if (logFile != null)
					IdeServices.DesktopService.OpenFile (logFile);
			}
		}
	}
}
