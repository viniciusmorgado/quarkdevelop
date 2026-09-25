//
// DotNetNewTemplatingTests.cs
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
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// T152 (ADR 0026): the New Project dialog's templates are those of <c>dotnet new</c>, grouped by their first tag
	/// segment, in C# and F#; projects, solutions and items are created by the .NET CLI.
	/// </summary>
	[TestFixture]
	public class DotNetNewTemplatingTests : IdeTestBase
	{
		static readonly string[] CSharpAndFSharp = { "C#", "F#" };
		static readonly string[] FSharpOnly = { "F#" };
		static readonly string[] CSharpOnly = { "C#" };
		static readonly string[] ProjectCategories = { "Common", "Web", "Test", "Solution" };

		readonly List<string> directories = new List<string> ();
		readonly List<IDisposable> items = new List<IDisposable> ();

		[OneTimeSetUp]
		public void RequireSdk ()
		{
			if (DotNetCoreSdkInfo.FindDefault () == null)
				Assert.Ignore ("no .NET SDK");
		}

		[TearDown]
		public void DeleteDirectories ()
		{
			foreach (var item in items)
				item.Dispose ();
			items.Clear ();
			foreach (var dir in directories)
				Directory.Delete (dir, true);
			directories.Clear ();
		}

		string CreateDirectory ()
		{
			// outside the repository: its global.json, Directory.Build.props and NuGet.config must not apply
			var dir = Directory.CreateTempSubdirectory ("md-dotnetnew-").FullName;
			directories.Add (dir);
			return dir;
		}

		static IEnumerable<SolutionTemplate> GetTemplates (IEnumerable<TemplateCategory> categories)
		{
			return categories
				.SelectMany (top => top.Categories)
				.SelectMany (second => second.Categories)
				.SelectMany (third => third.Templates);
		}

		static SolutionTemplate FindTemplate (TemplatingService service, string shortName, string language)
		{
			var categories = service.GetProjectTemplateCategories (ProjectTemplateCategorizer.MatchNewSolutionTemplates);
			var group = GetTemplates (categories).Single (t => ((DotNetNewSolutionTemplate)t).Template.ShortNames.Contains (shortName));
			return group.GetTemplate (language) ?? group;
		}

		[Test]
		public void CategoriesAreTheFirstSegmentOfTheTags ()
		{
			var service = new TemplatingService ();
			var categories = service.GetProjectTemplateCategories (ProjectTemplateCategorizer.MatchNewSolutionTemplates).ToList ();

			var dotnet = categories.Single (c => c.Id == DotNetNewSolutionTemplate.TopLevelCategoryId);
			Assert.IsTrue (dotnet.IsTopLevel);
			var names = dotnet.Categories.Select (c => c.Name).ToList ();
			Assert.AreEqual ("Common", names[0]);
			CollectionAssert.IsSubsetOf (ProjectCategories, names);
			CollectionAssert.DoesNotContain (names, "Config", "items are New File templates");

			var templates = GetTemplates (categories).Cast<DotNetNewSolutionTemplate> ().ToList ();
			CollectionAssert.IsEmpty (templates.Where (t => t.Template.Tags.Contains ("WinForms") || t.Template.Tags.Contains ("WPF")));
			foreach (var template in templates)
				CollectionAssert.IsSubsetOf (template.AvailableLanguages, CSharpAndFSharp, template.Name);

			var console = templates.Single (t => t.Template.ShortName == "console");
			CollectionAssert.AreEquivalent (CSharpAndFSharp, console.AvailableLanguages);
			Assert.AreEqual ("dotnet/common/general", console.Category);
			Assert.AreEqual ("Microsoft.Common.Console.CSharp", console.GetTemplate ("C#").Id);
			Assert.AreEqual ("Microsoft.Common.Console.FSharp", console.GetTemplate ("F#").Id);
			Assert.AreEqual ("dotnet/common/general", templates.Single (t => t.Template.ShortName == "mcpserver").Category);

			var sln = templates.Single (t => t.Template.ShortName == "sln");
			Assert.IsFalse (sln.HasProjects);
			CollectionAssert.IsEmpty (templates.Where (t => t.Template.ShortName == "slnf"), "a solution filter needs a solution");

			// adding a project to a solution: no solution templates
			var newProject = service.GetProjectTemplateCategories (ProjectTemplateCategorizer.MatchNewProjectTemplates);
			CollectionAssert.DoesNotContain (newProject.Single ().Categories.Select (c => c.Name).ToList (), "Solution");
		}

		static async Task<string[]> ListSolutionAsync (FilePath sln)
		{
			var result = await DotNetNewCli.RunAsync (sln.ParentDirectory, new[] { "sln", sln.ToString (), "list" }, CancellationToken.None, englishOutput: true);
			Assert.AreEqual (0, result.ExitCode, result.Error);
			return result.Output.Split ('\n').Select (l => l.Trim ().Replace ('\\', '/')).Where (l => l.Length > 0).ToArray ();
		}

		/// <summary>New solution with a console project, then a class library added to it, in C# and in F#.</summary>
		[TestCase ("C#", ".csproj")]
		[TestCase ("F#", ".fsproj")]
		public async Task ConsoleAndLibraryAreCreatedWithTheCliAsync (string language, string extension)
		{
			var service = new TemplatingService ();
			var location = CreateDirectory ();
			var config = new NewProjectConfiguration {
				CreateSolution = true,
				CreateProjectDirectoryInsideSolutionDirectory = true,
				Location = location,
				ProjectName = "App",
				SolutionName = "App"
			};
			Directory.CreateDirectory (config.ProjectLocation);

			var result = await service.ProcessTemplate (FindTemplate (service, "console", language), config, null);

			var solution = (Solution)result.WorkspaceItems.Single ();
			items.Add (solution);
			Assert.AreEqual (Path.Combine (location, "App", "App.sln"), (string)solution.FileName);
			Assert.IsTrue (File.Exists (Path.Combine (config.ProjectLocation, "App" + extension)));
			CollectionAssert.AreEqual (new[] { language == "C#" ? "Program.cs" : "Program.fs" }, result.Actions);
			CollectionAssert.Contains (await ListSolutionAsync (solution.FileName), "App/App" + extension);

			// Add > New Project: the IDE adds the project to the open solution and saves it
			var libraryConfig = new NewProjectConfiguration {
				CreateSolution = false,
				Location = solution.BaseDirectory,
				ProjectName = "Lib",
				SolutionName = "App"
			};
			Directory.CreateDirectory (libraryConfig.ProjectLocation);
			var library = await service.ProcessTemplate (FindTemplate (service, "classlib", language), libraryConfig, solution.RootFolder);
			var project = (SolutionItem)library.WorkspaceItems.Single ();
			Assert.AreEqual ("Lib" + extension, project.FileName.FileName);
			solution.RootFolder.AddItem (project, true);
			await solution.SaveAsync (Util.GetMonitor ());

			var listed = await ListSolutionAsync (solution.FileName);
			CollectionAssert.Contains (listed, "App/App" + extension);
			CollectionAssert.Contains (listed, "Lib/Lib" + extension);
		}

		[Test]
		public async Task BlankSolutionIsASlnFileAsync ()
		{
			var service = new TemplatingService ();
			var location = CreateDirectory ();
			var config = new NewProjectConfiguration {
				CreateSolution = true,
				IsNewSolutionWithoutProjects = true,
				Location = location,
				SolutionName = "Blank"
			};

			var result = await service.ProcessTemplate (FindTemplate (service, "sln", null), config, null);

			var solution = (Solution)result.WorkspaceItems.Single ();
			items.Add (solution);
			Assert.AreEqual (Path.Combine (location, "Blank", "Blank.sln"), (string)solution.FileName);
			CollectionAssert.IsEmpty (solution.GetAllProjects ());
		}

		/// <summary>New File: .gitignore (fixed name), an NUnit test and a C# class (it needs a C# project).</summary>
		[Test]
		public async Task ItemsAreCreatedWithTheCliAsync ()
		{
			var dir = CreateDirectory ();
			var templates = await DotNetNewTemplateCatalog.Default.GetTemplatesAsync ();
			var console = templates.Single (t => t.ShortName == "console");
			await DotNetNewCli.CreateAsync (console, Path.Combine (dir, "App"), "App", "C#", CancellationToken.None);
			var project = (Project)await Services.ProjectService.ReadSolutionItem (Util.GetMonitor (), Path.Combine (dir, "App", "App.csproj"));
			items.Add (project);

			var available = DotNetNewItemTemplates.GetItemTemplates (templates);
			var gitignore = available.Single (c => c.Template.ShortName == "gitignore");
			var nunit = available.Single (c => c.Template.ShortName == "nunit-test");
			var @class = available.Single (c => c.Template.ShortName == "class");
			CollectionAssert.IsEmpty (available.Where (c => c.Template.ShortName == "webconfig"));
			CollectionAssert.AreEqual (new[] { string.Empty }, DotNetNewItemTemplates.GetLanguages (gitignore, project));
			CollectionAssert.AreEqual (CSharpOnly, DotNetNewItemTemplates.GetLanguages (nunit, project));
			// the C# NUnit test item has a CSharp project-capability constraint, the F# one has none
			CollectionAssert.AreEqual (FSharpOnly, DotNetNewItemTemplates.GetLanguages (nunit, null));
			CollectionAssert.AreEqual (CSharpOnly, DotNetNewItemTemplates.GetLanguages (@class, project));
			CollectionAssert.IsEmpty (DotNetNewItemTemplates.GetLanguages (@class, null), "class needs a C# project");
			Assert.AreEqual (true, DotNetNewItemTemplates.CanCreate (templates, "class", project));
			Assert.AreEqual (false, DotNetNewItemTemplates.CanCreate (templates, "class", null));
			Assert.IsNull (DotNetNewItemTemplates.CanCreate (templates, "EmptyClass", project));

			var created = await DotNetNewItemTemplates.CreateAsync (gitignore.Template, string.Empty, project.BaseDirectory, null, project, CancellationToken.None);
			CollectionAssert.AreEqual (new[] { project.BaseDirectory.Combine (".gitignore") }, created);

			created = await DotNetNewItemTemplates.CreateAsync (nunit.Template, "C#", project.BaseDirectory, "CalculatorTests", project, CancellationToken.None);
			CollectionAssert.AreEqual (new[] { project.BaseDirectory.Combine ("CalculatorTests.cs") }, created);
			Assert.IsNotNull (project.Files.GetFile (created[0]), "the SDK globs include the new file");

			created = await DotNetNewItemTemplates.CreateAsync (@class.Template, "C#", project.BaseDirectory, "Calculator", project, CancellationToken.None);
			CollectionAssert.AreEqual (new[] { project.BaseDirectory.Combine ("Calculator.cs") }, created);
			StringAssert.Contains ("class Calculator", await File.ReadAllTextAsync (created[0]));
			Assert.IsNotNull (project.Files.GetFile (created[0]));
		}
	}
}
