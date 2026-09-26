// Duplicate-assembly check (risk R9, task T068). Fails when
//   - an assembly name exists both in main/build/bin and in an add-in directory (main/build/AddIns/**): the add-in
//     engine and the default load context would load two copies;
//   - an assembly under main/build/bin or main/build/AddIns has the name of an assembly of the .NET shared framework
//     (e.g. WindowsBase.dll): the framework copy wins and types are missing.
// Usage: dotnet scripts/check-assemblies.cs

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

var build = Path.Combine (RepoRoot (), "main", "build");
var bin = Path.Combine (build, "bin");
var addIns = Path.Combine (build, "AddIns");
if (!Directory.Exists (bin))
	Die ($"nothing built: {bin}");

// The shared framework this script runs on: the newest Microsoft.NETCore.App 10, as the IDE's.
var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory ();
if (!File.Exists (Path.Combine (runtimeDirectory, "System.Private.CoreLib.dll")))
	Die ("cannot find the Microsoft.NETCore.App runtime");

var status = 0;
var inBin = Directory.GetFiles (bin, "*.dll").Select (Path.GetFileName).ToHashSet ();
var inAddIns = Directory.Exists (addIns) ? Directory.GetFiles (addIns, "*.dll", SearchOption.AllDirectories) : [];
foreach (var file in inAddIns) {
	if (inBin.Contains (Path.GetFileName (file))) {
		Console.Error.WriteLine ($"duplicate: {Path.GetFileName (file)} in bin and {Path.GetRelativePath (build, file)}");
		status = 1;
	}
}
foreach (var file in Directory.GetFiles (bin, "*.dll", SearchOption.AllDirectories).Concat (inAddIns)) {
	if (File.Exists (Path.Combine (runtimeDirectory, Path.GetFileName (file)))) {
		Console.Error.WriteLine ($"shadows the shared framework: {Path.GetRelativePath (build, file)}");
		status = 1;
	}
}

if (status == 0)
	Log ($"no duplicate or framework-shadowing assemblies ({inBin.Count} in bin)");
return status;

static string RepoRoot ([CallerFilePath] string script = "") => Path.GetFullPath (Path.Combine (Path.GetDirectoryName (script)!, ".."));

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}
