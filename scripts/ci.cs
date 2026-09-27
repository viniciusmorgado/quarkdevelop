// The whole Linux CI gate in one script, run locally (T115); the ci workflow runs the same steps inline, except the
// smoke tests, which run only here.
// Steps (each timed; the first failure stops the run): setup, lint, build --check (Release), duplicate-assembly check,
// tests with coverage, vulnerability audit, mdtool smoke (build linux-smoke/Hello and linux-smoke/Modern and run them),
// GUI smoke (the IDE's --smoke-test under Xvfb and Wayland: Smoke.sln, the Broken project, the C# 8 to 14 Modern.sln and a
// debug session of Smoke.sln stopped at a breakpoint).
// Writes out/ci/summary.txt (step, status, seconds) and fails when the total exceeds the budget of 15 minutes
// (MD_CI_BUDGET_SECONDS overrides). Needs Xvfb, Weston and ImageMagick (dotnet scripts/setup.cs lists what is missing).
// Usage: dotnet scripts/ci.cs

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Linux only, like the IDE (Unix file modes)
[assembly: System.Runtime.Versioning.SupportedOSPlatform ("linux")]

var root = RepoRoot ();
var ci = Path.Combine (root, "out", "ci");
if (Directory.Exists (ci))
	Directory.Delete (ci, true);
Directory.CreateDirectory (ci);
var summary = Path.Combine (ci, "summary.txt");
var budget = int.TryParse (Environment.GetEnvironmentVariable ("MD_CI_BUDGET_SECONDS"), out var seconds) ? seconds : 900;
var all = Stopwatch.StartNew ();
var ide = Path.Combine (root, "main", "build", "bin", "MonoDevelop.dll");
var mdtool = Path.Combine (root, "main", "build", "bin", "mdtool.dll");
string[] xvfb = ["xvfb-run", "-a", "-s", "-screen 0 1600x1000x24"];

Step ("setup", log => Script (log, "setup.cs"));
Step ("lint", log => Script (log, "lint.cs"));
Step ("build", log => Script (log, "build.cs", "-c", "Release", "--check"));
Step ("assemblies", log => Script (log, "check-assemblies.cs"));
Step ("test", log => Script (log, "test.cs", "--no-build", "--parallel"));
Step ("audit", log => Script (log, "audit.cs"));
Step ("mdtool-smoke", MdtoolSmoke);
Step ("gui-smoke", GuiSmoke);
Step ("gui-smoke-debug", GuiSmokeDebug);
Step ("gui-smoke-errors", GuiSmokeErrors);
Step ("gui-smoke-modern", GuiSmokeModern);
Step ("wayland-smoke", WaylandSmoke);

var total = (int)all.Elapsed.TotalSeconds;
Report ($"{"total",-18}      {total,4}s (budget {budget}s)");
if (total > budget)
	Die ($"CI took {total}s, over the {budget}s budget");
return 0;

void Step (string name, Func<StreamWriter, bool> body)
{
	var watch = Stopwatch.StartNew ();
	Log ($"ci: {name}");
	var logFile = Path.Combine (ci, name + ".log");
	bool ok;
	using (var log = new StreamWriter (logFile) { AutoFlush = true }) {
		try {
			ok = body (log);
		} catch (Exception e) {
			// a missing program or file (xvfb-run, weston, the IDE's log) fails the step, as a failing command did in the
			// shell script
			log.WriteLine ($"error: {e.GetType ().Name}: {e.Message}");
			ok = false;
		}
	}
	if (ok) {
		Report ($"{name,-18} ok   {(int)watch.Elapsed.TotalSeconds,4}s");
		return;
	}
	Report ($"{name,-18} FAIL {(int)watch.Elapsed.TotalSeconds,4}s");
	// The failing tests (name, message, first stack lines) and the crashed test hosts, so that the console shows the cause.
	var failures = Failures (File.ReadAllLines (logFile)).Take (200).ToList ();
	File.WriteAllLines (Path.Combine (ci, "failures.txt"), failures);
	if (failures.Count > 0) {
		Console.Error.WriteLine ($"---- failures ({name}) ----");
		foreach (var line in failures)
			Console.Error.WriteLine (line);
	}
	foreach (var line in File.ReadLines (logFile).TakeLast (40))
		Console.Error.WriteLine (line);
	Die ($"ci step '{name}' failed (log: out/ci/{name}.log)");
}

void Report (string line)
{
	Console.WriteLine (line);
	File.AppendAllText (summary, line + "\n");
}

// Another developer script (dotnet scripts/<name>.cs), its output in the log of the step.
bool Script (StreamWriter log, string script, params string[] arguments) =>
	RunToWriter (["dotnet", Path.Combine (root, "scripts", script), "--", .. arguments], log) == 0;

// A copy of main/tests/linux-smoke, removed when the step ends.
string SmokeCopy ()
{
	var directory = Directory.CreateTempSubdirectory ("md-smoke.").FullName;
	CopyDirectory (Path.Combine (root, "main", "tests", "linux-smoke"), directory);
	return directory;
}

// The environment of an IDE started by a smoke test: its own profile, the tools of ~/.local/bin (netcoredbg), and the X
// display of Xvfb even on a Wayland desktop (GDK tries Wayland first, and without WAYLAND_DISPLAY it still connects to
// wayland-0; the Wayland smoke test sets its own display).
Dictionary<string, string?> SmokeEnvironment (string copy, string output) => new () {
	["WAYLAND_DISPLAY"] = null,
	["GDK_BACKEND"] = "x11",
	["XDG_CONFIG_HOME"] = Path.Combine (copy, ".profile", "config"),
	["XDG_DATA_HOME"] = Path.Combine (copy, ".profile", "data"),
	["XDG_CACHE_HOME"] = Path.Combine (copy, ".profile", "cache"),
	["MD_SMOKE_OUT"] = Path.Combine (ci, output),
	["PATH"] = Environment.GetEnvironmentVariable ("PATH") + ":" + Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"),
};

string IdeLog (string output) => File.ReadAllText (Path.Combine (ci, output, "ide.log"));

bool MdtoolSmoke (StreamWriter log)
{
	var copy = SmokeCopy ();
	try {
		var profile = Path.Combine (copy, ".profile");
		var environment = new Dictionary<string, string?> { ["MONODEVELOP_PROFILE"] = profile, ["MONO_ADDINS_REGISTRY"] = profile, ["XDG_CONFIG_HOME"] = profile };
		var hello = RunToWriter (["dotnet", mdtool, "build", Path.Combine (copy, "Hello", "Hello.csproj")], log, environment) == 0
			&& Prints (["dotnet", Path.Combine (copy, "Hello", "bin", "Debug", "net10.0", "Hello.dll")], "Hello, MonoDevelop!", log);
		// T138: the C# 8 to 14 sample builds with no warnings (checked even when Hello failed)
		var modernLog = Path.Combine (ci, "mdtool-modern.log");
		bool modern;
		using (var writer = new StreamWriter (modernLog))
			modern = RunToWriter (["dotnet", mdtool, "build", Path.Combine (copy, "Modern", "Modern.csproj")], writer, environment) == 0;
		modern = modern && File.ReadAllText (modernLog).Contains (" 0 Warning(s)")
			&& Prints (["dotnet", Path.Combine (copy, "Modern", "bin", "Debug", "net10.0", "Modern.dll")], "Modern C#: OK", log);
		return hello && modern;
	} finally {
		Directory.Delete (copy, true);
	}
}

bool GuiSmoke (StreamWriter log)
{
	// The IDE's --smoke-test (contracts/smoke-test.md, T103): start under Xvfb, open a copy of the smoke solution and build
	// it; exit 0 only when it builds with no errors and no unhandled exception was logged. T152: before the build, the New
	// Project and New File dialogs open with the dotnet new templates.
	var copy = SmokeCopy ();
	int status;
	try {
		var environment = SmokeEnvironment (copy, "gui-smoke");
		environment["MD_SMOKE_NEW_PROJECT"] = "1";
		environment["MD_SMOKE_NEW_FILE"] = "1";
		status = RunToWriter ([.. xvfb, "dotnet", ide, "--smoke-test", "-no-redirect", Path.Combine (copy, "Smoke.sln")], log, environment);
	} finally {
		Directory.Delete (copy, true);
	}
	var ideLog = IdeLog ("gui-smoke");
	return ScreenshotHasContent (Path.Combine (ci, "gui-smoke", "screenshot.png"))
		&& ideLog.Contains ("New Project dialog categories: Common, Web, Test, Solution; selected Console App (C#, F#)")
		// the main menus measure themselves (their size request used to overflow the stack on the first click)
		&& ideLog.Contains ("main menus measured: File ")
		&& ScreenshotHasContent (Path.Combine (ci, "gui-smoke", "new-project.png"))
		&& ScreenshotHasContent (Path.Combine (ci, "gui-smoke", "new-file.png"))
		&& status == 0;
}

bool GuiSmokeDebug (StreamWriter log)
{
	// T153: debug Hello (Smoke.sln) with netcoredbg to a breakpoint on the first line of Program.cs (MD_SMOKE_DEBUG, T112).
	// The Debug layout opens the Locals, Watch and Call Stack pads while the source editor is reparented; that crashed the
	// IDE or logged GLib-GObject criticals (toggle references on freed GdkWindows), which fail the smoke test.
	var copy = SmokeCopy ();
	int status;
	try {
		var environment = SmokeEnvironment (copy, "gui-smoke-debug");
		environment["MD_SMOKE_DEBUG"] = "1";
		status = RunToWriter ([.. xvfb, "dotnet", ide, "--smoke-test", "-no-redirect", Path.Combine (copy, "Smoke.sln")], log, environment);
	} finally {
		Directory.Delete (copy, true);
	}
	return IdeLog ("gui-smoke-debug").Contains ("the debugger stopped at the breakpoint") && status == 0;
}

bool GuiSmokeErrors (StreamWriter log)
{
	// T105: build the Broken project in the IDE; the smoke test activates the first row of the Errors pad and checks that
	// the editor opens Program.cs on the error line. Expected exit status: 1 (build errors).
	var copy = SmokeCopy ();
	int status;
	try {
		status = RunToWriter ([.. xvfb, "dotnet", ide, "--smoke-test", "-no-redirect", Path.Combine (copy, "Broken", "Broken.csproj")], log, SmokeEnvironment (copy, "gui-smoke-errors"));
	} finally {
		Directory.Delete (copy, true);
	}
	return IdeLog ("gui-smoke-errors").Contains ("error list navigation opened Program.cs at line 2") && status == 1;
}

bool GuiSmokeModern (StreamWriter log)
{
	// T138: build the C# 8 to 14 sample in the IDE with no errors or warnings, open Patterns.cs (MD_SMOKE_OPEN) and check
	// that the IDE's workspace parses it as C# 14 with no syntax errors; the screenshot shows its highlighting.
	// T146: the workspace also compiles the project with no errors (implicit usings: Console, ReadOnlySpan).
	// T147: with the documents of the source generators ([GeneratedRegex] and System.Text.Json in Generators.cs), and go
	// to definition of the [GeneratedRegex] method opens the generated file, read-only (the screenshot shows it).
	// Then Return and Tab typed in Typing.cs (MD_SMOKE_TYPING) put the caret at the indentation of the code, on its line.
	var copy = SmokeCopy ();
	int status;
	try {
		var environment = SmokeEnvironment (copy, "gui-smoke-modern");
		environment["MD_SMOKE_OPEN"] = "Modern/Patterns.cs";
		environment["MD_SMOKE_GOTO"] = "Word";
		environment["MD_SMOKE_TYPING"] = "Modern/Typing.cs";
		status = RunToWriter ([.. xvfb, "dotnet", ide, "--smoke-test", "-no-redirect", Path.Combine (copy, "Modern.sln")], log, environment);
	} finally {
		Directory.Delete (copy, true);
	}
	var ideLog = IdeLog ("gui-smoke-modern");
	return ideLog.Contains ("build finished with 0 errors, 0 warnings")
		&& ideLog.Contains ("Patterns.cs parses as C# 14.0 with 0 syntax errors")
		&& Regex.IsMatch (ideLog, @"Modern compiles in the workspace with 0 errors, .*\([1-9][0-9]* source-generated documents")
		&& Regex.IsMatch (ideLog, "go to definition of Word opened RegexGenerator.g.cs at line [0-9]+, read-only: True")
		&& ideLog.Contains ("Smoke test: typing in Typing.cs")
		&& status == 0;
}

bool WaylandSmoke (StreamWriter log)
{
	// The same smoke test on Wayland (T104): a headless Weston compositor (no input devices: GDK logs criticals for the
	// missing seat, which the smoke test tolerates), GDK_BACKEND=wayland, no X display.
	var copy = SmokeCopy ();
	var runtime = Directory.CreateTempSubdirectory ("md-wayland.").FullName;
	File.SetUnixFileMode (runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
	int status;
	var started = false;
	var westonInfo = new ProcessStartInfo ("weston") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in new[] { "--backend=headless", "--socket=wayland-md", "--width=1600", "--height=1000", "--idle-time=0" })
		westonInfo.ArgumentList.Add (argument);
	westonInfo.Environment["XDG_RUNTIME_DIR"] = runtime;
	using var westonLog = new StreamWriter (Path.Combine (ci, "weston.log"));
	using var weston = new Process { StartInfo = westonInfo };
	weston.OutputDataReceived += (_, e) => { if (e.Data != null) lock (westonLog) westonLog.WriteLine (e.Data); };
	weston.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (westonLog) westonLog.WriteLine (e.Data); };
	try {
		started = weston.Start ();
		weston.BeginOutputReadLine ();
		weston.BeginErrorReadLine ();
		for (var i = 0; i < 50 && !File.Exists (Path.Combine (runtime, "wayland-md")); i++)
			Thread.Sleep (100);
		var environment = SmokeEnvironment (copy, "wayland-smoke");
		environment["DISPLAY"] = null;
		environment["XDG_RUNTIME_DIR"] = runtime;
		environment["WAYLAND_DISPLAY"] = "wayland-md";
		environment["GDK_BACKEND"] = "wayland";
		status = RunToWriter (["dotnet", ide, "--smoke-test", "-no-redirect", Path.Combine (copy, "Smoke.sln")], log, environment);
	} finally {
		if (started)
			Stop (weston);
		Directory.Delete (copy, true);
		Directory.Delete (runtime, true);
	}
	var ideLog = IdeLog ("wayland-smoke");
	return ideLog.Contains ("GDK display wayland-md")
		&& ideLog.Contains ("main menus measured: File ")
		&& ScreenshotHasContent (Path.Combine (ci, "wayland-smoke", "screenshot.png"))
		&& status == 0;
}

// The repository root, above scripts/. dotnet gives a file-based app the directory of its file; [CallerFilePath] would be
// /_/... when the script is compiled with ContinuousIntegrationBuild=true (package-flatpak.cs sets it for build.cs).
static string RepoRoot () => Path.GetFullPath (Path.Combine (AppContext.GetData ("EntryPointFileDirectoryPath") as string
	?? throw new InvalidOperationException ("run the script as a file-based app: dotnet scripts/<name>.cs"), ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

// The lines of the failed tests and crashed test hosts, each with the 8 lines after it (grep -A8, "--" between groups).
static IEnumerable<string> Failures (string[] lines)
{
	var pattern = new Regex (@"^ +Failed [^!]|Test host process crashed|Test Run Aborted|The active test run was aborted");
	var last = -1;
	for (var i = 0; i < lines.Length; i++) {
		if (!pattern.IsMatch (lines[i]))
			continue;
		if (last >= 0 && i > last + 1)
			yield return "--";
		for (var j = Math.Max (i, last + 1); j <= Math.Min (i + 8, lines.Length - 1); j++)
			yield return lines[j];
		last = Math.Max (last, Math.Min (i + 8, lines.Length - 1));
	}
}

// A smoke screenshot must show something: a uniform image (e.g. a Wayland read-back that returned nothing, T109) fails.
static bool ScreenshotHasContent (string png)
{
	foreach (var tool in new[] { "magick", "convert" }) {
		try {
			var output = CaptureOutput ([tool, png, "-colorspace", "Gray", "-format", "%[fx:standard_deviation]", "info:"]).Output;
			return double.TryParse (output.Trim (), NumberStyles.Float, CultureInfo.InvariantCulture, out var deviation) && deviation > 0.02;
		} catch (System.ComponentModel.Win32Exception) {
			// not installed: try the other name
		}
	}
	return false;
}

static void CopyDirectory (string source, string target)
{
	foreach (var directory in Directory.GetDirectories (source, "*", SearchOption.AllDirectories))
		Directory.CreateDirectory (Path.Combine (target, Path.GetRelativePath (source, directory)));
	foreach (var file in Directory.GetFiles (source, "*", SearchOption.AllDirectories))
		File.Copy (file, Path.Combine (target, Path.GetRelativePath (source, file)), true);
}

static ProcessStartInfo Start (IReadOnlyList<string> command, IDictionary<string, string?>? environment)
{
	var info = new ProcessStartInfo (command[0]) { UseShellExecute = false, WorkingDirectory = RepoRoot () };
	foreach (var argument in command.Skip (1))
		info.ArgumentList.Add (argument);
	foreach (var (name, value) in environment ?? new Dictionary<string, string?> ()) {
		if (value == null)
			info.Environment.Remove (name);
		else
			info.Environment[name] = value;
	}
	return info;
}

// Standard output and error to a writer, in the order they come.
static int RunToWriter (IReadOnlyList<string> command, StreamWriter writer, IDictionary<string, string?>? environment = null)
{
	var info = Start (command, environment);
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

// The standard output of a command and its exit status; its errors go to the writer, when there is one.
static (int ExitCode, string Output) CaptureOutput (IReadOnlyList<string> command, StreamWriter? errors = null)
{
	var info = Start (command, null);
	info.RedirectStandardOutput = info.RedirectStandardError = true;
	using var process = Process.Start (info)!;
	var error = process.StandardError.ReadToEndAsync ();
	var output = process.StandardOutput.ReadToEnd ();
	errors?.Write (error.Result);
	process.WaitForExit ();
	return (process.ExitCode, output);
}

// A program that exits with 0 and prints the text (`program | grep -q text` with pipefail); its errors go to the log.
static bool Prints (IReadOnlyList<string> command, string text, StreamWriter log)
{
	var (exitCode, output) = CaptureOutput (command, log);
	return exitCode == 0 && output.Contains (text);
}

// SIGTERM, as `kill` in the shell script, and SIGKILL when the process has not stopped after 5 seconds.
static void Stop (Process process)
{
	if (!process.HasExited) {
		kill (process.Id, 15);
		if (!process.WaitForExit (5000))
			process.Kill ();
	}
	process.WaitForExit ();
}

[DllImport ("libc", SetLastError = true)]
static extern int kill (int pid, int signal);
