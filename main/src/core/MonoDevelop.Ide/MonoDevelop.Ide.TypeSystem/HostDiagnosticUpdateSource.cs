//
// HostDiagnosticUpdateSource.cs
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
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.TypeSystem
{
	// Roslyn 5.9 removed the push-based diagnostic update sources (AbstractHostDiagnosticUpdateSource,
	// IDiagnosticUpdateSourceRegistrationService, DiagnosticsUpdatedArgs). Host diagnostics (analyzer load
	// failures) are now kept here, logged and exposed through the DiagnosticsUpdated event.
	internal sealed class HostDiagnosticUpdateSource
	{
		private readonly MonoDevelopWorkspace _workspace;
		private readonly object _gate = new object ();
		private readonly Dictionary<ProjectId, Dictionary<object, ImmutableArray<Diagnostic>>> _diagnosticMap = new Dictionary<ProjectId, Dictionary<object, ImmutableArray<Diagnostic>>> ();

		public HostDiagnosticUpdateSource (MonoDevelopWorkspace workspace)
		{
			_workspace = workspace;
		}

		public Microsoft.CodeAnalysis.Workspace Workspace {
			get {
				return _workspace;
			}
		}

		/// <summary>
		/// Raised with the project whose host diagnostics changed.
		/// </summary>
		public event EventHandler<ProjectId> DiagnosticsUpdated;

		public ImmutableArray<Diagnostic> GetDiagnostics (ProjectId projectId)
		{
			lock (_gate) {
				if (!_diagnosticMap.TryGetValue (projectId, out var map))
					return ImmutableArray<Diagnostic>.Empty;
				return map.Values.SelectMany (d => d).ToImmutableArray ();
			}
		}

		public void UpdateDiagnosticsForProject (ProjectId projectId, object key, IEnumerable<Diagnostic> items)
		{
			ArgumentNullException.ThrowIfNull (projectId);
			ArgumentNullException.ThrowIfNull (key);
			ArgumentNullException.ThrowIfNull (items);

			var diagnostics = items.ToImmutableArray ();
			lock (_gate) {
				if (!_diagnosticMap.TryGetValue (projectId, out var map))
					_diagnosticMap [projectId] = map = new Dictionary<object, ImmutableArray<Diagnostic>> ();
				map [key] = diagnostics;
			}

			foreach (var diagnostic in diagnostics)
				LoggingService.LogWarning ("{0}: {1}", diagnostic.Id, diagnostic.GetMessage ());

			DiagnosticsUpdated?.Invoke (this, projectId);
		}

		public void ClearAllDiagnosticsForProject (ProjectId projectId)
		{
			ArgumentNullException.ThrowIfNull (projectId);

			bool removed;
			lock (_gate) {
				removed = _diagnosticMap.Remove (projectId);
			}

			if (removed)
				DiagnosticsUpdated?.Invoke (this, projectId);
		}

		public void ClearDiagnosticsForProject (ProjectId projectId, object key)
		{
			ArgumentNullException.ThrowIfNull (projectId);
			ArgumentNullException.ThrowIfNull (key);

			var raiseEvent = false;
			lock (_gate) {
				if (_diagnosticMap.TryGetValue (projectId, out var map)) {
					raiseEvent = map.Remove (key);
				}
			}

			if (raiseEvent)
				DiagnosticsUpdated?.Invoke (this, projectId);
		}

		public void ClearAnalyzerReferenceDiagnostics (AnalyzerFileReference fileReference, string language, ProjectId projectId)
		{
			ClearDiagnosticsForProject (projectId, fileReference);
		}
	}
}
