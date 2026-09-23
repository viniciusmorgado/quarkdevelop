//
// MonoDevelopExtensionManager.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2019 Microsoft Inc.
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
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.RoslynServices
{
	[ExportWorkspaceServiceFactory (typeof (IExtensionManager), ServiceLayer.Host), Shared]
	class MonoDevelopExtensionManager : IWorkspaceServiceFactory
	{
		private readonly List<IExtensionErrorHandler> _errorHandlers;

		[ImportingConstructor]
		public MonoDevelopExtensionManager(
			[ImportMany]IEnumerable<IExtensionErrorHandler> errorHandlers)
		{
			_errorHandlers = errorHandlers.ToList();
		}

		public IWorkspaceService CreateService (HostWorkspaceServices workspaceServices)
		{
			return new ExtensionManager (_errorHandlers);
		}

		// Roslyn 5.9: EditorLayerExtensionManager (EditorFeatures) is gone; derive from the Workspaces-layer
		// AbstractExtensionManager and log through LoggingService instead of IErrorLoggerService.
		internal class ExtensionManager : AbstractExtensionManager
		{
			readonly List<IExtensionErrorHandler> errorHandlers;

			public ExtensionManager (List<IExtensionErrorHandler> errorHandlers)
			{
				this.errorHandlers = errorHandlers;
			}

			public override void HandleNonCancellationException (object provider, Exception exception)
			{
				var name = provider?.GetType ().Name ?? "Roslyn extension";
#if !DEBUG
				// Disable info bar for crashing analyzers in release builds.
				if (provider is CodeFixProvider || provider is FixAllProvider || provider is CodeRefactoringProvider) {
					LoggingService.LogInternalError (name, exception);
					return;
				}
#endif
				// HACK: Let Roslyn's CSharpSemanticQuickInfoProvider throw as many errors as it wants without becoming
				//       disabled. This is just a temporary workaround until we find the right fix for
				//       https://devdiv.visualstudio.com/DevDiv/_workitems/edit/960181 .
				//       Without this, as soon as the above bug is hit, most useful C# tooltips stop appearing until
				//       you close/reopen the solution.
				if (provider?.GetType().FullName == "Microsoft.CodeAnalysis.CSharp.QuickInfo.CSharpSemanticQuickInfoProvider") {
					LoggingService.LogInternalError (name, exception);
					return;
				}

				LoggingService.LogInternalError (name, exception);
				DisableProvider (provider);
				foreach (var handler in errorHandlers) {
					try {
						handler.HandleError (provider, exception);
					} catch (Exception e) {
						LoggingService.LogError ("Extension error handler failed", e);
					}
				}
			}
		}
	}
}
