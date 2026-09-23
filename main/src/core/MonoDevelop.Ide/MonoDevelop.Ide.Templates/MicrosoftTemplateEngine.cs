//
// MicrosoftTemplateEngine.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2017 Xamarin Inc. (http://xamarin.com)
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Ide.Codons;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.Ide.Templates
{
	class MicrosoftTemplateEngine
	{
		static readonly AddinTemplatePackageProvider packageProvider = new AddinTemplatePackageProvider ();
		// TE 3.0 kept its template cache on disk and deleted it before every scan; TE 10 keeps the settings in memory.
		static readonly EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings (CreateHost (packageProvider), virtualizeSettings: true);
		static readonly TemplatePackageManager templatePackageManager = new TemplatePackageManager (environmentSettings);
		static readonly TemplateCreator templateCreator = new TemplateCreator (environmentSettings);
		static readonly object scanLock = new object ();
		static bool initialized;
		static bool dontUpdateCache = true;

		static List<MicrosoftTemplateEngineSolutionTemplate> projectTemplates = new List<MicrosoftTemplateEngineSolutionTemplate> ();
		static List<MicrosoftTemplateEngineItemTemplate> itemTemplates = new List<MicrosoftTemplateEngineItemTemplate> ();

		// Template sources: those of the add-in extension nodes, plus those added by unit tests (CreateProjectTemplate).
		static IReadOnlyList<string> addinScanPaths = Array.Empty<string> ();
		static readonly List<string> additionalScanPaths = new List<string> ();

		static void UpdateCache ()
		{
			if (dontUpdateCache)//Avoid updating cache while scan paths are added during registration 
				return;

			var projectTemplateNodes = AddinManager.GetExtensionNodes<TemplateExtensionNode> ("/MonoDevelop/Ide/Templates");
			var itemTemplateNodes = AddinManager.GetExtensionNodes<ItemTemplateExtensionNode> ("/MonoDevelop/Ide/ItemTemplates");

			addinScanPaths = projectTemplateNodes.Select (t => t.ScanPath)
				.Concat (itemTemplateNodes.Select (t => t.ScanPath))
				.Select (path => StringParserService.Parse (path))
				.Where (path => !string.IsNullOrEmpty (path))
				.Distinct ()
				.ToList ();

			var templateInfos = ScanTemplates ();
			var newProjectTemplates = new List<MicrosoftTemplateEngineSolutionTemplate> ();
			foreach (var template in projectTemplateNodes) {
				ITemplateInfo templateInfo;
				if (!templateInfos.TryGetValue (template.TemplateId, out templateInfo)) {
					LoggingService.LogWarning ("Template {0} not found.", template.TemplateId);
					continue;
				}
				newProjectTemplates.Add (new MicrosoftTemplateEngineSolutionTemplate (template, templateInfo));
			}
			projectTemplates = newProjectTemplates;

			var newItemTemplates = new List<MicrosoftTemplateEngineItemTemplate> ();
			foreach (var template in itemTemplateNodes) {
				ITemplateInfo templateInfo;
				if (!templateInfos.TryGetValue (template.TemplateId, out templateInfo)) {
					LoggingService.LogWarning ("Template {0} not found.", template.TemplateId);
					continue;
				}
				newItemTemplates.Add (new MicrosoftTemplateEngineItemTemplate (template, templateInfo));
			}
			itemTemplates = newItemTemplates;
		}

		/// <summary>
		/// Rescans every template source and returns the templates found, by identity.
		/// </summary>
		static Dictionary<string, ITemplateInfo> ScanTemplates ()
		{
			var templateInfos = new Dictionary<string, ITemplateInfo> ();
			lock (scanLock) {
				var scanPaths = addinScanPaths.Concat (additionalScanPaths)
					.SelectMany (path => InstallRequestPathResolution.ExpandMaskedPath (path, environmentSettings))
					.Distinct ()
					.ToList ();
				packageProvider.SetScanPaths (scanPaths);

				IReadOnlyList<ITemplateInfo> templates;
				try {
					// Run off the calling (UI) thread's synchronization context: the TE API is async only.
					templates = Task.Run (async () => {
						// TE 3.0 deleted its cache before every scan; TE 10 rebuilds it.
						await templatePackageManager.RebuildTemplateCacheAsync (CancellationToken.None).ConfigureAwait (false);
						return await templatePackageManager.GetTemplatesAsync (CancellationToken.None).ConfigureAwait (false);
					}).GetAwaiter ().GetResult ();
				} catch (Exception ex) {
					LoggingService.LogError ("Could not scan the templates", ex);
					return templateInfos;
				}

				foreach (var templateInfo in templates)
					templateInfos [templateInfo.Identity] = templateInfo;
			}
			return templateInfos;
		}

		static void OnProjectTemplateExtensionChanged (object sender, ExtensionNodeEventArgs args)
		{
			UpdateCache ();
		}

		static void OnItemTemplateExtensionChanged (object sender, ExtensionNodeEventArgs args)
		{
			UpdateCache ();
		}

		public static IEnumerable<SolutionTemplate> GetProjectTemplates ()
		{
			EnsureInitialized ();
			return projectTemplates;
		}

		public static IEnumerable<ItemTemplate> GetItemTemplates ()
		{
			EnsureInitialized ();
			return itemTemplates;
		}

		static void EnsureInitialized ()
		{
			if (initialized)
				return;

			Runtime.AssertMainThread ();

			AddinManager.AddExtensionNodeHandler ("/MonoDevelop/Ide/Templates", OnProjectTemplateExtensionChanged);
			AddinManager.AddExtensionNodeHandler ("/MonoDevelop/Ide/ItemTemplates", OnItemTemplateExtensionChanged);
			dontUpdateCache = false;
			UpdateCache ();

			initialized = true;
		}

		/// <summary>
		/// Used by unit tests to create a new solution template without having to use an addin.
		/// </summary>
		static internal SolutionTemplate CreateProjectTemplate (string templateId, string scanPath)
		{
			lock (scanLock) {
				if (!additionalScanPaths.Contains (scanPath))
					additionalScanPaths.Add (scanPath);
			}

			ScanTemplates ().TryGetValue (templateId, out ITemplateInfo templateInfo);

			return new MicrosoftTemplateEngineSolutionTemplate (templateId, templateId, null, templateInfo);
		}

		// TE 3.0 asked the host before overwriting existing files and the default host always agreed:
		// TE 10 has no such callback, forceCreation keeps that behaviour.
		public static Task<ITemplateCreationResult> InstantiateAsync (
			ITemplateInfo templateInfo,
			NewProjectConfiguration config,
			IReadOnlyDictionary<string, string> parameters)
		{
			return templateCreator.InstantiateAsync (
				templateInfo,
				config.ProjectName,
				config.GetValidProjectName (),
				config.ProjectLocation,
				parameters,
				forceCreation: true
			);
		}

		public static Task<ITemplateCreationResult> InstantiateAsync (
			ITemplateInfo templateInfo,
			NewItemConfiguration config,
			IReadOnlyDictionary<string, string> parameters)
		{
			return templateCreator.InstantiateAsync (
				templateInfo,
				config.NameWithoutExtension,
				config.NameWithoutExtension,
				config.Directory,
				parameters,
				forceCreation: true
			);
		}

		public static string GetPath (ICreationPath path)
		{
			return NormalizePath (path.Path);
		}

		static string NormalizePath (string path)
		{
			if (Path.DirectorySeparatorChar != '\\')
				return path.Replace ('\\', Path.DirectorySeparatorChar);

			return path;
		}

		public static async Task FormatFile (PolicyContainer policies, FilePath file)
		{
			string mime = IdeServices.DesktopService.GetMimeTypeForUri (file);
			if (mime == null)
				return;

			var formatter = CodeFormatterService.GetFormatter (mime);
			if (formatter != null) {
				try {
					var content = await TextFileUtility.ReadAllTextAsync (file);
					var formatted = formatter.FormatText (policies, content.Text);
					if (formatted != null)
						TextFileUtility.WriteText (file, formatted, content.Encoding);
				} catch (Exception ex) {
					LoggingService.LogError ("File formatting failed", ex);
				}
			}
		}

		public static string MergeDefaultParameters (string defaultParameters, ITemplateInfo templateInfo)
		{
			List<TemplateParameter> priorityParameters = null;
			var parameters = new List<string> ();
			// TE 3.0 CacheParameters: the non-choice parameters. TE 10 also lists the implicit 'name' parameter
			// (its default is the template's sourceName), which must not become a default parameter.
			var cacheParameters = templateInfo.ParameterDefinitions
				.Where (p => !p.IsChoice () && p.Name != "name" && !string.IsNullOrEmpty (p.DefaultValue));

			if (!cacheParameters.Any ())
				return defaultParameters;

			if (!string.IsNullOrEmpty (defaultParameters)) {
				priorityParameters = TemplateParameter.CreateParameters (defaultParameters).ToList ();
				defaultParameters += ",";
			}

			foreach (var p in cacheParameters) {
				if (priorityParameters == null || !priorityParameters.Exists (t => t.Name == p.Name))
					parameters.Add ($"{p.Name}={p.DefaultValue}");
			}

			return defaultParameters += string.Join (",", parameters);
		}

		public static string GetLanguage (ITemplateInfo templateInfo)
		{
			if (templateInfo.TagsCollection.TryGetValue ("language", out string language) && language != null) {
				return language;
			}

			return string.Empty;
		}

		/// <summary>
		/// Use '${TemplateConfigDirectory}/template.json' to get the template.json file
		/// without having to specify the full path.
		/// </summary>
		public static Stream GetStream (ITemplateInfo template, string path)
		{
			path = NormalizePath (template, path);

			return OpenFile (template, path);
		}

		/// <summary>
		/// Reads a file of the template's source (folder or .nupkg), path being relative to its root.
		/// </summary>
		internal static Stream OpenFile (ITemplateInfo template, string path)
		{
			if (!environmentSettings.TryGetMountPoint (template.MountPointUri, out IMountPoint mountPoint))
				return null;

			// TE 10 mount points are disposable (a .nupkg is closed with its mount point): return a copy.
			using (mountPoint) {
				IFile file = mountPoint.FileInfo (path);
				if (file == null || !file.Exists)
					return null;

				var content = new MemoryStream ();
				using (var stream = file.OpenRead ())
					stream.CopyTo (content);
				content.Position = 0;
				return content;
			}
		}

		public static Xwt.Drawing.Image GetImage (ITemplateInfo template, string path)
		{
			var loader = new MicrosoftTemplateEngineImageLoader (environmentSettings, template);

			path = NormalizePath (template, path);

			return Xwt.Drawing.Image.FromCustomLoader (loader, path);
		}

		static string NormalizePath (ITemplateInfo template, string path)
		{
			path = NormalizePath (path);

			var tags = new string[,] {
				{"TemplateConfigDirectory", Path.GetDirectoryName (template.ConfigPlace) }
			};

			return StringParserService.Parse (path, tags);
		}

		static Microsoft.TemplateEngine.Edge.DefaultTemplateEngineHost CreateHost (ITemplatePackageProviderFactory packageProviderFactory)
		{
			// Built-in components: mount points (folders, .nupkg), constraints and bind sources of the Edge, and the
			// RunnableProjects generator with its macros. The Edge's own template package provider (the global
			// templates installed with 'dotnet new install') is left out: templates come from the add-ins only.
			var builtIns = Microsoft.TemplateEngine.Edge.Components.AllComponents
				.Where (component => component.Type != typeof (ITemplatePackageProviderFactory))
				.Concat (Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents)
				.Append ((typeof (ITemplatePackageProviderFactory), packageProviderFactory))
				.ToList ();

			// The only host parameter MonoDevelop ever answered (TE 3.0 overrode TryGetHostParamDefault for it).
			var defaults = new Dictionary<string, string> {
				{ "HostIdentifier", BrandingService.ApplicationName }
			};

			return new Microsoft.TemplateEngine.Edge.DefaultTemplateEngineHost (BrandingService.ApplicationName, BuildInfo.CompatVersion, defaults, builtIns);
		}

		/// <summary>
		/// Template sources registered by the add-ins (.nupkg files or template folders). TE 3.0 scanned them straight
		/// into its settings cache; in TE 10 a template package provider hands them to the TemplatePackageManager.
		/// </summary>
		class AddinTemplatePackageProvider : ITemplatePackageProviderFactory, ITemplatePackageProvider
		{
			static readonly Guid FactoryId = new Guid ("5e4a8c33-7d0f-4a8e-9a55-2f5c4b4d8e61");

			IReadOnlyList<string> scanPaths = Array.Empty<string> ();

			public Guid Id => FactoryId;

			public string DisplayName => "MonoDevelop add-in templates";

			public ITemplatePackageProviderFactory Factory => this;

			public event Action TemplatePackagesChanged;

			public ITemplatePackageProvider CreateProvider (IEngineEnvironmentSettings settings)
			{
				return this;
			}

			public void SetScanPaths (IReadOnlyList<string> paths)
			{
				scanPaths = paths;
				TemplatePackagesChanged?.Invoke ();
			}

			public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync (CancellationToken cancellationToken)
			{
				var packages = new List<ITemplatePackage> ();
				foreach (string path in scanPaths) {
					if (File.Exists (path)) {
						packages.Add (new TemplatePackage (this, path, File.GetLastWriteTimeUtc (path)));
					} else if (Directory.Exists (path)) {
						packages.Add (new TemplatePackage (this, path, Directory.GetLastWriteTimeUtc (path)));
					} else {
						LoggingService.LogDebug ("Template source {0} not found.", path);
					}
				}
				return Task.FromResult<IReadOnlyList<ITemplatePackage>> (packages);
			}
		}
	}
}
