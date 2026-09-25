// NewFileDialog.cs
//
// Author:
//   Todd Berman  <tberman@off.net>
//   Viktoria Dudka  <viktoriad@remobjects.com>
//
// Copyright (c) 2004 Todd Berman
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
//
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Gtk;
using MonoDevelop.Components;
using MonoDevelop.Components.AtkCocoaHelper;
using MonoDevelop.Components.AutoTest;
using MonoDevelop.Core;
using MonoDevelop.Ide.Templates;
using MonoDevelop.Projects;

namespace MonoDevelop.Ide.Projects
{
	/// <summary>
	///  Creates a file from an item template of <c>dotnet new</c> (T152, ADR 0026): NUnit Test Item, Razor Page, .gitignore,
	///  global.json, Directory.Build.props… The CLI writes the file in the project folder (or the chosen folder); the
	///  project is then re-evaluated, its SDK-style globs include the new files.
	/// </summary>
	internal partial class NewFileDialog : Gtk.Dialog
	{
		List<Category> categories = new List<Category> ();

		TreeStore catStore;
		TemplateView iconView;

		// Add To Project widgets
		string[] projectNames;
		Project[] projectRefs;
		bool hasProjectChoice;

		Project parentProject;
		SolutionFolder parentSolutionFolder;
		string basePath;

		string userEditedEntryText;
		string previousDefaultEntryText;
		bool creating;

		public NewFileDialog (Project parentProject, string basePath, SolutionFolder parentSolutionFolder = null)
		{
			Build ();
			this.parentProject = parentProject;
			this.parentSolutionFolder = parentSolutionFolder;
			this.basePath = basePath;

			BorderWidth = 6;
			// GTK3 dialogs have no separator (Dialog.HasSeparator is gone)

			InitializeComponents ();

			nameEntry.GrabFocus ();

			// Accessibility
			Accessible.Name = "NewFileDialog";
			Accessible.Description = GettextCatalog.GetString ("Add a new file to the project");

			okButton.Accessible.Name = "NewFileDialog.OkButton";
			okButton.Accessible.Description = GettextCatalog.GetString ("Create the new file and close the dialog");

			cancelButton.Accessible.Name = "NewFileDialog.CancelButton";
			cancelButton.Accessible.Description = GettextCatalog.GetString ("Close the dialog without creating a new file");

			catView.Accessible.Name = "NewFileDialog.CategoryView";
			catView.Accessible.Description = GettextCatalog.GetString ("Select a category for the new file");
			catView.Accessible.SetTitle (GettextCatalog.GetString ("Categories"));

			labelTemplateTitle.Accessible.Name = "NewFileDialog.TemplateTitleLabel";
			labelTemplateTitle.Accessible.Description = GettextCatalog.GetString ("The name of the selected template");

			infoLabel.Accessible.Name = "NewFileDialog.InfoLabel";
			infoLabel.Accessible.Description = GettextCatalog.GetString ("The description of the selected template");

			iconView.Accessible.Name = "NewFileDialog.TemplateList";
			iconView.Accessible.Description = GettextCatalog.GetString ("Select a template for the new file");
			iconView.Accessible.SetTitle (GettextCatalog.GetString ("Templates"));

			nameEntry.SetCommonAccessibilityAttributes ("NewFileDialog.NameEntry",
														GettextCatalog.GetString ("Name"),
														GettextCatalog.GetString ("Enter the name of the new file"));
			nameEntry.Accessible.SetTitleUIElement (label1.Accessible);

			label1.Accessible.Name = "NewFileDialog.NameLabel";
			label1.Accessible.SetTitleFor (nameEntry.Accessible);

			projectAddCheckbox.Accessible.Name = "NewFileDialog.AddCheckbox";
			projectAddCheckbox.Accessible.Description = GettextCatalog.GetString ("Select whether to add this new file to an existing project");
			projectAddCheckbox.Accessible.AddLinkedUIElement (projectAddCombo.Accessible);

			projectAddCombo.Accessible.Name = "NewFileDialog.AddProjectCombo";
			projectAddCombo.Accessible.Description = GettextCatalog.GetString ("Select which project to add the file to");
			projectAddCombo.Accessible.SetTitleUIElement (projectAddCombo.Accessible);

			projectFolderEntry.Accessible.Name = "NewFileDialog.ProjectFolderEntry";
			projectFolderEntry.Accessible.Description = GettextCatalog.GetString ("Select which the project folder to add the file");
			projectFolderEntry.Accessible.SetLabel (GettextCatalog.GetString ("Project Folder"));
		}

		/// <summary>The project the file is added to, or null.</summary>
		Project TargetProject => !hasProjectChoice || projectAddCheckbox.Active ? parentProject : null;

		/// <summary>The folder the CLI writes to.</summary>
		string TargetDirectory {
			get {
				if (hasProjectChoice && projectAddCheckbox.Active)
					return basePath;
				if (!string.IsNullOrEmpty (projectFolderEntry.Path))
					return projectFolderEntry.Path;
				return basePath;
			}
		}

		void InitializeView ()
		{
			InsertCategories (TreeIter.Zero, categories);

			TreeIter treeIter;
			if (!FindCatIter (PropertyService.Get (GetCategoryPropertyKey (parentProject), "Config"), out treeIter)) {
				if (!catStore.GetIterFirst (out treeIter))
					return;
			}
			catView.Selection.SelectIter (treeIter);
		}

		void InsertCategories (TreeIter node, List<Category> catarray)
		{
			foreach (Category category in catarray) {
				if (TreeIter.Zero.Equals (node))
					InsertCategories (catStore.AppendValues (category.Name, category.Categories, category.Templates), category.Categories);
				else
					InsertCategories (catStore.AppendValues (node, category.Name, category.Categories, category.Templates), category.Categories);
			}
		}

		Category GetCategory (string categoryname)
		{
			foreach (Category category in categories) {
				if (category.Name == categoryname)
					return category;
			}

			Category cat = new Category (categoryname);
			categories.Add (cat);
			return cat;
		}

		void CategoryChange (object sender, EventArgs e)
		{
			ITreeModel treeModel;
			TreeIter treeIter;

			if (catView.Selection.GetSelected (out treeModel, out treeIter)) {
				FillCategoryTemplates (treeIter);
				if (!IdeServices.DesktopService.AccessibilityInUse) {
					// When accessibility is being used, don't expand rows automatically
					// as it can be confusing when using a screen reader
					catView.ExpandRow (treeModel.GetPath (treeIter), false);
				}
				UpdateOkStatus ();
			}
		}

		void InitializeDialog (bool update)
		{
			if (update) {
				categories.Clear ();
				catStore.Clear ();
			}

			InitializeTemplates ();

			if (update) {
				iconView.Clear ();
				InitializeView ();
			}
		}

		static string GetCategoryPropertyKey (Project proj)
		{
			string key = "Dialogs.NewFileDialog.LastSelectedCategory";
			if (proj != null) {
				string projectType = proj.GetTypeTags ().FirstOrDefault ();
				if (projectType != null) {
					key += "." + projectType;
					var dnp = proj as DotNetProject;
					if (dnp != null)
						key += "." + dnp.LanguageName;
				}
			}
			return key;
		}

		bool FindCatIter (string catPath, out TreeIter iter)
		{
			string[] cats = catPath.Split ('/');
			iter = TreeIter.Zero;

			TreeIter nextIter;
			if (!catStore.GetIterFirst (out nextIter))
				return false;

			for (int i = 0; i < cats.Length; i++) {
				if (FindCategoryAtCurrentLevel (cats[i], ref nextIter)) {
					iter = nextIter;
					if (i >= cats.Length - 1 || !catStore.IterChildren (out nextIter, nextIter))
						return true;
				}
			}
			return false;
		}

		bool FindCategoryAtCurrentLevel (string category, ref TreeIter iter)
		{
			TreeIter trial = iter;
			do {
				string val = (string)catStore.GetValue (trial, 0);
				if (val == category) {
					iter = trial;
					return true;
				}
			} while (catStore.IterNext (ref trial));
			return false;
		}

		string GetCatPath (TreeIter iter)
		{
			TreeIter currentIter = iter;
			string path = (string)catStore.GetValue (currentIter, 0);
			while (catStore.IterParent (out currentIter, currentIter)) {
				path = ((string)catStore.GetValue (currentIter, 0)) + "/" + path;
			}
			return path;
		}

/// <summary>Selects the template with this short name (or identity).</summary>
		public void SelectTemplate (string id)
		{
			TreeIter iter;
			if (catStore.GetIterFirst (out iter))
				SelectTemplate (iter, id);
		}

		public bool SelectTemplate (TreeIter iter, string id)
		{
			do {
				foreach (TemplateItem item in (List<TemplateItem>)(catStore.GetValue (iter, 2))) {
					if (item.Template.Identity == id || item.Template.ShortNames.Contains (id, StringComparer.OrdinalIgnoreCase)) {
						catView.ExpandToPath (catStore.GetPath (iter));
						catView.Selection.SelectIter (iter);
						CategoryChange (null, null);
						iconView.CurrentlySelected = item;
						return true;
					}
				}

				TreeIter citer;
				if (catStore.IterChildren (out citer, iter)) {
					do {
						if (SelectTemplate (citer, id))
							return true;
					} while (catStore.IterNext (ref citer));
				}

			} while (catStore.IterNext (ref iter));
			return false;
		}

		void InitializeTemplates ()
		{
			Project project = TargetProject;
			var items = DotNetNewItemTemplates.GetItemTemplates (DotNetNewTemplateCatalog.Default.GetTemplates ());
			DotNetNewTemplateCatalog.Default.RefreshInBackground ();

			foreach (var item in items) {
				foreach (string language in DotNetNewItemTemplates.GetLanguages (item, project))
					GetCategory (item.Category).Templates.Add (new TemplateItem (item, language));
			}
			categories.Sort ((x, y) => DotNetNewTemplateClassifier.CompareCategories (x.Name, y.Name));
		}

		//tree view event handler for double-click
		//toggle the expand collapse methods.
		void CategoryActivated (object sender, RowActivatedArgs args)
		{
			if (!catView.GetRowExpanded (args.Path)) {
				catView.ExpandRow (args.Path, false);
			} else {
				catView.CollapseRow (args.Path);
			}
		}

		void FillCategoryTemplates (TreeIter iter)
		{
			iconView.Clear ();
			var list = (List<TemplateItem>)(catStore.GetValue (iter, 2));
			foreach (TemplateItem item in list)
				iconView.Add (item);

			// select first template
			var templateItem = list.FirstOrDefault ();
			if (templateItem != null)
				iconView.CurrentlySelected = templateItem;
		}
		
		void SelectedTemplateChanged (object sender, EventArgs e)
		{
			var titem = iconView.CurrentlySelected;
			if (titem != null) {
				var template = titem.Template;
				labelTemplateTitle.Markup = "<b>" + GLib.Markup.EscapeText (template.Name) + "</b>";
				string command = "dotnet new " + template.ShortName;
				infoLabel.Text = string.IsNullOrEmpty (template.Description) ? command : template.Description + "\n\n" + command;
			
				string filename = GetFileNameFromEntry ();
				string name = null;
				
				// Desensitize the text entry if the name is fixed (.gitignore, global.json…).
				// Be careful to store user-entered text so we can replace it if they change their selection
				if (!template.UsesName) {
					if (userEditedEntryText == null)
						userEditedEntryText = filename;
					name = template.ShortName;
					nameEntry.Sensitive = false;
				} else {
					if (userEditedEntryText != null) {
						name = userEditedEntryText;
						userEditedEntryText = null;
					}
					nameEntry.Sensitive = true;
				}
				
				// Fill in a default name if text entry is empty or contains a default name
				if (template.UsesName && (string.IsNullOrEmpty (filename) || previousDefaultEntryText == filename)) {
					previousDefaultEntryText = GetDefaultName (template);
					name = previousDefaultEntryText;
				}
				
				if (name != null) {
					// Note: this will cause UpdateOkStatus() to be invoked via the Gtk.Entry.Changed event.
					nameEntry.Text = name;
				} else {
					UpdateOkStatus ();
				}
			} else {
				labelTemplateTitle.Text = string.Empty;
				infoLabel.Text = string.Empty;
				nameEntry.Sensitive = true;
			}
		}

		/// <summary>The template's own default name (e.g. Class1), else its name without spaces and punctuation.</summary>
		static string GetDefaultName (DotNetNewTemplate template)
		{
			if (!string.IsNullOrEmpty (template.DefaultName))
				return template.DefaultName;
			var name = new string ((template.Name ?? template.ShortName).Where (char.IsLetterOrDigit).ToArray ());
			return name.Length > 0 && !char.IsDigit (name [0]) ? name : "NewItem";
		}

		void NameChanged (object sender, EventArgs e)
		{
			UpdateOkStatus ();
		}

		string GetFileNameFromEntry ()
		{
			return nameEntry.Text.Trim ();
		}
		
		void UpdateOkStatus ()
		{
			var titem = iconView.CurrentlySelected;
			if (titem == null || creating || string.IsNullOrEmpty (TargetDirectory)) {
				okButton.Sensitive = false;
				return;
			}
			if (!titem.Template.UsesName) {
				okButton.Sensitive = true;
				return;
			}
			string filename = GetFileNameFromEntry ();
			okButton.Sensitive = filename.Length > 0 && FileService.IsValidFileName (filename) && filename.IndexOf (System.IO.Path.DirectorySeparatorChar) < 0;
		}

		public event EventHandler OnOked;

		async void OpenEvent (object sender, EventArgs e)
		{
			if (!okButton.Sensitive)
				return;

			TreeIter selectedIter;
			if (catView.Selection.GetSelected (out selectedIter))
				PropertyService.Set (GetCategoryPropertyKey (parentProject), GetCatPath (selectedIter));

			var titem = iconView.CurrentlySelected;
			string filename = GetFileNameFromEntry ();
			if (titem == null)
				return;

			Project project = TargetProject;
			string directory = TargetDirectory;
			IReadOnlyList<FilePath> created;
			creating = true;
			UpdateOkStatus ();
			try {
				created = await DotNetNewItemTemplates.CreateAsync (titem.Template, titem.Language, directory,
					titem.Template.UsesName ? filename : null, project, CancellationToken.None);
			} catch (UserException ex) {
				MessageService.ShowError (this, ex.Message, ex.Details);
				return;
			} catch (Exception ex) {
				LoggingService.LogError ("Error creating file", ex);
				MessageService.ShowError (GettextCatalog.GetString ("Error creating file"), ex);
				return;
			} finally {
				creating = false;
				UpdateOkStatus ();
			}

			if (parentSolutionFolder != null) {
				foreach (var file in created)
					parentSolutionFolder.Files.Add (file);
				IdeApp.ProjectOperations.SaveAsync (parentSolutionFolder.ParentSolution).Ignore ();
			}

			foreach (var file in created)
				IdeApp.Workbench.OpenDocument (file, project).Ignore ();

			if (OnOked != null)
				OnOked (null, null);
			Respond (Gtk.ResponseType.Ok);
			Destroy ();
		}

		/// <summary>
		///  An item template in one language
		/// </summary>
		private class TemplateItem
		{
			public TemplateItem (DotNetNewTemplateClassification classification, string language)
			{
				Classification = classification;
				Language = language;
			}

			public DotNetNewTemplateClassification Classification { get; }

			public DotNetNewTemplate Template => Classification.Template;

			public string Language { get; }

			public string Name => Template.Name;
		}

		void cancelClicked (object o, EventArgs e)
		{
			Destroy ();
		}

		void AddToProjectToggled (object o, EventArgs e)
		{
			projectAddCombo.Sensitive = projectAddCheckbox.Active;

			TemplateItem titem = iconView.CurrentlySelected;
			
			if (projectAddCheckbox.Active) {
				AddToProjectComboChanged (null, null);
			} else {
				parentProject = null;
				InitializeDialog (true);
			}

			if (titem != null)
				SelectTemplate (titem.Template.Identity);

			UpdateOkStatus ();
		}

		void AddToProjectComboChanged (object o, EventArgs e)
		{
			int which = projectAddCombo.Active;
			Project project = null;
			
			try {
				project = projectRefs[which];
			} catch (IndexOutOfRangeException) { }

			if (project != null) {
				if (basePath == null || basePath == String.Empty || (parentProject != null && basePath == parentProject.BaseDirectory)) {
					basePath = project.BaseDirectory;
					projectFolderEntry.Path = basePath;
				}

				parentProject = project;

				InitializeDialog (true);
			}
		}

		void AddToProjectPathChanged (object o, EventArgs e)
		{
			if (hasProjectChoice && projectAddCheckbox.Active)
				basePath = projectFolderEntry.Path;
			UpdateOkStatus ();
		}

		void InitializeComponents ()
		{
			iconView = new TemplateView ();
			iconView.ShowAll ();
			boxTemplates.PackStart (iconView, true, true, 0);
			
			catStore = new TreeStore (typeof(string), typeof(List<Category>), typeof(List<TemplateItem>));

			TreeViewColumn treeViewColumn = new TreeViewColumn ();
			treeViewColumn.Title = "categories";
			CellRenderer cellRenderer = new CellRendererText ();
			treeViewColumn.PackStart (cellRenderer, true);
			treeViewColumn.AddAttribute (cellRenderer, "text", 0);
			catView.AppendColumn (treeViewColumn);

			catView.Model = catStore;
			catView.SearchColumn = -1; // disable the interactive search

			okButton.Clicked += new EventHandler (OpenEvent);
			cancelButton.Clicked += new EventHandler (cancelClicked);

			nameEntry.Changed += new EventHandler (NameChanged);
			nameEntry.Activated += new EventHandler (OpenEvent);

			infoLabel.Text = string.Empty;
			labelTemplateTitle.Text = string.Empty;
			
			Project[] projects = null;
			if (parentProject == null && parentSolutionFolder == null)
				projects = IdeApp.Workspace.GetAllProjects ().ToArray ();

			if (projects != null && projects.Length > 0) {
				Project curProject = IdeApp.ProjectOperations.CurrentSelectedProject;

				hasProjectChoice = true;
				boxProject.Visible = true;
				projectAddCheckbox.Active = curProject != null;
				projectAddCheckbox.Toggled += new EventHandler (AddToProjectToggled);

				projectNames = new string[projects.Length];
				projectRefs = new Project[projects.Length];
				int i = 0;

				bool singleSolution = IdeApp.Workspace.Items.Count == 1 && IdeApp.Workspace.Items[0] is Solution;

				foreach (Project project in projects) {
					projectRefs[i] = project;
					if (singleSolution)
						projectNames[i++] = project.Name; else
						projectNames[i++] = project.ParentSolution.Name + "/" + project.Name;
				}

				Array.Sort (projectNames, projectRefs);
				i = Array.IndexOf (projectRefs, curProject);

				foreach (string pn in projectNames)
					projectAddCombo.AppendText (pn);

				projectAddCombo.Active = i != -1 ? i : 0;
				projectAddCombo.Sensitive = projectAddCheckbox.Active;
				projectAddCombo.Changed += new EventHandler (AddToProjectComboChanged);

				// the folder the file is written to, in the project or not
				if (curProject != null)
					projectFolderEntry.Path = curProject.BaseDirectory;

				if (curProject != null) {
					basePath = curProject.BaseDirectory;
					parentProject = curProject;
				}
			} else if (parentProject == null && parentSolutionFolder == null) {
				// No project: the CLI writes the file to a folder of the user's choice.
				boxProject.Visible = true;
				hbox3.Visible = false;
				projectFolderEntry.Path = !string.IsNullOrEmpty (basePath) ? basePath : (string)IdeApp.Preferences.ProjectsDefaultPath;
			} else {
				boxProject.Visible = false;
			}
			projectFolderEntry.PathChanged += new EventHandler (AddToProjectPathChanged);

			catView.Selection.Changed += new EventHandler (CategoryChange);
			catView.RowActivated += new RowActivatedHandler (CategoryActivated);
			iconView.SelectionChanged += new EventHandler (SelectedTemplateChanged);
			iconView.DoubleClicked += new EventHandler (OpenEvent);
			InitializeDialog (false);
			InitializeView ();
			UpdateOkStatus ();
		}

		protected virtual void OnScrolledInfoSizeAllocated (object o, Gtk.SizeAllocatedArgs args)
		{
			if (infoLabel.WidthRequest != scrolledInfo.Allocation.Width) {
				infoLabel.WidthRequest = scrolledInfo.Allocation.Width;
				labelTemplateTitle.WidthRequest = scrolledInfo.Allocation.Width;
			}
		}
		
		class Category
		{
			public Category (string name)
			{
				Name = name;
			}

			public string Name { get; }

			public List<Category> Categories { get; } = new List<Category> ();

			public List<TemplateItem> Templates { get; } = new List<TemplateItem> ();
		}
		
		class TemplateView: ScrolledWindow
		{
			TemplateTreeView tree;
			
			public TemplateView ()
			{
				tree = new TemplateTreeView ();
				tree.Selection.Changed += delegate {
					if (SelectionChanged != null)
						SelectionChanged (this, EventArgs.Empty);
				};
				tree.RowActivated += delegate {
					if (DoubleClicked != null)
						DoubleClicked (this, EventArgs.Empty);
				};
				Add (tree);
				HscrollbarPolicy = PolicyType.Automatic;
				VscrollbarPolicy = PolicyType.Automatic;
				ShadowType = ShadowType.In;
				ShowAll ();
			}
			
			public TemplateItem CurrentlySelected {
				get { return tree.CurrentlySelected; }
				set { tree.CurrentlySelected = value; }
			}
			
			public void Add (TemplateItem templateItem)
			{
				tree.Add (templateItem);
			}
			
			public void Clear ()
			{
				tree.Clear ();
			}
			
			public event EventHandler SelectionChanged;
			public event EventHandler DoubleClicked;
		}
			
		class TemplateTreeView: TreeView
		{
			Gtk.ListStore templateStore;
			
			public TemplateTreeView ()
			{
				HeadersVisible = false;
				templateStore = new ListStore (typeof(string), typeof(string), typeof(TemplateItem));
				Model = templateStore;
				SearchColumn = -1; // disable the interactive search

				SemanticModelAttribute modelAttr = new SemanticModelAttribute ("templateStore__Icon", "templateStore__Name", "templateStore__Template");
				TypeDescriptor.AddAttributes (templateStore, modelAttr);
				
				TreeViewColumn col = new TreeViewColumn ();
				CellRendererImage crp = new CellRendererImage ();
				crp.StockSize = Gtk.IconSize.Dnd;
				crp.Ypad = 2;
				col.PackStart (crp, false);
				col.AddAttribute (crp, "stock-id", 0);
				
				CellRendererText crt = new CellRendererText ();
				col.PackStart (crt, false);
				col.AddAttribute (crt, "markup", 1);
				
				AppendColumn (col);
				ShowAll ();
			}
			
			public TemplateItem CurrentlySelected {
				get {
					Gtk.TreeIter iter;
					if (!Selection.GetSelected (out iter))
						return null;
					return (TemplateItem) templateStore.GetValue (iter, 2);
				}
				set {
					Gtk.TreeIter iter;
					if (templateStore.GetIterFirst (out iter)) {
						do {
							TemplateItem t = (TemplateItem) templateStore.GetValue (iter, 2);
							if (t == value) {
								Selection.SelectIter (iter);
								return;
							}
						} while (templateStore.IterNext (ref iter));
					}
				}
			}
			
			public void Add (TemplateItem templateItem)
			{
				string name = GLib.Markup.EscapeText (templateItem.Name);
				string detail = string.IsNullOrEmpty (templateItem.Language) ? templateItem.Template.ShortName : templateItem.Language;
				name += "\n<span foreground='darkgrey'><small>" + GLib.Markup.EscapeText (detail) + "</small></span>";
				templateStore.AppendValues ("md-file-source", name, templateItem);
			}
			
			public void Clear ()
			{
				templateStore.Clear ();
			}
		}
	}
}
