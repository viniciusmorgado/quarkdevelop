//
// NetCoreDbgSession.cs
//
// Based on DotNetCoreDebuggerSession.cs of the DotDevelop netcoredbg add-in
// (https://github.com/dotdevelop/monodevelop.netcoredbg, branch dotdevelop,
// commit 0a52b6c4c6de32a66d6549d3c6abca8152bf310d), by Lex Li.
//
// Copyright (c) 2019 LeXtudio Inc. (http://www.lextudio.com)
// Copyright (c) 2026 MonoDevelop contributors
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
using System.IO;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Mono.Debugging.Client;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Debugger.VsCodeDebugProtocol;
using Newtonsoft.Json.Linq;

namespace MonoDevelop.Debugger.NetCoreDbg
{
	/// <summary>
	/// A Debug Adapter Protocol session with <c>netcoredbg --interpreter=vscode</c> (ADR 0016). The launch and attach
	/// arguments are the ones of netcoredbg's "coreclr" configurations (the same as in a VS Code launch.json).
	/// </summary>
	public class NetCoreDbgSession : VSCodeDebuggerSession
	{
		readonly string adapterPath;

		public NetCoreDbgSession () : this (null)
		{
		}

		/// <param name="adapterPath">The netcoredbg executable; null to look it up (<see cref="NetCoreDbgLocator"/>).</param>
		public NetCoreDbgSession (string adapterPath)
		{
			this.adapterPath = adapterPath;
		}

		protected override string GetDebugAdapterPath ()
		{
			var path = adapterPath ?? NetCoreDbgLocator.FindDebugAdapter ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path)) {
				throw new InvalidOperationException (GettextCatalog.GetString (
					"The .NET debugger (netcoredbg) was not found. Install netcoredbg and add it to PATH, or set the '{0}' property to its path.",
					NetCoreDbgLocator.PathPropertyName));
			}
			return path;
		}

		protected override string GetDebugAdapterArguments ()
		{
			return "--interpreter=vscode";
		}

		protected override InitializeRequest CreateInitRequest ()
		{
			var request = new InitializeRequest ();
			request.Args.ClientID = "monodevelop";
			request.Args.ClientName = "MonoDevelop";
			request.Args.AdapterID = "coreclr";
			request.Args.LinesStartAt1 = true;
			request.Args.ColumnsStartAt1 = true;
			request.Args.PathFormat = InitializeArguments.PathFormatValue.Path;
			request.Args.SupportsVariableType = true;
			return request;
		}

		protected override LaunchRequest CreateLaunchRequest (DebuggerStartInfo startInfo)
		{
			var properties = CreateLaunchProperties (startInfo, Options?.ProjectAssembliesOnly ?? true);
			var request = new LaunchRequest ();
			request.Args.ConfigurationProperties = properties;
			return request;
		}

		protected override AttachRequest CreateAttachRequest (long processId)
		{
			var request = new AttachRequest ();
			request.Args.ConfigurationProperties = new Dictionary<string, JToken> {
				["processId"] = processId,
				["justMyCode"] = Options?.ProjectAssembliesOnly ?? true
			};
			return request;
		}

		/// <summary>The "launch" arguments of netcoredbg: program (a .dll), args, cwd, env, justMyCode.</summary>
		internal static Dictionary<string, JToken> CreateLaunchProperties (DebuggerStartInfo startInfo, bool justMyCode)
		{
			string[] args = Array.Empty<string> ();
			if (!string.IsNullOrWhiteSpace (startInfo.Arguments) && !ProcessArgumentBuilder.TryParse (startInfo.Arguments, out args))
				throw new ArgumentException ($"Cannot parse the program arguments: {startInfo.Arguments}");

			var environment = new JObject ();
			foreach (var variable in startInfo.EnvironmentVariables)
				environment[variable.Key] = variable.Value;

			var workingDirectory = startInfo.WorkingDirectory;
			if (string.IsNullOrEmpty (workingDirectory))
				workingDirectory = Path.GetDirectoryName (Path.GetFullPath (startInfo.Command));

			return new Dictionary<string, JToken> {
				["name"] = ".NET Launch",
				["type"] = "coreclr",
				["request"] = "launch",
				["program"] = startInfo.Command,
				["args"] = new JArray (args),
				["cwd"] = workingDirectory,
				["env"] = environment,
				["stopAtEntry"] = false,
				["justMyCode"] = justMyCode
			};
		}
	}
}
