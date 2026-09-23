//
// MonoDevelopInfoBarService.cs
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
using System.Linq;
using Microsoft.CodeAnalysis.ErrorReporting;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.Ide.RoslynServices
{
	// Roslyn 5.9 removed IInfoBarService (it lived in EditorFeatures); this is now a plain helper used by
	// MonoDevelopErrorReportingService, and UI work is marshalled with Runtime.RunInMainThread.
	sealed class MonoDevelopInfoBarService
	{
		public static MonoDevelopInfoBarService Instance { get; } = new MonoDevelopInfoBarService ();

		public void ShowInfoBarInActiveView (string message, params InfoBarUI [] items)
		{
			ShowInfoBar (activeView: true, message: message, items: items);
		}

		public void ShowInfoBarInGlobalView (string message, params InfoBarUI [] items)
		{
			ShowInfoBar (activeView: false, message: message, items: items);
		}

		void ShowInfoBar (bool activeView, string message, params InfoBarUI [] items)
		{
			// We can be called from any thread since errors can occur anywhere, however we can only construct and InfoBar from the UI thread.
			Runtime.RunInMainThread (() => {
				if (TryGetInfoBarHost (activeView, out var infoBarHost)) {
					var options = new InfoBarOptions (message) {
						Items = ToUIItems (items)
					};
					infoBarHost.AddInfoBar (options);
				}
			}).Ignore ();

			static InfoBarItem [] ToUIItems (InfoBarUI [] items)
				=> items?.Select (x => new InfoBarItem (x.Title, ToUIKind (x.Kind), x.Action, x.CloseAfterAction)).ToArray ();
		}

		static InfoBarItemKind ToUIKind (InfoBarUI.UIKind kind)
		{
			switch (kind) {
			case InfoBarUI.UIKind.Button:
				return InfoBarItemKind.Button;
			case InfoBarUI.UIKind.Close:
				return InfoBarItemKind.Close;
			case InfoBarUI.UIKind.HyperLink:
				return InfoBarItemKind.Hyperlink;
			default:
				LoggingService.LogError ("Unknown InfoBarUI.UIKind value {0}", kind.ToString ());
				return InfoBarItemKind.Button;
			}

		}

		bool TryGetInfoBarHost (bool activeView, out IInfoBarHost infoBarHost)
		{
			Runtime.AssertMainThread ();

			infoBarHost = null;
			if (!IdeApp.IsInitialized || IdeApp.Workbench == null)
				return false;

			if (activeView) {
				// Maybe for pads also? Not sure if we should.
				infoBarHost = IdeApp.Workbench.ActiveDocument?.GetContent<IInfoBarHost> (true);
			}

			if (infoBarHost == null)
				infoBarHost = IdeApp.Workbench.RootWindow as IInfoBarHost;

			return infoBarHost != null;
		}
	}
}
