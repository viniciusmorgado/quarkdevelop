//
// BooleanEditorCell.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
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
using System.ComponentModel;

namespace MonoDevelop.Components.PropertyGrid.PropertyEditors
{
	[PropertyEditorType (typeof (bool))]
	public class BooleanEditorCell : PropertyEditorCell 
	{
		static int indicatorSize;
		static int indicatorSpacing;
		static bool styleInitialized;

		static BooleanEditorCell ()
		{
			// reinit style
			MonoDevelop.Ide.Gui.Styles.Changed += (sender, e) => styleInitialized = false;
		}

		// GTK3 style properties can be read without a realized widget
		static void InitializeStyle ()
		{
			if (styleInitialized)
				return;
			var cb = new BooleanEditor ();
			indicatorSize = (int) cb.StyleGetProperty ("indicator-size");
			indicatorSpacing = (int) cb.StyleGetProperty ("indicator-spacing");
			cb.Destroy ();
			styleInitialized = true;
		}

		public override void GetSize (int availableWidth, out int width, out int height)
		{
			InitializeStyle ();
			width = indicatorSize;
			height = indicatorSize;
		}

		public override void Render (Cairo.Context ctx, Gdk.Rectangle bounds, Gtk.StateType state)
		{
			InitializeStyle ();

			int s = indicatorSize - 1;
			if (s > bounds.Height)
				s = bounds.Height;
			if (s > bounds.Width)
				s = bounds.Width;

			RenderCheck (Container, ctx, state, (bool) Value, bounds.X + indicatorSpacing - 1, bounds.Y + (bounds.Height - s) / 2, s);
		}

		/// <summary>Draws a check box the way GtkCellRendererToggle does (GTK2 used Style.PaintCheck).</summary>
		internal static void RenderCheck (Gtk.Widget container, Cairo.Context ctx, Gtk.StateType state, bool active, int x, int y, int size)
		{
			var flags = state.ToStateFlags ();
			if (active)
				flags |= Gtk.StateFlags.Checked;
			var sc = container.StyleContext;
			sc.Save ();
			sc.AddClass ("check");
			sc.State = flags;
			sc.RenderBackground (ctx, x, y, size, size);
			sc.RenderFrame (ctx, x, y, size, size);
			sc.RenderCheck (ctx, x, y, size, size);
			sc.Restore ();
		}
		
		protected override IPropertyEditor CreateEditor (Gdk.Rectangle cell_area, Gtk.StateType state)
		{
			return new BooleanEditor { State = state };
		}
	}
	
	public class BooleanEditor : Gtk.CheckButton, IPropertyEditor 
	{
		public void Initialize (EditSession session)
		{
			if (session.Property.PropertyType != typeof(bool))
				throw new ApplicationException ("Boolean editor does not support editing values of type " + session.Property.PropertyType);
			Sensitive = !session.Property.IsReadOnly;
		}
		
		public object Value { 
			get { return Active; } 
			set { Active = (bool) value; }
		}
		
		protected override void OnToggled ()
		{
			base.OnToggled ();
			if (ValueChanged != null)
				ValueChanged (this, EventArgs.Empty);
		}

		public event EventHandler ValueChanged;
	}
}
