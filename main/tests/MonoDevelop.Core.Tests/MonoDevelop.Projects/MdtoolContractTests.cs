//
// MdtoolContractTests.cs
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
using System.Diagnostics;
using System.IO;
using MonoDevelop.Core.Assemblies;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Projects
{
	/// <summary>
	/// Task T064: the mdtool command-line contract (specs/001-linux-dotnet10-migration/contracts/mdtool-cli.md),
	/// exercised against the real main/build/bin/mdtool.dll and the linux-smoke sample projects.
	/// </summary>
	[TestFixture]
	[Category ("Integration")]
	[NonParallelizable]
	public class MdtoolContractTests
	{
		string mdtool;
		string smokeDir;
		string workDir;

		[OneTimeSetUp]
		public void FindTool ()
		{
			var mainDir = Path.GetFullPath (Path.Combine (Util.TestsRootDir, ".."));
			mdtool = Path.Combine (mainDir, "build", "bin", "mdtool.dll");
			if (!File.Exists (mdtool))
				Assert.Ignore ("mdtool is not built: " + mdtool);
			// Work on a copy so outputs do not pollute the source tree.
			workDir = Path.Combine (Path.GetTempPath (), "md-mdtool-contract-" + Guid.NewGuid ());
			CopyDirectory (Path.Combine (mainDir, "tests", "linux-smoke"), workDir);
			smokeDir = workDir;
		}

		[OneTimeTearDown]
		public void Cleanup ()
		{
			if (workDir != null && Directory.Exists (workDir))
				Directory.Delete (workDir, true);
		}

		(int exitCode, string output) Run (params string [] args) => RunWithEnvironment (null, args);

		(int exitCode, string output) RunWithEnvironment (System.Collections.Generic.IDictionary<string, string> environment, params string [] args)
		{
			var psi = new ProcessStartInfo (DotNetCoreSdkInfo.GetDotNetHostPath ()) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = smokeDir,
			};
			psi.ArgumentList.Add (mdtool);
			foreach (var a in args)
				psi.ArgumentList.Add (a);
			// Isolated profile and add-in registry: the test host points both at main/tests/config, and an
			// mdtool sharing that registry rewrites it with its own add-in set under the running tests.
			var profile = Path.Combine (workDir, ".profile");
			psi.Environment ["MONODEVELOP_PROFILE"] = profile;
			psi.Environment ["MONO_ADDINS_REGISTRY"] = profile;
			psi.Environment ["XDG_CONFIG_HOME"] = profile;
			if (environment != null) {
				foreach (var e in environment) {
					if (e.Value == null)
						psi.Environment.Remove (e.Key);
					else
						psi.Environment [e.Key] = e.Value;
				}
			}
			using (var p = Process.Start (psi)) {
				var stdout = p.StandardOutput.ReadToEndAsync ();
				var stderr = p.StandardError.ReadToEndAsync ();
				Assert.IsTrue (p.WaitForExit (300000), "mdtool timed out: " + string.Join (" ", args));
				return (p.ExitCode, stdout.Result + stderr.Result);
			}
		}

		[Test]
		public void ListsTools ()
		{
			var (exitCode, output) = Run ("-q");
			Assert.AreEqual (0, exitCode, output);
			StringAssert.Contains ("- build:", output);
		}

		[Test]
		public void BuildsProjectAndItsReferences ()
		{
			var (exitCode, output) = Run ("build", "Hello/Hello.csproj");
			Assert.AreEqual (0, exitCode, output);
			Assert.IsTrue (File.Exists (Path.Combine (smokeDir, "Hello", "bin", "Debug", "net10.0", "Hello.dll")), output);
			Assert.IsTrue (File.Exists (Path.Combine (smokeDir, "Greeter", "bin", "Debug", "net10.0", "Greeter.dll")), output);
		}

		[Test]
		public void CompileErrorsFailWithFileAndLine ()
		{
			var (exitCode, output) = Run ("build", "Broken/Broken.csproj");
			Assert.AreEqual (1, exitCode, output);
			StringAssert.Contains ("Program.cs(2,27)", output);
			StringAssert.Contains ("CS0103", output);
		}

		[Test]
		public void BuildsOneProjectOfASolutionByName ()
		{
			var (exitCode, output) = Run ("build", "-p:Greeter", "Smoke.sln");
			Assert.AreEqual (0, exitCode, output);
			Assert.IsTrue (File.Exists (Path.Combine (smokeDir, "Greeter", "bin", "Debug", "net10.0", "Greeter.dll")), output);
		}

		[Test]
		public void BuildsTheRequestedConfiguration ()
		{
			var (exitCode, output) = Run ("build", "-c:Release", "Smoke.sln");
			Assert.AreEqual (0, exitCode, output);
			Assert.IsTrue (File.Exists (Path.Combine (smokeDir, "Hello", "bin", "Release", "net10.0", "Hello.dll")), output);
		}

		[Test]
		public void CleanRemovesOutputs ()
		{
			Assert.AreEqual (0, Run ("build", "Hello/Hello.csproj").exitCode);
			var (exitCode, output) = Run ("build", "-t:Clean", "Hello/Hello.csproj");
			Assert.AreEqual (0, exitCode, output);
			Assert.IsFalse (File.Exists (Path.Combine (smokeDir, "Hello", "bin", "Debug", "net10.0", "Hello.dll")), output);
		}

		[Test]
		public void MessagesAreTranslatedForTheUserLocale ()
		{
			// Task T047: GettextCatalog (NGettext, ADR 0014) reads the catalogs compiled by
			// po/MonoDevelop.Translations.csproj into build/locale.
			var localeDir = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (mdtool), "..", "locale"));
			if (!File.Exists (Path.Combine (localeDir, "de", "LC_MESSAGES", "monodevelop.mo")))
				Assert.Ignore ("translations are not built: " + localeDir);
			var env = new System.Collections.Generic.Dictionary<string, string> {
				["LANG"] = "de_DE.UTF-8",
				["LC_ALL"] = null,
				["LC_MESSAGES"] = null,
				["LANGUAGE"] = null,
				["MONODEVELOP_LOCALE_PATH"] = localeDir,
			};
			var (exitCode, output) = RunWithEnvironment (env, "build", "-p:Greeter", "Smoke.sln");
			Assert.AreEqual (0, exitCode, output);
			// msgid "Loading solution: {0}" -> msgstr "Projektmappe wird geladen: {0} " in po/de.po
			StringAssert.Contains ("Projektmappe wird geladen:", output);
		}

		[Test]
		public void MissingFileFails ()
		{
			var (exitCode, output) = Run ("build", "Missing.csproj");
			Assert.AreEqual (1, exitCode, output);
		}

		[Test]
		public void MonoRuntimeOptionIsIgnoredWithAWarning ()
		{
			var (exitCode, output) = Run ("build", "-r:/opt/mono", "Hello/Hello.csproj");
			Assert.AreEqual (0, exitCode, output);
			StringAssert.Contains ("-r:/opt/mono ignored", output);
		}

		static void CopyDirectory (string source, string target)
		{
			foreach (var dir in Directory.GetDirectories (source, "*", SearchOption.AllDirectories)) {
				var name = Path.GetFileName (dir);
				if (name == "bin" || name == "obj")
					continue;
				Directory.CreateDirectory (dir.Replace (source, target));
			}
			Directory.CreateDirectory (target);
			foreach (var file in Directory.GetFiles (source, "*", SearchOption.AllDirectories)) {
				if (file.Contains ("/bin/") || file.Contains ("/obj/"))
					continue;
				var dest = file.Replace (source, target);
				Directory.CreateDirectory (Path.GetDirectoryName (dest));
				File.Copy (file, dest, true);
			}
		}
	}
}
