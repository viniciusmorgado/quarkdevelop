//
// MicrosoftTemplateEngineTests.cs
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
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using MonoDevelop.Ide.Projects;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// MonoDevelop.Ide.Templates.MicrosoftTemplateEngine on Microsoft.TemplateEngine 10: a template folder (or the same
	/// folder packed as a .nupkg) is scanned, its metadata and parameters listed, and it is instantiated into a directory
	/// with its primary outputs and post actions reported.
	/// The engine classes are internal (and still excluded from the GTK3 build by Gtk3PortPending.props), so they are
	/// reached by reflection; the tests are ignored while the classes are not part of MonoDevelop.Ide.
	/// </summary>
	[TestFixture]
	public class MicrosoftTemplateEngineTests
	{
		const string TemplateId = "MonoDevelop.Ide.Gtk3.Tests.Sample.CSharp";
		static readonly Guid OpenFilesPostActionId = new Guid ("84C0DA21-51C8-4541-9940-6CA19AF04EE6");
		static readonly string[] ExpectedClassifications = { "Common", "Console" };
		static readonly string[] ExpectedPrimaryOutputs = { "MyConsole.csproj", "Program.cs" };
		static readonly Type[] InstantiateProjectSignature = {
			typeof (ITemplateInfo), typeof (NewProjectConfiguration), typeof (IReadOnlyDictionary<string, string>)
		};

		Type engineType;
		string tempDirectory;

		[SetUp]
		public void SetUp ()
		{
			engineType = typeof (NewProjectConfiguration).Assembly.GetType ("MonoDevelop.Ide.Templates.MicrosoftTemplateEngine");
			if (engineType == null)
				Assert.Ignore ("MicrosoftTemplateEngine is not part of the MonoDevelop.Ide build yet (Gtk3PortPending.props).");

			tempDirectory = Path.Combine (Path.GetTempPath (), "md-te-tests-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			if (tempDirectory != null && Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, true);
		}

		[Test]
		public void TemplateFolderIsListedWithItsMetadata ()
		{
			string source = CreateTemplateFolder ("folder");
			object template = CreateProjectTemplate (source);
			var templateInfo = GetTemplateInfo (template);

			Assert.AreEqual (TemplateId, templateInfo.Identity);
			Assert.AreEqual ("Sample console", templateInfo.Name);
			Assert.AreEqual ("MonoDevelop.Ide.Gtk3.Tests.Sample", templateInfo.GroupIdentity);
			CollectionAssert.AreEqual (ExpectedClassifications, templateInfo.Classifications);
			Assert.AreEqual ("C#", CallEngine ("GetLanguage", templateInfo));

			Assert.IsTrue ((bool)Call (template, "IsSupportedParameter", "Framework"));
			Assert.IsTrue ((bool)Call (template, "IsSupportedParameter", "Greeting"));
			Assert.IsFalse ((bool)Call (template, "IsSupportedParameter", "Unknown"));

			var choices = (IReadOnlyDictionary<string, string>)Call (template, "GetParameterChoices", "Framework");
			Assert.AreEqual (2, choices.Count);
			Assert.AreEqual ("Target .NET 8", choices["net8.0"]);
			Assert.AreEqual ("Target .NET 10", choices["net10.0"]);
			Assert.IsNull (Call (template, "GetParameterChoices", "Unknown"));
		}

		[Test]
		public void DefaultParametersAreMergedWithTheTemplateDefaults ()
		{
			object template = CreateProjectTemplate (CreateTemplateFolder ("folder"));
			var templateInfo = GetTemplateInfo (template);

			// Only the non-choice parameters with a default value (not the implicit 'name' parameter).
			Assert.AreEqual ("Greeting=Hello", CallEngine ("MergeDefaultParameters", null, templateInfo));
			Assert.AreEqual ("Greeting=Hi", CallEngine ("MergeDefaultParameters", "Greeting=Hi", templateInfo));
			Assert.AreEqual ("Framework=net10.0,Greeting=Hello", CallEngine ("MergeDefaultParameters", "Framework=net10.0", templateInfo));
		}

		[Test]
		public void TemplateFilesCanBeRead ()
		{
			object template = CreateProjectTemplate (CreateTemplateFolder ("folder"));
			var templateInfo = GetTemplateInfo (template);

			using (var stream = (Stream)CallEngine ("GetStream", templateInfo, "${TemplateConfigDirectory}/template.json")) {
				Assert.IsNotNull (stream);
				using (var reader = new StreamReader (stream))
					StringAssert.Contains (TemplateId, reader.ReadToEnd ());
			}
			Assert.IsNull (CallEngine ("GetStream", templateInfo, "${TemplateConfigDirectory}/missing.json"));
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task TemplateIsInstantiatedWithParametersAsync (bool packed)
		{
			string source = CreateTemplateFolder ("folder");
			if (packed) {
				string nupkg = Path.Combine (tempDirectory, "Sample.Templates.1.0.0.nupkg");
				await ZipFile.CreateFromDirectoryAsync (source, nupkg);
				Directory.Delete (source, true);
				source = nupkg;
			}
			object template = CreateProjectTemplate (source);
			var templateInfo = GetTemplateInfo (template);

			var config = new NewProjectConfiguration {
				CreateSolution = false,
				CreateProjectDirectoryInsideSolutionDirectory = false,
				Location = Path.Combine (tempDirectory, "output"),
				ProjectName = "MyConsole",
			};
			Directory.CreateDirectory (config.ProjectLocation);
			var parameters = new Dictionary<string, string> {
				{ "Framework", "net10.0" },
				{ "Greeting", "Hi" },
			};

			var task = (Task<ITemplateCreationResult>)engineType
				.GetMethod ("InstantiateAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, InstantiateProjectSignature, null)
				.Invoke (null, new object[] { templateInfo, config, parameters });
			var result = await task;

			Assert.AreEqual (CreationResultStatus.Success, result.Status, result.ErrorMessage);
			CollectionAssert.AreEqual (ExpectedPrimaryOutputs,
				result.CreationResult.PrimaryOutputs.Select (output => (string)CallEngine ("GetPath", output)));

			var postAction = result.CreationResult.PostActions.Single ();
			Assert.AreEqual (OpenFilesPostActionId, postAction.ActionId);
			Assert.AreEqual ("1", postAction.Args["files"]);

			string project = await File.ReadAllTextAsync (Path.Combine (config.ProjectLocation, "MyConsole.csproj"));
			StringAssert.Contains ("<TargetFramework>net10.0</TargetFramework>", project);
			string program = await File.ReadAllTextAsync (Path.Combine (config.ProjectLocation, "Program.cs"));
			StringAssert.Contains ("Hi from MyConsole", program);
		}

		object CreateProjectTemplate (string scanPath)
		{
			object template = CallEngine ("CreateProjectTemplate", TemplateId, scanPath);
			Assert.IsNotNull (template);
			Assert.IsNotNull (GetTemplateInfo (template), "template not found in " + scanPath);
			return template;
		}

		static ITemplateInfo GetTemplateInfo (object template)
		{
			return (ITemplateInfo)template.GetType ()
				.GetField ("templateInfo", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				.GetValue (template);
		}

		object CallEngine (string name, params object[] args)
		{
			var method = engineType.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.Single (m => m.Name == name && m.GetParameters ().Length == args.Length);
			return method.Invoke (null, args);
		}

		static object Call (object target, string name, params object[] args)
		{
			return target.GetType ().GetMethod (name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke (target, args);
		}

		string CreateTemplateFolder (string name)
		{
			string root = Path.Combine (tempDirectory, name);
			Directory.CreateDirectory (Path.Combine (root, ".template.config"));
			File.WriteAllText (Path.Combine (root, ".template.config", "template.json"), @"{
	""$schema"": ""http://json.schemastore.org/template"",
	""author"": ""MonoDevelop"",
	""classifications"": [ ""Common"", ""Console"" ],
	""name"": ""Sample console"",
	""groupIdentity"": ""MonoDevelop.Ide.Gtk3.Tests.Sample"",
	""identity"": """ + TemplateId + @""",
	""shortName"": ""mdsample"",
	""tags"": { ""language"": ""C#"", ""type"": ""project"" },
	""sourceName"": ""SampleApp"",
	""symbols"": {
		""Framework"": {
			""type"": ""parameter"",
			""datatype"": ""choice"",
			""choices"": [
				{ ""choice"": ""net8.0"", ""description"": ""Target .NET 8"" },
				{ ""choice"": ""net10.0"", ""description"": ""Target .NET 10"" }
			],
			""defaultValue"": ""net8.0"",
			""replaces"": ""FRAMEWORK""
		},
		""Greeting"": {
			""type"": ""parameter"",
			""datatype"": ""text"",
			""defaultValue"": ""Hello"",
			""replaces"": ""GREETING""
		}
	},
	""primaryOutputs"": [
		{ ""path"": ""SampleApp.csproj"" },
		{ ""path"": ""Program.cs"" }
	],
	""postActions"": [
		{
			""description"": ""Opens files in the editor"",
			""manualInstructions"": [],
			""actionId"": ""84C0DA21-51C8-4541-9940-6CA19AF04EE6"",
			""args"": { ""files"": ""1"" },
			""continueOnError"": true
		}
	]
}
");
			File.WriteAllText (Path.Combine (root, "SampleApp.csproj"), @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>FRAMEWORK</TargetFramework>
  </PropertyGroup>
</Project>
");
			File.WriteAllText (Path.Combine (root, "Program.cs"), "System.Console.WriteLine (\"GREETING from SampleApp\");\n");
			return root;
		}
	}
}
