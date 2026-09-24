// 
// Main.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.Projects.MSBuild
{
	sealed class MainClass
	{
		public static void Main (string[] args)
		{
			// Disable the shared compiler server: the IDE owns the build process lifetime and orphaned
			// VBCSCompiler processes would outlive it.
			Environment.SetEnvironmentVariable ("UseSharedCompilation", bool.FalseString);

			RemoteProcessServer server = new RemoteProcessServer ();
			server.Connect (args, new MSBuildRegistration (server));
		}

		/// <summary>
		/// BuildEngine uses MSBuild types directly, so the SDK's MSBuild must be registered before the
		/// BuildEngine type is loaded. The IDE sends the MSBuild bin directory in InitializeRequest;
		/// this listener registers it with Microsoft.Build.Locator (ADR 0008), then replaces itself
		/// with the real BuildEngine.
		/// </summary>
		sealed class MSBuildRegistration
		{
			readonly RemoteProcessServer server;

			public MSBuildRegistration (RemoteProcessServer server)
			{
				this.server = server;
			}

			[MessageHandler]
			public BinaryMessage Initialize (InitializeRequest msg)
			{
				RegisterMSBuild (msg.BinDir);
				return CreateBuildEngineAndRespondToInitialize (msg);
			}

			static void RegisterMSBuild (string binDir)
			{
				if (MSBuildLocator.IsRegistered)
					return;
				var normalized = Path.GetFullPath (binDir).TrimEnd (Path.DirectorySeparatorChar);
				// Prefer the matching SDK instance: RegisterInstance also sets MSBUILD_EXE_PATH,
				// MSBuildExtensionsPath and MSBuildSDKsPath for SDK resolution.
				var instance = MSBuildLocator.QueryVisualStudioInstances ()
					.FirstOrDefault (i => string.Equals (Path.GetFullPath (i.MSBuildPath).TrimEnd (Path.DirectorySeparatorChar), normalized, StringComparison.Ordinal));
				if (instance != null)
					MSBuildLocator.RegisterInstance (instance);
				else
					MSBuildLocator.RegisterMSBuildPath (binDir);
			}

			// Keep in a separate method so MSBuild is registered before BuildEngine is loaded.
			[MethodImpl (MethodImplOptions.NoInlining)]
			BinaryMessage CreateBuildEngineAndRespondToInitialize (InitializeRequest msg)
			{
				var buildEngine = new BuildEngine (server);
				server.AddListener (buildEngine);
				server.RemoveListener (this);
				return buildEngine.Initialize (msg);
			}
		}
	}
}
