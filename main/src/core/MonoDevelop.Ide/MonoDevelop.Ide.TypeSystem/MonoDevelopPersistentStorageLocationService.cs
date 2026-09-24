//
// MonoDevelopPersistentStorageLocationService.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2017 Microsoft Inc.
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
using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Storage;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.TypeSystem
{
	// Roslyn 5.9 replaced IPersistentStorageLocationService (and its StorageLocationChanging event) with
	// IPersistentStorageConfiguration, which is queried per solution; storage is only provided for the
	// solution of the primary MonoDevelop workspace, in the solution preferences directory.
	[ExportWorkspaceService (typeof (IPersistentStorageConfiguration), ServiceLayer.Host), Shared]
	class MonoDevelopPersistentStorageLocationService : IPersistentStorageConfiguration
	{
		private readonly object _gate = new object ();
		private WorkspaceId primaryWorkspace = WorkspaceId.Empty;
		private SolutionId _currentSolutionId;
		private string _currentWorkingFolderPath;

		[ImportingConstructor]
		[Obsolete (MefConstruction.ImportingConstructorMessage, error: true)]
		public MonoDevelopPersistentStorageLocationService ()
		{
		}

		public bool ThrowOnFailure => false;

		public IDisposable RegisterPrimaryWorkspace (WorkspaceId id)
		{
			if (primaryWorkspace.Equals (WorkspaceId.Empty)) {
				primaryWorkspace = id;
				return new WorkspaceRegistration (this);
			}
			return null;
		}

		class WorkspaceRegistration : IDisposable
		{
			readonly MonoDevelopPersistentStorageLocationService service;
			bool disposed;

			public WorkspaceRegistration (MonoDevelopPersistentStorageLocationService service) => this.service = service;

			public void Dispose ()
			{
				if (!disposed) {
					service.DisconnectCurrentStorage ();
					disposed = true;
				}
			}
		}

		public bool IsSupported (Workspace workspace) => workspace is MonoDevelopWorkspace;

		public string TryGetStorageLocation (SolutionKey solutionKey)
			=> TryGetStorageLocation (solutionKey.Id);

		public string TryGetStorageLocation (SolutionId solutionId)
		{
			lock (_gate) {
				if (solutionId == _currentSolutionId) {
					return _currentWorkingFolderPath;
				}
			}

			return null;
		}

		internal void SetupSolution (MonoDevelopWorkspace visualStudioWorkspace)
		{
			lock (_gate) {
				// Don't trigger events for workspaces other than those we want to inspect.
				if (!primaryWorkspace.Equals (visualStudioWorkspace.Id))
					return;

				if (visualStudioWorkspace.CurrentSolution.Id == _currentSolutionId && _currentWorkingFolderPath != null) {
					return;
				}

				var solution = visualStudioWorkspace.MonoDevelopSolution;
				solution.Modified += OnSolutionModified;
				if (string.IsNullOrWhiteSpace (solution.BaseDirectory))
					return;

				var workingFolderPath = solution.GetPreferencesDirectory ();

				try {
					if (!string.IsNullOrWhiteSpace (workingFolderPath)) {
						OnWorkingFolderChanging_NoLock (visualStudioWorkspace.CurrentSolution.Id, workingFolderPath);
					}
				} catch {
					// don't crash just because solution having problem getting working folder information
				}
			}
		}

		async void OnSolutionModified (object sender, MonoDevelop.Projects.WorkspaceItemEventArgs args)
		{
			var sol = (MonoDevelop.Projects.Solution)args.Item;
			var workspace = await IdeServices.TypeSystemService.GetWorkspaceAsync (sol, CancellationToken.None);
			if (workspace.Id.Equals (primaryWorkspace)) {
				DisconnectCurrentStorage ();
			}
		}

		private void OnWorkingFolderChanging_NoLock (SolutionId solutionId, string newStorageLocation)
		{
			_currentSolutionId = solutionId;
			_currentWorkingFolderPath = newStorageLocation;
		}

		void DisconnectCurrentStorage ()
		{
			lock (_gate) {
				var workspace = IdeServices.TypeSystemService.GetWorkspace (primaryWorkspace);
				var solution = workspace.MonoDevelopSolution;
				if (solution != null)
					solution.Modified -= OnSolutionModified;

				OnWorkingFolderChanging_NoLock (_currentSolutionId, newStorageLocation: null);
				primaryWorkspace = WorkspaceId.Empty;
			}
		}
	}
}
