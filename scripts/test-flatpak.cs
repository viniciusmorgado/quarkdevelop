// Install test of the Flatpak bundle in a clean flatpak installation (ADR 0022).
// Usage: dotnet scripts/test-flatpak.cs [-- bundle]   (default out/monodevelop.flatpak)
// Steps (timed, the first failure stops the run; results in out/flatpak-test/summary.txt): checksum, install (fresh
// user installation; the GNOME runtime comes from Flathub), desktop integration (exported desktop entry, icon, MIME),
// `monodevelop --version`, `mdtool build` of a copy of main/tests/linux-smoke/Hello and running the result, the IDE's
// --smoke-test on a copy of Smoke.sln under Xvfb (screenshot).
// Isolated from the desktop: its own D-Bus session (dbus-run-session), the installation
// ~/.cache/monodevelop/flatpak/test-user (MD_FLATPAK_STORE overrides the parent) and a home directory of its own for
// the app (test-home next to it, where ~/.var/app/<app id> lives), all wiped first. The host's flatpak installation and
// the data of an installed IDE are never used.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

// Linux only, like the IDE (Unix file modes)
[assembly: System.Runtime.Versioning.SupportedOSPlatform ("linux")]

const string appId = "io.github.viniciusmorgado.MonoDevelop";
var root = RepoRoot ();
var store = Env ("MD_FLATPAK_STORE")
	?? Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".cache", "monodevelop", "flatpak");
var userDirectory = Path.Combine (store, "test-user");
var home = Path.Combine (store, "test-home");
// The bundle as the caller named it (relative to the caller's directory, symbolic links resolved): the D-Bus session
// below runs this script again, in the repository root.
var bundle = RealPath (args.Length > 0 && args[0].Length > 0 ? args[0] : Path.Combine (root, "out", "monodevelop.flatpak"));

// `flatpak run` needs a session bus and a runtime directory. The test runs in a D-Bus session of its own, so that the
// activation environment it changes (FLATPAK_USER_DIR, for the services D-Bus activates: the Flatpak portal runs the
// image loaders of GNOME 50+ with glycin and `flatpak-spawn --sandbox`) never reaches the desktop session.
if (Environment.GetEnvironmentVariable ("MD_FLATPAK_TEST_SESSION") != "1") {
	var runtime = Directory.CreateTempSubdirectory ("md-flatpak-test.").FullName;
	File.SetUnixFileMode (runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
	try {
		return Run (["dbus-run-session", "--", "dotnet", Path.Combine (root, "scripts", "test-flatpak.cs"), "--", bundle],
			new Dictionary<string, string?> { ["MD_FLATPAK_TEST_SESSION"] = "1", ["XDG_RUNTIME_DIR"] = runtime });
	} finally {
		Directory.Delete (runtime, true);
	}
}

// flatpak also connects to the system bus: the session bus of the test stands in for it, as for the activated services.
var environment = new Dictionary<string, string?> {
	["FLATPAK_USER_DIR"] = userDirectory,
	["DBUS_SYSTEM_BUS_ADDRESS"] = Environment.GetEnvironmentVariable ("DBUS_SESSION_BUS_ADDRESS"),
	["HOME"] = home,
};
if (Run (["dbus-update-activation-environment", "DBUS_SYSTEM_BUS_ADDRESS", "FLATPAK_USER_DIR", "HOME"], environment) != 0)
	Die ("dbus-update-activation-environment failed");

if (!File.Exists (bundle))
	Die ($"{bundle} not found; run dotnet scripts/package-flatpak.cs");
if (FindTool ("flatpak") == null)
	Die ("flatpak not found; install it (dotnet scripts/setup.cs lists the packages)");

var output = Path.Combine (root, "out", "flatpak-test");
foreach (var directory in new[] { userDirectory, home, output }) {
	if (Directory.Exists (directory))
		Directory.Delete (directory, true);
	Directory.CreateDirectory (directory);
}
var summary = Path.Combine (output, "summary.txt");
var all = Stopwatch.StartNew ();

// The sandbox sees the home directory (--filesystem=home), not the repository: the test works on copies there, which
// are removed when the script ends, also after a failure.
var work = Path.Combine (home, "md-flatpak-test");
AppDomain.CurrentDomain.ProcessExit += (_, _) => {
	try {
		if (Directory.Exists (work))
			Directory.Delete (work, true);
	} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
		Console.Error.WriteLine ($"cannot remove {work}: {e.Message}");
	}
};
CopyDirectory (Path.Combine (root, "main", "tests", "linux-smoke"), work);

Step ("checksum", log => Sha256Check (bundle + ".sha256", log));
Step ("install", log => All (
	() => Flatpak (log, "install", "--user", "--noninteractive", "-y", bundle),
	() => Flatpak (log, "info", "--user", appId),
	() => Flatpak (log, "list", "--user", "--columns=application,version,branch,runtime,installation")));
Step ("desktop", log => {
	var exports = Path.Combine (userDirectory, "exports", "share");
	var desktop = Path.Combine (exports, "applications", appId + ".desktop");
	return All (
		() => RunToWriter (["desktop-file-validate", desktop], log, environment),
		() => Check (Grep (desktop, "^(Exec|Icon|MimeType)=", log)),
		() => Check (NotEmpty (Path.Combine (exports, "icons", "hicolor", "128x128", "apps", appId + ".png"))),
		() => Check (NotEmpty (Path.Combine (exports, "icons", "hicolor", "512x512", "apps", appId + ".png"))),
		() => Check (NotEmpty (Path.Combine (exports, "mime", "packages", appId + ".xml"))),
		() => RunToWriter (["appstreamcli", "validate", "--no-net", Path.Combine (exports, "metainfo", appId + ".metainfo.xml")], log, environment));
});
Step ("version", log => All (
	() => Tee (["flatpak", "run", appId, "--version"], Path.Combine (output, "version.txt"), log, environment),
	() => Check (Regex.IsMatch (File.ReadAllText (Path.Combine (output, "version.txt")), "^MonoDevelop ", RegexOptions.Multiline)),
	() => Flatpak (log, "run", "--command=dotnet", appId, "--list-sdks")));
Step ("mdtool-build", log => All (
	() => Flatpak (log, "run", "--command=mdtool", appId, "build", Path.Combine (work, "Hello", "Hello.csproj")),
	() => Tee (["flatpak", "run", "--command=dotnet", appId, Path.Combine (work, "Hello", "bin", "Debug", "net10.0", "Hello.dll")],
		Path.Combine (output, "hello.txt"), log, environment),
	() => Check (File.ReadAllText (Path.Combine (output, "hello.txt")).Contains ("Hello, MonoDevelop!"))));
Step ("ide-smoke", log => {
	// The IDE's --smoke-test (contracts/smoke-test.md): open the solution, build it, take a screenshot and exit 0 when it
	// builds with no errors. X11 through Xvfb (no Wayland display: fallback-x11 applies).
	var smoke = new Dictionary<string, string?> (environment) { ["MD_SMOKE_OUT"] = Path.Combine (work, "smoke"), ["WAYLAND_DISPLAY"] = null, ["DISPLAY"] = null };
	var status = RunToWriter (["xvfb-run", "-a", "-s", "-screen 0 1600x1000x24", "flatpak", "run", appId, "--smoke-test", "-no-redirect", Path.Combine (work, "Smoke.sln")], log, smoke);
	// the IDE's log and screenshot, also after a failure
	foreach (var file in new[] { "ide.log", "screenshot.png" }) {
		if (File.Exists (Path.Combine (work, "smoke", file)))
			File.Copy (Path.Combine (work, "smoke", file), Path.Combine (output, file), true);
	}
	return All (
		() => status,
		() => Check (File.Exists (Path.Combine (output, "ide.log")) && File.Exists (Path.Combine (output, "screenshot.png"))),
		// every image of the IDE loads (GNOME 50+ loads images in a sandboxed glycin process)
		() => Check (!File.ReadAllText (Path.Combine (output, "ide.log")).Contains ("Error loading icon")));
});

Report ($"{"total",-14}          {(int)all.Elapsed.TotalSeconds,4}s");
var info = Capture (["flatpak", "info", "--user", appId], environment).Split ('\n').Where (line => Regex.IsMatch (line, "Installed|Runtime|Version|Commit"));
string[] sizes = [
	$"bundle: {DiskUsage (bundle)} ({new FileInfo (bundle).Length} bytes)",
	.. info,
	$"runtime: {DiskUsage (Path.Combine (userDirectory, "runtime"))} in {Path.Combine (userDirectory, "runtime")}",
];
File.WriteAllLines (Path.Combine (output, "sizes.txt"), sizes);
foreach (var line in sizes)
	Console.WriteLine (line);
Log ("flatpak install test OK");
return 0;

void Step (string name, Func<StreamWriter, int> body)
{
	var watch = Stopwatch.StartNew ();
	Log ($"flatpak-test: {name}");
	var logFile = Path.Combine (output, name + ".log");
	int status;
	using (var log = new StreamWriter (logFile) { AutoFlush = true }) {
		try {
			status = body (log);
		} catch (Exception e) {
			// a missing program or file fails the step, as a failing command did in the shell script
			log.WriteLine ($"error: {e.GetType ().Name}: {e.Message}");
			status = 1;
		}
	}
	Report ($"{name,-14} exit {status,-3} {(int)watch.Elapsed.TotalSeconds,4}s");
	if (status != 0) {
		foreach (var line in File.ReadLines (logFile).TakeLast (40))
			Console.Error.WriteLine (line);
		Die ($"step '{name}' failed (log: out/flatpak-test/{name}.log)");
	}
}

void Report (string line)
{
	Console.WriteLine (line);
	File.AppendAllText (summary, line + "\n");
}

int Flatpak (StreamWriter log, params string[] arguments) => RunToWriter (["flatpak", .. arguments], log, environment);

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

// An environment variable, unset when empty (as ${NAME:-default} in a shell).
static string? Env (string name) => Environment.GetEnvironmentVariable (name) is { Length: > 0 } value ? value : null;

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

// The commands of a step in order, as `set -e` ran them: the exit status of the first that fails, or 0.
static int All (params Func<int>[] commands)
{
	foreach (var command in commands) {
		var status = command ();
		if (status != 0)
			return status;
	}
	return 0;
}

// A check as an exit status.
static int Check (bool condition) => condition ? 0 : 1;

// realpath: the absolute path, symbolic links resolved.
static string RealPath (string path)
{
	var full = Path.GetFullPath (path);
	try {
		return File.ResolveLinkTarget (full, returnFinalTarget: true)?.FullName ?? full;
	} catch (IOException) {
		return full;
	}
}

// sha256sum -c: each file that the checksum file lists (relative to its directory) has its checksum.
static int Sha256Check (string sums, StreamWriter log)
{
	var formatted = 0;
	var status = 0;
	foreach (var line in File.ReadLines (sums).Where (line => line.Length > 0)) {
		var match = Regex.Match (line, "^([0-9a-fA-F]{64}) [ *](.+)$");
		if (!match.Success) {
			log.WriteLine ($"{sums}: improperly formatted line ignored: {line}");
			continue;
		}
		formatted++;
		string actual;
		using (var stream = File.OpenRead (Path.Combine (Path.GetDirectoryName (sums)!, match.Groups[2].Value)))
			actual = Convert.ToHexStringLower (SHA256.HashData (stream));
		var ok = actual == match.Groups[1].Value.ToLowerInvariant ();
		log.WriteLine ($"{match.Groups[2].Value}: {(ok ? "OK" : "FAILED")}");
		if (!ok)
			status = 1;
	}
	if (formatted == 0) {
		log.WriteLine ($"{sums}: no properly formatted checksum lines found");
		status = 1;
	}
	return status;
}

// grep -E: the lines of the file that match go to the log; false when there are none.
static bool Grep (string file, string pattern, StreamWriter log)
{
	var lines = File.ReadLines (file).Where (line => Regex.IsMatch (line, pattern)).ToList ();
	foreach (var line in lines)
		log.WriteLine (line);
	return lines.Count > 0;
}

// test -s: the file exists and is not empty, following symbolic links (the exports of an installation are links).
static bool NotEmpty (string file)
{
	try {
		using var stream = File.OpenRead (file);
		return stream.Length > 0;
	} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
		return false;
	}
}

// du -sh: the disk space of a file or directory, as du prints it (hard links counted once).
static string DiskUsage (string path) => Capture (["du", "-sh", path], null).Split ('\t')[0];

// A program of PATH, or one that dotnet scripts/setup.cs installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
}

static void CopyDirectory (string source, string target)
{
	foreach (var directory in Directory.GetDirectories (source, "*", SearchOption.AllDirectories))
		Directory.CreateDirectory (Path.Combine (target, Path.GetRelativePath (source, directory)));
	Directory.CreateDirectory (target);
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

static int Run (IReadOnlyList<string> command, IDictionary<string, string?>? environment)
{
	using var process = Process.Start (Start (command, environment))!;
	process.WaitForExit ();
	return process.ExitCode;
}

// Standard output and error to a writer, in the order they come.
static int RunToWriter (IReadOnlyList<string> command, StreamWriter writer, IDictionary<string, string?>? environment)
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

// command | tee file, with pipefail: the standard output goes to the file and to the log, the errors to the log; the
// exit status is the command's.
static int Tee (IReadOnlyList<string> command, string file, StreamWriter log, IDictionary<string, string?> environment)
{
	var info = Start (command, environment);
	info.RedirectStandardOutput = info.RedirectStandardError = true;
	using var copy = new StreamWriter (file);
	using var process = new Process { StartInfo = info };
	process.OutputDataReceived += (_, e) => {
		if (e.Data != null) {
			lock (log) {
				log.WriteLine (e.Data);
				copy.WriteLine (e.Data);
			}
		}
	};
	process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine (e.Data); };
	process.Start ();
	process.BeginOutputReadLine ();
	process.BeginErrorReadLine ();
	process.WaitForExit ();
	return process.ExitCode;
}

// The standard output of a command (its errors go to the console).
static string Capture (IReadOnlyList<string> command, IDictionary<string, string?>? environment)
{
	var info = Start (command, environment);
	info.RedirectStandardOutput = true;
	using var process = Process.Start (info)!;
	var output = process.StandardOutput.ReadToEnd ();
	process.WaitForExit ();
	return output;
}
