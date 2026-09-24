//
// EditorPlatformServices.cs
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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.SourceEditor
{
    /// <summary>
    /// Editor platform services that upstream came from the closed-source VS editor
    /// (Microsoft.VisualStudio.Platform.VSEditor), which is not part of the Linux build (ADR 0012). The vendored
    /// vs-editor-api implementations import them (EditorCommandHandlerServiceFactory, the tool tip presenters).
    /// </summary>
    [Export(typeof(ILoggingServiceInternal))]
    sealed class EditorLoggingService : ILoggingServiceInternal
    {
        // Editor telemetry events are not collected: every event is dropped.
        public void PostEvent(string key, params object[] namesAndProperties)
        {
        }

        public void PostEvent(string key, IReadOnlyList<object> namesAndProperties)
        {
        }

        public void PostEvent(TelemetryEventType eventType, string eventName, TelemetryResult result = TelemetryResult.Success, params (string name, object property)[] namesAndProperties)
        {
        }

        public void PostEvent(TelemetryEventType eventType, string eventName, TelemetryResult result, IReadOnlyList<(string name, object property)> namesAndProperties)
        {
        }

        // Faults are exceptions caught by the editor platform: they go to the IDE log.
        public void PostFault(string eventName, string description, Exception exceptionObject, string additionalErrorInfo = null, bool? isIncludedInWatsonSample = null, object[] correlations = null)
        {
            MonoDevelop.Core.LoggingService.LogError($"{eventName}: {description} {additionalErrorInfo}", exceptionObject);
        }

        public void AdjustCounter(string key, string name, int delta = 1)
        {
        }

        public void PostCounters()
        {
        }

        public object CreateTelemetryOperationEventScope(string eventName, TelemetrySeverity severity, object[] correlations, IDictionary<string, object> startingProperties)
        {
            return null;
        }

        public object GetCorrelationFromTelemetryScope(object telemetryScope)
        {
            return null;
        }

        public void EndTelemetryScope(object telemetryScope, TelemetryResult result, string summary = null)
        {
        }
    }

    /// <summary>
    /// The "BraceCompletion/Enabled" editor option (EditorPreferences.EnableBraceCompletion wraps it). Upstream it was
    /// defined by the vs-editor-api brace completion implementation, which is not vendored: SourceEditor2 has its own
    /// (MonoDevelop.SourceEditor.Braces).
    /// </summary>
    [Export(typeof(EditorOptionDefinition))]
    [Name(DefaultTextViewOptions.BraceCompletionEnabledOptionName)]
    sealed class BraceCompletionEnabledOption : EditorOptionDefinition<bool>
    {
        public override bool Default => true;

        public override EditorOptionKey<bool> Key => DefaultTextViewOptions.BraceCompletionEnabledOptionId;
    }

    /// <summary>
    /// Keeps the tips shown over a text view (<see cref="IObscuringTip"/>), per view. MonoDevelop.Ide has a
    /// placeholder that throws and is not exported; the GTK editor positions its popups itself, so the tips are
    /// only tracked here (pushed tips stay visible, as with a single tip).
    /// </summary>
    [Export(typeof(IObscuringTipManager))]
    sealed class EditorObscuringTipManager : IObscuringTipManager
    {
        public void PushTip(ITextView view, IObscuringTip tip)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (tip == null)
                throw new ArgumentNullException(nameof(tip));
            var tips = GetTips(view);
            tips.Remove(tip);
            tips.Add(tip);
        }

        public void RemoveTip(ITextView view, IObscuringTip tip)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            GetTips(view).Remove(tip);
        }

        internal static IReadOnlyList<IObscuringTip> GetTipsForTesting(ITextView view) => GetTips(view);

        static List<IObscuringTip> GetTips(ITextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(typeof(EditorObscuringTipManager), () => new List<IObscuringTip>());
        }
    }
}
