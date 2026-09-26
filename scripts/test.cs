// Runs the tests of the Linux solution with coverage. Idempotent (the results are overwritten).
// Usage: dotnet scripts/test.cs [-- [--all] [--no-build] [--parallel] [extra dotnet test arguments...]]
//   (dotnet test runs in the repository root, for global.json: paths in the extra arguments are relative to it)
//   default     excludes the tests of the Quarantine category
//   --all       runs the quarantined tests too (informational; failures are expected there)
//   --parallel  two lanes at once (T145): Core, DotNetCore and PackageManagement in one, the GUI and other suites in
//               the other; one assembly at a time per lane, each lane with its own profile
// Outputs: out/tests/*.trx, out/coverage/Summary.txt (+ Cobertura XML). The GTK tests need a display: they run under Xvfb
// (xvfb-run) when it is installed, as in CI, also on a desktop; otherwise on the desktop's display. The coverage ratchet
// (minimum line coverage per assembly) runs in the ci workflow.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

var root = RepoRoot ();
var sln = Path.Combine (root, "main", "MonoDevelop.Linux.sln");
var outDirectory = Path.Combine (root, "out");

var filter = "Category!=Quarantine";
var parallel = false;
var buildArguments = new List<string> ();
var extra = new List<string> ();
foreach (var argument in args) {
	switch (argument) {
	case "--all":
		filter = "";
		break;
	case "--no-build":
		buildArguments.Add ("--no-build");
		break;
	case "--parallel":
		parallel = true;
		break;
	default:
		extra.Add (argument);
		break;
	}
}

// out/test-profile is the tests' MonoDevelop profile (the runsettings of main/msbuild/Linux/Test.targets point XDG_*
// there, per checkout): removed so that every run starts with a fresh add-in registry.
foreach (var directory in new[] { "tests", "coverage", "test-profile", "test-settings" }) {
	var path = Path.Combine (outDirectory, directory);
	if (Directory.Exists (path))
		Directory.Delete (path, true);
}
Directory.CreateDirectory (Path.Combine (outDirectory, "tests"));
Directory.CreateDirectory (Path.Combine (outDirectory, "coverage"));

// -m:1: one test assembly at a time. Run in parallel, the test hosts interfered with each other (mdtool children of
// Core.Tests intermittently could not load the C# project type, file watcher tests timed out); root cause still open
// (T135). Costs about 3.5 minutes (519 s instead of about 300 s).
var testArguments = new List<string> { "-m:1", "--logger", "trx", "--results-directory", Path.Combine (outDirectory, "tests"), "--collect", "XPlat Code Coverage" };
if (filter.Length > 0)
	testArguments.AddRange (["--filter", filter]);

// The debugger tests need netcoredbg, which dotnet scripts/setup.cs installs in ~/.local/bin: on PATH even when that
// directory is not.
Environment.SetEnvironmentVariable ("PATH", Environment.GetEnvironmentVariable ("PATH") + ":" + Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));

Log (filter.Length > 0 ? $"dotnet test --filter {filter}" : "dotnet test");
// The GTK tests (MonoDevelop.Ide.Gtk3.Tests...) need a display: Xvfb when it is installed, as in CI, so that the results
// do not depend on the desktop and no test window opens there. GDK tries Wayland first, and without WAYLAND_DISPLAY it
// still connects to wayland-0: GDK_BACKEND=x11 keeps the tests on the X display of Xvfb.
string[] runner = [];
if (FindTool ("xvfb-run") != null) {
	runner = ["xvfb-run", "-a", "-s", "-screen 0 1280x1024x24"];
	Environment.SetEnvironmentVariable ("WAYLAND_DISPLAY", null);
	Environment.SetEnvironmentVariable ("GDK_BACKEND", "x11");
} else if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY")) && string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY")))
	Log ("no display and no xvfb-run: the GTK tests cannot run (dotnet scripts/setup.cs lists the packages)");
var status = 0;
if (!parallel) {
	if (Run ([.. runner, "dotnet", "test", sln, .. testArguments, .. buildArguments, .. extra]) != 0)
		status = 1;
} else {
	// The test projects of the solution in two lanes of similar length (about 5 minutes each with coverage). Each
	// project runs with a copy of its runsettings whose MonoDevelop profile (XDG_*) belongs to its lane, so that the two
	// lanes never share a profile or an add-in registry cache.
	var projects = Capture ("dotnet", ["sln", sln, "list"]).Output.Split ('\n')
		.Select (line => line.Trim ().Replace ('\\', '/'))
		.Where (line => Regex.IsMatch (line, @"\.Tests\.csproj$"))
		.Order (StringComparer.Ordinal)
		.ToList ();
	string[] laneA = ["MonoDevelop.Core.Tests.csproj", "MonoDevelop.DotNetCore.Tests.csproj", "MonoDevelop.PackageManagement.Tests.csproj"];
	var lanes = new Dictionary<string, List<string>> {
		["a"] = projects.Where (project => laneA.Contains (Path.GetFileName (project))).ToList (),
		["b"] = projects.Where (project => !laneA.Contains (Path.GetFileName (project))).ToList (),
	};
	Log ($"lane a: {string.Join (' ', lanes["a"].Select (Path.GetFileName))}; lane b: {string.Join (' ', lanes["b"].Select (Path.GetFileName))}");
	var results = lanes.Select (lane => Task.Run (() => {
		var laneStatus = 0;
		var settingsDirectory = Path.Combine (outDirectory, "test-settings", lane.Key);
		Directory.CreateDirectory (settingsDirectory);
		using var laneLog = new StreamWriter (Path.Combine (outDirectory, "tests", $"lane-{lane.Key}.log"));
		try {
			foreach (var project in lane.Value) {
				var projectPath = Path.Combine (root, "main", project);
				var settings = Path.Combine (settingsDirectory, Path.GetFileNameWithoutExtension (project) + ".runsettings");
				var runsettings = Path.Combine (Path.GetDirectoryName (projectPath)!, "obj", "monodevelop.runsettings");
				File.WriteAllText (settings, File.ReadAllText (runsettings).Replace ("out/test-profile/", $"out/test-profile/{lane.Key}/"));
				if (RunToWriter ([.. runner, "dotnet", "test", projectPath, "--settings", settings, .. testArguments, .. buildArguments, .. extra], laneLog) != 0)
					laneStatus = 1;
			}
		} catch (Exception e) {
			// e.g. a project that was never built has no runsettings: this lane stops, as with `set -e` in the shell script,
			// and the other lane, the logs and the coverage report go on
			lock (laneLog)
				laneLog.WriteLine ($"error: {e.GetType ().Name}: {e.Message}");
			laneStatus = 1;
		}
		return laneStatus;
	})).ToArray ();
	Task.WaitAll (results);
	if (results.Any (result => result.Result != 0))
		status = 1;
	foreach (var lane in lanes.Keys)
		Console.Write (File.ReadAllText (Path.Combine (outDirectory, "tests", $"lane-{lane}.log")));
}

var reports = Directory.GetFiles (Path.Combine (outDirectory, "tests"), "coverage.cobertura.xml", SearchOption.AllDirectories);
if (reports.Length > 0) {
	Log ("coverage report");
	var coverage = Path.Combine (outDirectory, "coverage");
	// its output is not shown (only its errors); a failure stops the script with its exit status
	var report = Capture ("dotnet", ["tool", "run", "reportgenerator", "--", "-reports:" + string.Join (';', reports), "-targetdir:" + coverage, "-reporttypes:TextSummary;Cobertura"]);
	if (report.ExitCode != 0)
		return report.ExitCode;
	Console.Write (File.ReadAllText (Path.Combine (coverage, "Summary.txt")));
}
return status;

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

static ProcessStartInfo Start (IReadOnlyList<string> command)
{
	var info = new ProcessStartInfo (command[0]) { UseShellExecute = false, WorkingDirectory = RepoRoot () };
	foreach (var argument in command.Skip (1))
		info.ArgumentList.Add (argument);
	return info;
}

static int Run (IReadOnlyList<string> command)
{
	using var process = Process.Start (Start (command))!;
	process.WaitForExit ();
	return process.ExitCode;
}

// Standard output and error to a writer, in the order they come.
static int RunToWriter (IReadOnlyList<string> command, StreamWriter writer)
{
	var info = Start (command);
	info.RedirectStandardOutput = info.RedirectStandardError = true;
	using var process = new Process { StartInfo = info };
	process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine (e.Data); };
	process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine (e.Data); };
	process.Start ();
	process.BeginOutputReadLine ();
	process.BeginErrorReadLine ();
	process.WaitForExit ();
	return process.ExitCode;
}

// The standard output of a command and its exit status (its errors go to the console).
static (int ExitCode, string Output) Capture (string fileName, IEnumerable<string> arguments)
{
	var info = Start ([fileName, .. arguments]);
	info.RedirectStandardOutput = true;
	using var process = Process.Start (info)!;
	var output = process.StandardOutput.ReadToEnd ();
	process.WaitForExit ();
	return (process.ExitCode, output);
}

// A program of PATH, or one that dotnet scripts/setup.cs installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
}
