// Starts the IDE from the build output.
// Usage: dotnet scripts/run.cs [-- [--headless] [IDE arguments...]]
//   --headless  run under Xvfb (xvfb-run), also on a Wayland desktop, e.g. for the smoke tests; otherwise the IDE
//               opens on the desktop
// The tools that dotnet scripts/setup.cs installs in ~/.local/bin (netcoredbg) are on the IDE's PATH.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// Linux only, like the IDE (POSIX signals)
[assembly: System.Runtime.Versioning.SupportedOSPlatform ("linux")]

var root = RepoRoot ();
var ide = Path.Combine (root, "main", "build", "bin", "MonoDevelop.dll");
if (!File.Exists (ide))
	Die ($"IDE not built yet ({ide}); run dotnet scripts/build.cs");

var info = args.FirstOrDefault () == "--headless"
	? Start ("xvfb-run", ["-a", "-s", "-screen 0 1600x1000x24", "dotnet", ide, .. args.Skip (1)])
	: Start ("dotnet", [ide, .. args]);
if (args.FirstOrDefault () == "--headless") {
	// the X display of Xvfb: GDK tries Wayland first, and without WAYLAND_DISPLAY it still connects to wayland-0
	info.Environment.Remove ("WAYLAND_DISPLAY");
	info.Environment["GDK_BACKEND"] = "x11";
}
info.Environment["MONODEVELOP_LOCALE_PATH"] = Env ("MONODEVELOP_LOCALE_PATH") ?? Path.Combine (root, "main", "build", "locale");
var localBin = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin");
info.Environment["PATH"] = Environment.GetEnvironmentVariable ("PATH") + ":" + localBin;
using var process = Process.Start (info)!;
// As `exec` did in the shell script: the terminal sends Ctrl+C and Ctrl+\ to the whole process group, so they are left to
// the IDE; SIGTERM and SIGHUP sent to this script are passed on to it. The script ends when the IDE does.
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

// An environment variable, unset when empty (as ${NAME:-default} in a shell).
static string? Env (string name) => Environment.GetEnvironmentVariable (name) is { Length: > 0 } value ? value : null;

// A signal for this script, passed on to the process it started.
static void Forward (PosixSignalContext context, Process process)
{
	context.Cancel = true;
	kill (process.Id, context.Signal == PosixSignal.SIGTERM ? 15 : 1);
}

[DllImport ("libc", SetLastError = true)]
static extern int kill (int pid, int signal);

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

static ProcessStartInfo Start (string fileName, IEnumerable<string> arguments)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	return info;
}
