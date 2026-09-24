//
// GitCredentials.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using MonoDevelop.Core;
using MonoDevelop.Ide;
using LibGit2Sharp;
using System.IO;
using System.Collections.Generic;
using MonoDevelop.Components;
using System.Linq;
using Mono.Addins;
using System.Threading.Tasks;

namespace MonoDevelop.VersionControl.Git
{
	public enum GitCredentialsType
	{
		Normal,
		Tfs,
	}

	public class GitCredentialsState
	{
		public string UrlUsed { get; set; }
		public bool AgentUsed { get; set; }
		public int KeyUsed { get; set; }
		public bool NativePasswordUsed { get; set; }
		public Dictionary<string, int> KeyForUrl = new Dictionary<string, int> ();
		public Dictionary<string, bool> AgentForUrl = new Dictionary<string, bool> ();

		public GitCredentialsState ()
		{
			KeyUsed = -1;
		}
	}

	static class GitCredentials
	{
		internal static readonly string UserCancelledExceptionMessage = GettextCatalog.GetString("Operation cancelled");

		static Dictionary<GitCredentialsType, GitCredentialsState> credState = new Dictionary<GitCredentialsType, GitCredentialsState> ();

		public static Credentials TryGet (string url, string userFromUrl, SupportedCredentialTypes types, GitCredentialsType type)
		{
			bool result = false;
			Uri uri = null;
			GitCredentialsState state;
			if (!credState.TryGetValue (type, out state))
				credState [type] = state = new GitCredentialsState ();
			state.UrlUsed = url;
			Credentials cred = null;

			// nuget.org LibGit2Sharp has no SSH credential types (SshUserKeyCredentials, SshAgentCredentials): libgit2 runs
			// the system OpenSSH client for SSH remotes, which authenticates with ssh-agent and ~/.ssh on its own.

			// We always need to run the TryGet* methods as we need the passphraseItem/passwordItem populated even
			// if the password store contains an invalid password/no password
			if ((types & SupportedCredentialTypes.UsernamePassword) != 0) {
				if (Uri.TryCreate (url, UriKind.RelativeOrAbsolute, out uri)) {
					if (!state.NativePasswordUsed && TryGetUsernamePassword (uri, out var username, out var password)) {
						state.NativePasswordUsed = true;
						return new UsernamePasswordCredentials {
							Username = username,
							Password = password
						};
					}
				}
			}
			if (cred == null)
				cred = new UsernamePasswordCredentials ();

			var gitCredentialsProviders = AddinManager.GetExtensionObjects<IGitCredentialsProvider> ();

			if (gitCredentialsProviders != null) {
				foreach (var gitCredentialsProvider in gitCredentialsProviders) {
					if (gitCredentialsProvider.SupportsUrl (url)) {
						var providerResult = GetCredentialsFromProvider (gitCredentialsProvider, url, types, cred);
						if (providerResult == GitCredentialsProviderResult.Cancelled)
							throw new UserCancelledException (UserCancelledExceptionMessage);
						if (result = providerResult == GitCredentialsProviderResult.Found)
							break;
					}
				}
			}

			if (!result) {
				result = GetCredentials (url, types, cred);
			}

			if (result) {
				if ((types & SupportedCredentialTypes.UsernamePassword) != 0) {
					var upcred = (UsernamePasswordCredentials)cred;
					if (!string.IsNullOrEmpty (upcred.Password) && uri != null) {
						PasswordService.AddWebUserNameAndPassword (uri, upcred.Username, upcred.Password);
					}
				}
				return cred;
			}

			throw new UserCancelledException (UserCancelledExceptionMessage);
		}

		static GitCredentialsProviderResult GetCredentialsFromProvider (IGitCredentialsProvider gitCredentialsProvider, string uri, SupportedCredentialTypes type, Credentials cred)
		{
			if (type != SupportedCredentialTypes.UsernamePassword)
				return GitCredentialsProviderResult.NotFound;

			var (result, credentials) = Task.Run (() => gitCredentialsProvider.TryGetCredentialsAsync (uri)).Result;
		
			if (result == GitCredentialsProviderResult.Found) {
				((UsernamePasswordCredentials)cred).Username = credentials.Username;
				((UsernamePasswordCredentials)cred).Password = credentials.Password;
			}

			return result;
		}

		static bool GetCredentials (string uri, SupportedCredentialTypes type, Credentials cred)
		{
			return XwtCredentialsDialog.Run (uri, type, cred).Result;
		}

		static bool TryGetPassphrase (Uri uri, out string passphrase)
		{
			var passphraseValue = PasswordService.GetWebPassword (uri);
			if (passphraseValue != null) {
				passphrase = passphraseValue;
				return true;
			}

			passphrase = null;
			return false;
		}

		static bool TryGetUsernamePassword (Uri uri, out string username, out string password)
		{
			var cred = PasswordService.GetWebUserNameAndPassword (uri);
			// if the Uri has a path, fallback to base Uri if available
			if (cred == null && !string.IsNullOrEmpty (uri.PathAndQuery) && Uri.TryCreate (uri.GetLeftPart (UriPartial.Authority), UriKind.Absolute, out var baseUri)) {
				cred = PasswordService.GetWebUserNameAndPassword (baseUri);
			}
			if (cred != null) {
				username = cred.Item1;
				password = cred.Item2;
				return true;
			}

			username = null;
			password = null;
			return false;
		}

		internal static void StoreCredentials (GitCredentialsType type)
		{
			GitCredentialsState state;
			if (!credState.TryGetValue (type, out state))
				return;

			var url = state.UrlUsed;
			var key = state.KeyUsed;

			state.NativePasswordUsed = false;

			if (!string.IsNullOrEmpty (url) && key != -1)
				state.KeyForUrl [url] = key;

			Cleanup (state);
		}

		internal static void InvalidateCredentials (GitCredentialsType type)
		{
			GitCredentialsState state;
			if (!credState.TryGetValue (type, out state))
				return;

			if (!string.IsNullOrEmpty (state.UrlUsed) && state.AgentForUrl.ContainsKey (state.UrlUsed))
				state.AgentForUrl [state.UrlUsed] &= !state.AgentUsed;

			Cleanup (state);
		}

		static void Cleanup (GitCredentialsState state)
		{
			state.UrlUsed = null;
			state.AgentUsed = false;
			state.KeyUsed = -1;
		}
	}
}
