//
// RoundedFrame.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Cairo;

using MonoDevelop.Components.Theming;

namespace MonoDevelop.Components
{
	public class RoundedFrame : Bin, IScrollableImplementor
	{
		private Theme theme;

		private int frame_width = 3;

		private Widget child;
		private Gdk.Rectangle child_allocation;
		private bool fill_color_set;
		private Cairo.Color fill_color;
		private bool draw_border = true;
		private Pattern fill_pattern;

		// Ugh, this is to avoid the GLib.MissingIntPtrCtorException seen by some; BGO #552169
		protected RoundedFrame (IntPtr ptr) : base(ptr)
		{
		}

		public RoundedFrame ()
		{
			this.Events =  Gdk.EventMask.AllEventsMask;
			HasWindow = false;
			DoubleBuffered = true;
			AppPaintable = false;
		}

		public void SetFillColor (Cairo.Color color)
		{
			// GTK3 has no base color (ModifyBase); ModifyBg below sets the background.
			this.ModifyBg (Gtk.StateType.Normal, CairoExtensions.CairoColorToGdkColor (color));
			fill_color = color;
			fill_color_set = true;
			QueueDraw ();
		}

		public void UnsetFillColor ()
		{
			fill_color_set = false;
			QueueDraw ();
		}

		public Pattern FillPattern {
			get { return fill_pattern; }
			set {
				fill_pattern = value;
				QueueDraw ();
			}
		}

		public bool DrawBorder {
			get { return draw_border; }
			set {
				draw_border = value;
				QueueDraw ();
			}
		}

		#region Gtk.Widget Overrides

		protected override void OnRealized ()
		{
			base.OnRealized ();
			theme = MonoDevelop.Components.Theming.ThemeEngine.CreateTheme (this);
		}

		Gtk.Requisition Gtk3SizeRequest ()
		{
			var requisition = new Gtk.Requisition ();
			if (child != null && child.Visible) {
				// Add the child's width/height
				Requisition child_requisition = child.SizeRequest ();
				requisition.Width = Math.Max (0, child_requisition.Width);
				requisition.Height = child_requisition.Height;
			} else {
				requisition.Width = 0;
				requisition.Height = 0;
			}
			
			// Add the frame border
			requisition.Width += ((int)BorderWidth + frame_width) * 2;
			requisition.Height += ((int)BorderWidth + frame_width) * 2;
			return requisition;
		}

		protected override void OnGetPreferredWidth (out int minimum_width, out int natural_width)
		{
			minimum_width = natural_width = Gtk3SizeRequest ().Width;
		}

		protected override void OnGetPreferredHeight (out int minimum_height, out int natural_height)
		{
			minimum_height = natural_height = Gtk3SizeRequest ().Height;
		}

		protected override void OnSizeAllocated (Gdk.Rectangle allocation)
		{
			base.OnSizeAllocated (child_allocation);
			
			if (child == null || !child.Visible)
				return;
			
			int border = (int)BorderWidth + frame_width;
			
			child_allocation = new Gdk.Rectangle (allocation.X + border,
			                                      allocation.Y + border,
			                                      (int)Math.Max (1, allocation.Width - border * 2),
			                                      (int)Math.Max (1, allocation.Height - border * 2));
			
			child.SizeAllocate (child_allocation);
		}

		// Gtk.IScrollable is implemented to satisfy GtkScrolledWindow, which would otherwise wrap
		// this non-scrollable child in a viewport (GTK2 complained instead); the adjustments are
		// only stored, as GTK2's set-scroll-adjustments handler ignored them.
		public Adjustment Hadjustment { get; set; }

		public Adjustment Vadjustment { get; set; }

		public ScrollablePolicy HscrollPolicy { get; set; }

		public ScrollablePolicy VscrollPolicy { get; set; }

		public bool GetBorder (out Gtk.Border border)
		{
			border = default (Gtk.Border);
			return false;
		}

		protected override bool OnDrawn (Cairo.Context gtk3cr)
		{
			var evnt = new MonoDevelop.Components.Gtk3ExposeEvent (this, gtk3cr);
			if (!IsDrawable) {
				return false;
			}
			
			using (Context cr = evnt.CreateContext ()) {
				DrawFrame (cr, evnt.Area);
				if (child != null) 
					PropagateDraw (child, gtk3cr);
				return false;
			}
		}

		private void DrawFrame (Cairo.Context cr, Gdk.Rectangle clip)
		{
			int x = child_allocation.X - frame_width;
			int y = child_allocation.Y - 2 * frame_width - 1;
			int width = child_allocation.Width + 2 * frame_width;
			int height = child_allocation.Height + 3 * frame_width;
			
			Gdk.Rectangle rect = new Gdk.Rectangle (x, y, width, height);
			
			theme.Context.ShowStroke = draw_border;
			
			if (fill_color_set) {
				theme.DrawFrameBackground (cr, rect, fill_color);
				theme.DrawFrameBorder (cr, rect);
			} else if (fill_pattern != null) {
				theme.DrawFrameBackground (cr, rect, fill_pattern);
			} else {
				theme.DrawFrameBackground (cr, rect, true);
				theme.DrawFrameBorder (cr, rect);
			}
		}

		#endregion

		#region Gtk.Container Overrides

		protected override void OnAdded (Widget widget)
		{
			child = widget;
			base.OnAdded (widget);
		}

		protected override void OnRemoved (Widget widget)
		{
			if (child == widget) {
				child = null;
			}
			
			base.OnRemoved (widget);
		}
		
		#endregion
		
	}
}
