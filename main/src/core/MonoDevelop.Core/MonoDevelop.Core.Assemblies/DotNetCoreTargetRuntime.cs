//
// DotNetCoreTargetRuntime.cs
//
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.Core.Assemblies
{
	/// <summary>
	/// Target runtime for the .NET (CoreCLR) process the IDE runs on (ADR 0007). User projects are built
	/// with the MSBuild of an installed .NET SDK and executed with <c>dotnet exec</c>. There is no GAC and
	/// no .NET Framework installation on Linux.
	/// </summary>
	public class DotNetCoreTargetRuntime : TargetRuntime
	{
		readonly DotNetCoreSdkInfo sdk;
		readonly DotNetCoreExecutionHandler executionHandler;

		public DotNetCoreTargetRuntime (DotNetCoreSdkInfo sdk)
		{
			this.sdk = sdk;
			executionHandler = new DotNetCoreExecutionHandler ();
		}

		/// <summary>The SDK used to build user projects; null when no SDK is installed.</summary>
		public DotNetCoreSdkInfo Sdk => sdk;

		/// <summary>False when no .NET SDK was found; <see cref="CannotBuildReason"/> explains why.</summary>
		public bool CanBuild => sdk != null;

		public string CannotBuildReason => CanBuild ? null :
			GettextCatalog.GetString ("No .NET SDK was found. Install a .NET 10 SDK and make sure 'dotnet' is on PATH or DOTNET_ROOT is set.");

		public override string DisplayRuntimeName => ".NET";

		public override string RuntimeId => "DotNetCore";

		public override string Version => RuntimeInformation.FrameworkDescription.Replace (".NET ", string.Empty);

		public override bool IsRunning => DotNetCoreSdkInfo.IsRunningOnCoreClr;

		protected override void OnInitialize ()
		{
			// No GAC and no assembly folders to scan: frameworks come from NuGet reference packs.
		}

		public override IExecutionHandler GetExecutionHandler () => executionHandler;

		// Frameworks found in the reference folders (e.g. .NET Framework reference assemblies) need a backend;
		// the base class returns null.
		protected override TargetFrameworkBackend CreateBackend (TargetFramework fx) => new DotNetCoreFrameworkBackend ();

		[Obsolete ("Use DotNetProject.GetAssemblyDebugInfoFile()")]
		public override string GetAssemblyDebugInfoFile (string assemblyPath) => Path.ChangeExtension (assemblyPath, ".pdb");

		public override string GetMSBuildBinPath (string toolsVersion) => sdk?.MSBuildPath;

		public override string GetMSBuildToolsPath (string toolsVersion) => sdk?.MSBuildPath;

		public override string GetMSBuildExtensionsPath () => sdk?.MSBuildPath;

		internal protected override IEnumerable<string> GetGacDirectories () => Enumerable.Empty<string> ();

		public override IEnumerable<FilePath> GetReferenceFrameworkDirectories ()
		{
			// Old-style .NET Framework projects resolve framework references from reference assemblies
			// (<root>/.NETFramework/vX/RedistList/FrameworkList.xml); Linux has none unless they were
			// installed, e.g. by scripts/netfx-refasm.cs (task T134).
			var netfx = NetFrameworkReferenceAssembliesDirectory;
			if (!netfx.IsNull)
				yield return netfx;
			if (sdk == null)
				yield break;
			var packs = sdk.DotNetRoot.Combine ("packs");
			if (Directory.Exists (packs))
				yield return packs;
		}

		/// <summary>
		/// Root of the .NET Framework reference assemblies: $MD_NETFX_REFASM, else
		/// ~/.cache/monodevelop/netfx-refasm; null when it does not exist.
		/// </summary>
		public static FilePath NetFrameworkReferenceAssembliesDirectory {
			get {
				var dir = Environment.GetEnvironmentVariable ("MD_NETFX_REFASM");
				if (string.IsNullOrEmpty (dir))
					dir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".cache", "monodevelop", "netfx-refasm");
				return Directory.Exists (Path.Combine (dir, ".NETFramework")) ? new FilePath (dir) : FilePath.Null;
			}
		}
	}

	/// <summary>
	/// Framework backend of <see cref="DotNetCoreTargetRuntime"/>: the defaults of TargetFrameworkBackend (framework
	/// folders from the framework definition, tools on PATH).
	/// </summary>
	public class DotNetCoreFrameworkBackend : TargetFrameworkBackend<DotNetCoreTargetRuntime>
	{
	}

	/// <summary>
	/// Creates the <see cref="DotNetCoreTargetRuntime"/> when the IDE runs on CoreCLR. Registered in
	/// MonoDevelop.Core.addin.xml under /MonoDevelop/Core/Runtimes.
	/// </summary>
	public class DotNetCoreTargetRuntimeFactory : AddIns.ITargetRuntimeFactory
	{
		public IEnumerable<TargetRuntime> CreateRuntimes ()
		{
			if (!DotNetCoreSdkInfo.IsRunningOnCoreClr)
				yield break;
			var sdk = DotNetCoreSdkInfo.FindDefault ();
			if (sdk == null)
				LoggingService.LogWarning ("No .NET SDK found; building projects will not be possible.");
			yield return new DotNetCoreTargetRuntime (sdk);
		}
	}

	/// <summary>
	/// Executes <see cref="DotNetExecutionCommand"/>s with <c>dotnet exec</c> (or directly, when the command is
	/// a native executable such as an app host).
	/// </summary>
	public class DotNetCoreExecutionHandler : NativePlatformExecutionHandler
	{
		public override bool CanExecute (ExecutionCommand command) => command is DotNetExecutionCommand;

		public override ProcessAsyncOperation Execute (ExecutionCommand command, OperationConsole console)
		{
			var cmd = (DotNetExecutionCommand)command;
			var native = CreateNativeCommand (cmd);
			return base.Execute (native, console);
		}

		/// <summary>Builds the native command line used to run a .NET assembly.</summary>
		public static NativeExecutionCommand CreateNativeCommand (DotNetExecutionCommand cmd)
		{
			var program = cmd.Command;
			string file, arguments;
			if (program.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) || program.EndsWith (".exe", StringComparison.OrdinalIgnoreCase)) {
				file = DotNetCoreSdkInfo.GetDotNetHostPath ();
				arguments = "exec " + ProcessArgumentBuilder.Quote (program);
				if (!string.IsNullOrEmpty (cmd.Arguments))
					arguments += " " + cmd.Arguments;
			} else {
				file = program;
				arguments = cmd.Arguments;
			}
			var env = new Dictionary<string, string> (cmd.EnvironmentVariables);
			// Let tools started by the program (e.g. MSBuild tasks) find the same host.
			if (!env.ContainsKey ("DOTNET_HOST_PATH"))
				env["DOTNET_HOST_PATH"] = DotNetCoreSdkInfo.GetDotNetHostPath ();
			return new NativeExecutionCommand (file, arguments, cmd.WorkingDirectory, env);
		}
	}

	/// <summary>
	/// Information about an installed .NET SDK, discovered with <c>dotnet --list-sdks</c>.
	/// </summary>
	public class DotNetCoreSdkInfo
	{
		DotNetCoreSdkInfo (FilePath dotnetRoot, Version version, string versionString, FilePath msbuildPath)
		{
			DotNetRoot = dotnetRoot;
			Version = version;
			VersionString = versionString;
			MSBuildPath = msbuildPath;
		}

		/// <summary>Directory containing the <c>dotnet</c> host.</summary>
		public FilePath DotNetRoot { get; }

		public Version Version { get; }

		/// <summary>Full SDK version, e.g. 10.0.401.</summary>
		public string VersionString { get; }

		/// <summary>The SDK directory, which holds MSBuild.dll and the SDK targets.</summary>
		public FilePath MSBuildPath { get; }

		public static bool IsRunningOnCoreClr =>
			Type.GetType ("Mono.Runtime") == null &&
			!RuntimeInformation.FrameworkDescription.StartsWith (".NET Framework", StringComparison.Ordinal);

		/// <summary>
		/// Path of the <c>dotnet</c> host: DOTNET_HOST_PATH, the current process when it is the host,
		/// DOTNET_ROOT, or the first <c>dotnet</c> on PATH.
		/// </summary>
		public static string GetDotNetHostPath ()
		{
			var hostPath = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH");
			if (!string.IsNullOrEmpty (hostPath) && File.Exists (hostPath))
				return hostPath;
			var processPath = Environment.ProcessPath;
			if (!string.IsNullOrEmpty (processPath) && Path.GetFileNameWithoutExtension (processPath) == "dotnet")
				return processPath;
			var root = Environment.GetEnvironmentVariable ("DOTNET_ROOT");
			if (!string.IsNullOrEmpty (root) && File.Exists (Path.Combine (root, "dotnet")))
				return Path.Combine (root, "dotnet");
			foreach (var dir in (Environment.GetEnvironmentVariable ("PATH") ?? string.Empty).Split (Path.PathSeparator)) {
				if (dir.Length == 0)
					continue;
				var candidate = Path.Combine (dir, "dotnet");
				if (File.Exists (candidate))
					return candidate;
			}
			return "dotnet";
		}

		/// <summary>All SDKs reported by <c>dotnet --list-sdks</c>, newest first.</summary>
		public static IReadOnlyList<DotNetCoreSdkInfo> FindAll ()
		{
			var host = GetDotNetHostPath ();
			var list = new List<DotNetCoreSdkInfo> ();
			try {
				var psi = new ProcessStartInfo (host, "--list-sdks") {
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				};
				// The IDE's own global.json must not restrict which SDKs are listed.
				psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
				using (var p = Process.Start (psi)) {
					var output = p.StandardOutput.ReadToEnd ();
					p.WaitForExit (30000);
					list.AddRange (ParseListSdksOutput (output));
				}
			} catch (Exception ex) {
				LoggingService.LogWarning ("Could not run '" + host + " --list-sdks'", ex);
			}
			return list.OrderByDescending (s => s.Version).ToList ();
		}

		/// <summary>The newest installed SDK, or null if none is installed.</summary>
		public static DotNetCoreSdkInfo FindDefault ()
		{
			var all = FindAll ();
			return all.Count > 0 ? all[0] : null;
		}

		/// <summary>Parses lines such as <c>10.0.401 [/usr/share/dotnet/sdk]</c>.</summary>
		public static IEnumerable<DotNetCoreSdkInfo> ParseListSdksOutput (string output)
		{
			foreach (var rawLine in (output ?? string.Empty).Split ('\n')) {
				var line = rawLine.Trim ();
				int open = line.IndexOf (" [", StringComparison.Ordinal);
				if (open <= 0 || !line.EndsWith (']'))
					continue;
				var versionString = line.Substring (0, open);
				var sdksDir = line.Substring (open + 2, line.Length - open - 3);
				var numeric = versionString.Split ('-')[0];
				if (!System.Version.TryParse (numeric, out var version))
					continue;
				var sdkDir = Path.Combine (sdksDir, versionString);
				yield return new DotNetCoreSdkInfo (Path.GetDirectoryName (sdksDir), version, versionString, sdkDir);
			}
		}
	}
}
