//
// PackageSourceCellRenderer.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
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
using Gtk;
using MonoDevelop.PackageManagement;
using MonoDevelop.Components;
using MonoDevelop.Ide;
using Gdk;

namespace MonoDevelop.PackageManagement
{
	internal class PackageSourceCellRenderer : CellRenderer
	{
		[GLib.Property("package-source")]
		public PackageSourceViewModel PackageSourceViewModel { get; set; }

		[GLib.Property("text")]
		public string Text {
			get {
				if (PackageSourceViewModel == null) {
					return "";
				}

				return PackageSourceViewModel.Name + " - " + PackageSourceViewModel.Source;
			}
		}

		static Xwt.Drawing.Image warningImage = ImageService.GetIcon (MonoDevelop.Ide.Gui.Stock.Warning, Gtk.IconSize.Menu);
		const int imageSpacing = 5;
		const int textSpacing = 7;
		const int textTopSpacing = 3;

		protected override void OnRender (Cairo.Context gtk3cr, Gtk.Widget widget, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, Gtk.CellRendererState flags)
		{
			base.OnRender (gtk3cr, widget, background_area, cell_area, flags);

			if (PackageSourceViewModel == null)
				return;
				
			using (var layout = new Pango.Layout (widget.PangoContext)) {
				layout.Alignment = Pango.Alignment.Left;
				layout.SetMarkup (GetPackageSourceNameMarkup ());
				int packageSourceNameWidth = GetLayoutWidth (layout);
				StateType state = GetState (widget, flags);

				layout.SetMarkup (GetPackageSourceDescriptionMarkup (flags));

				gtk3cr.DrawLayout (widget, state, cell_area.X + textSpacing, cell_area.Y + textTopSpacing, layout);

				if (!PackageSourceViewModel.IsValid) {
					using (var ctx = gtk3cr.CreateSharedContext ()) {
						ctx.DrawImage (widget, warningImage, cell_area.X + textSpacing + packageSourceNameWidth + imageSpacing, cell_area.Y + textTopSpacing);
					}

					layout.SetMarkup (GetPackageSourceErrorMarkup (flags));
					int packageSourceErrorTextX = cell_area.X + textSpacing + packageSourceNameWidth + (int)warningImage.Width + (2 * imageSpacing);
					gtk3cr.DrawLayout (widget, state, packageSourceErrorTextX, cell_area.Y + textTopSpacing, layout);
				}
			}
		}

		new StateType GetState (Widget widget, CellRendererState flags)
		{
			if (flags.HasFlag (CellRendererState.Selected)) {
				if (widget.IsFocus) {
					return StateType.Selected;
				}
				return StateType.Active;
			}
			return StateType.Normal;
		}

		string GetPackageSourceNameMarkup ()
		{
			return MarkupString.Format (
				"<b>{0}</b>",
				PackageSourceViewModel.Name);
		}

		int GetLayoutWidth (Pango.Layout layout)
		{
			return GetLayoutSize (layout).Width;
		}

		Size GetLayoutSize (Pango.Layout layout)
		{
			int width;
			int height;
			layout.GetPixelSize (out width, out height);

			return new Size (width, height);
		}

		string GetPackageSourceDescriptionMarkup (CellRendererState flags = CellRendererState.Focused)
		{
			return MarkupString.Format (
				"<b>{0}</b>\n<span foreground='{2}'>{1}</span>",
				PackageSourceViewModel.Name,
				PackageSourceViewModel.Source,
				Ide.Gui.Styles.ColorGetHex (flags.HasFlag (CellRendererState.Selected) ? Styles.PackageSourceUrlSelectedTextColor : Styles.PackageSourceUrlTextColor));
		}

		string GetPackageSourceErrorMarkup (CellRendererState flags = CellRendererState.Focused)
		{
			return MarkupString.Format (
				"<span foreground='{0}'>{1}</span>",
				Ide.Gui.Styles.ColorGetHex (flags.HasFlag (CellRendererState.Selected) ? Styles.PackageSourceErrorSelectedTextColor : Styles.PackageSourceErrorTextColor),
				PackageSourceViewModel.ValidationFailureMessage);
		}

		protected override void OnGetSize (Gtk.Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
		{
			Gtk3BaseGetSize (widget, ref cell_area, out x_offset, out y_offset, out width, out height);

			using (var layout = new Pango.Layout (widget.PangoContext)) {
				layout.SetMarkup (GetPackageSourceDescriptionMarkup ());
				height = GetLayoutSize (layout).Height + 8 + textTopSpacing;
			}
		}

		protected override void OnGetPreferredWidth (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			var area = Gdk.Rectangle.Zero;
			OnGetSize (widget, ref area, out _, out _, out int width, out _);
			Gtk3Compat.SetPreferredSize (out minimum_size, out natural_size, width);
		}

		protected override void OnGetPreferredHeight (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			var area = Gdk.Rectangle.Zero;
			OnGetSize (widget, ref area, out _, out _, out _, out int height);
			Gtk3Compat.SetPreferredSize (out minimum_size, out natural_size, height);
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
			x_offset = y_offset = width = height = 0;
		}
	}
}

