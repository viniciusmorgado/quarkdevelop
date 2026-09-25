//
// DotNetNewItemTemplates.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// The New File dialog's templates: the item templates of <c>dotnet new</c> (Razor Page, MVC Controller, NUnit Test
	/// Item, .gitignore, global.json, Directory.Build.props…), created with the CLI (T152, ADR 0026).
	/// </summary>
	static class DotNetNewItemTemplates
	{
		/// <summary>The visible item templates, by name.</summary>
		public static IReadOnlyList<DotNetNewTemplateClassification> GetItemTemplates (IEnumerable<DotNetNewTemplate> templates)
		{
			return DotNetNewTemplateClassifier.Classify (templates)
				.Where (c => c.IsVisible && c.Kind == DotNetNewTemplateKind.Item)
				.OrderBy (c => c.Template.Name, StringComparer.OrdinalIgnoreCase)
				.ToList ();
		}

		/// <summary>
		/// The languages to offer for an item added to <paramref name="project"/>: the project's own when supported. An
		/// item with <c>project-capability</c> constraints is offered only for a project that has the capabilities.
		/// </summary>
		public static IReadOnlyList<string> GetLanguages (DotNetNewTemplateClassification item, Project project)
		{
			IEnumerable<string> languages = item.Languages.Count == 0 ? new[] { string.Empty } : item.Languages;
			string projectLanguage = GetProjectLanguage (project);
			if (projectLanguage != null && item.Languages.Count > 0)
				languages = languages.Where (l => l == projectLanguage);
			return languages.Where (l => HasCapabilities (item.Template, l, project)).ToList ();
		}

		static bool HasCapabilities (DotNetNewTemplate template, string language, Project project)
		{
			if (template.ProjectCapabilities == null || !template.ProjectCapabilities.TryGetValue (language, out var capabilities))
				return true;
			return project != null && capabilities.All (capability => project.IsCapabilityMatch (capability));
		}

		/// <summary>
		/// True when the item template with this short name (or identity) can be added to the project; null when no
		/// such template is known (or the templates are still being read).
		/// </summary>
		public static bool? CanCreate (IReadOnlyList<DotNetNewTemplate> templates, string id, Project project)
		{
			if (templates == null || string.IsNullOrEmpty (id))
				return null;
			var item = GetItemTemplates (templates).FirstOrDefault (c =>
				c.Template.Identity == id || c.Template.ShortNames.Contains (id, StringComparer.OrdinalIgnoreCase));
			if (item == null)
				return null;
			return GetLanguages (item, project).Count > 0;
		}

		/// <summary>C# for .csproj, F# for .fsproj, null otherwise.</summary>
		public static string GetProjectLanguage (Project project)
		{
			if (project == null)
				return null;
			if (project is DotNetProject dotNetProject && DotNetNewTemplateClassifier.SupportedLanguages.Contains (dotNetProject.LanguageName))
				return dotNetProject.LanguageName;
			switch (project.FileName.Extension.ToLowerInvariant ()) {
			case ".csproj":
				return "C#";
			case ".fsproj":
				return "F#";
			}
			return null;
		}

		/// <summary>
		/// Runs <c>dotnet new &lt;item&gt; -o &lt;directory&gt; -n &lt;name&gt;</c> and returns the files it wrote. For an item
		/// of a project, the project is re-evaluated (SDK-style globs include the new files) and the source files the
		/// globs do not include (F# projects list their sources) are added to it.
		/// </summary>
		public static async Task<IReadOnlyList<FilePath>> CreateAsync (DotNetNewTemplate template, string language, FilePath directory, string name,
			Project project, CancellationToken cancellationToken)
		{
			var existing = DotNetNewCli.GetFiles (directory);
			var options = project != null ? new[] { "--project", project.FileName.ToString () } : null;
			await DotNetNewCli.CreateAsync (template, directory, name, language, cancellationToken, options);
			var created = DotNetNewCli.GetFiles (directory)
				.Where (f => !existing.Contains (f))
				.OrderBy (f => f, StringComparer.Ordinal)
				.Select (f => new FilePath (f))
				.ToList ();

			if (project != null && created.Count > 0) {
				using (var monitor = new ProgressMonitor ()) {
					await project.ReevaluateProject (monitor);
					var missing = created
						.Where (f => f.IsChildPathOf (project.BaseDirectory) && project.Files.GetFile (f) == null && project.IsCompileable (f))
						.ToList ();
					if (missing.Count > 0) {
						foreach (var file in missing)
							project.AddFile (file);
						await project.SaveAsync (monitor);
					}
				}
			}
			return created;
		}
	}
}
