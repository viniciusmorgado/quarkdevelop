// Verifies the toolchain of this machine, restores the local dotnet tools and installs the pinned developer tools.
// Idempotent.
// Usage: dotnet scripts/setup.cs
//
// Installs in ~/.cache/monodevelop/tools, linked from ~/.local/bin (versions and SHA-256 pinned below):
//   netcoredbg  the debug adapter of the IDE and of dotnet scripts/debug.cs (ADR 0016)
//   actionlint  checks .github/workflows (dotnet scripts/lint.cs)
// Lists the system packages that the build and the other scripts need and that are missing: gettext (the build compiles
// the translations with msgfmt), Xvfb (the headless tests and smoke tests), Weston (the Wayland smoke test), ImageMagick
// (the screenshot checks) and the Flatpak tools (the bundle and its test).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

var root = RepoRoot ();
var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

Log ("toolchain");
var sdk = Capture ("dotnet", ["--version"], root).Output.Trim ();
if (!sdk.StartsWith ("10.0.", StringComparison.Ordinal))
	Die ($"expected a .NET 10 SDK (global.json), got {sdk}");
Console.WriteLine ($"dotnet SDK {sdk}");
// The IDE loads the SDK's NuGet and is compiled against the NuGet of SDK 10.0.4xx (ADR 0020): an older feature band
// builds it, but the IDE's package management fails at run time.
if (!int.TryParse (sdk.Split ('.', '-')[2], out var patch) || patch < 400)
	Console.WriteLine ($"warning: SDK {sdk} is older than 10.0.400: the IDE needs its NuGet 7.9 or later (ADR 0020)");
var msbuild = Capture ("dotnet", ["msbuild", "-version", "-nologo"], root);
if (msbuild.ExitCode != 0)
	Die ("dotnet msbuild -version failed:\n" + msbuild.Output);
Console.WriteLine ("MSBuild " + msbuild.Output.Trim ().Split ('\n').Last ());
var gtk = FindTool ("pkg-config") is string pkgConfig ? Capture (pkgConfig, ["--modversion", "gtk+-3.0"], root) : (ExitCode: 1, Output: "");
Console.WriteLine (gtk.ExitCode == 0
	? "GTK " + gtk.Output.Trim ()
	: "GTK " + (Directory.GetDirectories ("/usr/lib").Any (directory => File.Exists (Path.Combine (directory, "libgtk-3.so.0"))) ? "present" : "missing"));
// The IDE and the build never run on Mono; a Mono installation for other work is harmless.
Console.WriteLine (FindTool ("mono") == null ? "Mono: absent" : "Mono: installed (not used by the build or the IDE)");

if (File.Exists (Path.Combine (root, "dotnet-tools.json")) || File.Exists (Path.Combine (root, ".config", "dotnet-tools.json"))) {
	Log ("restoring local dotnet tools");
	if (Run ("dotnet", ["tool", "restore"], root) != 0)
		Die ("dotnet tool restore failed");
}

Log (".NET Framework reference assemblies for legacy test fixtures");
// its progress (standard error) is shown as it comes, and the last line of its output: the newest framework
var refasm = CaptureOutput ("dotnet", [Path.Combine (root, "scripts", "netfx-refasm.cs")], root);
if (refasm.ExitCode != 0)
	Die ("dotnet scripts/netfx-refasm.cs failed");
Console.WriteLine (refasm.Output.Trim ().Split ('\n').Last ());

Log ("developer tools (~/.local/bin)");
var tools = Path.Combine (home, ".cache", "monodevelop", "tools");
var localBin = Path.Combine (home, ".local", "bin");
Directory.CreateDirectory (localBin);
// name, version, archive URL, its SHA-256, the executable inside the archive
(string Name, string Version, string Url, string Sha256, string Executable)[] pinned = [
	("netcoredbg", "3.2.0-1092", "https://github.com/Samsung/netcoredbg/releases/download/3.2.0-1092/netcoredbg-linux-amd64.tar.gz",
		"080eb3b2d2152465f599d3b33d1ee6e747794e11cc0a3773ec689f5e5f2c5afa", "netcoredbg/netcoredbg"),
	("actionlint", "1.7.12", "https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_linux_amd64.tar.gz",
		"8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8", "actionlint"),
];
if (RuntimeInformation.OSArchitecture != Architecture.X64) {
	Console.WriteLine ($"the pinned tools are linux-x64 builds; install netcoredbg and actionlint for {RuntimeInformation.OSArchitecture} yourself");
	pinned = [];
}
using (var http = new HttpClient ()) {
	foreach (var tool in pinned) {
		var directory = Path.Combine (tools, $"{tool.Name}-{tool.Version}");
		var executable = Path.Combine (directory, tool.Executable);
		if (!File.Exists (executable)) {
			Log ($"downloading {tool.Name} {tool.Version}");
			var archive = await http.GetByteArrayAsync (tool.Url);
			if (Convert.ToHexStringLower (SHA256.HashData (archive)) != tool.Sha256)
				Die ($"checksum mismatch for {tool.Url}");
			if (Directory.Exists (directory))
				Directory.Delete (directory, true);
			Directory.CreateDirectory (directory);
			using var gzip = new GZipStream (new MemoryStream (archive), CompressionMode.Decompress);
			await TarFile.ExtractToDirectoryAsync (gzip, directory, overwriteFiles: true);
		}
		var link = Path.Combine (localBin, tool.Name);
		var current = new FileInfo (link).LinkTarget;
		if (current != executable) {
			if (current != null && current.StartsWith (tools + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
				// a link of this script to another version
				File.Delete (link);
			} else if (current != null || File.Exists (link)) {
				// a program or link of the user's own: kept
				Console.WriteLine ($"{tool.Name}: keeping {link} (not installed by this script)");
				continue;
			}
			File.CreateSymbolicLink (link, executable);
		}
		Console.WriteLine ($"{tool.Name} {tool.Version}: {link}");
	}
}
if (!(Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':').Contains (localBin))
	Console.WriteLine ($"note: {localBin} is not on PATH; the scripts find the tools there, add it to PATH for the IDE");

Log ("system packages");
(string Tool, string Package, string UsedBy)[] system = [
	("msgfmt", "gettext", "the build (the translations of main/po)"),
	("xvfb-run", "xvfb xauth", "the headless tests and smoke tests (test.cs, ci.cs, run.cs --headless)"),
	("weston", "weston", "the Wayland smoke test (ci.cs)"),
	("magick|convert", "imagemagick", "the screenshot checks (ci.cs)"),
	("flatpak-builder", "flatpak-builder", "the Flatpak bundle (package-flatpak.cs)"),
	("flatpak", "flatpak", "the Flatpak bundle and its test"),
	("desktop-file-validate", "desktop-file-utils", "the Flatpak bundle and its test"),
	("appstreamcli", "appstream", "the Flatpak bundle and its test"),
	("dbus-run-session", "dbus", "the Flatpak test (test-flatpak.cs)"),
	("dbus-update-activation-environment", "dbus", "the Flatpak test (test-flatpak.cs)"),
];
var missing = system.Where (entry => entry.Tool.Split ('|').All (tool => FindTool (tool) == null)).ToList ();
foreach (var entry in missing)
	Console.WriteLine ($"missing: {entry.Tool} (package {entry.Package}), needed by {entry.UsedBy}");
if (missing.Count > 0)
	Console.WriteLine ("install them with your package manager, e.g. sudo apt install " + string.Join (' ', missing.Select (entry => entry.Package).Distinct ()));

Log ("setup OK");
return 0;

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

static int Run (string fileName, IEnumerable<string> arguments, string workingDirectory)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false, WorkingDirectory = workingDirectory };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
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

// The standard output of a command and its exit status; its errors go to the console as they come.
static (int ExitCode, string Output) CaptureOutput (string fileName, IEnumerable<string> arguments, string workingDirectory)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = workingDirectory };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	using var process = Process.Start (info)!;
	var output = process.StandardOutput.ReadToEnd ();
	process.WaitForExit ();
	return (process.ExitCode, output);
}

// A program of PATH, or one that this script installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
}
