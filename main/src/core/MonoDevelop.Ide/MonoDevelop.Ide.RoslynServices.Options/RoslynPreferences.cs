//
// RoslynPreferences.cs
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
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using MonoDevelop.Core;
using MonoDevelop.Ide.Composition;
using Roslyn.Utilities;

namespace MonoDevelop.Ide.RoslynServices.Options
{
	sealed partial class RoslynPreferences
	{
		public bool FullSolutionAnalysisRuntimeEnabled { get; internal set; } = true;
		public event EventHandler FullSolutionAnalysisRuntimeEnabledChanged;

		internal PerLanguagePreferences CSharp { get; }

		public PerLanguagePreferences For (string languageName)
		{
			lock (languageConfigs) {
				if (!languageConfigs.TryGetValue (languageName, out var value)) {
					languageConfigs [languageName] = value = new PerLanguagePreferences(languageName, this);
				}
				return value;
			}
		}

		// Preferences migrated from the IDE to roslyn keys are held here.
		internal const string globalKey = "MonoDevelop.RoslynPreferences";

		readonly Dictionary<string, PerLanguagePreferences> languageConfigs = new Dictionary<string, PerLanguagePreferences> ();

		internal RoslynPreferences ()
		{
			foreach (var language in RoslynService.AllLanguages)
				languageConfigs.Add (language, new PerLanguagePreferences (language, this));
			CSharp = languageConfigs [LanguageNames.CSharp];
		}

		public class PerLanguagePreferences
		{
			readonly string language;
			readonly RoslynPreferences roslynPreferences;

			public readonly ConfigurationProperty<bool> AutoFormattingOnCloseBrace;
			public readonly ConfigurationProperty<bool> AutoFormattingOnReturn;
			public readonly ConfigurationProperty<bool> AutoFormattingOnSemicolon;
			public readonly ConfigurationProperty<bool> AutoFormattingOnTyping;
			public readonly ConfigurationProperty<bool> FormatOnPaste;
			public readonly ConfigurationProperty<bool> PlaceSystemNamespaceFirst;
			public readonly ConfigurationProperty<bool> SeparateImportDirectiveGroups;
			public readonly ConfigurationProperty<bool> ShowCompletionItemFilters;
			public readonly ConfigurationProperty<bool?> ShowItemsFromUnimportedNamespaces;
			public readonly ConfigurationProperty<bool> SuggestForTypesInNuGetPackages;
			public readonly ConfigurationProperty<bool> SolutionCrawlerClosedFileDiagnostic;
			public readonly ConfigurationProperty<bool?> TriggerOnDeletion;
			readonly Lazy<ConfigurationProperty<bool>> triggerOnTypingLetters;
			public ConfigurationProperty<bool> TriggerOnTypingLetters => triggerOnTypingLetters.Value;

			// Roslyn 5.9 moved the editor, completion and solution crawler options (FeatureOnOffOptions,
			// CompletionOptions storage, ServiceFeatureOnOffOptions, SymbolSearchOptions) out of the
			// Workspaces/Features layers: callers now pass option records explicitly. Those preferences are
			// plain MonoDevelop properties (default values of Roslyn 3.4); only options that still exist as
			// Roslyn global options are bound two-way through Wrap.
			internal PerLanguagePreferences (string language, RoslynPreferences preferences)
			{
				this.language = language;
				roslynPreferences = preferences;

				AutoFormattingOnCloseBrace = Create (nameof (AutoFormattingOnCloseBrace), true, migrate: true);
				AutoFormattingOnReturn = Create (nameof (AutoFormattingOnReturn), true, migrate: true);
				AutoFormattingOnSemicolon = Create (nameof (AutoFormattingOnSemicolon), true, migrate: true);
				AutoFormattingOnTyping = Create (nameof (AutoFormattingOnTyping), true, migrate: true);
				FormatOnPaste = Create (nameof (FormatOnPaste), true, migrate: true);

				PlaceSystemNamespaceFirst = preferences.Wrap<bool> (
					new OptionKey2 (Microsoft.CodeAnalysis.Editing.GenerationOptions.PlaceSystemNamespaceFirst, language),
					language + ".PlaceSystemNamespaceFirst"
				);

				SeparateImportDirectiveGroups = preferences.Wrap<bool> (
					new OptionKey2 (Microsoft.CodeAnalysis.Editing.GenerationOptions.SeparateImportDirectiveGroups, language),
					language + ".SeparateImportDirectiveGroups"
				);

				ShowCompletionItemFilters = Create (nameof (ShowCompletionItemFilters), true, migrate: true);
				ShowItemsFromUnimportedNamespaces = Create<bool?> (nameof (ShowItemsFromUnimportedNamespaces), IdeApp.Preferences.AddImportedItemsToCompletionList.Value, migrate: true);
				SuggestForTypesInNuGetPackages = Create (nameof (SuggestForTypesInNuGetPackages), true);

				SolutionCrawlerClosedFileDiagnostic = new ClosedFileDiagnosticProperty (
					Create<bool?> ("ClosedFileDiagnostic", null), language, roslynPreferences);

				TriggerOnDeletion = Create<bool?> (nameof (TriggerOnDeletion), null, migrate: true);

				triggerOnTypingLetters = new Lazy<ConfigurationProperty<bool>> (() => Create (
					nameof (TriggerOnTypingLetters),
					MonoDevelop.Ide.Editor.DefaultSourceEditorOptions.Instance.EnableAutoCodeCompletion,
					migrate: true
				));
			}

			ConfigurationProperty<T> Create<T> (string name, T defaultValue, bool migrate = false)
			{
				// Property names of the preferences that no longer map to a Roslyn option.
				var propertyName = globalKey + "." + language + "." + name;
				return ConfigurationProperty.Create (propertyName, defaultValue, migrate ? language + "." + name : null);
			}

			class ClosedFileDiagnosticProperty : ConfigurationProperty<bool>
			{
				readonly ConfigurationProperty<bool?> underlying;
				readonly RoslynPreferences roslynPreferences;
				readonly string language;

				public ClosedFileDiagnosticProperty (ConfigurationProperty<bool?> underlying, string language, RoslynPreferences roslynPreferences)
				{
					this.underlying = underlying;
					underlying.Changed += (sender, args) => {
						bool? newValue = underlying.Value;
						if (newValue.HasValue)
							OnSetValue (newValue.Value);
					};
					this.language = language;
					this.roslynPreferences = roslynPreferences;
				}

				protected override bool OnGetValue () => underlying.Value ?? language != LanguageNames.CSharp;

				protected override bool OnSetValue (bool value)
				{
					underlying.Value = value;
					roslynPreferences.FullSolutionAnalysisRuntimeEnabled |= value;
					roslynPreferences.FullSolutionAnalysisRuntimeEnabledChanged?.Invoke (this, EventArgs.Empty);
					return true;
				}
			}
		}
	}
}
