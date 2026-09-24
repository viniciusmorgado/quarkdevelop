//
// RoslynClassificationScopeTests.cs
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
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Platform;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// Task T138: in a C# file of a project, Roslyn's classifications replace the grammar (HighlightUsagesExtension sets
	/// TagBasedSyntaxHighlighting). Every classification Roslyn gives to the C# 8 to 14 constructs of the Modern sample
	/// (main/tests/linux-smoke/Modern) must map to a theme scope, or the token is drawn in the plain text color.
	/// </summary>
	[TestFixture]
	public sealed class RoslynClassificationScopeTests
	{
		static readonly string[] SampleFiles = {
			"GlobalUsings.cs", "Program.cs", "Records.cs", "Patterns.cs", "Strings.cs", "Generics.cs", "Pointers.cs", "Extensions.cs",
		};

		readonly Dictionary<string, (SourceText Text, IReadOnlyList<ClassifiedSpan> Spans)> classified =
			new Dictionary<string, (SourceText, IReadOnlyList<ClassifiedSpan>)> ();

		[OneTimeSetUp]
		public void ClassifySample ()
		{
			// On the thread pool: the tests run on the GTK thread, whose synchronization context needs the main loop.
#pragma warning disable VSTHRD002 // nothing waits for the GTK thread here
			Task.Run (ClassifySampleAsync).GetAwaiter ().GetResult ();
#pragma warning restore VSTHRD002
		}

		async Task ClassifySampleAsync ()
		{
			var directory = FindSampleDirectory ();
			using var workspace = new AdhocWorkspace (MefHostServices.DefaultHost);
			var references = ((string)AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")).Split (Path.PathSeparator)
				.Where (path => Path.GetFileName (path).StartsWith ("System.", StringComparison.Ordinal))
				.Select (path => MetadataReference.CreateFromFile (path));
			var project = workspace.AddProject (ProjectInfo.Create (
				ProjectId.CreateNewId (), VersionStamp.Create (), "Modern", "Modern", LanguageNames.CSharp,
				compilationOptions: new CSharpCompilationOptions (OutputKind.ConsoleApplication, allowUnsafe: true),
				parseOptions: new CSharpParseOptions (LanguageVersion.Default),
				metadataReferences: references));
			foreach (var file in SampleFiles) {
				var document = workspace.AddDocument (project.Id, file, SourceText.From (await File.ReadAllTextAsync (Path.Combine (directory, file))));
				var text = await document.GetTextAsync ();
				var spans = await Classifier.GetClassifiedSpansAsync (document, new TextSpan (0, text.Length), default);
				classified[file] = (text, spans.ToList ());
			}
		}

		static string FindSampleDirectory ()
		{
			for (var directory = new DirectoryInfo (AppContext.BaseDirectory); directory != null; directory = directory.Parent) {
				var candidate = Path.Combine (directory.FullName, "tests", "linux-smoke", "Modern");
				if (File.Exists (Path.Combine (candidate, "Modern.csproj")))
					return candidate;
			}
			throw new DirectoryNotFoundException ("tests/linux-smoke/Modern not found above " + AppContext.BaseDirectory);
		}

		[TestCase ("Program.cs", "foreach (var line in lines)", "foreach", "keyword")]
		[TestCase ("Program.cs", "Person older = ada with { Born = 1814 };", "with", "keyword")]
		[TestCase ("Records.cs", "public record Person (string First", "record", "keyword")]
		[TestCase ("Records.cs", "public record Person (string First", "Person", "entity.name.class")]
		[TestCase ("Records.cs", "public readonly record struct Vector", "Vector", "entity.name.struct")]
		[TestCase ("Records.cs", "public required int Born { get; init; }", "required", "keyword")]
		[TestCase ("Records.cs", "public required int Born { get; init; }", "init", "keyword")]
		[TestCase ("Records.cs", "file sealed class Hidden", "file", "keyword")]
		[TestCase ("Records.cs", "nameof (Hidden)", "nameof", "keyword")]
		[TestCase ("Patterns.cs", "shape switch {", "switch", "keyword")]
		[TestCase ("Patterns.cs", "Circle { Radius: > 0 and < 10 } circle", "and", "keyword")]
		[TestCase ("Patterns.cs", "when width == height", "when", "keyword")]
		[TestCase ("Patterns.cs", "value is not null", "not", "keyword")]
		[TestCase ("Strings.cs", "{ \"name\": \"MonoDevelop\", \"empty\": \"\" }", "\"MonoDevelop\"", "string")]
		[TestCase ("Strings.cs", "\"MonoDevelop\"u8", "\"MonoDevelop\"", "string")]
		[TestCase ("Strings.cs", "\"MonoDevelop\"u8", "u8", "keyword")]
		[TestCase ("Generics.cs", "where T : IMeasured, allows ref struct", "allows", "keyword")]
		[TestCase ("Generics.cs", "where TKey : notnull", "notnull", "keyword")]
		[TestCase ("Generics.cs", "(scoped ReadOnlySpan<int> values)", "scoped", "keyword")]
		[TestCase ("Generics.cs", "Offset (nint start, nuint count)", "nint", "keyword")]
		[TestCase ("Generics.cs", "return total;", "return", "keyword")]
		[TestCase ("Pointers.cs", "delegate* managed<int, int> twice", "managed", "keyword")]
		[TestCase ("Pointers.cs", "where T : unmanaged", "unmanaged", "keyword")]
		[TestCase ("Extensions.cs", "extension(string text)", "extension", "keyword")]
		[TestCase ("Extensions.cs", "get => field;", "field", "keyword")]
		public void RoslynClassificationHasAThemeScope (string file, string lineText, string token, string scope)
		{
			var (text, spans) = classified[file];
			var line = text.Lines.FirstOrDefault (l => l.ToString ().Contains (lineText));
			Assert.That (line.Span.IsEmpty, Is.False, "no line contains: " + lineText);
			int start = line.Start + line.ToString ().IndexOf (lineText, StringComparison.Ordinal) + lineText.IndexOf (token, StringComparison.Ordinal);
			var tokenSpan = new TextSpan (start, token.Length);
			var names = spans
				.Where (s => s.TextSpan.OverlapsWith (tokenSpan) && !ClassificationTypeNames.AdditiveTypeNames.Contains (s.ClassificationType))
				.Select (s => s.ClassificationType)
				.Distinct ()
				.ToList ();
			Assert.That (names, Is.Not.Empty, $"Roslyn did not classify '{token}'");
			var map = TagBasedSyntaxHighlighting.CreateClassificationMap ("source.cs");
			foreach (var name in names) {
				Assert.IsTrue (map.TryGetValue (name, out var scopeStack), $"'{token}' is classified \"{name}\", which has no theme scope");
				Assert.That (scopeStack.Peek (), Does.StartWith (scope + "."), $"'{token}' is classified \"{name}\"");
			}
		}
	}
}
