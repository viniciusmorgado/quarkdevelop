// Lints the developer scripts and the workflows: every script builds (warnings are errors, scripts/Directory.Build.props)
// and follows the code style of .editorconfig, and actionlint checks .github/workflows when it is installed
// (dotnet scripts/setup.cs installs it).
// Usage: dotnet scripts/lint.cs

using System.Diagnostics;

var root = RepoRoot ();
var scripts = Directory.GetFiles (Path.Combine (root, "scripts"), "*.cs")
	.Select (path => Path.GetRelativePath (root, path))
	.Order (StringComparer.Ordinal)
	.ToList ();

Log ($"dotnet build {scripts.Count} scripts");
var failed = 0;
foreach (var script in scripts) {
	// file-based programs take -v but not every MSBuild switch (-nologo, -clp: make dotnet read the file as a project)
	var (exitCode, output) = Capture ("dotnet", ["build", script, "-v", "q"], root);
	if (exitCode != 0) {
		Console.Error.WriteLine (output);
		Console.Error.WriteLine ($"{script}: build failed");
		failed++;
	}
}
if (failed > 0)
	return 1;

Log ("dotnet format whitespace --verify-no-changes (scripts)");
var status = Run ("dotnet", ["format", "whitespace", ".", "--folder", "--verify-no-changes", "--include", .. scripts], root);
if (status != 0)
	return status;

var actionlint = FindTool ("actionlint");
if (actionlint != null && Directory.Exists (Path.Combine (root, ".github", "workflows"))) {
	Log ("actionlint");
	status = Run (actionlint, [], root);
	if (status != 0)
		return status;
} else if (actionlint == null) {
	Log ("actionlint is not installed: the workflows are not checked (dotnet scripts/setup.cs installs it)");
}
Log ("lint OK");
return 0;

// The repository root, above scripts/. dotnet gives a file-based app the directory of its file; [CallerFilePath] would be
// /_/... when the script is compiled with ContinuousIntegrationBuild=true (package-flatpak.cs sets it for build.cs).
static string RepoRoot () => Path.GetFullPath (Path.Combine (AppContext.GetData ("EntryPointFileDirectoryPath") as string
	?? throw new InvalidOperationException ("run the script as a file-based app: dotnet scripts/<name>.cs"), ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

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

// Standard output and error together, in the order they come.
static (int ExitCode, string Output) Capture (string fileName, IEnumerable<string> arguments, string workingDirectory)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workingDirectory };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	var output = new System.Text.StringBuilder ();
	using var process = new Process { StartInfo = info };
	process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine (e.Data); };
	process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine (e.Data); };
	process.Start ();
	process.BeginOutputReadLine ();
	process.BeginErrorReadLine ();
	process.WaitForExit ();
	return (process.ExitCode, output.ToString ());
}

// A program of PATH, or one that dotnet scripts/setup.cs installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
}
