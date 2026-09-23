//
// UserProfileTests.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2019 Microsoft Inc.
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
using NUnit.Framework;
namespace MonoDevelop.Core
{
	[TestFixture]
	public class UserProfileTests
	{
		[Test]
		public void TestCurrentVersionWillMigrate ()
		{
			Assert.That (UserProfile.ProfileVersions, Contains.Item (BuildInfo.CompatVersion));
		}

		/// <summary>
		/// Without XDG variables the profile lives under $HOME. On .NET, SpecialFolder.Personal is the XDG
		/// documents folder and "" when it does not exist, which made the profile paths relative to the
		/// current directory.
		/// </summary>
		[Test]
		public void UnixProfileWithoutXdgVariablesIsUnderHome ()
		{
			var names = new [] { "XDG_DATA_HOME", "XDG_CONFIG_HOME", "XDG_CACHE_HOME" };
			var saved = Array.ConvertAll (names, Environment.GetEnvironmentVariable);
			try {
				foreach (var name in names)
					Environment.SetEnvironmentVariable (name, null);
				var profile = UserProfile.ForUnix ("8.6", false);
				FilePath home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

				foreach (var dir in new [] { profile.UserDataRoot, profile.ConfigDir, profile.CacheDir, profile.LogDir }) {
					Assert.IsTrue (dir.IsAbsolute, dir);
					Assert.IsTrue (dir.IsChildPathOf (home), dir + " under " + home);
				}
			} finally {
				for (int i = 0; i < names.Length; i++)
					Environment.SetEnvironmentVariable (names [i], saved [i]);
			}
		}
	}
}
