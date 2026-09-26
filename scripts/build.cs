// Restores and builds the Linux solution. Idempotent.
// Usage: dotnet scripts/build.cs [-- [-c Debug|Release] [--check]]
//   --check  also verify the formatting of the C# files added by this fork (dotnet format whitespace)

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

var root = RepoRoot ();
var configuration = "Debug";
var check = false;
for (var i = 0; i < args.Length; i++) {
	switch (args[i]) {
	case "-c":
	case "--configuration":
		if (i + 1 == args.Length)
			Die ($"{args[i]} needs a configuration");
		configuration = args[++i];
		break;
	case "--check":
		check = true;
		break;
	default:
		Die ($"unknown argument: {args[i]}");
		break;
	}
}

var status = Run ("dotnet", [Path.Combine (root, "scripts", "restore.cs")]);
if (status != 0)
	return status;

Log ($"dotnet build ({configuration})");
status = Run ("dotnet", ["build", Path.Combine (root, "main", "MonoDevelop.Linux.sln"), "-c", configuration, "--no-restore", "-nologo"]);
if (status != 0)
	return status;

if (check) {
	var files = NewCSharpFiles (root);
	if (files.Count > 0) {
		Log ($"dotnet format whitespace --verify-no-changes ({files.Count} new files)");
		status = Run ("dotnet", ["format", "whitespace", ".", "--folder", "--verify-no-changes", "--include", .. files], root);
		if (status != 0)
			return status;
	}
}

Log ("build OK");
return 0;

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

static int Run (string fileName, IEnumerable<string> arguments, string? workingDirectory = null)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	if (workingDirectory != null)
		info.WorkingDirectory = workingDirectory;
	using var process = Process.Start (info)!;
	process.WaitForExit ();
	return process.ExitCode;
}

static (int ExitCode, string Output) Capture (string fileName, IEnumerable<string> arguments)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	using var process = Process.Start (info)!;
	var error = process.StandardError.ReadToEndAsync ();
	var output = process.StandardOutput.ReadToEnd ();
	error.Wait ();
	process.WaitForExit ();
	return (process.ExitCode, output);
}

// The C# files added by this fork: added since the upstream base commit, plus untracked files. Their formatting is
// enforced; legacy files are reformatted per project in dedicated commits (ADR 0018).
static List<string> NewCSharpFiles (string root)
{
	// The upstream mono/monodevelop commit this fork starts from, pinned so the check works in fresh clones and after
	// merges. A missing history (a shallow clone) fails loudly.
	const string baseCommit = "ba01d2d6d3c84e92a6b5f360dac76ee821547529";
	if (Capture ("git", ["-C", root, "cat-file", "-e", baseCommit + "^{commit}"]).ExitCode != 0)
		Die ($"upstream base commit {baseCommit} not found; fetch full history (git fetch --unshallow)");
	var added = Capture ("git", ["-C", root, "diff", "--diff-filter=A", "--name-only", baseCommit, "--", "*.cs"]);
	var untracked = Capture ("git", ["-C", root, "ls-files", "--others", "--exclude-standard", "--", "*.cs"]);
	if (added.ExitCode != 0 || untracked.ExitCode != 0)
		Die ("git could not list the new C# files");
	return (added.Output + untracked.Output)
		.Split ('\n', StringSplitOptions.RemoveEmptyEntries)
		.Where (file => !Regex.IsMatch (file, "^(main/external|main/vendor)/"))
		.Distinct ()
		.Order (StringComparer.Ordinal)
		.ToList ();
}
