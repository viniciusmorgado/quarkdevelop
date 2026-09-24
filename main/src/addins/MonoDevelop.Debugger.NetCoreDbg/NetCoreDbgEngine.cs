//
// NetCoreDbgEngine.cs
//
// Based on DotNetCoreDebuggerEngine.cs of the DotDevelop netcoredbg add-in
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Debugging.Client;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.Debugger.NetCoreDbg
{
	/// <summary>
	/// Debugger engine for .NET programs (ADR 0016). It debugs the commands that run a managed assembly with the
	/// dotnet host (<c>dotnet program.dll args</c>): the execution command of .NET SDK projects
	/// (MonoDevelop.DotNetCore's DotNetCoreExecutionCommand is such a <see cref="ProcessExecutionCommand"/>), so the
	/// add-in does not depend on the DotNetCore add-in.
	/// </summary>
	public class NetCoreDbgEngine : DebuggerEngineBackend
	{
		public override bool CanDebugCommand (ExecutionCommand cmd)
		{
			return TryGetProgram (cmd, out _, out _);
		}

		/// <summary>netcoredbg is the only engine for CoreCLR programs on Linux (the Mono soft debugger is excluded, ADR 0017).</summary>
		public override bool IsDefaultDebugger (ExecutionCommand cmd)
		{
			return CanDebugCommand (cmd);
		}

		public override DebuggerStartInfo CreateDebuggerStartInfo (ExecutionCommand cmd)
		{
			if (!TryGetProgram (cmd, out var program, out var arguments))
				throw new NotSupportedException ("netcoredbg debugs 'dotnet <program>.dll' commands only.");

			var command = (ProcessExecutionCommand)cmd;
			var startInfo = new DebuggerStartInfo {
				Command = program,
				Arguments = arguments,
				WorkingDirectory = command.WorkingDirectory
			};
			if (command.EnvironmentVariables != null) {
				foreach (var variable in command.EnvironmentVariables)
					startInfo.EnvironmentVariables[variable.Key] = variable.Value;
			}
			return startInfo;
		}

		public override ProcessInfo[] GetAttachableProcesses ()
		{
			var processes = new List<ProcessInfo> ();
			foreach (var process in Process.GetProcesses ()) {
				using (process) {
					try {
						if (process.Id != Environment.ProcessId && IsDotNetProcess (process))
							processes.Add (new ProcessInfo (process.Id, GetDescription (process)));
					} catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException) {
						// The process exited or belongs to another user.
					}
				}
			}
			return processes.ToArray ();
		}

		public override DebuggerSession CreateSession ()
		{
			return new NetCoreDbgSession ();
		}

		/// <summary>
		/// Splits a command that runs a managed program into the program (a .dll path) and its arguments: either
		/// <c>dotnet program.dll args</c> (with any path to the dotnet host) or <c>program.dll args</c>.
		/// </summary>
		internal static bool TryGetProgram (ExecutionCommand cmd, out string program, out string arguments)
		{
			program = null;
			arguments = null;
			if (!(cmd is ProcessExecutionCommand command) || string.IsNullOrEmpty (command.Command))
				return false;

			if (IsManagedAssembly (command.Command)) {
				program = command.Command;
				arguments = command.Arguments ?? string.Empty;
				return true;
			}

			if (!string.Equals (Path.GetFileNameWithoutExtension (command.Command), "dotnet", StringComparison.Ordinal))
				return false;
			if (!ProcessArgumentBuilder.TryParse (command.Arguments ?? string.Empty, out var argv) || argv.Length == 0 || !IsManagedAssembly (argv[0]))
				return false;

			program = argv[0];
			var builder = new ProcessArgumentBuilder ();
			builder.AddQuoted (argv.Skip (1).ToArray ());
			arguments = builder.ToString ();
			return true;
		}

		static bool IsManagedAssembly (string path)
		{
			return path.EndsWith (".dll", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>A process that runs CoreCLR: the dotnet host or an application host (it maps libcoreclr.so).</summary>
		static bool IsDotNetProcess (Process process)
		{
			if (process.ProcessName == "dotnet")
				return true;
			if (!OperatingSystem.IsLinux ())
				return false;
			var maps = $"/proc/{process.Id}/maps";
			return File.Exists (maps) && File.ReadLines (maps).Any (line => line.EndsWith ("/libcoreclr.so", StringComparison.Ordinal));
		}

		static string GetDescription (Process process)
		{
			var cmdline = $"/proc/{process.Id}/cmdline";
			if (OperatingSystem.IsLinux () && File.Exists (cmdline)) {
				var text = File.ReadAllText (cmdline).Replace ('\0', ' ').Trim ();
				if (text.Length > 0)
					return text;
			}
			return process.ProcessName;
		}
	}
}
