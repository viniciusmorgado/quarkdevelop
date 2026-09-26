// Restores the NuGet packages of the Linux solution. Idempotent.
// Usage: dotnet scripts/restore.cs [-- --locked]
//   --locked  locked mode (packages.lock.json); also when CI=true

using System.Diagnostics;
using System.Runtime.CompilerServices;

var root = RepoRoot ();
var arguments = new List<string> { "restore", Path.Combine (root, "main", "MonoDevelop.Linux.sln") };
if (args.FirstOrDefault () == "--locked" || Environment.GetEnvironmentVariable ("CI") == "true")
	arguments.Add ("--locked-mode");

Log ($"dotnet restore main/MonoDevelop.Linux.sln {string.Join (' ', arguments.Skip (2))}".TrimEnd ());
return Run ("dotnet", arguments);

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

static int Run (string fileName, IEnumerable<string> arguments)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	using var process = Process.Start (info)!;
	process.WaitForExit ();
	return process.ExitCode;
}
