//
// DotNetNewTemplateCatalog.cs
//
// Copyright (c) 2026 MonoDevelop contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// The templates <c>dotnet new</c> offers (T152, ADR 0026). The template packages the CLI uses (those of the SDK in
	/// <c>dotnet/templates/&lt;version&gt;</c>, the workload packs in <c>dotnet/template-packs</c> and the packages
	/// installed with <c>dotnet new install</c>) are read in process with Microsoft.TemplateEngine, off the UI thread;
	/// the result is cached on disk per SDK version and invalidated when a package changes. When the engine reads
	/// nothing, the table of <c>dotnet new list</c> is the fallback.
	/// </summary>
	sealed class DotNetNewTemplateCatalog
	{
		const int CacheFormat = 1;

		public static DotNetNewTemplateCatalog Default { get; } = new DotNetNewTemplateCatalog ();

		readonly object gate = new object ();
		Task<IReadOnlyList<DotNetNewTemplate>> loading;

		/// <summary>Where the per-SDK lists are cached (tests use a directory of their own).</summary>
		internal Func<FilePath> CacheDirectory { get; set; } = () => UserProfile.Current.CacheDir.Combine ("DotNetNewTemplates");

		/// <summary>How the list was obtained the last time: "cache", "engine" or "cli" (logged, and checked by tests).</summary>
		internal string LastSource { get; private set; }

		/// <summary>The templates, read once (later calls return the same task until <see cref="Invalidate"/>).</summary>
		public Task<IReadOnlyList<DotNetNewTemplate>> GetTemplatesAsync ()
		{
			lock (gate) {
				if (loading == null || loading.IsFaulted || loading.IsCanceled)
					loading = Task.Run (LoadAsync);
				return loading;
			}
		}

		/// <summary>The templates if they are already read, else null (and they are read in the background).</summary>
		public IReadOnlyList<DotNetNewTemplate> TryGetTemplates ()
		{
			var task = GetTemplatesAsync ();
#pragma warning disable VSTHRD002 // the task has completed
			return task.Status == TaskStatus.RanToCompletion ? task.Result : null;
#pragma warning restore VSTHRD002
		}

		/// <summary>Starts reading the templates so that the New Project dialog finds them ready.</summary>
		public void LoadInBackground ()
		{
			GetTemplatesAsync ().Ignore ();
		}

		/// <summary>The next request reads the templates again (from the disk cache when no package changed).</summary>
		public void Invalidate ()
		{
			lock (gate)
				loading = null;
		}

		DateTime lastRefresh;

		/// <summary>
		/// Reads the templates again in the background (at most every 10 s) and uses the new list once it is read, so
		/// that templates installed while the IDE runs appear the next time a dialog opens. The current list stays in
		/// use meanwhile.
		/// </summary>
		public void RefreshInBackground ()
		{
			lock (gate) {
				if (loading == null || !loading.IsCompleted) {
					GetTemplatesAsync ().Ignore ();
					return;
				}
				if (DateTime.UtcNow - lastRefresh < TimeSpan.FromSeconds (10))
					return;
				lastRefresh = DateTime.UtcNow;
			}
			Task.Run (LoadAsync).ContinueWith (task => {
				if (task.Status == TaskStatus.RanToCompletion && task.Result.Count > 0) {
					lock (gate)
						loading = task;
				}
			}, TaskScheduler.Default).Ignore ();
		}

		/// <summary>
		/// The templates, for synchronous callers such as the New Project dialog. On the UI thread the GTK main loop keeps
		/// running while the list is read (usually it is ready: <see cref="LoadInBackground"/> runs at start-up).
		/// </summary>
		public IReadOnlyList<DotNetNewTemplate> GetTemplates ()
		{
			var task = GetTemplatesAsync ();
			if (!task.IsCompleted && Runtime.IsMainThread && IdeApp.IsInitialized) {
#pragma warning disable VSTHRD002 // synchronous callers; the main loop keeps running while waiting
				while (!task.Wait (20))
					DispatchService.RunPendingEvents (0);
			}
			try {
				return task.GetAwaiter ().GetResult ();
#pragma warning restore VSTHRD002
			} catch (Exception ex) {
				LoggingService.LogError ("Could not read the dotnet new templates", ex);
				return Array.Empty<DotNetNewTemplate> ();
			}
		}

		async Task<IReadOnlyList<DotNetNewTemplate>> LoadAsync ()
		{
			var clock = Stopwatch.StartNew ();
			var sdk = DotNetCoreSdkInfo.FindDefault ();
			if (sdk == null) {
				LoggingService.LogWarning ("dotnet new templates: no .NET SDK found");
				LastSource = null;
				return Array.Empty<DotNetNewTemplate> ();
			}

			var packages = FindTemplatePackages (sdk.DotNetRoot, sdk.Version, GetCliHome ());
			string fingerprint = GetFingerprint (sdk.VersionString, packages);
			var cacheFile = CacheDirectory ().Combine (sdk.VersionString + ".json");

			var templates = ReadCache (cacheFile, fingerprint);
			string source = "cache";
			if (templates == null) {
				source = "engine";
				try {
					templates = await ScanAsync (packages, sdk.VersionString).ConfigureAwait (false);
				} catch (Exception ex) {
					LoggingService.LogError ("dotnet new templates: the template engine could not read the SDK's templates", ex);
				}
				if (templates != null && templates.Count > 0) {
					WriteCache (cacheFile, fingerprint, templates);
				} else {
					source = "cli";
					templates = await ListWithCliAsync ().ConfigureAwait (false);
				}
			}

			LastSource = source;
			LoggingService.LogInfo ("dotnet new templates: {0} templates of SDK {1} ({2} packages) read from the {3} in {4} ms",
				templates.Count, sdk.VersionString, packages.Count, source, clock.ElapsedMilliseconds);
			return templates;
		}

		/// <summary>DOTNET_CLI_HOME or the home directory: where <c>dotnet new install</c> keeps its packages.</summary>
		internal static string GetCliHome ()
		{
			var home = Environment.GetEnvironmentVariable ("DOTNET_CLI_HOME");
			return string.IsNullOrEmpty (home) ? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile) : home;
		}

		/// <summary>
		/// The template packages <c>dotnet new</c> reads: the newest <c>templates/&lt;version&gt;</c> folder of the SDK's
		/// major.minor (or older), the workload packs, and the packages listed in <c>.templateengine/packages.json</c>.
		/// </summary>
		internal static IReadOnlyList<string> FindTemplatePackages (FilePath dotnetRoot, Version sdkVersion, string cliHome)
		{
			var packages = new List<string> ();

			var templatesRoot = dotnetRoot.Combine ("templates");
			if (Directory.Exists (templatesRoot)) {
				var versions = new List<(Version Version, string Path)> ();
				foreach (var dir in Directory.EnumerateDirectories (templatesRoot)) {
					if (Version.TryParse (Path.GetFileName (dir).Split ('-')[0], out var version))
						versions.Add ((version, dir));
				}
				var best = versions
					.Where (v => v.Version.Major < sdkVersion.Major || (v.Version.Major == sdkVersion.Major && v.Version.Minor <= sdkVersion.Minor))
					.OrderByDescending (v => v.Version)
					.Select (v => v.Path)
					.FirstOrDefault ();
				if (best != null)
					packages.AddRange (Directory.EnumerateFiles (best, "*.nupkg").OrderBy (p => p, StringComparer.Ordinal));
			}

			var workloadPacks = dotnetRoot.Combine ("template-packs");
			if (Directory.Exists (workloadPacks))
				packages.AddRange (Directory.EnumerateFiles (workloadPacks, "*.nupkg").OrderBy (p => p, StringComparer.Ordinal));

			if (!string.IsNullOrEmpty (cliHome))
				packages.AddRange (ReadInstalledPackages (Path.Combine (cliHome, ".templateengine", "packages.json")));

			return packages;
		}

		/// <summary>The mount points of <c>packages.json</c> (<c>{"Packages":[{"MountPointUri":…}]}</c>) that exist.</summary>
		internal static IEnumerable<string> ReadInstalledPackages (string packagesFile)
		{
			if (!File.Exists (packagesFile))
				return Array.Empty<string> ();

			var result = new List<string> ();
			try {
				using (var document = JsonDocument.Parse (File.ReadAllText (packagesFile).TrimStart ('﻿'))) {
					if (document.RootElement.TryGetProperty ("Packages", out var list) && list.ValueKind == JsonValueKind.Array) {
						foreach (var package in list.EnumerateArray ()) {
							if (package.ValueKind == JsonValueKind.Object
								&& package.TryGetProperty ("MountPointUri", out var uri) && uri.ValueKind == JsonValueKind.String) {
								var path = uri.GetString ();
								if (File.Exists (path) || Directory.Exists (path))
									result.Add (path);
							}
						}
					}
				}
			} catch (Exception ex) {
				LoggingService.LogWarning ("dotnet new templates: could not read " + packagesFile, ex);
			}
			return result;
		}

		/// <summary>Identifies a set of packages: the SDK, the UI language (names are localized) and each package's time and size.</summary>
		internal static string GetFingerprint (string sdkVersion, IEnumerable<string> packages)
		{
			var text = new StringBuilder ();
			text.Append (CacheFormat).Append ('|').Append (sdkVersion).Append ('|').Append (CultureInfo.CurrentUICulture.Name);
			foreach (var package in packages) {
				text.Append ('|').Append (package);
				if (File.Exists (package)) {
					var info = new FileInfo (package);
					text.Append (':').Append (info.LastWriteTimeUtc.Ticks).Append (':').Append (info.Length);
				} else if (Directory.Exists (package)) {
					// a template folder: its template.json files
					var newest = Directory.EnumerateFiles (package, "template.json", SearchOption.AllDirectories)
						.Select (File.GetLastWriteTimeUtc)
						.DefaultIfEmpty (Directory.GetLastWriteTimeUtc (package))
						.Max ();
					text.Append (':').Append (newest.Ticks);
				}
			}
			return text.ToString ();
		}

		sealed class CacheFile
		{
			public int Format { get; set; }
			public string Fingerprint { get; set; }
			public List<DotNetNewTemplate> Templates { get; set; }
		}

		internal static IReadOnlyList<DotNetNewTemplate> ReadCache (FilePath file, string fingerprint)
		{
			try {
				if (!File.Exists (file))
					return null;
				var cache = JsonSerializer.Deserialize<CacheFile> (File.ReadAllText (file));
				if (cache == null || cache.Format != CacheFormat || cache.Fingerprint != fingerprint || cache.Templates == null)
					return null;
				return cache.Templates;
			} catch (Exception ex) {
				LoggingService.LogWarning ("dotnet new templates: ignoring the cache " + file, ex);
				return null;
			}
		}

		internal static void WriteCache (FilePath file, string fingerprint, IReadOnlyList<DotNetNewTemplate> templates)
		{
			try {
				Directory.CreateDirectory (file.ParentDirectory);
				var cache = new CacheFile { Format = CacheFormat, Fingerprint = fingerprint, Templates = templates.ToList () };
				var temp = file + ".tmp";
				File.WriteAllText (temp, JsonSerializer.Serialize (cache));
				File.Move (temp, file, true);
			} catch (Exception ex) {
				LoggingService.LogWarning ("dotnet new templates: could not write the cache " + file, ex);
			}
		}

		/// <summary>Reads the template packages with Microsoft.TemplateEngine, the library of <c>dotnet new</c>.</summary>
		internal static async Task<IReadOnlyList<DotNetNewTemplate>> ScanAsync (IReadOnlyList<string> packages, string sdkVersion)
		{
			var provider = new PackageListProvider (packages);
			using (var settings = new EngineEnvironmentSettings (CreateHost (provider, sdkVersion), virtualizeSettings: true))
			using (var manager = new TemplatePackageManager (settings)) {
				var infos = await manager.GetTemplatesAsync (CancellationToken.None).ConfigureAwait (false);
				return Group (infos);
			}
		}

		/// <summary>One <see cref="DotNetNewTemplate"/> per template group (the languages of a template share a group).</summary>
		internal static IReadOnlyList<DotNetNewTemplate> Group (IEnumerable<ITemplateInfo> infos)
		{
			var result = new List<DotNetNewTemplate> ();
			foreach (var group in infos.GroupBy (info => string.IsNullOrEmpty (info.GroupIdentity) ? info.Identity : info.GroupIdentity)) {
				// the newest version of each language (templates of older SDKs have a lower precedence)
				var byLanguage = group
					.GroupBy (GetLanguage)
					.Select (language => language.OrderByDescending (info => info.Precedence).First ())
					.OrderBy (info => LanguageOrder (GetLanguage (info)))
					.ThenBy (info => GetLanguage (info), StringComparer.Ordinal)
					.ToList ();
				var main = byLanguage[0];
				var template = new DotNetNewTemplate {
					Identity = group.Key,
					Name = main.Name,
					Description = main.Description,
					Author = main.Author,
					ShortNames = byLanguage.SelectMany (info => info.ShortNameList).Distinct (StringComparer.OrdinalIgnoreCase).ToList (),
					Languages = byLanguage.Select (GetLanguage).Where (language => language.Length > 0).ToList (),
					Type = main.TagsCollection.TryGetValue ("type", out var type) ? type : string.Empty,
					Tags = main.Classifications.ToList (),
					UsesName = UsesName (main),
					DefaultName = main.ParameterDefinitions.TryGetValue ("name", out var nameParameter) ? nameParameter.DefaultValue : null,
					OperatingSystems = GetConstraintArguments (main, "os"),
					ProjectCapabilities = GetProjectCapabilities (byLanguage),
					SolutionFormats = GetChoices (main, "format")
				};
				if (template.ShortNames.Count > 0)
					result.Add (template);
			}
			return result.OrderBy (t => t.Name, StringComparer.OrdinalIgnoreCase).ToList ();
		}

		static string GetLanguage (ITemplateInfo info)
		{
			return info.TagsCollection.TryGetValue ("language", out var language) && language != null ? language : string.Empty;
		}

		static int LanguageOrder (string language)
		{
			int index = DotNetNewTemplateClassifier.SupportedLanguages.ToList ().IndexOf (language);
			return index < 0 ? int.MaxValue : index;
		}

		/// <summary>The template engine lists a <c>name</c> parameter whose default is the template's sourceName, if any.</summary>
		static bool UsesName (ITemplateInfo info)
		{
			return info.ParameterDefinitions.TryGetValue ("name", out var name) && !string.IsNullOrEmpty (name.DefaultValue);
		}

		/// <summary>The choices of a choice parameter (the name is compared ignoring case: the solution template's is Format).</summary>
		static List<string> GetChoices (ITemplateInfo info, string parameterName)
		{
			var parameter = info.ParameterDefinitions.FirstOrDefault (p => string.Equals (p.Name, parameterName, StringComparison.OrdinalIgnoreCase));
			return parameter?.Choices?.Keys.ToList ();
		}

		/// <summary>The project capabilities each language of the template requires.</summary>
		static Dictionary<string, List<string>> GetProjectCapabilities (IEnumerable<ITemplateInfo> byLanguage)
		{
			Dictionary<string, List<string>> result = null;
			foreach (var info in byLanguage) {
				var capabilities = GetConstraintArguments (info, "project-capability");
				if (capabilities == null)
					continue;
				result ??= new Dictionary<string, List<string>> ();
				result[GetLanguage (info)] = capabilities;
			}
			return result;
		}

		/// <summary>The arguments of the constraints of a type (the arguments are a JSON string or array).</summary>
		static List<string> GetConstraintArguments (ITemplateInfo info, string type)
		{
			List<string> result = null;
			foreach (var constraint in info.Constraints ?? Array.Empty<TemplateConstraintInfo> ()) {
				if (!string.Equals (constraint.Type, type, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty (constraint.Args))
					continue;
				result ??= new List<string> ();
				try {
					using (var args = JsonDocument.Parse (constraint.Args)) {
						if (args.RootElement.ValueKind == JsonValueKind.String)
							result.Add (args.RootElement.GetString ());
						else if (args.RootElement.ValueKind == JsonValueKind.Array)
							result.AddRange (args.RootElement.EnumerateArray ().Where (e => e.ValueKind == JsonValueKind.String).Select (e => e.GetString ()));
					}
				} catch (JsonException) {
					result.Add (constraint.Args.Trim ('"'));
				}
			}
			return result;
		}

		static readonly string[] ListArguments = { "new", "list", "--columns-all" };

		/// <summary>The fallback: the table of <c>dotnet new list --columns-all</c> (names may be cut, no descriptions).</summary>
		internal static async Task<IReadOnlyList<DotNetNewTemplate>> ListWithCliAsync ()
		{
			try {
				var result = await DotNetNewCli.RunAsync (null, ListArguments, CancellationToken.None, englishOutput: true).ConfigureAwait (false);
				if (result.ExitCode == 0)
					return DotNetNewListParser.Parse (result.Output);
				LoggingService.LogWarning ("dotnet new list failed: " + result.Error);
			} catch (Exception ex) {
				LoggingService.LogError ("dotnet new list failed", ex);
			}
			return Array.Empty<DotNetNewTemplate> ();
		}

		static DefaultTemplateEngineHost CreateHost (ITemplatePackageProviderFactory packageProviderFactory, string sdkVersion)
		{
			// The components of the Edge (mount points for folders and .nupkg files, constraints) and of the
			// RunnableProjects generator; the packages come from PackageListProvider only.
			var builtIns = Microsoft.TemplateEngine.Edge.Components.AllComponents
				.Where (component => component.Type != typeof (ITemplatePackageProviderFactory))
				.Concat (Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents)
				.Append ((typeof (ITemplatePackageProviderFactory), packageProviderFactory))
				.ToList ();

			// the host of dotnet new: its dotnetcli.host.json files apply
			return new DefaultTemplateEngineHost ("dotnetcli", sdkVersion, new Dictionary<string, string> (), builtIns);
		}

		/// <summary>Hands a fixed list of template packages (.nupkg files or template folders) to the template engine.</summary>
		sealed class PackageListProvider : ITemplatePackageProviderFactory, ITemplatePackageProvider
		{
			static readonly Guid FactoryId = new Guid ("7d7a6a3e-0f7c-4d53-9c7b-5a4f2b9f1e30");

			readonly IReadOnlyList<string> packages;

			public PackageListProvider (IReadOnlyList<string> packages)
			{
				this.packages = packages;
			}

			public Guid Id => FactoryId;

			public string DisplayName => "dotnet new template packages";

			public ITemplatePackageProviderFactory Factory => this;

#pragma warning disable CS0067 // the list never changes
			public event Action TemplatePackagesChanged;
#pragma warning restore CS0067

			public ITemplatePackageProvider CreateProvider (IEngineEnvironmentSettings settings) => this;

			public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync (CancellationToken cancellationToken)
			{
				var result = new List<ITemplatePackage> ();
				foreach (var path in packages) {
					if (File.Exists (path))
						result.Add (new TemplatePackage (this, path, File.GetLastWriteTimeUtc (path)));
					else if (Directory.Exists (path))
						result.Add (new TemplatePackage (this, path, Directory.GetLastWriteTimeUtc (path)));
				}
				return Task.FromResult<IReadOnlyList<ITemplatePackage>> (result);
			}
		}
	}
}
