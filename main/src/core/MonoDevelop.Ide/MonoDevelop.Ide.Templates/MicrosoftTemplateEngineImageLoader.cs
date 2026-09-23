//
// MicrosoftTemplateEngineImageLoader.cs
//
// Author:
//       Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2018 Microsoft Corp.
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
using System.Collections.Generic;

using Xwt;
using Xwt.Drawing;

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

namespace MonoDevelop.Ide.Templates
{
	class MicrosoftTemplateEngineImageLoader : IImageLoader
	{
		static char[] DirectorySeparators = new [] { Path.DirectorySeparatorChar };
		readonly IEngineEnvironmentSettings settings;
		readonly ITemplateInfo template;

		public MicrosoftTemplateEngineImageLoader (IEngineEnvironmentSettings environmentSettings, ITemplateInfo templateInfo)
		{
			settings = environmentSettings;
			template = templateInfo;
		}

		public IEnumerable<string> GetAlternativeFiles (string fileName, string baseName, string ext)
		{
			IMountPoint mountPoint;
			IDirectory dir;

			if (!settings.TryGetMountPoint (template.MountPointUri, out mountPoint))
				yield break;

			using (mountPoint) {
				dir = mountPoint.Root;

				var path = baseName.Split (DirectorySeparators, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < path.Length - 1; i++) {
					dir = dir.EnumerateDirectories (path[i], SearchOption.TopDirectoryOnly).FirstOrDefault ();

					if (dir == null)
						yield break;
				}

				var pattern = string.Format ("{0}*{1}", path[path.Length - 1], ext);
				foreach (var entry in dir.EnumerateFiles (pattern, SearchOption.TopDirectoryOnly).ToList ())
					yield return entry.FullPath;
			}
		}

		public Stream LoadImage (string fileName)
		{
			return MicrosoftTemplateEngine.OpenFile (template, fileName);
		}
	}
}
