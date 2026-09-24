//
// CommentTasksProvider.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2018
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
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;
using System.Linq;
using System.Threading.Tasks;
using MonoDevelop.Ide.Composition;

namespace MonoDevelop.Ide.Tasks
{
	partial class CommentTasksProvider
	{

		public static void Initialize ()
		{
			// Roslyn 5.9 has no ITodoListProvider; MonoDevelopTaskListProvider computes the task list items.
			MonoDevelopTaskListProvider.TaskListUpdated += OnTodoListUpdated;
			UpdateTokens ();

			Runtime.ServiceProvider.WhenServiceInitialized<RootWorkspace> (workspace => {
				workspace.WorkspaceItemClosed += OnWorkspaceItemClosed;
				Legacy.Initialize ();
			});
			CommentTag.SpecialCommentTagsChanged += OnSpecialTagsChanged;
		}

		static bool TryGetDocument (TaskListUpdatedEventArgs args, out Document doc, out MonoDevelop.Projects.Project project)
		{
			doc = null;
			project = null;

			if (!(args.Workspace is MonoDevelopWorkspace ws))
				return false;

			doc = ws.GetDocument (args.DocumentId);
			if (doc == null)
				return false;

			project = ws.GetMonoProject (args.ProjectId);
			if (project == null)
				return false;

			return true;
		}

		static object lockObject = new object ();
		static Dictionary<object, TaskListUpdatedEventArgs> cachedUntilViewCreated = new Dictionary<object, TaskListUpdatedEventArgs> ();

		internal static void LoadCachedContents ()
		{
			lock (lockObject) {
				if (cachedUntilViewCreated == null)
					return;

				if (triggerLoad == null || triggerLoad.Invoke (cachedUntilViewCreated.Count)) {
					var changes = cachedUntilViewCreated.Values.Select (x => ToCommentTaskChange (x)).Where (x => x != null).ToList ();
					IdeServices.TaskService.InformCommentTasks (new CommentTasksChangedEventArgs (changes));
					cachedUntilViewCreated = null;
					triggerLoad = null;
				}
			}
		}

		public static CommentTaskChange ToCommentTaskChange (TaskListUpdatedEventArgs args)
		{
			if (!TryGetDocument (args, out var doc, out var project))
				return null;

			var file = doc.FilePath;
			var tags = args.TaskListItems.Length == 0 ? (IReadOnlyList<Tag>)null : args.TaskListItems.Select (x => x.ToTag ()).ToArray ();
			return new CommentTaskChange (file, tags, project);
		}

		// Test helper utilities so we can test multiple tests for cached
		#region Test Helpers
		static Func<int, bool> triggerLoad;
		internal static void ResetCachedContents (Func<int, bool> shouldTriggerLoad)
		{
			lock (lockObject) {
				cachedUntilViewCreated = new Dictionary<object, TaskListUpdatedEventArgs> ();
				triggerLoad = shouldTriggerLoad;
			}
		}

		internal static int GetCachedContentsCount ()
		{
			lock (lockObject) {
				if (cachedUntilViewCreated == null)
					return -1;
				return cachedUntilViewCreated.Count;
			}
		}

		#endregion

		static bool TryCache (TaskListUpdatedEventArgs args)
		{
			if (cachedUntilViewCreated == null)
				return false;

			lock (lockObject) {
				if (cachedUntilViewCreated == null)
					return false;

				cachedUntilViewCreated [args.Id] = args;

				if (triggerLoad != null)
					LoadCachedContents ();
				return true;
			}
		}

		static void OnTodoListUpdated (object sender, TaskListUpdatedEventArgs args)
		{
			if (TryCache (args))
				return;

			var change = ToCommentTaskChange (args);
			if (change != null)
				IdeServices.TaskService.InformCommentTasks (new CommentTasksChangedEventArgs (new [] { change }));
		}

		static void OnWorkspaceItemClosed (object sender, WorkspaceItemEventArgs args)
		{
			if (args.Item is MonoDevelop.Projects.Solution sol) {
				var ws = IdeApp.TypeSystemService.GetWorkspace (sol);

				lock (lockObject) {
					if (cachedUntilViewCreated == null)
						return;

					cachedUntilViewCreated = cachedUntilViewCreated.Where (x => x.Value.Workspace != ws).ToDictionary (x => x.Key, x => x.Value);
				}
			}
		}

		// Roslyn 5.9: the TODO token list is no longer a workspace option; tokens are passed to the task list provider.
		static void UpdateTokens ()
		{
			MonoDevelopTaskListProvider.SetTokens (CommentTag.SpecialCommentTags.Select (t => t.Tag + ":" + t.Priority));
		}

		static void OnSpecialTagsChanged (object sender, EventArgs args)
		{
			UpdateTokens ();
		}
	}
}
