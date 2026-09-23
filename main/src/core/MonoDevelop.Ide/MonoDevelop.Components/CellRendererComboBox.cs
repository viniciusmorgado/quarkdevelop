//
// CellRendererComboBox.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;
using Gdk;

namespace MonoDevelop.Components
{
	public class CellRendererComboBox: CellRendererText
	{
		string[] values;
		string path;
		int rowHeight;
		
		public CellRendererComboBox ()
		{
			Mode |= Gtk.CellRendererMode.Editable;
			var dummyEntry = new Gtk.ComboBoxText ();
			rowHeight = dummyEntry.SizeRequest ().Height + (2 * dummyEntry.Style?.Ythickness ?? 0);
			dummyEntry.Destroy ();
			Ypad = 0;
		}

		public string[] Values {
			get { return values; }
			set { values = value; }
		}
		
		protected override void OnGetSize (Gtk.Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
		{
			Gtk3BaseGetSize (widget, ref cell_area, out x_offset, out y_offset, out width, out height);
			if (height < rowHeight)
				height = rowHeight;
		}

		protected override void OnGetPreferredWidth (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			var area = Gdk.Rectangle.Zero;
			OnGetSize (widget, ref area, out _, out _, out natural_size, out _);
			minimum_size = natural_size;
		}

		protected override void OnGetPreferredHeight (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			var area = Gdk.Rectangle.Zero;
			OnGetSize (widget, ref area, out _, out _, out _, out natural_size);
			minimum_size = natural_size;
		}

		protected override void OnGetPreferredHeightForWidth (Gtk.Widget widget, int width, out int minimum_height, out int natural_height)
		{
			OnGetPreferredHeight (widget, out minimum_height, out natural_height);
		}

		protected override void OnGetPreferredWidthForHeight (Gtk.Widget widget, int height, out int minimum_width, out int natural_width)
		{
			OnGetPreferredWidth (widget, out minimum_width, out natural_width);
		}

		void Gtk3BaseGetSize (Gtk.Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
		{
			base.OnGetPreferredWidth (widget, out _, out width);
			base.OnGetPreferredHeightForWidth (widget, width, out _, out height);
			MonoDevelop.Components.Gtk3CompatExtensions.Gtk3CalcOffset (this, widget, cell_area, width, height, out x_offset, out y_offset);
		}
		
		protected override ICellEditable OnStartEditing (Gdk.Event ev, Widget widget, string path, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, CellRendererState flags)
		{
			this.path = path;

			var combo = new Gtk.ComboBoxText ();
			foreach (string s in values)
				combo.AppendText (s);
			
			combo.Active = Array.IndexOf (values, Text);
			combo.Changed += new EventHandler (SelectionChanged);
			return new TreeViewCellContainer (combo);
		}
		
		void SelectionChanged (object s, EventArgs a)
		{
			var combo = (Gtk.ComboBoxText) s;
			if (Changed != null)
				Changed (this, new ComboSelectionChangedArgs (path, combo.Active, combo.ActiveText));
		}
		
		// Fired when the selection changes
		public event ComboSelectionChangedHandler Changed;
	}
	
	public delegate void ComboSelectionChangedHandler (object sender, ComboSelectionChangedArgs args);
	
	public class ComboSelectionChangedArgs: EventArgs 
	{
		string path;
		int active;
		string activeText;
		
		public ComboSelectionChangedArgs (string path, int active, string activeText)
		{
			this.path = path;
			this.active = active;
			this.activeText = activeText;
		}
		
		public string Path {
			get { return path; }
		}
		
		public int Active {
			get { return active; }
		}
		
		public string ActiveText {
			get { return activeText; }
		}
	}
}
