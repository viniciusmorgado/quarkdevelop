// Starts the IDE (or mdtool) under netcoredbg for command-line debugging.
// Usage: dotnet scripts/debug.cs [-- [ide|mdtool] [arguments...]]
// netcoredbg comes from PATH or from ~/.local/bin, where dotnet scripts/setup.cs installs it.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// Linux only, like the IDE (POSIX signals)
[assembly: System.Runtime.Versioning.SupportedOSPlatform ("linux")]

var root = RepoRoot ();
var target = args.Length > 0 && args[0].Length > 0 ? args[0] : "ide";
var dll = target switch {
	"ide" => Path.Combine (root, "main", "build", "bin", "MonoDevelop.dll"),
	"mdtool" => Path.Combine (root, "main", "build", "bin", "mdtool.dll"),
	_ => null,
};
if (dll == null)
	Die ($"unknown target '{target}' (ide|mdtool)");
if (!File.Exists (dll))
	Die ($"{dll} not built yet; run dotnet scripts/build.cs");

var netcoredbg = FindTool ("netcoredbg");
if (netcoredbg == null)
	Die ("netcoredbg is not installed; run dotnet scripts/setup.cs");

var arguments = args.Skip (1).ToArray ();
Log ($"netcoredbg --interpreter=cli -- dotnet {Path.GetRelativePath (root, dll)} {string.Join (' ', arguments)}".TrimEnd ());
var info = new ProcessStartInfo (netcoredbg) { UseShellExecute = false };
foreach (var argument in new[] { "--interpreter=cli", "--", "dotnet", dll }.Concat (arguments))
	info.ArgumentList.Add (argument);
using var process = Process.Start (info)!;
// As `exec` did in the shell script: the terminal sends Ctrl+C and Ctrl+\ to the whole process group, so they are left to
// the debugger; SIGTERM and SIGHUP sent to this script are passed on to it. The script ends when the debugger does.
PosixSignalRegistration[] signals = [
	PosixSignalRegistration.Create (PosixSignal.SIGINT, context => context.Cancel = true),
	PosixSignalRegistration.Create (PosixSignal.SIGQUIT, context => context.Cancel = true),
	PosixSignalRegistration.Create (PosixSignal.SIGTERM, context => Forward (context, process)),
	PosixSignalRegistration.Create (PosixSignal.SIGHUP, context => Forward (context, process)),
];
process.WaitForExit ();
GC.KeepAlive (signals);
return process.ExitCode;

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

// A program of PATH, or one that dotnet scripts/setup.cs installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
}

// A signal for this script, passed on to the process it started.
static void Forward (PosixSignalContext context, Process process)
{
	context.Cancel = true;
	kill (process.Id, context.Signal == PosixSignal.SIGTERM ? 15 : 1);
}

[DllImport ("libc", SetLastError = true)]
static extern int kill (int pid, int signal);
