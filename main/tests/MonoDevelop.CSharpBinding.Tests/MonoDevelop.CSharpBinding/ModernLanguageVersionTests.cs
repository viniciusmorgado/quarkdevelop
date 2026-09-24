//
// ModernLanguageVersionTests.cs
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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MonoDevelop.CSharp.Project;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;
using NUnit.Framework;
using Solution = MonoDevelop.Projects.Solution;
using UnitTests;

namespace MonoDevelop.CSharpBinding
{
	/// <summary>
	/// Task T138: the IDE parses a net10.0 project without LangVersion as the SDK compiles it, with C# 14 (the SDK sets
	/// LangVersion from the target framework). An older language version would report the C# 8 to 14 constructs of
	/// main/tests/linux-smoke/Modern as errors in the editor.
	/// </summary>
	[TestFixture]
	public sealed class ModernLanguageVersionTests : IdeTestBase
	{
		static async Task<(Solution Solution, DotNetProject Project)> LoadModernSampleAsync ()
		{
			var source = Path.Combine (Util.TestsRootDir, "linux-smoke");
			var directory = Util.CreateTmpDir ("modern-language-version");
			File.Copy (Path.Combine (source, "Modern.sln"), Path.Combine (directory, "Modern.sln"), true);
			Directory.CreateDirectory (Path.Combine (directory, "Modern"));
			foreach (var file in Directory.GetFiles (Path.Combine (source, "Modern")))
				File.Copy (file, Path.Combine (directory, "Modern", Path.GetFileName (file)), true);
			StringAssert.DoesNotContain ("LangVersion", await File.ReadAllTextAsync (Path.Combine (directory, "Modern", "Modern.csproj")));

			var solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), Path.Combine (directory, "Modern.sln"));
			return (solution, solution.GetAllProjects ().OfType<DotNetProject> ().Single ());
		}

		[Test]
		public async Task CompilerParametersOfNet10ProjectWithoutLangVersionParseCSharp14Async ()
		{
			var (solution, project) = await LoadModernSampleAsync ();
			using (solution) {
				var configuration = project.Configurations.OfType<DotNetProjectConfiguration> ().First (c => c.Name == "Debug");
				var parameters = (CSharpCompilerParameters)configuration.CompilationParameters;
				Assert.AreEqual (LanguageVersion.CSharp14, parameters.LangVersion.MapSpecifiedToEffectiveVersion ());
				var options = (CSharpParseOptions)parameters.CreateParseOptions (configuration);
				Assert.AreEqual (LanguageVersion.CSharp14, options.LanguageVersion);
			}
		}

		[Test]
		public async Task WorkspaceProjectOfNet10ProjectWithoutLangVersionParsesCSharp14Async ()
		{
			var (solution, project) = await LoadModernSampleAsync ();
			using (solution) {
				await TypeSystemServiceTestExtensions.LoadSolution (solution);
				try {
					var roslynProject = await IdeApp.TypeSystemService.GetCodeAnalysisProjectAsync (project);
					Assert.IsNotNull (roslynProject);
					var options = (CSharpParseOptions)roslynProject.ParseOptions;
					Assert.AreEqual (LanguageVersion.CSharp14, options.LanguageVersion);
					// with these options, extension blocks and the field keyword (C# 14) parse without errors
					var text = await File.ReadAllTextAsync (project.BaseDirectory.Combine ("Extensions.cs"));
					var tree = CSharpSyntaxTree.ParseText (text, options);
					Assert.IsEmpty (tree.GetDiagnostics ().Where (d => d.Severity == DiagnosticSeverity.Error).Select (d => d.ToString ()));
				} finally {
					TypeSystemServiceTestExtensions.UnloadSolution (solution);
				}
			}
		}
	}
}
