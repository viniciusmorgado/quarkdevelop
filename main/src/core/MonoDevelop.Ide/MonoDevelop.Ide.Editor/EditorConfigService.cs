//
// EditorConfigService.cs
//
// Author:
//       Mike Krüger <mikkrg@microsoft.com>
//
// Copyright (c) 2017 Microsoft
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
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.CodingConventions;
using MonoDevelop.Projects;
using CodingIndentStyle = Microsoft.VisualStudio.CodingConventions.IndentStyle;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.Editor
{
	// Microsoft.VisualStudio.CodingConventions is not available for .NET; .editorconfig files are now read with
	// Roslyn's parser (AnalyzerConfig/AnalyzerConfigSet) and exposed through the subset of the CodingConventions
	// API used by MonoDevelop (see the Microsoft.VisualStudio.CodingConventions namespace below).
	static class EditorConfigService
	{
		public readonly static string MaxLineLengthConvention = "max_line_length";
		public readonly static string RulersConvention = "rulers";

		readonly static object contextCacheLock = new object ();
		static ImmutableDictionary<string, EditorConfigContext> contextCache = ImmutableDictionary<string, EditorConfigContext>.Empty;

		public static async Task<ICodingConventionContext> GetEditorConfigContext (string fileName, CancellationToken token = default (CancellationToken))
		{
			if (string.IsNullOrEmpty (fileName))
				return null;
			try {
				var directory = Path.GetDirectoryName (fileName);
				if (string.IsNullOrEmpty (directory)) {
					return null;
				}

				// HACK: Work around for a library issue https://github.com/mono/monodevelop/issues/6104
				if (directory == "/") {
					return null;
				}
			} catch {
				return null;
			}
			if (contextCache.TryGetValue (fileName, out var oldresult) && !oldresult.IsDisposed)
				return oldresult;
			try {
				var result = await Task.Run (() => {
					var context = new EditorConfigContext (fileName);
					context.Load ();
					return context;
				}, token).ConfigureAwait (false);
				lock (contextCacheLock) {
					// check if another thread already requested a coding convention context and ensure
					// that only one is alive.
					if (contextCache.TryGetValue (fileName, out var result2) && !result2.IsDisposed) {
						if (result != result2)
							result.Dispose ();
						return result2;
					}
					contextCache = contextCache.SetItem (fileName, result);
				}
				return result;
			} catch (OperationCanceledException) {
				return null;
			} catch (Exception e) {
				LoggingService.LogError ("Error while getting coding conventions,", e);
				return null;
			}
		}

		public static Task RemoveEditConfigContext (string fileName)
		{
			return Task.Run (() => {
				EditorConfigContext ctx;
				lock (contextCacheLock) {
					if (!contextCache.TryGetValue (fileName, out ctx))
						return;
					contextCache = contextCache.Remove (fileName);
				}
				if (ctx != null)
					ctx.Dispose ();
			});
		}

		/// <summary>
		/// The .editorconfig conventions that apply to a file; reloaded when one of the .editorconfig files of
		/// its directory chain is created, changed or removed.
		/// </summary>
		sealed class EditorConfigContext : ICodingConventionContext
		{
			const string EditorConfigFileName = ".editorconfig";

			readonly FilePath fileName;
			readonly object gate = new object ();
			ImmutableArray<FilePath> directories = ImmutableArray<FilePath>.Empty;

			public EditorConfigContext (FilePath fileName)
			{
				this.fileName = fileName.FullPath;
				CurrentConventions = new CodingConventionsSnapshot (ImmutableDictionary<string, string>.Empty);
			}

			public bool IsDisposed { get; private set; }

			public ICodingConventionsSnapshot CurrentConventions { get; private set; }

			public event CodingConventionsChangedAsyncEventHandler CodingConventionsChangedAsync;

			public void Load ()
			{
				var configs = new List<AnalyzerConfig> ();
				var chain = ImmutableArray.CreateBuilder<FilePath> ();
				var directory = fileName.ParentDirectory;
				while (!directory.IsNullOrEmpty) {
					chain.Add (directory);
					var configFile = directory.Combine (EditorConfigFileName);
					if (File.Exists (configFile)) {
						try {
							var config = AnalyzerConfig.Parse (File.ReadAllText (configFile), configFile);
							configs.Add (config);
							if (config.IsRoot)
								break;
						} catch (Exception e) {
							LoggingService.LogError ("Error while reading " + configFile, e);
						}
					}
					var parent = directory.ParentDirectory;
					if (parent == directory)
						break;
					directory = parent;
				}

				var options = ImmutableDictionary<string, string>.Empty;
				if (configs.Count > 0) {
					var set = AnalyzerConfigSet.Create (configs);
					options = set.GetOptionsForSourcePath (fileName).AnalyzerOptions;
				}

				lock (gate) {
					if (IsDisposed)
						return;
					CurrentConventions = new CodingConventionsSnapshot (options);
					if (directories.IsEmpty) {
						FileService.FileChanged += OnFileChanged;
						FileService.FileCreated += OnFileChanged;
						FileService.FileRemoved += OnFileChanged;
					}
					directories = chain.ToImmutable ();
				}
				FileWatcherService.WatchDirectories (this, directories).Ignore ();
			}

			void OnFileChanged (object sender, FileEventArgs e)
			{
				ImmutableArray<FilePath> watched;
				lock (gate) {
					if (IsDisposed)
						return;
					watched = directories;
				}
				foreach (var file in e) {
					if (file.FileName.FileName == EditorConfigFileName && watched.Contains (file.FileName.ParentDirectory)) {
						ReloadAsync ().Ignore ();
						return;
					}
				}
			}

			async Task ReloadAsync ()
			{
				await Task.Run (Load).ConfigureAwait (false);
				var handler = CodingConventionsChangedAsync;
				if (handler == null)
					return;
				var args = new CodingConventionsChangedEventArgs (this);
				foreach (CodingConventionsChangedAsyncEventHandler h in handler.GetInvocationList ()) {
					try {
						await h (this, args).ConfigureAwait (false);
					} catch (Exception ex) {
						LoggingService.LogError ("Error while updating coding conventions", ex);
					}
				}
			}

			public void Dispose ()
			{
				lock (gate) {
					if (IsDisposed)
						return;
					IsDisposed = true;
					FileService.FileChanged -= OnFileChanged;
					FileService.FileCreated -= OnFileChanged;
					FileService.FileRemoved -= OnFileChanged;
				}
				FileWatcherService.WatchDirectories (this, null).Ignore ();
			}
		}

		sealed class CodingConventionsSnapshot : ICodingConventionsSnapshot, IUniversalCodingConventions
		{
			readonly ImmutableDictionary<string, string> options;

			public CodingConventionsSnapshot (ImmutableDictionary<string, string> options)
			{
				this.options = options;
				AllRawConventions = options.ToImmutableDictionary (kv => kv.Key, kv => (object)kv.Value, options.KeyComparer);
			}

			public IUniversalCodingConventions UniversalConventions => this;

			public IReadOnlyDictionary<string, object> AllRawConventions { get; }

			public bool TryGetConventionValue<T> (string conventionName, out T conventionValue)
			{
				conventionValue = default (T);
				if (!options.TryGetValue (conventionName, out var raw))
					return false;
				if (raw is T value) {
					conventionValue = value;
					return true;
				}
				try {
					var type = Nullable.GetUnderlyingType (typeof (T)) ?? typeof (T);
					conventionValue = (T)(type.IsEnum ? Enum.Parse (type, raw, true) : Convert.ChangeType (raw, type, CultureInfo.InvariantCulture));
					return true;
				} catch (Exception) {
					return false;
				}
			}

			bool TryGetInt (string key, out int value)
			{
				value = 0;
				return options.TryGetValue (key, out var raw) && int.TryParse (raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
			}

			bool TryGetBool (string key, out bool value)
			{
				value = false;
				return options.TryGetValue (key, out var raw) && bool.TryParse (raw, out value);
			}

			public bool TryGetIndentStyle (out CodingIndentStyle indentStyle)
			{
				indentStyle = CodingIndentStyle.Tabs;
				if (!options.TryGetValue ("indent_style", out var raw))
					return false;
				switch (raw.ToLowerInvariant ()) {
				case "tab":
					indentStyle = CodingIndentStyle.Tabs;
					return true;
				case "space":
					indentStyle = CodingIndentStyle.Spaces;
					return true;
				}
				return false;
			}

			public bool TryGetIndentSize (out int indentSize)
			{
				if (options.TryGetValue ("indent_size", out var raw) && string.Equals (raw, "tab", StringComparison.OrdinalIgnoreCase))
					return TryGetInt ("tab_width", out indentSize);
				return TryGetInt ("indent_size", out indentSize);
			}

			public bool TryGetTabWidth (out int tabWidth)
			{
				// tab_width defaults to indent_size (editorconfig specification).
				if (TryGetInt ("tab_width", out tabWidth))
					return true;
				return TryGetInt ("indent_size", out tabWidth);
			}

			public bool TryGetLineEnding (out string lineEnding)
			{
				lineEnding = null;
				if (!options.TryGetValue ("end_of_line", out var raw))
					return false;
				switch (raw.ToLowerInvariant ()) {
				case "lf":
					lineEnding = "\n";
					return true;
				case "crlf":
					lineEnding = "\r\n";
					return true;
				case "cr":
					lineEnding = "\r";
					return true;
				}
				return false;
			}

			public bool TryGetAllowTrailingWhitespace (out bool allowTrailingWhitespace)
			{
				allowTrailingWhitespace = true;
				if (!TryGetBool ("trim_trailing_whitespace", out var trim))
					return false;
				allowTrailingWhitespace = !trim;
				return true;
			}

			public bool TryGetRequireFinalNewline (out bool requireFinalNewline)
			{
				return TryGetBool ("insert_final_newline", out requireFinalNewline);
			}

			public bool TryGetEncoding (out Encoding encoding)
			{
				encoding = null;
				if (!options.TryGetValue ("charset", out var raw))
					return false;
				switch (raw.ToLowerInvariant ()) {
				case "latin1":
					encoding = Encoding.Latin1;
					return true;
				case "utf-8":
					encoding = new UTF8Encoding (false);
					return true;
				case "utf-8-bom":
					encoding = new UTF8Encoding (true);
					return true;
				case "utf-16be":
					encoding = Encoding.BigEndianUnicode;
					return true;
				case "utf-16le":
					encoding = Encoding.Unicode;
					return true;
				}
				return false;
			}
		}
	}
}

namespace Microsoft.VisualStudio.CodingConventions
{
	// Subset of the Microsoft.VisualStudio.CodingConventions API (the package is not available for .NET) used by
	// MonoDevelop and its add-ins; implemented by MonoDevelop.Ide.Editor.EditorConfigService.
	enum IndentStyle
	{
		Tabs,
		Spaces
	}

	interface ICodingConventionContext : IDisposable
	{
		ICodingConventionsSnapshot CurrentConventions { get; }
		event CodingConventionsChangedAsyncEventHandler CodingConventionsChangedAsync;
	}

	interface ICodingConventionsSnapshot
	{
		IUniversalCodingConventions UniversalConventions { get; }
		IReadOnlyDictionary<string, object> AllRawConventions { get; }
		bool TryGetConventionValue<T> (string conventionName, out T conventionValue);
	}

	interface IUniversalCodingConventions
	{
		bool TryGetIndentStyle (out IndentStyle indentStyle);
		bool TryGetIndentSize (out int indentSize);
		bool TryGetTabWidth (out int tabWidth);
		bool TryGetLineEnding (out string lineEnding);
		bool TryGetAllowTrailingWhitespace (out bool allowTrailingWhitespace);
		bool TryGetRequireFinalNewline (out bool requireFinalNewline);
		bool TryGetEncoding (out Encoding encoding);
	}

	delegate Task CodingConventionsChangedAsyncEventHandler (object sender, CodingConventionsChangedEventArgs args);

	sealed class CodingConventionsChangedEventArgs : EventArgs
	{
		public CodingConventionsChangedEventArgs (ICodingConventionContext context)
		{
			Context = context;
		}

		public ICodingConventionContext Context { get; }
	}
}
