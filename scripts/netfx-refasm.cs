// Installs the .NET Framework reference assemblies of the legacy test fixtures (task T134).
//
// Hundreds of projects under main/tests/test-projects are old-style .NET Framework projects (v2.0 to v4.7.2). Linux has
// no .NETFramework reference directory, so building them fails with MSB3644. This script downloads the
// Microsoft.NETFramework.ReferenceAssemblies.* packages from nuget.org (versions and SHA-256 pinned below), verifies them
// and merges their build/.NETFramework/v* folders into one TargetFrameworkRootPath:
//     ~/.cache/monodevelop/netfx-refasm/.NETFramework/v4.5 …   (MD_NETFX_REFASM overrides the root)
// The test harness (main/tests/UnitTests/Util.cs) points the copied fixtures at it. Idempotent.
// Usage: dotnet scripts/netfx-refasm.cs

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;

var cache = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".cache", "monodevelop");
// as ${MD_NETFX_REFASM:-…} in a shell and in DotNetCoreTargetRuntime: an empty value is unset
var root = Environment.GetEnvironmentVariable ("MD_NETFX_REFASM") is { Length: > 0 } custom ? custom : Path.Combine (cache, "netfx-refasm");
var frameworks = Path.GetFullPath (Path.Combine (root, ".NETFramework"));
var downloads = Path.Combine (cache, "netfx-refasm-downloads");
Directory.CreateDirectory (frameworks);
Directory.CreateDirectory (downloads);

// moniker, version, SHA-256 of the nuget.org package microsoft.netframework.referenceassemblies.<moniker>
(string Moniker, string Version, string Sha256)[] packages = [
	("net20", "1.0.3", "7f74997fec338dde70f5a8c9fd09aa6fe21286731e7b133cc133d644e4e3c5e8"),
	("net40", "1.0.3", "54d6e20a1b61caf79395d6d71d091265e81f5a5705ac4ae52af45ca143c3c694"),
	("net45", "1.0.3", "23a9f94ea3e2cb88cd8341af75b811c6fb5cb82516fc696e95ed4620279128e3"),
	("net451", "1.0.3", "770b33dfa64dc430c9d9ba4e1438b920dee905f7ea60791147d70689f68692d3"),
	("net461", "1.0.3", "bd52289e5fb8765090bb189b399a06477a350c0863018a455b5e5d9e45298cc9"),
	("net47", "1.0.3", "b1e7f2ea457ee84fdacaa67d0b37445b31606e4f75c8939a57bf4a6836001d45"),
	("net472", "1.0.3", "ffa0a5570a39f911399164d0581ffddef99b5e3dfbaa5f220e5ce22969bcf57c"),
];

using var http = new HttpClient ();
foreach (var (moniker, version, sha256) in packages) {
	var id = "microsoft.netframework.referenceassemblies." + moniker;
	var stamp = Path.Combine (root, $".{moniker}-{version}");
	if (File.Exists (stamp))
		continue;
	var nupkg = Path.Combine (downloads, $"{id}.{version}.nupkg");
	if (!File.Exists (nupkg) || Sha256 (nupkg) != sha256) {
		Log ($"downloading {id} {version}");
		var url = $"https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg";
		byte[] bytes = [];
		try {
			bytes = await http.GetByteArrayAsync (url);
		} catch (HttpRequestException e) {
			Die ($"cannot download {url}: {e.Message}");
		}
		File.WriteAllBytes (nupkg + ".tmp", bytes);
		File.Move (nupkg + ".tmp", nupkg, overwrite: true);
	}
	if (Sha256 (nupkg) != sha256)
		Die ($"checksum mismatch for {nupkg}");
	using (var archive = ZipFile.OpenRead (nupkg)) {
		const string prefix = "build/.NETFramework/";
		foreach (var entry in archive.Entries.Where (entry => entry.FullName.StartsWith (prefix, StringComparison.Ordinal))) {
			var target = Path.GetFullPath (Path.Combine (frameworks, entry.FullName[prefix.Length..]));
			if (target != frameworks && !target.StartsWith (frameworks + Path.DirectorySeparatorChar, StringComparison.Ordinal))
				Die ($"{nupkg}: {entry.FullName} is outside {prefix}");
			if (entry.FullName.EndsWith ('/')) {
				Directory.CreateDirectory (target);
				continue;
			}
			Directory.CreateDirectory (Path.GetDirectoryName (target)!);
			entry.ExtractToFile (target, overwrite: true);
		}
	}
	File.WriteAllText (stamp, "");
	Console.WriteLine ($"installed {moniker} ({version})");
}

// as ls lists them: hidden entries left out
foreach (var name in Directory.GetFileSystemEntries (frameworks).Select (Path.GetFileName).OfType<string> ().Where (name => !name.StartsWith ('.')).Order (StringComparer.Ordinal))
	Console.WriteLine (name);
return 0;

static void Log (string message) => Console.Error.WriteLine ($"\u001b[1;34m==>\u001b[0m {message}");

[DoesNotReturn]
static void Die (string message)
{
	Console.Error.WriteLine ($"\u001b[1;31merror:\u001b[0m {message}");
	Environment.Exit (1);
}

static string Sha256 (string file)
{
	using var stream = File.OpenRead (file);
	return Convert.ToHexStringLower (SHA256.HashData (stream));
}
