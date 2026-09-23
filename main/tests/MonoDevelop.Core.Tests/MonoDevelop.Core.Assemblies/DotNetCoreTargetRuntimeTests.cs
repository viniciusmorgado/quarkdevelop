//
// DotNetCoreTargetRuntimeTests.cs
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
using MonoDevelop.Core.Execution;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core.Assemblies
{
	/// <summary>Tasks T042/T043 (ADR 0007): the .NET target runtime and its execution handler.</summary>
	[TestFixture]
	public class DotNetCoreTargetRuntimeTests : TestBase
	{
		[Test]
		public void ParsesListSdksOutput ()
		{
			const string output = "8.0.414 [/usr/share/dotnet/sdk]\n10.0.401 [/usr/share/dotnet/sdk]\n10.0.100-rc.2.25502.107 [/opt/dotnet/sdk]\n\ngarbage line\n";
			var sdks = DotNetCoreSdkInfo.ParseListSdksOutput (output).ToList ();

			Assert.AreEqual (3, sdks.Count);
			var sdk = sdks[1];
			Assert.AreEqual ("10.0.401", sdk.VersionString);
			Assert.AreEqual (new System.Version (10, 0, 401), sdk.Version);
			Assert.AreEqual (new FilePath ("/usr/share/dotnet/sdk/10.0.401"), sdk.MSBuildPath);
			Assert.AreEqual (new FilePath ("/usr/share/dotnet"), sdk.DotNetRoot);
			Assert.AreEqual ("10.0.100-rc.2.25502.107", sdks[2].VersionString);
			Assert.AreEqual (new System.Version (10, 0, 100), sdks[2].Version);
		}

		[Test]
		public void ParsingEmptyOutputYieldsNoSdk ()
		{
			Assert.IsEmpty (DotNetCoreSdkInfo.ParseListSdksOutput (string.Empty));
			Assert.IsEmpty (DotNetCoreSdkInfo.ParseListSdksOutput (null));
		}

		[Test]
		public void CurrentRuntimeIsDotNet ()
		{
			Assert.IsTrue (DotNetCoreSdkInfo.IsRunningOnCoreClr);
			Assert.IsInstanceOf<DotNetCoreTargetRuntime> (Runtime.SystemAssemblyService.CurrentRuntime);
			Assert.IsTrue (Runtime.SystemAssemblyService.CurrentRuntime.IsRunning);
		}

		[Test]
		public void InstalledSdkProvidesMSBuild ()
		{
			var runtime = (DotNetCoreTargetRuntime)Runtime.SystemAssemblyService.CurrentRuntime;
			Assert.IsTrue (runtime.CanBuild, runtime.CannotBuildReason);
			var binPath = runtime.GetMSBuildBinPath ("Current");
			Assert.IsTrue (File.Exists (Path.Combine (binPath, "MSBuild.dll")), "MSBuild.dll in " + binPath);
		}

		[Test]
		public void RuntimeWithoutSdkCannotBuild ()
		{
			var runtime = new DotNetCoreTargetRuntime (null);
			Assert.IsFalse (runtime.CanBuild);
			Assert.IsNotEmpty (runtime.CannotBuildReason);
			Assert.IsNull (runtime.GetMSBuildBinPath ("Current"));
		}

		/// <summary>
		/// Frameworks found in the reference assemblies folder (e.g. .NET Framework ones installed by
		/// scripts/netfx-refasm.sh) get a backend: without one, the runtime initialization failed with a
		/// NullReferenceException and stopped creating frameworks.
		/// </summary>
		[Test]
		public void CustomFrameworksFromReferenceAssembliesAreInstalled ()
		{
			var root = Path.Combine (Path.GetTempPath (), "md-refasm-" + System.Guid.NewGuid ().ToString ("N"));
			var redist = Path.Combine (root, ".NETFramework", "v99.0", "RedistList");
			Directory.CreateDirectory (redist);
			File.WriteAllText (Path.Combine (redist, "FrameworkList.xml"),
				"<FileList Name=\".NET Framework 99\" RedistName=\"Framework\">" +
				"<File AssemblyName=\"System\" Version=\"4.0.0.0\" PublicKeyToken=\"b77a5c561934e089\" Culture=\"neutral\" ProcessorArchitecture=\"MSIL\" InGac=\"true\" />" +
				"</FileList>");
			var previous = System.Environment.GetEnvironmentVariable ("MD_NETFX_REFASM");
			try {
				System.Environment.SetEnvironmentVariable ("MD_NETFX_REFASM", root);
				var runtime = new DotNetCoreTargetRuntime (null);
				runtime.EnsureInitialized ();

				var fx = runtime.CustomFrameworks.SingleOrDefault (f => f.Id == new TargetFrameworkMoniker (".NETFramework", "v99.0"));
				Assert.IsNotNull (fx, "the framework in the reference assemblies folder is found");
				Assert.IsTrue (runtime.IsInstalled (fx));
			} finally {
				System.Environment.SetEnvironmentVariable ("MD_NETFX_REFASM", previous);
				Directory.Delete (root, true);
			}
		}

		[Test]
		public void AssembliesRunThroughDotNetExec ()
		{
			var cmd = new DotNetExecutionCommand ("/tmp/app with space/App.dll", "--flag value", "/tmp");
			var native = DotNetCoreExecutionHandler.CreateNativeCommand (cmd);

			Assert.AreEqual ("dotnet", Path.GetFileName (native.Command));
			Assert.AreEqual ("exec \"/tmp/app with space/App.dll\" --flag value", native.Arguments);
			Assert.AreEqual ("/tmp", native.WorkingDirectory);
			Assert.IsTrue (native.EnvironmentVariables.ContainsKey ("DOTNET_HOST_PATH"));
		}

		[Test]
		public void NativeExecutablesRunDirectly ()
		{
			var cmd = new DotNetExecutionCommand ("/tmp/App", "arg");
			var native = DotNetCoreExecutionHandler.CreateNativeCommand (cmd);

			Assert.AreEqual ("/tmp/App", native.Command);
			Assert.AreEqual ("arg", native.Arguments);
		}

		[Test]
		public void HandlerExecutesDotNetCommandsOnly ()
		{
			var handler = new DotNetCoreExecutionHandler ();
			Assert.IsTrue (handler.CanExecute (new DotNetExecutionCommand ("App.dll")));
			Assert.IsFalse (handler.CanExecute (new NativeExecutionCommand ("ls")));
		}
	}
}
