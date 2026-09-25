//
// DotNetNewCli.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;

namespace MonoDevelop.Ide.Templates
{
	sealed class DotNetCliResult
	{
		public string CommandLine { get; set; }
		public int ExitCode { get; set; }
		public string Output { get; set; }
		public string Error { get; set; }
	}

	/// <summary>A <c>dotnet</c> command that failed; the details are what the CLI printed.</summary>
	sealed class DotNetCliException : UserException
	{
		public DotNetCliException (DotNetCliResult result)
			: base (GettextCatalog.GetString ("'{0}' failed with exit code {1}", result.CommandLine, result.ExitCode),
				string.Join (Environment.NewLine, new[] { result.Error, result.Output }.Where (s => !string.IsNullOrWhiteSpace (s))).Trim ())
		{
			Result = result;
		}

		public DotNetCliResult Result { get; }
	}

	/// <summary>
	/// Creates projects, items and solutions with the .NET CLI (T152, ADR 0026): <c>dotnet new</c> and <c>dotnet sln add</c>.
	/// </summary>
	static class DotNetNewCli
	{
		/// <summary>Variables of the IDE's own MSBuild set-up that must not leak into the CLI.</summary>
		static readonly string[] removedVariables = { "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBuildLoadMicrosoftTargetsReadOnly" };

		public static async Task<DotNetCliResult> RunAsync (string workingDirectory, IEnumerable<string> arguments, CancellationToken cancellationToken, bool englishOutput = false)
		{
			var psi = new ProcessStartInfo (DotNetCoreSdkInfo.GetDotNetHostPath ()) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			if (!string.IsNullOrEmpty (workingDirectory))
				psi.WorkingDirectory = workingDirectory;
			foreach (var argument in arguments)
				psi.ArgumentList.Add (argument);
			foreach (var name in removedVariables)
				psi.Environment.Remove (name);
			psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
			psi.Environment["DOTNET_NOLOGO"] = "1";
			psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
			if (englishOutput)
				psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";

			var result = new DotNetCliResult {
				CommandLine = "dotnet " + string.Join (" ", psi.ArgumentList.Select (Quote))
			};
			LoggingService.LogInfo ("Running {0}{1}", result.CommandLine, string.IsNullOrEmpty (workingDirectory) ? "" : " in " + workingDirectory);

			using (var process = new Process { StartInfo = psi }) {
				process.Start ();
				var output = process.StandardOutput.ReadToEndAsync ();
				var error = process.StandardError.ReadToEndAsync ();
				try {
					await process.WaitForExitAsync (cancellationToken).ConfigureAwait (false);
				} catch (OperationCanceledException) {
					try {
						process.Kill (true);
					} catch (InvalidOperationException) {
						// already exited
					}
					throw;
				}
				result.ExitCode = process.ExitCode;
				result.Output = await output.ConfigureAwait (false);
				result.Error = await error.ConfigureAwait (false);
			}
			if (result.ExitCode != 0)
				LoggingService.LogWarning ("{0} exited with {1}: {2}{3}", result.CommandLine, result.ExitCode, result.Error, result.Output);
			return result;
		}

		static string Quote (string argument)
		{
			return argument.Length == 0 || argument.Any (char.IsWhiteSpace) || argument.Contains ('"') ? "\"" + argument.Replace ("\"", "\\\"") + "\"" : argument;
		}

		/// <summary>
		/// The arguments of <c>dotnet new &lt;short name&gt; -o &lt;dir&gt; [-n &lt;name&gt;] [--language &lt;lang&gt;]</c>. The
		/// language is passed only when the template has several.
		/// </summary>
		public static List<string> GetNewArguments (DotNetNewTemplate template, string outputDirectory, string name, string language, IEnumerable<string> templateOptions = null)
		{
			var args = new List<string> { "new", template.ShortName, "-o", outputDirectory };
			if (!string.IsNullOrEmpty (name) && template.UsesName)
				args.AddRange (new[] { "-n", name });
			if (!string.IsNullOrEmpty (language) && template.Languages.Count > 1)
				args.AddRange (new[] { "--language", language });
			if (templateOptions != null)
				args.AddRange (templateOptions);
			return args;
		}

		/// <summary>Runs <c>dotnet new</c> for the template; throws <see cref="DotNetCliException"/> when the CLI fails.</summary>
		public static async Task<DotNetCliResult> CreateAsync (DotNetNewTemplate template, string outputDirectory, string name, string language,
			CancellationToken cancellationToken, IEnumerable<string> templateOptions = null)
		{
			Directory.CreateDirectory (outputDirectory);
			var result = await RunAsync (outputDirectory, GetNewArguments (template, outputDirectory, name, language, templateOptions), cancellationToken).ConfigureAwait (false);
			if (result.ExitCode != 0)
				throw new DotNetCliException (result);
			return result;
		}

		/// <summary>
		/// <c>dotnet new sln --format sln</c>: a Visual Studio solution file (SDK 10 writes .slnx by default, which the
		/// IDE's project model does not read).
		/// </summary>
		public static async Task<FilePath> CreateSolutionAsync (string directory, string name, CancellationToken cancellationToken)
		{
			Directory.CreateDirectory (directory);
			var args = new List<string> { "new", "sln", "-o", directory, "-n", name, "--format", "sln" };
			var result = await RunAsync (directory, args, cancellationToken).ConfigureAwait (false);
			if (result.ExitCode != 0)
				throw new DotNetCliException (result);
			return new FilePath (directory).Combine (name + ".sln");
		}

		/// <summary><c>dotnet sln &lt;solution&gt; add &lt;projects&gt;</c>.</summary>
		public static async Task AddToSolutionAsync (string solutionFile, IEnumerable<string> projectFiles, CancellationToken cancellationToken)
		{
			var projects = projectFiles.ToList ();
			if (projects.Count == 0)
				return;
			var args = new List<string> { "sln", solutionFile, "add" };
			args.AddRange (projects);
			var result = await RunAsync (Path.GetDirectoryName (solutionFile), args, cancellationToken).ConfigureAwait (false);
			if (result.ExitCode != 0)
				throw new DotNetCliException (result);
		}

		/// <summary>The files below <paramref name="directory"/>, except build output (bin, obj).</summary>
		public static HashSet<string> GetFiles (string directory)
		{
			var files = new HashSet<string> (FilePath.PathComparer);
			if (!Directory.Exists (directory))
				return files;
			var pending = new Stack<string> ();
			pending.Push (directory);
			while (pending.Count > 0) {
				var current = pending.Pop ();
				foreach (var file in Directory.EnumerateFiles (current))
					files.Add (file);
				foreach (var sub in Directory.EnumerateDirectories (current)) {
					var name = Path.GetFileName (sub);
					if (name != "bin" && name != "obj" && name != ".git")
						pending.Push (sub);
				}
			}
			return files;
		}
	}
}
