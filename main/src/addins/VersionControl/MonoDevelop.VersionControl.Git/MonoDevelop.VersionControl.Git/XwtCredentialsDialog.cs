//
// XwtCredentialsDialog.cs
//
// Author:
//       Jose Medrano <josmed@nmicrosoft.com>
//
// Copyright (c) 2019 Microsoft Corp, Inc
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
using System.Threading.Tasks;
using LibGit2Sharp;
using MonoDevelop.Components;
using MonoDevelop.Core;
using Xwt;
using MonoDevelop.Ide;

namespace MonoDevelop.VersionControl.Git
{
	public class XwtCredentialsDialog : Xwt.Dialog
	{
		internal const int DefaultlLabelWidth = 100;
		internal const int InputContainerContainerSpacing = 10;
		readonly DialogButton okButton;

		readonly ICredentialsWidget credentialsWidget;

		const string credentialMarkupFormat = "<b>{0}</b>";

		public XwtCredentialsDialog (string uri, SupportedCredentialTypes supportedCredential, Credentials credentials)
		{
			Title = GettextCatalog.GetString ("Git Credentials");
			Resizable = false;

			Width = 500;

			var mainContainer = new VBox ();
			Content = mainContainer;

			//Credentials
			var credentialsLabel = new Label (GettextCatalog.GetString ("Credentials required for the repository:")) {
				Wrap = WrapMode.Word
			};
			mainContainer.PackStart (credentialsLabel);

			var credentialValue = new Label {
				Markup = string.Format (credentialMarkupFormat, uri),
				Wrap = WrapMode.Word
			};
			mainContainer.PackStart (credentialValue);

			// nuget.org LibGit2Sharp has no SSH key credentials: SSH remotes authenticate through the system OpenSSH
			// client (ssh-agent, ~/.ssh/config), so the SSH key/passphrase widget is gone.
			credentialsWidget = new UserPasswordCredentialsWidget (credentials as UsernamePasswordCredentials);

			credentialsWidget.CredentialsChanged += OnCredentialsChanged;
			mainContainer.PackStart (credentialsWidget.Widget, marginTop: InputContainerContainerSpacing);

			//Buttons
			Buttons.Add (new DialogButton (Command.Cancel));
			Buttons.Add (okButton = new DialogButton (Command.Ok));
			DefaultCommand = Command.Ok;

			okButton.Sensitive = credentialsWidget.CredentialsAreValid;
		}

		void OnCredentialsChanged (object sender, EventArgs e)
		{
			okButton.Sensitive = credentialsWidget.CredentialsAreValid;
		}

		public static Task<bool> Run (string url, SupportedCredentialTypes types, Credentials cred, Components.Window parentWindow = null)
		{
			return Runtime.RunInMainThread (() => {
				var engine = Platform.IsMac ? Toolkit.NativeEngine : Toolkit.CurrentEngine;
				var response = false;
				engine.Invoke (() => {
					using (var xwtDialog = new XwtCredentialsDialog (url, types, cred)) {
						response = xwtDialog.Run (parentWindow ?? IdeServices.DesktopService.GetFocusedTopLevelWindow ()) == Command.Ok;
					}
				});
				return response;
			});
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && !IsDisposed) {
				credentialsWidget.CredentialsChanged -= OnCredentialsChanged;
				credentialsWidget.Dispose ();
			}
			base.Dispose (disposing);
		}
	}

	interface ICredentialsWidget : IDisposable
	{
		Widget Widget { get; }
		Credentials Credentials { get; }
		bool CredentialsAreValid { get; }
		event EventHandler CredentialsChanged;
	}

	class UserPasswordCredentialsWidget : Table, ICredentialsWidget
	{
		readonly PasswordEntry passwordEntry;
		readonly TextEntry userTextEntry;

		public Widget Widget => this;

		public UsernamePasswordCredentials Credentials { get; private set; }

		Credentials ICredentialsWidget.Credentials => Credentials;

		public bool CredentialsAreValid {
			get {
				return true;
				// TODO: should we check the strings?
				//return !string.IsNullOrEmpty (Credentials?.Username) && !string.IsNullOrEmpty (Credentials?.Password);
			}
		}

		public event EventHandler CredentialsChanged;

		public UserPasswordCredentialsWidget (UsernamePasswordCredentials creds)
		{
			Credentials = creds ?? new UsernamePasswordCredentials ();

			DefaultRowSpacing = XwtCredentialsDialog.InputContainerContainerSpacing;

			int inputContainerCurrentRow = 0;
			//user container
			var userLabel = new Label (GettextCatalog.GetString ("Username:")) {
				MinWidth = XwtCredentialsDialog.DefaultlLabelWidth
			};
			Add (userLabel, 0, inputContainerCurrentRow, hexpand: false, vpos: WidgetPlacement.Center);
			userLabel.TextAlignment = Alignment.End;
			userTextEntry = new TextEntry { Text = Credentials.Username ?? string.Empty };
			Add (userTextEntry, 1, inputContainerCurrentRow, hexpand: true, vpos: WidgetPlacement.Center, marginRight: Toolkit.CurrentEngine.Type == ToolkitType.XamMac ? 10 : -1);

			userTextEntry.Changed += UserTextEntry_Changed;
			inputContainerCurrentRow++;

			//password container
			var passwordLabel = new Label () {
				TextAlignment = Alignment.End,
				Text = GettextCatalog.GetString ("Password:"),
				MinWidth = XwtCredentialsDialog.DefaultlLabelWidth
			};
			Add (passwordLabel, 0, inputContainerCurrentRow, hexpand: false, vpos: WidgetPlacement.Center);

			passwordEntry = new PasswordEntry () { Password = Credentials.Password ?? string.Empty, MarginTop = 5 };
			passwordEntry.Accessible.LabelWidget = passwordLabel;
			Add (passwordEntry, 1, inputContainerCurrentRow, hexpand: true, vpos: WidgetPlacement.Center, marginRight: Toolkit.CurrentEngine.Type == ToolkitType.XamMac ? 10 : -1);
			passwordEntry.Changed += PasswordEntry_Changed;
		}

		void UserTextEntry_Changed (object sender, EventArgs e) => OnCredentialsChanged ();
		void PasswordEntry_Changed (object sender, EventArgs e) => OnCredentialsChanged ();

		void OnCredentialsChanged ()
		{
			Credentials.Username = userTextEntry.Text;
			Credentials.Password = passwordEntry.Password;
			CredentialsChanged?.Invoke (this, EventArgs.Empty);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && !IsDisposed) {
				userTextEntry.KeyPressed -= UserTextEntry_Changed;
				passwordEntry.Changed -= PasswordEntry_Changed;
			}
			base.Dispose (disposing);
		}
	}
}
