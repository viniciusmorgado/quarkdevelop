//
// DotNetNewProjectTemplatingProvider.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>A templating provider that also defines the categories of its templates.</summary>
	interface IProjectTemplateCategoryProvider
	{
		IEnumerable<TemplateCategory> GetCategories ();
	}

	/// <summary>A project or solution template of <c>dotnet new</c>, in one language.</summary>
	sealed class DotNetNewSolutionTemplate : SolutionTemplate
	{
		public const string TopLevelCategoryId = "dotnet";
		public const string ThirdLevelCategoryId = "general";

		internal DotNetNewSolutionTemplate (DotNetNewTemplateClassification classification, string language)
			: base (GetId (classification.Template, language), classification.Template.Name, GetIconId (classification))
		{
			Classification = classification;
			Description = classification.Template.Description;
			Category = TopLevelCategoryId + "/" + GetCategoryId (classification.Category) + "/" + ThirdLevelCategoryId;
			Language = language;
			if (classification.Languages.Count > 0)
				GroupId = "dotnetnew:" + classification.Template.Identity;
			ImageId = GetImageId (classification);
			if (classification.Kind == DotNetNewTemplateKind.Solution) {
				HasProjects = false;
				Visibility = SolutionTemplateVisibility.NewSolution;
			}
		}

		public DotNetNewTemplateClassification Classification { get; }

		public DotNetNewTemplate Template => Classification.Template;

		/// <summary>The identity and the language: an id without '/' or '#' (recent templates are stored as URIs).</summary>
		static string GetId (DotNetNewTemplate template, string language)
		{
			if (string.IsNullOrEmpty (language))
				return template.Identity;
			return template.Identity + "." + language.Replace ("#", "Sharp").Replace ("+", "Plus");
		}

		internal static string GetCategoryId (string category)
		{
			return new string (category.ToLowerInvariant ().Select (c => char.IsLetterOrDigit (c) ? c : '-').ToArray ());
		}

		static bool HasTag (DotNetNewTemplateClassification classification, string tag)
		{
			return classification.Template.Tags.Contains (tag, StringComparer.OrdinalIgnoreCase);
		}

		static string GetIconId (DotNetNewTemplateClassification classification)
		{
			if (classification.Kind == DotNetNewTemplateKind.Solution)
				return "md-solution";
			if (HasTag (classification, "Console"))
				return "md-console-project";
			if (HasTag (classification, "Library"))
				return "md-library-project";
			if (string.Equals (classification.Category, "Test", StringComparison.OrdinalIgnoreCase))
				return "md-test-project";
			if (string.Equals (classification.Category, "Web", StringComparison.OrdinalIgnoreCase))
				return "md-gui-project";
			return "md-project";
		}

		static string GetImageId (DotNetNewTemplateClassification classification)
		{
			string icon = GetIconId (classification);
			return icon == "md-gui-project" ? "md-project" : icon;
		}
	}

	/// <summary>
	/// The New Project dialog's templates: the project templates of <c>dotnet new</c> (and the solution templates that
	/// write a .sln file), created by running the CLI (T152, ADR 0026).
	/// </summary>
	sealed class DotNetNewProjectTemplatingProvider : IProjectTemplatingProvider, IProjectTemplateCategoryProvider
	{
		readonly DotNetNewTemplateCatalog catalog;
		readonly object gate = new object ();
		IReadOnlyList<DotNetNewTemplate> snapshot;
		List<DotNetNewSolutionTemplate> templates = new List<DotNetNewSolutionTemplate> ();

		public DotNetNewProjectTemplatingProvider () : this (DotNetNewTemplateCatalog.Default)
		{
		}

		internal DotNetNewProjectTemplatingProvider (DotNetNewTemplateCatalog catalog)
		{
			this.catalog = catalog;
		}

		/// <summary>The templates of the New Project dialog: visible project templates, and solution templates writing .sln files.</summary>
		internal static bool IsNewProjectTemplate (DotNetNewTemplateClassification classification)
		{
			if (!classification.IsVisible)
				return false;
			if (classification.Kind == DotNetNewTemplateKind.Project)
				return true;
			return classification.Kind == DotNetNewTemplateKind.Solution
				&& classification.Template.SolutionFormats != null
				&& classification.Template.SolutionFormats.Contains ("sln", StringComparer.OrdinalIgnoreCase);
		}

		List<DotNetNewSolutionTemplate> GetSolutionTemplates ()
		{
			var current = catalog.GetTemplates ();
			catalog.RefreshInBackground ();
			lock (gate) {
				if (!ReferenceEquals (current, snapshot)) {
					templates = DotNetNewTemplateClassifier.Classify (current)
						.Where (IsNewProjectTemplate)
						.SelectMany (c => c.Languages.Count == 0
							? new[] { new DotNetNewSolutionTemplate (c, string.Empty) }
							: c.Languages.Select (language => new DotNetNewSolutionTemplate (c, language)))
						.ToList ();
					snapshot = current;
				}
				return templates;
			}
		}

		public IEnumerable<SolutionTemplate> GetTemplates ()
		{
			return GetSolutionTemplates ();
		}

		/// <summary>One top-level category (.NET) with a category per first tag segment (Common, Web, Test, Solution…).</summary>
		public IEnumerable<TemplateCategory> GetCategories ()
		{
			var top = new TemplateCategory (DotNetNewSolutionTemplate.TopLevelCategoryId, GettextCatalog.GetString (".NET"), "md-platform-netcore") {
				IsTopLevel = true
			};
			var names = GetSolutionTemplates ()
				.Select (t => t.Classification.Category)
				.Distinct (StringComparer.OrdinalIgnoreCase)
				.ToList ();
			names.Sort (DotNetNewTemplateClassifier.CompareCategories);
			foreach (var name in names) {
				var second = new TemplateCategory (DotNetNewSolutionTemplate.GetCategoryId (name), name, null);
				second.AddCategory (new TemplateCategory (DotNetNewSolutionTemplate.ThirdLevelCategoryId, GettextCatalog.GetString ("General"), null));
				top.AddCategory (second);
			}
			return new[] { top };
		}

		public bool CanProcessTemplate (SolutionTemplate template)
		{
			return template is DotNetNewSolutionTemplate;
		}

		public async Task<ProcessedTemplateResult> ProcessTemplate (SolutionTemplate template, NewProjectConfiguration config, SolutionFolder parentFolder)
		{
			var dotnetTemplate = (DotNetNewSolutionTemplate)template;
			var cancellationToken = CancellationToken.None;
			using (var monitor = new ProgressMonitor ()) {
				if (dotnetTemplate.Classification.Kind == DotNetNewTemplateKind.Solution) {
					var sln = await CreateSolutionAsync (dotnetTemplate.Template, config.SolutionLocation, config.SolutionName, cancellationToken);
					var solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (monitor, sln);
					return new DotNetNewProcessedTemplateResult (new IWorkspaceFileObject[] { solution }, sln, config.SolutionLocation, Array.Empty<string> ());
				}

				string projectDirectory = config.ProjectLocation;
				var existing = DotNetNewCli.GetFiles (projectDirectory);
				await DotNetNewCli.CreateAsync (dotnetTemplate.Template, projectDirectory, config.ProjectName, template.Language, cancellationToken);
				var created = DotNetNewCli.GetFiles (projectDirectory).Where (f => !existing.Contains (f)).OrderBy (f => f, StringComparer.Ordinal).ToList ();
				var projectFiles = created.Where (f => Services.ProjectService.IsSolutionItemFile (f)).ToList ();
				if (projectFiles.Count == 0)
					throw new UserException (GettextCatalog.GetString ("'dotnet new {0}' created no project file in {1}", dotnetTemplate.Template.ShortName, projectDirectory));
				var filesToOpen = GetFilesToOpen (projectDirectory, created);

				if (parentFolder == null) {
					var sln = await DotNetNewCli.CreateSolutionAsync (config.SolutionLocation, config.SolutionName, cancellationToken);
					await DotNetNewCli.AddToSolutionAsync (sln, projectFiles, cancellationToken);
					var solution = (Solution)await Services.ProjectService.ReadWorkspaceItem (monitor, sln);
					return new DotNetNewProcessedTemplateResult (new IWorkspaceFileObject[] { solution }, sln, projectDirectory, filesToOpen);
				}

				// The open solution is the IDE's: the New Project dialog adds the project to it and saves it.
				var items = new List<IWorkspaceFileObject> ();
				foreach (var file in projectFiles)
					items.Add (await ReadProjectAsync (monitor, file));
				return new DotNetNewProcessedTemplateResult (items.ToArray (), parentFolder.ParentSolution.FileName, projectDirectory, filesToOpen);
			}
		}

		/// <summary>The F# project type: the IDE has no F# binding yet (T154) and keeps such projects as unsupported ones.</summary>
		const string FSharpProjectTypeGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";

		/// <summary>
		/// Reads a new project. A project of a language the IDE cannot load yet (F#) is read with its project type, as the
		/// solution reader does, so that the solution keeps it.
		/// </summary>
		static async Task<SolutionItem> ReadProjectAsync (ProgressMonitor monitor, FilePath file)
		{
			var item = await Services.ProjectService.ReadSolutionItem (monitor, file);
			if (item == null && file.HasExtension (".fsproj")) {
				using (var context = new SolutionLoadContext (null))
					item = await Services.ProjectService.ReadSolutionItem (monitor, file, null, FSharpProjectTypeGuid, null, context);
			}
			if (item == null)
				throw new UserException (GettextCatalog.GetString ("The project {0} was created, but the IDE cannot load it", file.FileName));
			return item;
		}

		static readonly string[] SlnFormatOption = { "--format", "sln" };

		/// <summary>
		/// Runs a solution template: <c>--format sln</c> is passed because SDK 10 writes .slnx by default, which the
		/// project model does not read.
		/// </summary>
		internal static async Task<FilePath> CreateSolutionAsync (DotNetNewTemplate template, string directory, string name, CancellationToken cancellationToken)
		{
			await DotNetNewCli.CreateAsync (template, directory, name, null, cancellationToken, SlnFormatOption);
			var sln = new FilePath (directory).Combine (name + ".sln");
			if (!File.Exists (sln))
				throw new UserException (GettextCatalog.GetString ("'dotnet new {0}' did not create {1}", template.ShortName, sln));
			return sln;
		}

		/// <summary>The main source file of the new project (Program, then the first source file), relative to it.</summary>
		static string[] GetFilesToOpen (string projectDirectory, IEnumerable<string> created)
		{
			var sources = created
				.Where (f => Path.GetDirectoryName (f) == projectDirectory.TrimEnd (Path.DirectorySeparatorChar))
				.Where (f => f.EndsWith (".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith (".fs", StringComparison.OrdinalIgnoreCase))
				.OrderBy (f => Path.GetFileNameWithoutExtension (f) == "Program" ? 0 : 1)
				.ThenBy (f => f, StringComparer.Ordinal)
				.ToList ();
			return sources.Count > 0 ? new[] { Path.GetFileName (sources[0]) } : Array.Empty<string> ();
		}
	}

	/// <summary>The metadata of the "Template Instantiated" counter.</summary>
	class TemplateMetadata : CounterMetadata
	{
		public string Id {
			get => GetProperty<string> ();
			set => SetProperty (value);
		}

		public string Name {
			get => GetProperty<string> ();
			set => SetProperty (value);
		}

		public string Language {
			get => GetProperty<string> ();
			set => SetProperty (value);
		}

		public string Platform {
			get => GetProperty<string> ();
			set => SetProperty (value);
		}
	}

	sealed class DotNetNewProcessedTemplateResult : ProcessedTemplateResult
	{
		readonly IWorkspaceFileObject[] workspaceItems;
		readonly IReadOnlyList<string> filesToOpen;

		public DotNetNewProcessedTemplateResult (IWorkspaceFileObject[] workspaceItems, string solutionFileName, string projectBasePath, IReadOnlyList<string> filesToOpen)
		{
			this.workspaceItems = workspaceItems;
			this.filesToOpen = filesToOpen;
			SolutionFileName = solutionFileName;
			ProjectBasePath = projectBasePath;
		}

		public override IEnumerable<IWorkspaceFileObject> WorkspaceItems => workspaceItems;

		public override IEnumerable<string> Actions => filesToOpen;

		public override bool HasPackages () => false;

		public override IList<PackageReferencesForCreatedProject> PackageReferences => Array.Empty<PackageReferencesForCreatedProject> ();
	}
}
