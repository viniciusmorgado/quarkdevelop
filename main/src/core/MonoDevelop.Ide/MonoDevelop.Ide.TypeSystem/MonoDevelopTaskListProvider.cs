//
// MonoDevelopTaskListProvider.cs
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.TaskList;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.TypeSystem
{
	/// <summary>
	/// The task list (TODO comment) items of a document changed.
	/// </summary>
	sealed class TaskListUpdatedEventArgs : EventArgs
	{
		public TaskListUpdatedEventArgs (Workspace workspace, Solution solution, DocumentId documentId, ImmutableArray<TaskListItem> taskListItems)
		{
			Workspace = workspace;
			Solution = solution;
			DocumentId = documentId;
			TaskListItems = taskListItems;
		}

		public object Id => DocumentId;
		public Workspace Workspace { get; }
		public Solution Solution { get; }
		public ProjectId ProjectId => DocumentId.ProjectId;
		public DocumentId DocumentId { get; }
		public ImmutableArray<TaskListItem> TaskListItems { get; }
	}

	/// <summary>
	/// Roslyn 5.9 removed the push-based TODO comment service (ITodoListProvider/TodoItemsUpdatedArgs, part of
	/// EditorFeatures and driven by the solution crawler). This provider computes the items with the language's
	/// ITaskListService for the documents of the registered workspaces when they change, and raises
	/// <see cref="TaskListUpdated"/>.
	/// </summary>
	static class MonoDevelopTaskListProvider
	{
		const int DelayMilliseconds = 1000;

		static readonly object gate = new object ();
		static readonly Dictionary<Workspace, WorkspaceState> workspaces = new Dictionary<Workspace, WorkspaceState> ();
		static ImmutableArray<TaskListItemDescriptor> descriptors = ImmutableArray<TaskListItemDescriptor>.Empty;

		public static event EventHandler<TaskListUpdatedEventArgs> TaskListUpdated;

		/// <summary>
		/// Sets the comment tokens, in the "TOKEN:priority" format (priority 0 = low, 1 = medium, 2 = high).
		/// </summary>
		public static void SetTokens (IEnumerable<string> tokens)
		{
			var newDescriptors = TaskListItemDescriptor.Parse (tokens.ToImmutableArray ());
			List<Workspace> toRescan;
			lock (gate) {
				descriptors = newDescriptors;
				foreach (var state in workspaces.Values)
					state.Versions.Clear ();
				toRescan = workspaces.Keys.ToList ();
			}

			foreach (var workspace in toRescan)
				Enqueue (workspace, GetAllDocumentIds (workspace.CurrentSolution));
		}

		public static void Register (Workspace workspace)
		{
			lock (gate) {
				if (workspaces.ContainsKey (workspace))
					return;
				workspaces[workspace] = new WorkspaceState ();
			}
			workspace.WorkspaceChanged += OnWorkspaceChanged;
			Enqueue (workspace, GetAllDocumentIds (workspace.CurrentSolution));
		}

		public static void Unregister (Workspace workspace)
		{
			workspace.WorkspaceChanged -= OnWorkspaceChanged;
			lock (gate) {
				if (workspaces.TryGetValue (workspace, out var state)) {
					state.Dispose ();
					workspaces.Remove (workspace);
				}
			}
		}

		static IEnumerable<DocumentId> GetAllDocumentIds (Solution solution) => solution.Projects.SelectMany (p => p.DocumentIds);

		static void OnWorkspaceChanged (object sender, WorkspaceChangeEventArgs e)
		{
			var workspace = (Workspace)sender;
			switch (e.Kind) {
			case WorkspaceChangeKind.SolutionAdded:
			case WorkspaceChangeKind.SolutionChanged:
			case WorkspaceChangeKind.SolutionReloaded:
				Enqueue (workspace, GetAllDocumentIds (e.NewSolution));
				break;
			case WorkspaceChangeKind.SolutionCleared:
			case WorkspaceChangeKind.SolutionRemoved:
				lock (gate) {
					if (workspaces.TryGetValue (workspace, out var state)) {
						state.Pending.Clear ();
						state.Versions.Clear ();
						state.WithItems.Clear ();
					}
				}
				break;
			case WorkspaceChangeKind.ProjectAdded:
			case WorkspaceChangeKind.ProjectChanged:
			case WorkspaceChangeKind.ProjectReloaded:
				var project = e.NewSolution.GetProject (e.ProjectId);
				if (project != null)
					Enqueue (workspace, project.DocumentIds);
				break;
			case WorkspaceChangeKind.ProjectRemoved:
				var oldProject = e.OldSolution.GetProject (e.ProjectId);
				if (oldProject != null)
					Enqueue (workspace, oldProject.DocumentIds);
				break;
			case WorkspaceChangeKind.DocumentAdded:
			case WorkspaceChangeKind.DocumentChanged:
			case WorkspaceChangeKind.DocumentReloaded:
			case WorkspaceChangeKind.DocumentRemoved:
				if (e.DocumentId != null)
					Enqueue (workspace, new[] { e.DocumentId });
				break;
			}
		}

		static void Enqueue (Workspace workspace, IEnumerable<DocumentId> documentIds)
		{
			CancellationToken token;
			lock (gate) {
				if (!workspaces.TryGetValue (workspace, out var state))
					return;
				foreach (var id in documentIds)
					state.Pending.Add (id);
				if (state.Scheduled || state.Pending.Count == 0)
					return;
				state.Scheduled = true;
				token = state.CancellationTokenSource.Token;
			}

			Task.Run (() => ProcessAsync (workspace, token)).Ignore ();
		}

		static async Task ProcessAsync (Workspace workspace, CancellationToken token)
		{
			try {
				await Task.Delay (DelayMilliseconds, token).ConfigureAwait (false);
			} catch (OperationCanceledException) {
				return;
			}

			DocumentId[] ids;
			ImmutableArray<TaskListItemDescriptor> currentDescriptors;
			WorkspaceState state;
			lock (gate) {
				if (!workspaces.TryGetValue (workspace, out state))
					return;
				ids = state.Pending.ToArray ();
				state.Pending.Clear ();
				state.Scheduled = false;
				currentDescriptors = descriptors;
			}

			var solution = workspace.CurrentSolution;
			foreach (var id in ids) {
				if (token.IsCancellationRequested)
					return;
				try {
					var document = solution.GetDocument (id);
					if (document == null) {
						bool hadItems;
						lock (gate) {
							state.Versions.Remove (id);
							hadItems = state.WithItems.Remove (id);
						}
						if (hadItems)
							TaskListUpdated?.Invoke (null, new TaskListUpdatedEventArgs (workspace, solution, id, ImmutableArray<TaskListItem>.Empty));
						continue;
					}

					var version = await document.GetTextVersionAsync (token).ConfigureAwait (false);
					lock (gate) {
						if (state.Versions.TryGetValue (id, out var oldVersion) && oldVersion == version)
							continue;
					}

					var service = document.Project.Services.GetService<ITaskListService> ();
					if (service == null)
						continue;

					var items = currentDescriptors.IsEmpty
						? ImmutableArray<TaskListItem>.Empty
						: await service.GetTaskListItemsAsync (document, currentDescriptors, token).ConfigureAwait (false);

					bool raise;
					lock (gate) {
						state.Versions[id] = version;
						if (items.Length > 0) {
							state.WithItems.Add (id);
							raise = true;
						} else {
							raise = state.WithItems.Remove (id);
						}
					}
					if (raise)
						TaskListUpdated?.Invoke (null, new TaskListUpdatedEventArgs (workspace, solution, id, items));
				} catch (OperationCanceledException) {
					return;
				} catch (Exception e) {
					LoggingService.LogError ("Error while computing task list items", e);
				}
			}
		}

		sealed class WorkspaceState : IDisposable
		{
			public readonly HashSet<DocumentId> Pending = new HashSet<DocumentId> ();
			public readonly Dictionary<DocumentId, VersionStamp> Versions = new Dictionary<DocumentId, VersionStamp> ();
			public readonly HashSet<DocumentId> WithItems = new HashSet<DocumentId> ();
			public readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource ();
			public bool Scheduled;

			public void Dispose ()
			{
				// Running computations hold the token, which stays valid (and cancelled) after disposal.
				CancellationTokenSource.Cancel ();
				CancellationTokenSource.Dispose ();
			}
		}
	}
}
