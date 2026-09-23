//
// LinuxRuntimeTests.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonoDevelop.Core.Assemblies;
using Mono.Unix.Native;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Core
{
	/// <summary>
	/// Tasks T046 and T049: platform services the core takes from Mono on the old runtime
	/// (Mono.Unix, code pages, runtime detection) behave on CoreCLR / Linux.
	/// </summary>
	[TestFixture]
	[Platform ("Linux")]
	public class LinuxRuntimeTests : TestBase
	{
		[Test]
		public void RuntimeIsCoreClrNotMono ()
		{
			Assert.IsTrue (Platform.IsLinux);
			Assert.IsFalse (Platform.IsMac);
			Assert.IsFalse (Platform.IsWindows);
			Assert.IsTrue (DotNetCoreSdkInfo.IsRunningOnCoreClr);
			Assert.IsNull (MonoRuntimeInfo.FromCurrentRuntime ());
			Assert.IsInstanceOf<DotNetCoreTargetRuntime> (Runtime.SystemAssemblyService.CurrentRuntime);
		}

		[Test]
		public void MSBuildComesFromTheSdkOfTheCurrentRuntime ()
		{
			// Runtime.Initialize loads MSBuild from the current runtime's bin path (LoadMSBuildLibraries);
			// on .NET that is the SDK registered with MSBuildLocator, not a copy next to MonoDevelop.
			var binPath = new FilePath (Runtime.SystemAssemblyService.CurrentRuntime.GetMSBuildBinPath ("15.0")).FullPath;
			var msbuild = AppDomain.CurrentDomain.GetAssemblies ().Single (a => a.GetName ().Name == "Microsoft.Build");
			Assert.AreEqual (binPath, new FilePath (Path.GetDirectoryName (msbuild.Location)).FullPath);
		}

		[Test]
		public void LegacyCodePagesDecode ()
		{
			// Runtime.Initialize registers CodePagesEncodingProvider; .NET only ships UTF/ASCII/Latin-1.
			Assert.AreEqual ("é€", Encoding.GetEncoding (1252).GetString (new byte[] { 0xE9, 0x80 }));
			Assert.AreEqual ("Ж", Encoding.GetEncoding ("windows-1251").GetString (new byte[] { 0xC6 }));
		}

		[Test]
		public void MonoUnixNativeCallsWork ()
		{
			// LoggingService redirects stdout/stderr with Mono.Unix (open, dup2, symlink); the native
			// helper library comes from the Mono.Unix NuGet package.
			var dir = Path.Combine (Path.GetTempPath (), "md-monounix-" + Guid.NewGuid ());
			Directory.CreateDirectory (dir);
			try {
				var file = Path.Combine (dir, "Ide-session.log");
				int fd = Syscall.open (file, OpenFlags.O_WRONLY | OpenFlags.O_CREAT | OpenFlags.O_TRUNC,
					FilePermissions.S_IRUSR | FilePermissions.S_IWUSR);
				Assert.GreaterOrEqual (fd, 0, "open: " + Stdlib.GetLastError ());
				int copy = Syscall.dup (fd);
				Assert.GreaterOrEqual (copy, 0, "dup: " + Stdlib.GetLastError ());
				Assert.AreEqual (0, Syscall.close (copy));
				Assert.AreEqual (0, Syscall.close (fd));

				// The same private helper the log redirection uses to point Ide.log at the session log.
				var symlink = typeof (LoggingService).GetMethod ("SymlinkWithRetry", BindingFlags.NonPublic | BindingFlags.Static);
				var link = Path.Combine (dir, "Ide.log");
				Assert.IsTrue ((bool)symlink.Invoke (null, new object[] { file, link, 3 }));
				Assert.AreEqual (file, new FileInfo (link).LinkTarget);
			} finally {
				Directory.Delete (dir, true);
			}
		}

		/// <summary>
		/// Assemblies next to the IDE that the host's deps.json does not list (add-in dependencies such as
		/// Mono.Debugging) are resolved from the application directory, as Mono did.
		/// </summary>
		[Test]
		public void AssembliesAreResolvedFromADirectory ()
		{
			var dir = Path.Combine (Path.GetTempPath (), "md-resolve-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (dir);
			var context = new System.Runtime.Loader.AssemblyLoadContext ("md-resolve-test", true);
			try {
				var name = new AssemblyName ("MdResolveTest" + Guid.NewGuid ().ToString ("N"));
				var builder = new System.Reflection.Emit.PersistedAssemblyBuilder (name, typeof (object).Assembly);
				builder.DefineDynamicModule (name.Name).DefineType ("Probe", TypeAttributes.Public).CreateType ();
				builder.Save (Path.Combine (dir, name.Name + ".dll"));

				var resolved = Runtime.ResolveFromDirectory (context, name, dir);
				Assert.IsNotNull (resolved);
				Assert.AreEqual (name.Name, resolved.GetName ().Name);
				Assert.IsNotNull (resolved.GetType ("Probe"));
				Assert.IsNull (Runtime.ResolveFromDirectory (context, new AssemblyName ("DoesNotExist"), dir));
			} finally {
				context.Unload ();
				Directory.Delete (dir, true);
			}
		}
	}
}
