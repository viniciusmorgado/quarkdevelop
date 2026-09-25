//
// DotNetNewTemplate.cs
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
using System.Text.Json.Serialization;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// A template of <c>dotnet new</c> with all its languages: one row of <c>dotnet new list</c> (T152, ADR 0026).
	/// The IDE lists these templates and creates projects and items from them by running the CLI.
	/// </summary>
	sealed class DotNetNewTemplate
	{
		/// <summary>The group identity of the template, or its identity when it has no group.</summary>
		public string Identity { get; set; }

		public string Name { get; set; }

		public string Description { get; set; }

		public string Author { get; set; }

		/// <summary>The names <c>dotnet new</c> accepts for the template; the first one is used to create it.</summary>
		public List<string> ShortNames { get; set; } = new List<string> ();

		/// <summary>The languages of the template, in the order the SDK lists them; empty when it has no language.</summary>
		public List<string> Languages { get; set; } = new List<string> ();

		/// <summary><c>project</c>, <c>item</c> or <c>solution</c> (the <c>type</c> tag of the template).</summary>
		public string Type { get; set; }

		/// <summary>The classifications of the template, shown by <c>dotnet new list</c> as its tags (e.g. Common/Console).</summary>
		public List<string> Tags { get; set; } = new List<string> ();

		/// <summary>
		/// True when <c>-n</c> names the output (the template has a <c>sourceName</c>); items such as .gitignore or
		/// global.json have a fixed file name.
		/// </summary>
		public bool UsesName { get; set; } = true;

		/// <summary>The name of the template's output when none is given (its sourceName, e.g. Class1); may be null.</summary>
		public string DefaultName { get; set; }

		/// <summary>The operating systems an <c>os</c> constraint of the template allows; null when it has none.</summary>
		public List<string> OperatingSystems { get; set; }

		/// <summary>
		/// The <c>project-capability</c> constraints, per language: e.g. the C# class, interface, enum, record and struct
		/// items need a project with the CSharp capability, and <c>dotnet new list</c> shows them only with a project.
		/// Null when the template has none.
		/// </summary>
		public Dictionary<string, List<string>> ProjectCapabilities { get; set; }

		/// <summary>
		/// True when every language of the template has <c>project-capability</c> constraints: the template needs a
		/// project, and <c>dotnet new list</c> does not show it without one.
		/// </summary>
		[JsonIgnore]
		public bool NeedsProject => ProjectCapabilities != null
			&& (Languages.Count == 0 ? ProjectCapabilities.ContainsKey (string.Empty) : Languages.All (ProjectCapabilities.ContainsKey));

		/// <summary>
		/// The choices of the template's <c>format</c> parameter (sln, slnx) for solution templates; null when it has
		/// none. The New Project dialog offers the solution templates that can write a .sln file.
		/// </summary>
		public List<string> SolutionFormats { get; set; }

		[JsonIgnore]
		public string ShortName => ShortNames.Count > 0 ? ShortNames[0] : null;

		public override string ToString () => $"{ShortName} ({Name}; {string.Join (",", Languages)}; {Type}; {string.Join ("/", Tags)})";
	}

	enum DotNetNewTemplateKind
	{
		Project,
		Item,
		Solution
	}

	enum DotNetNewHiddenReason
	{
		None,
		/// <summary>The template has none of the supported languages (e.g. Visual Basic only).</summary>
		UnsupportedLanguage,
		/// <summary>The template builds or runs on Windows only (Windows Forms, WPF, IIS web.config).</summary>
		WindowsOnly
	}

	/// <summary>How the IDE shows a <see cref="DotNetNewTemplate"/>.</summary>
	sealed class DotNetNewTemplateClassification
	{
		public DotNetNewTemplate Template { get; set; }

		public DotNetNewTemplateKind Kind { get; set; }

		/// <summary>The first segment of the template's tags (Common, Web, Test, Config, MSBuild, Solution…).</summary>
		public string Category { get; set; }

		/// <summary>The supported languages the template offers, C# first; empty when the template has no language.</summary>
		public IReadOnlyList<string> Languages { get; set; }

		public DotNetNewHiddenReason HiddenReason { get; set; }

		public bool IsVisible => HiddenReason == DotNetNewHiddenReason.None;

		public string DefaultLanguage => Languages.Count > 0 ? Languages[0] : string.Empty;
	}

	/// <summary>
	/// The rules deciding which <c>dotnet new</c> templates the IDE shows (T152): C# and F# only, Linux only, grouped by
	/// the first segment of their tags.
	/// </summary>
	static class DotNetNewTemplateClassifier
	{
		/// <summary>The languages the IDE offers, the default first.</summary>
		public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "C#", "F#" };

		/// <summary>Tag segments of templates that only build or run on Windows.</summary>
		static readonly string[] windowsOnlyTags = { "WinForms", "Windows Forms", "WPF", "WinUI", "UWP" };

		/// <summary>
		/// Windows-only templates whose tags cannot tell: <c>webconfig</c> is an IIS configuration file tagged Config.
		/// </summary>
		static readonly string[] windowsOnlyShortNames = { "webconfig" };

		public const string OtherCategory = "Other";
		public const string SolutionCategory = "Solution";

		/// <summary>The order of the categories the SDK has today; others follow in alphabetical order.</summary>
		static readonly string[] categoryOrder = { "Common", "Web", "Test", "Config", "MSBuild", SolutionCategory };

		public static DotNetNewTemplateClassification Classify (DotNetNewTemplate template)
		{
			ArgumentNullException.ThrowIfNull (template);

			var classification = new DotNetNewTemplateClassification {
				Template = template,
				Category = GetCategory (template),
				Kind = GetKind (template),
				Languages = SupportedLanguages.Where (language => template.Languages.Contains (language, StringComparer.OrdinalIgnoreCase)).ToList ()
			};

			if (IsWindowsOnly (template))
				classification.HiddenReason = DotNetNewHiddenReason.WindowsOnly;
			else if (template.Languages.Count > 0 && classification.Languages.Count == 0)
				classification.HiddenReason = DotNetNewHiddenReason.UnsupportedLanguage;

			return classification;
		}

		public static IEnumerable<DotNetNewTemplateClassification> Classify (IEnumerable<DotNetNewTemplate> templates)
		{
			return templates.Select (Classify);
		}

		public static string GetCategory (DotNetNewTemplate template)
		{
			string first = template.Tags.Select (tag => tag?.Trim ()).FirstOrDefault (tag => !string.IsNullOrEmpty (tag));
			return first ?? OtherCategory;
		}

		public static DotNetNewTemplateKind GetKind (DotNetNewTemplate template)
		{
			switch (template.Type?.Trim ().ToLowerInvariant ()) {
			case "project":
				return DotNetNewTemplateKind.Project;
			case "solution":
				return DotNetNewTemplateKind.Solution;
			case "item":
				return DotNetNewTemplateKind.Item;
			}
			// dotnet new list shows no type for the solution templates
			return string.Equals (GetCategory (template), SolutionCategory, StringComparison.OrdinalIgnoreCase)
				? DotNetNewTemplateKind.Solution
				: DotNetNewTemplateKind.Item;
		}

		/// <summary>
		/// True for Windows Forms and WPF (by their tags), for templates whose <c>os</c> constraint excludes Linux, and
		/// for the few templates of <see cref="windowsOnlyShortNames"/>.
		/// </summary>
		public static bool IsWindowsOnly (DotNetNewTemplate template)
		{
			if (template.Tags.Any (tag => windowsOnlyTags.Contains (tag?.Trim (), StringComparer.OrdinalIgnoreCase)))
				return true;
			if (template.OperatingSystems != null && template.OperatingSystems.Count > 0
				&& !template.OperatingSystems.Contains ("Linux", StringComparer.OrdinalIgnoreCase))
				return true;
			return template.ShortNames.Any (name => windowsOnlyShortNames.Contains (name, StringComparer.OrdinalIgnoreCase));
		}

		/// <summary>Sorts category names: the known SDK categories first, in <see cref="categoryOrder"/>, then by name.</summary>
		public static int CompareCategories (string x, string y)
		{
			int ix = Array.FindIndex (categoryOrder, c => string.Equals (c, x, StringComparison.OrdinalIgnoreCase));
			int iy = Array.FindIndex (categoryOrder, c => string.Equals (c, y, StringComparison.OrdinalIgnoreCase));
			if (ix < 0)
				ix = categoryOrder.Length;
			if (iy < 0)
				iy = categoryOrder.Length;
			return ix != iy ? ix.CompareTo (iy) : string.Compare (x, y, StringComparison.OrdinalIgnoreCase);
		}
	}
}
