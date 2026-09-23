//
// OptionsExtensions.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2018 Microsoft Inc.
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
using System.Linq;
using Microsoft.CodeAnalysis.Options;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.Ide.RoslynServices.Options
{
	static class OptionsExtensions
	{
		// Roslyn 5.9 options no longer carry roaming/local-user storage locations; the MonoDevelop property
		// name is derived from the option's configuration name (and language for per-language options).
		public static IEnumerable<string> GetPropertyNames (this OptionKey2 optionKey)
		{
			var name = GetPropertyName (optionKey);
			if (name != null)
				yield return name;
		}

		public static string GetPropertyName (this OptionKey2 optionKey)
		{
			var configName = optionKey.Option?.Definition?.ConfigName;
			if (string.IsNullOrEmpty (configName))
				return null;
			if (optionKey.Language != null)
				return "Roslyn." + optionKey.Language + "." + configName;
			return "Roslyn." + configName;
		}

		public static TextStylePolicy GetTextStylePolicy (this OptionKey2 optionKey)
		{
			var mimeChain = IdeServices.DesktopService.GetMimeTypeInheritanceChainForRoslynLanguage (optionKey.Language);
			if (mimeChain == null) {
				throw new Exception ($"Unknown Roslyn language {optionKey.Language}");
			}
			return PolicyService.GetDefaultPolicy<TextStylePolicy> (mimeChain);
		}
	}
}
