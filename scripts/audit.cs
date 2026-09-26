// Vulnerability gate: fails when a package of the Linux solution, transitive ones included, has a High or Critical
// advisory. `dotnet list package --vulnerable` itself always exits 0, so its report is parsed here.
// Usage: dotnet scripts/audit.cs

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

var root = RepoRoot ();
var outDirectory = Path.Combine (root, "out");
Directory.CreateDirectory (outDirectory);
var report = Path.Combine (outDirectory, "audit.txt");

Log ("dotnet list package --vulnerable --include-transitive");
var status = RunToFile ("dotnet", ["list", Path.Combine (root, "main", "MonoDevelop.Linux.sln"), "package", "--vulnerable", "--include-transitive"], report);
if (status != 0) {
	Console.Error.WriteLine (File.ReadAllText (report));
	return status;
}

var lines = File.ReadAllLines (report);
var high = lines.Where (line => Regex.IsMatch (line, @"\b(High|Critical)\b")).ToList ();
var moderate = lines.Count (line => Regex.IsMatch (line, @"\b(Moderate|Low)\b"));
Console.WriteLine ($"High/Critical findings: {high.Count}; Moderate/Low findings: {moderate} (report: {Path.GetRelativePath (root, report)})");

if (high.Count > 0) {
	foreach (var line in high)
		Console.Error.WriteLine (line);
	Die ("vulnerable packages with High/Critical severity");
}
Log ("audit OK");
return 0;

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

// Standard output and error to a file, in the order they come.
static int RunToFile (string fileName, IEnumerable<string> arguments, string file)
{
	var info = new ProcessStartInfo (fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in arguments)
		info.ArgumentList.Add (argument);
	using var writer = new StreamWriter (file);
	using var process = new Process { StartInfo = info };
	process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine (e.Data); };
	process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine (e.Data); };
	process.Start ();
	process.BeginOutputReadLine ();
	process.BeginErrorReadLine ();
	process.WaitForExit ();
	return process.ExitCode;
}
