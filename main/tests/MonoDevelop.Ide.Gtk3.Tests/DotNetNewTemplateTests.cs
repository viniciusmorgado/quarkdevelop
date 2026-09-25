//
// DotNetNewTemplateTests.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Ide.Templates;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// T152 (ADR 0026): the templates of <c>dotnet new</c>. The classification rules (C# and F# only, Linux only,
	/// categories from the first tag segment) run on the output of <c>dotnet new list --columns-all</c> captured from
	/// SDK 10.0.401 (TestData); the reader of the installed SDK's template packages must list what the CLI lists.
	/// </summary>
	[TestFixture]
	public class DotNetNewTemplateTests
	{
		const string Fixture = "dotnet-new-list-10.0.401.txt";

		static readonly string[] AllConsoleLanguages = { "C#", "F#", "VB" };
		static readonly string[] ConsoleTags = { "Common", "Console" };
		static readonly string[] GitignoreNames = { "gitignore", ".gitignore" };
		static readonly string[] NUnitTags = { "Test", "NUnit" };
		static readonly string[] SampleNames = { "sample", "s" };
		static readonly string[] CSharpAndFSharp = { "C#", "F#" };
		static readonly string[] SampleTags = { "Common", "Library", "Extra" };
		static readonly string[] CSharpOnly = { "C#" };
		static readonly string[] SortedCategories = { "Common", "Web", "Test", "Config", "MSBuild", "Solution", "Alpha", "Zeta" };
		static readonly string[] FSharpConsoleArguments = { "new", "console", "-o", "/p/App", "-n", "App", "--language", "F#" };
		static readonly string[] BlazorArguments = { "new", "blazor", "-o", "/p/Web", "-n", "Web" };
		static readonly string[] GitignoreArguments = { "new", "gitignore", "-o", "/p", "--project", "/p/App.csproj" };
		static readonly string[] ProjectOption = { "--project", "/p/App.csproj" };

		static readonly string[] IgnoreConstraints = { "new", "list", "--columns-all", "--ignore-constraints" };
		static readonly string[] ProjectItems = { "class", "interface", "enum", "record", "struct" };
		static readonly string[] CSharpCapability = { "CSharp" };

		static List<DotNetNewTemplate> ReadFixture ()
		{
			var path = Path.Combine (TestContext.CurrentContext.TestDirectory, "TestData", Fixture);
			return DotNetNewListParser.Parse (File.ReadAllText (path));
		}

		static DotNetNewTemplateClassification Classify (List<DotNetNewTemplate> templates, string shortName)
		{
			var template = templates.Single (t => t.ShortNames.Contains (shortName));
			return DotNetNewTemplateClassifier.Classify (template);
		}

		[Test]
		public void FixtureEveryRowIsRead ()
		{
			var templates = ReadFixture ();

			Assert.AreEqual (47, templates.Count);
			var console = templates.Single (t => t.ShortName == "console");
			Assert.AreEqual ("Console App", console.Name);
			CollectionAssert.AreEqual (AllConsoleLanguages, console.Languages);
			Assert.AreEqual ("project", console.Type);
			CollectionAssert.AreEqual (ConsoleTags, console.Tags);
			CollectionAssert.AreEqual (GitignoreNames, templates.Single (t => t.ShortName == "gitignore").ShortNames);
			CollectionAssert.IsEmpty (templates.Single (t => t.ShortName == "gitignore").Languages);
			CollectionAssert.AreEqual (NUnitTags, templates.Single (t => t.ShortName == "nunit-test").Tags);
		}

		[Test]
		public void ParserToleratesNoTableAndShortLastColumn ()
		{
			CollectionAssert.IsEmpty (DotNetNewListParser.Parse (null));
			CollectionAssert.IsEmpty (DotNetNewListParser.Parse ("No templates found matching: 'x'."));

			var text = "Template Name  Short Name  Language  Type     Author     Tags\n" +
				"-------------  ----------  --------  -------  ---------  ------------\n" +
				"Sample         sample,s    [C#],F#   project  Somebody   Common/Library/Extra\n";
			var sample = DotNetNewListParser.Parse (text).Single ();
			CollectionAssert.AreEqual (SampleNames, sample.ShortNames);
			CollectionAssert.AreEqual (CSharpAndFSharp, sample.Languages);
			CollectionAssert.AreEqual (SampleTags, sample.Tags);
		}

		[TestCase ("winforms")]
		[TestCase ("winformslib")]
		[TestCase ("winformscontrollib")]
		[TestCase ("wpf")]
		[TestCase ("wpflib")]
		[TestCase ("wpfcustomcontrollib")]
		[TestCase ("wpfusercontrollib")]
		[TestCase ("webconfig")]
		public void WindowsOnlyTemplatesAreHidden (string shortName)
		{
			var classification = Classify (ReadFixture (), shortName);

			Assert.AreEqual (DotNetNewHiddenReason.WindowsOnly, classification.HiddenReason);
			Assert.IsFalse (classification.IsVisible);
		}

		[Test]
		public void FutureTemplatesAreClassifiedByTagsAndConstraints ()
		{
			var winui = new DotNetNewTemplate { ShortNames = { "winui" }, Languages = { "C#" }, Type = "project", Tags = { "Common", "WinUI" } };
			var windowsOnly = new DotNetNewTemplate {
				ShortNames = { "winservice" },
				Languages = { "C#" },
				Type = "project",
				Tags = { "Common", "Service" },
				OperatingSystems = new List<string> { "Windows" }
			};
			var portable = new DotNetNewTemplate {
				ShortNames = { "tool" },
				Languages = { "C#" },
				Type = "project",
				Tags = { "Common", "Tool" },
				OperatingSystems = new List<string> { "Windows", "Linux", "OSX" }
			};
			var vbOnly = new DotNetNewTemplate { ShortNames = { "vbapp" }, Languages = { "VB" }, Type = "project", Tags = { "Common" } };
			var untagged = new DotNetNewTemplate { ShortNames = { "thing" }, Type = "item" };

			Assert.AreEqual (DotNetNewHiddenReason.WindowsOnly, DotNetNewTemplateClassifier.Classify (winui).HiddenReason);
			Assert.AreEqual (DotNetNewHiddenReason.WindowsOnly, DotNetNewTemplateClassifier.Classify (windowsOnly).HiddenReason);
			Assert.IsTrue (DotNetNewTemplateClassifier.Classify (portable).IsVisible);
			Assert.AreEqual (DotNetNewHiddenReason.UnsupportedLanguage, DotNetNewTemplateClassifier.Classify (vbOnly).HiddenReason);
			Assert.AreEqual (DotNetNewTemplateClassifier.OtherCategory, DotNetNewTemplateClassifier.Classify (untagged).Category);
			Assert.AreEqual (DotNetNewTemplateKind.Item, DotNetNewTemplateClassifier.Classify (untagged).Kind);
		}

		[Test]
		public void LanguagesAreCSharpThenFSharp ()
		{
			var templates = ReadFixture ();

			var console = Classify (templates, "console");
			CollectionAssert.AreEqual (CSharpAndFSharp, console.Languages);
			Assert.AreEqual ("C#", console.DefaultLanguage);
			CollectionAssert.AreEqual (CSharpOnly, Classify (templates, "blazor").Languages);
			CollectionAssert.IsEmpty (Classify (templates, "gitignore").Languages);
			Assert.AreEqual (string.Empty, Classify (templates, "gitignore").DefaultLanguage);

			foreach (var classification in DotNetNewTemplateClassifier.Classify (templates).Where (c => c.IsVisible))
				CollectionAssert.IsSubsetOf (classification.Languages, CSharpAndFSharp, classification.Template.ShortName);
		}

		[Test]
		public void CategoriesAreTheFirstTagSegment ()
		{
			var templates = ReadFixture ();

			Assert.AreEqual ("Common", Classify (templates, "mcpserver").Category, "Common/AI/MCP goes under Common");
			Assert.AreEqual ("Common", Classify (templates, "worker").Category);
			Assert.AreEqual ("Web", Classify (templates, "webapi").Category);
			Assert.AreEqual ("Test", Classify (templates, "nunit").Category);
			Assert.AreEqual ("Config", Classify (templates, "globaljson").Category);
			Assert.AreEqual ("MSBuild", Classify (templates, "buildprops").Category);
			Assert.AreEqual ("Solution", Classify (templates, "sln").Category);
			Assert.AreEqual (DotNetNewTemplateKind.Solution, Classify (templates, "sln").Kind, "dotnet new list shows no type for solutions");
			Assert.AreEqual (DotNetNewTemplateKind.Item, Classify (templates, "page").Kind);
			Assert.AreEqual (DotNetNewTemplateKind.Project, Classify (templates, "classlib").Kind);

			var order = new List<string> { "Web", "Solution", "Zeta", "Common", "MSBuild", "Config", "Alpha", "Test" };
			order.Sort (DotNetNewTemplateClassifier.CompareCategories);
			CollectionAssert.AreEqual (SortedCategories, order);
		}

		/// <summary>The counts of docs/evidence/M5/README.md (T152), per kind and category: shown / hidden (reason).</summary>
		[Test]
		public void CountsPerCategory ()
		{
			var classifications = DotNetNewTemplateClassifier.Classify (ReadFixture ()).ToList ();

			foreach (var row in GetCountRows (classifications))
				TestContext.Progress.WriteLine (row);

			Assert.AreEqual (39, classifications.Count (c => c.IsVisible));
			Assert.AreEqual (8, classifications.Count (c => !c.IsVisible));
			Assert.AreEqual (7, classifications.Count (c => c.HiddenReason == DotNetNewHiddenReason.WindowsOnly && c.Category == "Common"));
			Assert.AreEqual (4, classifications.Count (c => c.IsVisible && c.Kind == DotNetNewTemplateKind.Project && c.Category == "Common"));
			Assert.AreEqual (9, classifications.Count (c => c.IsVisible && c.Kind == DotNetNewTemplateKind.Project && c.Category == "Web"));
			Assert.AreEqual (5, classifications.Count (c => c.IsVisible && c.Kind == DotNetNewTemplateKind.Project && c.Category == "Test"));
		}

		/// <summary>Per kind and category: shown / hidden, with the hidden templates and why.</summary>
		static IEnumerable<string> GetCountRows (IEnumerable<DotNetNewTemplateClassification> classifications)
		{
			return classifications
				.GroupBy (c => (c.Kind, c.Category))
				.OrderBy (g => g.Key.Kind)
				.ThenBy (g => g.Key.Category, Comparer<string>.Create (DotNetNewTemplateClassifier.CompareCategories))
				.Select (g => $"{g.Key.Kind} {g.Key.Category}: shown {g.Count (c => c.IsVisible)}, hidden {g.Count (c => !c.IsVisible)}"
					+ string.Concat (g.Where (c => !c.IsVisible).Select (c => $" {c.Template.ShortName}={c.HiddenReason}"))
					+ string.Concat (g.Where (c => c.IsVisible && c.Template.NeedsProject).Select (c => $" {c.Template.ShortName}=needs a project")));
		}

		[Test]
		public void CliArgumentsHaveTheLanguageOnlyWhenTheTemplateHasSeveral ()
		{
			var templates = ReadFixture ();
			var console = templates.Single (t => t.ShortName == "console");
			var blazor = templates.Single (t => t.ShortName == "blazor");
			var gitignore = templates.Single (t => t.ShortName == "gitignore");
			gitignore.UsesName = false;

			CollectionAssert.AreEqual (FSharpConsoleArguments,
				DotNetNewCli.GetNewArguments (console, "/p/App", "App", "F#"));
			CollectionAssert.AreEqual (BlazorArguments,
				DotNetNewCli.GetNewArguments (blazor, "/p/Web", "Web", "C#"));
			CollectionAssert.AreEqual (GitignoreArguments,
				DotNetNewCli.GetNewArguments (gitignore, "/p", "Ignored", null, ProjectOption));
		}

		[Test]
		public void InstalledPackagesAreReadFromPackagesJson ()
		{
			var dir = Directory.CreateTempSubdirectory ("md-dotnetnew-").FullName;
			try {
				var package = Path.Combine (dir, "Sample.Templates.1.0.0.nupkg");
				File.WriteAllText (package, "");
				var json = Path.Combine (dir, "packages.json");
				File.WriteAllText (json, "﻿{\"Packages\":[{\"Details\":null,\"InstallerId\":\"f01dea33-e89c-46d1-89c2-1ca1f394c5aa\"," +
					"\"MountPointUri\":\"" + package + "\"},{\"MountPointUri\":\"/nonexistent/x.nupkg\"},{\"Other\":1}]}");

				CollectionAssert.AreEqual (new[] { package }, DotNetNewTemplateCatalog.ReadInstalledPackages (json));
				CollectionAssert.IsEmpty (DotNetNewTemplateCatalog.ReadInstalledPackages (Path.Combine (dir, "missing.json")));
				File.WriteAllText (json, "not json");
				CollectionAssert.IsEmpty (DotNetNewTemplateCatalog.ReadInstalledPackages (json));
			} finally {
				Directory.Delete (dir, true);
			}
		}

		static DotNetCoreSdkInfo RequireSdk ()
		{
			var sdk = DotNetCoreSdkInfo.FindDefault ();
			if (sdk == null)
				Assert.Ignore ("no .NET SDK");
			return sdk;
		}

		/// <summary>The template packages of the installed SDK, read in process, list the templates the CLI lists.</summary>
		[Test]
		public async Task EngineListsWhatTheCliListsAsync ()
		{
			var sdk = RequireSdk ();
			var packages = DotNetNewTemplateCatalog.FindTemplatePackages (sdk.DotNetRoot, sdk.Version, DotNetNewTemplateCatalog.GetCliHome ());
			Assert.That (packages, Has.Some.Contains ("microsoft.dotnet.common.projecttemplates"));

			var clock = Stopwatch.StartNew ();
			var engine = await DotNetNewTemplateCatalog.ScanAsync (packages, sdk.VersionString);
			await TestContext.Progress.WriteLineAsync ($"engine: {engine.Count} templates from {packages.Count} packages in {clock.ElapsedMilliseconds} ms");
			var cli = await DotNetNewTemplateCatalog.ListWithCliAsync ();
			var all = DotNetNewListParser.Parse ((await DotNetNewCli.RunAsync (null, IgnoreConstraints, CancellationToken.None, englishOutput: true)).Output);

			// dotnet new list shows the templates with project-capability constraints (class, interface…) only with a
			// project; they are listed with their constraints
			CollectionAssert.AreEquivalent (cli.Select (t => t.ShortName), engine.Where (t => !t.NeedsProject).Select (t => t.ShortName));
			CollectionAssert.AreEquivalent (all.Select (t => t.ShortName), engine.Select (t => t.ShortName));
			CollectionAssert.IsSubsetOf (ProjectItems, engine.Where (t => t.NeedsProject).Select (t => t.ShortName).ToList ());
			CollectionAssert.AreEqual (CSharpCapability, engine.Single (t => t.ShortName == "class").ProjectCapabilities["C#"]);
			foreach (var template in engine) {
				var row = all.Single (t => t.ShortName == template.ShortName);
				CollectionAssert.AreEquivalent (row.Languages, template.Languages, template.ShortName);
				CollectionAssert.AreEqual (row.Tags, template.Tags, template.ShortName);
				Assert.AreEqual (DotNetNewTemplateClassifier.Classify (row).HiddenReason, DotNetNewTemplateClassifier.Classify (template).HiddenReason, template.ShortName);
				Assert.IsFalse (template.Name.EndsWith ("...", StringComparison.Ordinal), template.Name);
			}

			foreach (var row in GetCountRows (DotNetNewTemplateClassifier.Classify (engine)))
				await TestContext.Progress.WriteLineAsync ("engine " + row);

			var console = engine.Single (t => t.ShortName == "console");
			Assert.IsNotEmpty (console.Description);
			Assert.IsTrue (console.UsesName);
			Assert.IsFalse (engine.Single (t => t.ShortName == "gitignore").UsesName, "a .gitignore has a fixed name");
			CollectionAssert.Contains (engine.Single (t => t.ShortName == "sln").SolutionFormats, "sln");
		}

		[Test]
		public async Task CatalogIsCachedPerSdkVersionAsync ()
		{
			var sdk = RequireSdk ();
			var dir = Directory.CreateTempSubdirectory ("md-dotnetnew-cache-").FullName;
			try {
				var first = new DotNetNewTemplateCatalog { CacheDirectory = () => dir };
				var templates = await first.GetTemplatesAsync ();
				Assert.AreEqual ("engine", first.LastSource);
				Assert.IsTrue (File.Exists (Path.Combine (dir, sdk.VersionString + ".json")));
				Assert.AreSame (templates, await first.GetTemplatesAsync (), "read once");

				var second = new DotNetNewTemplateCatalog { CacheDirectory = () => dir };
				var clock = Stopwatch.StartNew ();
				var cached = await second.GetTemplatesAsync ();
				await TestContext.Progress.WriteLineAsync ($"cache: {cached.Count} templates in {clock.ElapsedMilliseconds} ms");
				Assert.AreEqual ("cache", second.LastSource);
				CollectionAssert.AreEqual (templates.Select (t => t.ToString ()), cached.Select (t => t.ToString ()));

				// another fingerprint (a package changed): read again
				var packages = DotNetNewTemplateCatalog.FindTemplatePackages (sdk.DotNetRoot, sdk.Version, DotNetNewTemplateCatalog.GetCliHome ());
				var cacheFile = Path.Combine (dir, sdk.VersionString + ".json");
				Assert.IsNotNull (DotNetNewTemplateCatalog.ReadCache (cacheFile, DotNetNewTemplateCatalog.GetFingerprint (sdk.VersionString, packages)));
				Assert.IsNull (DotNetNewTemplateCatalog.ReadCache (cacheFile, DotNetNewTemplateCatalog.GetFingerprint (sdk.VersionString, packages.Skip (1))));
				await File.WriteAllTextAsync (cacheFile, "{ broken");
				Assert.IsNull (DotNetNewTemplateCatalog.ReadCache (cacheFile, "x"));

				second.Invalidate ();
				await second.GetTemplatesAsync ();
				Assert.AreEqual ("engine", second.LastSource);
			} finally {
				Directory.Delete (dir, true);
			}
		}

		[Test]
		public async Task CliErrorsAreReportedAsync ()
		{
			RequireSdk ();
			var dir = Directory.CreateTempSubdirectory ("md-dotnetnew-cli-").FullName;
			try {
				var missing = new DotNetNewTemplate { ShortNames = { "no-such-template-md" }, Languages = { "C#" } };
				var ex = Assert.ThrowsAsync<DotNetCliException> (() => DotNetNewCli.CreateAsync (missing, dir, "X", "C#", CancellationToken.None));
				StringAssert.Contains ("dotnet new no-such-template-md", ex.Message);
				Assert.AreNotEqual (0, ex.Result.ExitCode);
				Assert.IsNotEmpty (ex.Details);
				await Task.CompletedTask;
			} finally {
				Directory.Delete (dir, true);
			}
		}
	}
}
