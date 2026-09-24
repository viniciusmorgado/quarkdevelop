// 
// PackageSourcesOptionPanel.cs
// 
// Author:
//   Matt Ward <ward.matt@gmail.com>
// 
// Copyright (C) 2013 Matthew Ward
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using MonoDevelop.Components;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Dialogs;
using NuGet.Common;
using NuGet.Configuration;

namespace MonoDevelop.PackageManagement.Gui
{
	internal class PackageSourcesOptionsPanel : OptionsPanel
	{
		RegisteredPackageSourcesViewModel viewModel;
		PackageSourcesWidget packageSourcesWidget;
		bool loadError;

		public override Control CreatePanelWidget()
		{
			try {
				return CreatePackageSourcesWidget ();
			} catch (Exception ex) {
				LoggingService.LogError ("Unable to show package sources in NuGet.Config file.", ex);

				loadError = true;

				return new PackageSourcesLoadErrorWidget (
					GetLoadErrorMessage (ex),
					ex.Message,
					GetGlobalNuGetConfigFileName ());
			}
		}

		PackageSourcesWidget CreatePackageSourcesWidget ()
		{
			var settings = SettingsLoader.LoadDefaultSettings (throwError: true);
			var repositoryProvider = SourceRepositoryProviderFactory.CreateSourceRepositoryProvider (settings);
			viewModel = new RegisteredPackageSourcesViewModel (repositoryProvider);
			viewModel.Load ();
			
			packageSourcesWidget = new PackageSourcesWidget (viewModel);
			return packageSourcesWidget;
		}

		string GetLoadErrorMessage (Exception ex)
		{
			if (ex is CryptographicException)
				return GettextCatalog.GetString ("Unable to decrypt passwords stored in the NuGet.Config file.");

			return GettextCatalog.GetString ("Unable to read the NuGet.Config file.");
		}

		// The legacy ValidateChanges checked that Mono could encrypt passwords (ProtectedData and its
		// ~/.config/.mono/keypairs store). On .NET, ProtectedData is Windows only and NuGet stores package source
		// passwords in clear text on Linux (PackageSourceViewModel), so there is nothing to validate.

		public override void ApplyChanges()
		{
			if (loadError)
				return;

			try {
				if (packageSourcesWidget.HasPackageSourcesOrderChanged) {
					viewModel.Save (
						packageSourcesWidget.GetOrderedPackageSources ());
				} else {
					viewModel.Save ();
				}
				PackageManagementServices.Workspace.ReloadSettings ();
			} catch (Exception ex) {
				LoggingService.LogError ("Unable to save NuGet.config changes", ex);
				MessageService.ShowError (
					GettextCatalog.GetString ("Unable to save package source changes.{0}{0}{1}",
					Environment.NewLine,
					GetSaveNuGetConfigFileErrorMessage ()));
			}
		}

		/// <summary>
		/// Returns a non-Windows specific error message instead of the one NuGet returns.
		/// 
		/// NuGet returns a Windows specific error:
		/// 
		/// "DeleteSection" cannot be called on a NullSettings. This may be caused on account of 
		/// insufficient permissions to read or write to "%AppData%\NuGet\NuGet.config".
		/// </summary>
		string GetSaveNuGetConfigFileErrorMessage ()
		{
			string path = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
				"NuGet",
				"NuGet.config");
			return GettextCatalog.GetString ("Unable to read or write to \"{0}\".", path);
		}

		public override void Dispose ()
		{
			if (packageSourcesWidget != null) {
				packageSourcesWidget.Dispose ();
			}
			base.Dispose ();
		}

		string GetGlobalNuGetConfigFileName ()
		{
			try {
				FilePath fileName = GlobalNuGetConfigFilePath.GetFileName ();
				if (fileName.IsNotNull) {
					if (File.Exists (fileName))
						return fileName;
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to get global NuGet.Config filename.", ex);
			}
			return null;
		}
	}
}

