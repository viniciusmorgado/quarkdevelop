// Builds the Flatpak bundle of the IDE (M7, T123, ADR 0022). Idempotent.
// Usage: dotnet scripts/package-flatpak.cs [-- --rebuild-ide]
//   --rebuild-ide  run dotnet scripts/build.cs -c Release (ContinuousIntegrationBuild=true) first even if main/build/bin
//                  exists (it is also run when the IDE has not been built yet). An existing build must have been made
//                  with ContinuousIntegrationBuild=true, as CI does: the script refuses a bundle whose files contain the
//                  checkout path.
// Outputs:
//   out/monodevelop.flatpak         single-file bundle (runtime org.gnome.Platform from Flathub)
//   out/monodevelop.flatpak.sha256  its checksum (sha256sum format)
//   out/monodevelop.cdx.json        CycloneDX SBOM of the shipped NuGet packages and the bundled .NET SDK
//   out/flatpak/                    logs and validator output
// The Flatpak files (manifest, desktop entry, AppStream metadata, MIME types) are written to out/flatpak-files by the
// "Flatpak files" step of the release workflow. The Flathub runtimes, the flatpak-builder cache and the build directory
// live in ~/.cache/monodevelop/flatpak (MD_FLATPAK_STORE overrides), never in the host's flatpak installation.
// Needs flatpak, flatpak-builder, desktop-file-validate and appstreamcli (dotnet scripts/setup.cs lists what is missing).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;

const string appId = "io.github.viniciusmorgado.MonoDevelop";
const string flathub = "https://dl.flathub.org/repo/flathub.flatpakrepo";
var root = RepoRoot ();
var outDirectory = Path.Combine (root, "out");
var files = Path.Combine (outDirectory, "flatpak-files");
var manifest = Path.Combine (files, appId + ".yml");
var store = Env ("MD_FLATPAK_STORE")
	?? Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".cache", "monodevelop", "flatpak");
var flatpakUserDirectory = Env ("FLATPAK_USER_DIR") ?? Path.Combine (store, "user");
var work = Path.Combine (outDirectory, "flatpak");

var rebuildIde = false;
foreach (var argument in args) {
	if (argument == "--rebuild-ide")
		rebuildIde = true;
	else
		Die ($"unknown argument: {argument}");
}

if (FindTool ("flatpak-builder") == null)
	Die ("flatpak-builder not found; install it (dotnet scripts/setup.cs lists the packages)");
if (!File.Exists (manifest))
	Die ($"{manifest} not found: the release workflow writes the Flatpak files");
Directory.CreateDirectory (store);
Directory.CreateDirectory (work);
var all = Stopwatch.StartNew ();
var timings = Path.Combine (work, "timings.txt");
File.WriteAllText (timings, "");

// 1. The IDE (Release). ContinuousIntegrationBuild maps the source and PDB paths to /_/: the bundle must not carry the
// path of the checkout it was built from.
if (rebuildIde || !File.Exists (Path.Combine (root, "main", "build", "bin", "MonoDevelop.dll")))
	Timed ("build-ide", () => Run (["dotnet", Path.Combine (root, "scripts", "build.cs"), "--", "-c", "Release"], new Dictionary<string, string?> { ["ContinuousIntegrationBuild"] = "true" }));

// 2. Desktop entry, AppStream metadata and MIME types. The validators' output is shown and kept in validate.log, with the
// lines of the step.
using (var validateLog = new StreamWriter (Path.Combine (work, "validate.log")) { AutoFlush = true }) {
	var output = new Tee (validateLog, Console.Out);
	Timed ("validate", () => All (
		() => RunToWriter (["desktop-file-validate", Path.Combine (files, appId + ".desktop")], output),
		// --no-net: the screenshot URLs point at the default branch, which may not have them yet
		() => RunToWriter (["appstreamcli", "validate", "--no-net", "--explain", Path.Combine (files, appId + ".metainfo.xml")], output),
		() => WellFormed (Path.Combine (files, appId + ".xml"), output) ? 0 : 1), echo: validateLog);
}

// 3. The Flathub runtime, SDK and .NET extension (cached in the store)
if (Flatpak (["flatpak", "remote-add", "--user", "--if-not-exists", "flathub", flathub]) != 0)
	Die ("flatpak remote-add failed");

// 4. Build and export to a local repository. --disable-rofiles-fuse: no FUSE mounts needed.
var repo = Path.Combine (store, "repo");
if (Directory.Exists (repo))
	Directory.Delete (repo, true);
var builderLog = Path.Combine (work, "flatpak-builder.log");
Timed ("flatpak-build", () => {
	using var log = new StreamWriter (builderLog);
	var status = RunToWriter (["flatpak-builder", "--user", "--install-deps-from=flathub", "--assumeyes",
		"--disable-rofiles-fuse", "--force-clean", "--ccache=false",
		"--state-dir=" + Path.Combine (store, "builder"), "--repo=" + repo,
		Path.Combine (store, "build-dir"), manifest], log, FlatpakEnvironment ());
	if (status != 0) {
		log.Flush ();
		foreach (var line in File.ReadLines (builderLog).TakeLast (40))
			Console.Error.WriteLine (line);
	}
	return status;
}, "flatpak-builder failed (log: out/flatpak/flatpak-builder.log)");

// No file of the app may contain the checkout path (a build without ContinuousIntegrationBuild=true)
var appFiles = Path.Combine (store, "build-dir", "files");
var checkout = Encoding.UTF8.GetBytes (root);
var leaked = Directory.GetFiles (appFiles, "*", SearchOption.AllDirectories)
	.Where (file => !new FileInfo (file).Attributes.HasFlag (FileAttributes.ReparsePoint) && File.ReadAllBytes (file).AsSpan ().IndexOf (checkout) >= 0)
	.ToList ();
if (leaked.Count > 0)
	Die ($"{leaked.Count} files of the bundle contain the checkout path (e.g. {leaked[0]}); rebuild with --rebuild-ide");

// The exported (installed) metadata must still validate
using (var validateLog = new StreamWriter (Path.Combine (work, "validate.log"), append: true)) {
	if (RunToWriter (["appstreamcli", "validate", "--no-net", Path.Combine (appFiles, "share", "metainfo", appId + ".metainfo.xml")], validateLog) != 0
		|| RunToWriter (["desktop-file-validate", Path.Combine (store, "build-dir", "export", "share", "applications", appId + ".desktop")], validateLog) != 0)
		Die ("the exported metadata does not validate (log: out/flatpak/validate.log)");
}

// 5. Single-file bundle; --runtime-repo lets `flatpak install` fetch the runtime from Flathub
var bundle = Path.Combine (outDirectory, "monodevelop.flatpak");
Timed ("bundle", () => Flatpak (["flatpak", "build-bundle", "--runtime-repo=" + flathub, repo, bundle + ".tmp", appId]));
File.Move (bundle + ".tmp", bundle, overwrite: true);
string hash;
using (var stream = File.OpenRead (bundle))
	hash = Convert.ToHexStringLower (SHA256.HashData (stream));
File.WriteAllText (bundle + ".sha256", $"{hash}  monodevelop.flatpak\n");

// 6. SBOM: the NuGet packages of the shipped projects (tests and development-only packages excluded) plus the bundled .NET SDK
var sbomFile = Path.Combine (outDirectory, "monodevelop.cdx.json");
Timed ("sbom", () => {
	var sdkVersion = Directory.GetDirectories (Path.Combine (appFiles, "lib", "dotnet", "sdk"))
		.Select (Path.GetFileName)
		.OfType<string> ()
		.OrderBy (name => Version.TryParse (name.Split ('-')[0], out var version) ? version : new Version ())
		.ThenBy (name => name, StringComparer.Ordinal)
		.Last ();
	var revision = CaptureOutput (["git", "rev-parse", "--short=12", "HEAD"]).Trim ();
	var version = Env ("MD_RELEASE_VERSION") ?? $"{ProductVersion (root)}+{(revision.Length > 0 ? revision : "unknown")}";
	var status = Run (["dotnet", "tool", "restore"], null, quiet: true);
	if (status != 0)
		return status;
	using (var log = new StreamWriter (Path.Combine (work, "cyclonedx.log"))) {
		status = RunToWriter (["dotnet", "CycloneDX", Path.Combine (root, "main", "MonoDevelop.Linux.sln"), "--output", Path.Combine (work, "sbom"),
			"--filename", "bom.json", "--output-format", "Json", "--exclude-test-projects", "--exclude-dev", "--disable-package-restore",
			"--set-name", "MonoDevelop", "--set-version", version], log);
		if (status != 0)
			return status;
	}
	var bom = JsonNode.Parse (File.ReadAllText (Path.Combine (work, "sbom", "bom.json")))!;
	bom["metadata"]!["component"]!["description"] = "MonoDevelop for Linux, Flatpak " + appId;
	var components = bom["components"]?.AsArray () ?? [];
	bom["components"] = components;
	components.Add ((JsonNode)new JsonObject {
		["type"] = "framework",
		["bom-ref"] = "pkg:generic/microsoft/dotnet-sdk@" + sdkVersion,
		["supplier"] = new JsonObject { ["name"] = "Microsoft" },
		["name"] = "dotnet-sdk",
		["version"] = sdkVersion,
		["description"] = ".NET SDK bundled in /app/lib/dotnet (org.freedesktop.Sdk.Extension.dotnet10)",
		["licenses"] = new JsonArray (new JsonObject { ["license"] = new JsonObject { ["id"] = "MIT" } }),
		["purl"] = "pkg:generic/microsoft/dotnet-sdk@" + sdkVersion,
	});
	// as jq wrote it: indented, with the characters of the strings as they are
	File.WriteAllText (sbomFile, bom.ToJsonString (new () { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
	return 0;
});

Report ($"{"total",-12} {(int)all.Elapsed.TotalSeconds,4}s");
var componentCount = JsonNode.Parse (File.ReadAllText (sbomFile))!["components"]!.AsArray ().Count;
Log ($"{DiskUsage (bundle)} out/monodevelop.flatpak, {componentCount} SBOM components");
Console.Write (File.ReadAllText (bundle + ".sha256"));
return 0;

// A timed step. It stops the script when it fails: with its exit status, or with 1 and the failure message when there is
// one. echo also gets the step's lines.
void Timed (string name, Func<int> step, string? failure = null, TextWriter? echo = null)
{
	var watch = Stopwatch.StartNew ();
	Log (name);
	echo?.WriteLine ($"\u001b[1;34m==>\u001b[0m {name}");
	int status;
	try {
		status = step ();
	} catch (Exception e) {
		// a missing program or file fails the step, as a failing command did in the shell script
		Console.Error.WriteLine ($"error: {e.GetType ().Name}: {e.Message}");
		echo?.WriteLine ($"error: {e.GetType ().Name}: {e.Message}");
		status = 1;
	}
	var line = $"{name,-12} {(int)watch.Elapsed.TotalSeconds,4}s{(status == 0 ? "" : $" (exit {status})")}";
	Report (line);
	echo?.WriteLine (line);
	if (status != 0) {
		Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {failure ?? $"step '{name}' failed"}");
		Environment.Exit (failure != null ? 1 : status);
	}
}

void Report (string line)
{
	Console.Error.WriteLine (line);
	File.AppendAllText (timings, line + "\n");
}

Dictionary<string, string?> FlatpakEnvironment () => new () { ["FLATPAK_USER_DIR"] = flatpakUserDirectory };

int Flatpak (IReadOnlyList<string> command) => Run (command, FlatpakEnvironment ());

// The repository root, above scripts/. dotnet gives a file-based app the directory of its file; [CallerFilePath] would be
// /_/... when the script is compiled with ContinuousIntegrationBuild=true (package-flatpak.cs sets it for build.cs).
static string RepoRoot () => Path.GetFullPath (Path.Combine (AppContext.GetData ("EntryPointFileDirectoryPath") as string
	?? throw new InvalidOperationException ("run the script as a file-based app: dotnet scripts/<name>.cs"), ".."));

// An environment variable, unset when empty (as ${NAME:-default} in a shell).
static string? Env (string name) => Environment.GetEnvironmentVariable (name) is { Length: > 0 } value ? value : null;

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

// du -sh: the disk space of a file or directory, as du prints it.
static string DiskUsage (string path) => CaptureOutput (["du", "-sh", path]).Split ('\t')[0];

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

// The product version: MonoDevelopVersion of main/Directory.Build.props (ADR 0021).
static string ProductVersion (string root)
{
	var match = Regex.Match (File.ReadAllText (Path.Combine (root, "main", "Directory.Build.props")), "<MonoDevelopVersion>([^<]+)</MonoDevelopVersion>");
	return match.Success ? match.Groups[1].Value.Trim () : throw new InvalidDataException ("no MonoDevelopVersion in main/Directory.Build.props");
}

static bool WellFormed (string xml, TextWriter log)
{
	try {
		new XmlDocument ().Load (xml);
		return true;
	} catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException) {
		log.WriteLine ($"{xml}: {e.Message}");
		return false;
	}
}

// A program of PATH, or one that dotnet scripts/setup.cs installed in ~/.local/bin.
static string? FindTool (string name)
{
	var directories = (Environment.GetEnvironmentVariable ("PATH") ?? "").Split (':', StringSplitOptions.RemoveEmptyEntries)
		.Append (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".local", "bin"));
	return directories.Select (directory => Path.Combine (directory, name)).FirstOrDefault (File.Exists);
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

static int Run (IReadOnlyList<string> command, IDictionary<string, string?>? environment, bool quiet = false)
{
	var info = Start (command, environment);
	if (quiet)
		info.RedirectStandardOutput = true;
	using var process = Process.Start (info)!;
	if (quiet)
		process.StandardOutput.ReadToEnd ();
	process.WaitForExit ();
	return process.ExitCode;
}

// Standard output and error to a writer, in the order they come.
static int RunToWriter (IReadOnlyList<string> command, TextWriter writer, IDictionary<string, string?>? environment = null)
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

static string CaptureOutput (IReadOnlyList<string> command)
{
	var info = Start (command, null);
	info.RedirectStandardOutput = info.RedirectStandardError = true;
	using var process = Process.Start (info)!;
	var error = process.StandardError.ReadToEndAsync ();
	var output = process.StandardOutput.ReadToEnd ();
	error.Wait ();
	process.WaitForExit ();
	return output;
}

// Writes to several writers, as tee does.
sealed class Tee (params TextWriter[] writers) : TextWriter
{
	public override Encoding Encoding => Encoding.UTF8;

	public override void Write (char value)
	{
		foreach (var writer in writers)
			writer.Write (value);
	}

	public override void Write (string? value)
	{
		foreach (var writer in writers)
			writer.Write (value);
	}

	public override void WriteLine (string? value)
	{
		foreach (var writer in writers)
			writer.WriteLine (value);
	}
}
