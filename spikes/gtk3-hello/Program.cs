// Spike T006: GtkSharp 3 window on .NET 10 (run under xvfb-run inside the dev container).
using System;
using Gtk;

static class Program
{
	static int Main (string[] args)
	{
		Application.Init ();
		var win = new Window ("MonoDevelop GTK3 spike");
		win.SetDefaultSize (480, 200);
		var box = new Box (Orientation.Vertical, 6);
		box.PackStart (new Label ($"GTK {Global.MajorVersion}.{Global.MinorVersion}.{Global.MicroVersion} on .NET {Environment.Version}"), true, true, 0);
		var area = new DrawingArea ();
		area.SetSizeRequest (480, 80);
		area.Drawn += (o, e) => {
			var cr = e.Cr;
			cr.SetSourceRGB (0.2, 0.4, 0.8);
			cr.Rectangle (10, 10, 460, 60);
			cr.Fill ();
		};
		box.PackStart (area, false, false, 0);
		win.Add (box);
		win.DeleteEvent += (o, e) => Application.Quit ();
		win.ShowAll ();
		Console.WriteLine ($"gtk-version={Global.MajorVersion}.{Global.MinorVersion}.{Global.MicroVersion} runtime={Environment.Version}");
		// Quit automatically so the spike can run unattended.
		GLib.Timeout.Add ((uint)(args.Length > 0 ? int.Parse (args [0]) : 1500), () => { Application.Quit (); return false; });
		Application.Run ();
		Console.WriteLine ("gtk3-hello: OK");
		return 0;
	}
}
