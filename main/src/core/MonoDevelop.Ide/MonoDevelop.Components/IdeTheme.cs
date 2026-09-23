//
// ThemeExtensions.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
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
using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System.Linq;

#if MAC
using AppKit;
using Foundation;
using MonoDevelop.Components.Mac;
#endif

namespace MonoDevelop.Components
{
	public static class IdeTheme
	{
		internal static string DefaultTheme;

		public static Theme UserInterfaceTheme { get; private set; }

		static bool? highContrastThemeEnabled;
		internal static bool HighContrastThemeEnabled {
			get {
				return highContrastThemeEnabled ?? false;
			}
			set {
				if (highContrastThemeEnabled != value) {
					highContrastThemeEnabled = value;
					UpdateStyles ();
				}
			}
		}

		internal static bool AccessibilityEnabled { get; private set; }

		internal static void InitializeGtk (string progname, ref string[] args)
		{
			if (Gtk.Settings.Default != null)
				throw new InvalidOperationException ("Gtk already initialized!");

			IdeStartupTracker.StartupTracker.MarkSection ("PreGtkInitialization");
#if MAC
			// Early init Cocoa through xwt
			var loaded = NativeToolkitHelper.LoadCocoa ();
			IdeStartupTracker.StartupTracker.MarkSection ("XamarinMacInitialization");

			var disableA11y = Environment.GetEnvironmentVariable ("DISABLE_ATKCOCOA");
			if (Platform.IsMac && (NSUserDefaults.StandardUserDefaults.BoolForKey ("com.monodevelop.AccessibilityEnabled") && string.IsNullOrEmpty (disableA11y))) {
				// Load a private version of AtkCocoa stored in the XS app directory
				var appDir = Directory.GetParent (AppDomain.CurrentDomain.BaseDirectory);
				var gtkPath = $"{appDir.Parent.FullName}/lib/gtk-2.0";

				LoggingService.LogInfo ($"Loading modules from {gtkPath}");
				Environment.SetEnvironmentVariable ("GTK_MODULES", $"{gtkPath}/libatkcocoa.so");
				AccessibilityEnabled = true;
			} else {
				// If we are restarted from a running instance when changing the accessibility setting then
				// we inherit the environment from it
				Environment.SetEnvironmentVariable ("GTK_MODULES", null);
				LoggingService.LogInfo ("Accessibility disabled");
				AccessibilityEnabled = false;
			}
#endif
			Gtk.Application.Init (BrandingService.ApplicationName, ref args);
#if MAC
			// Reset our environment after initialization on Mac
			if (Platform.IsMac)
				Environment.SetEnvironmentVariable ("GTK_MODULES", null);
#endif
		}

		internal static void SetupXwtTheme ()
		{
			Xwt.Drawing.Context.RegisterStyles ("dark", "disabled", "error", "contrast");

			if (Core.Platform.IsMac) {
				Xwt.Drawing.Context.RegisterStyles ("mac", "sel");
				Xwt.Drawing.Context.SetGlobalStyle ("mac");
			} else if (Core.Platform.IsWindows) {
				Xwt.Drawing.Context.RegisterStyles ("win");
				Xwt.Drawing.Context.SetGlobalStyle ("win");
			} else if (Core.Platform.IsLinux) {
				Xwt.Drawing.Context.RegisterStyles ("linux");
				Xwt.Drawing.Context.SetGlobalStyle ("linux");
			}

			Xwt.Toolkit.CurrentEngine.RegisterBackend <Xwt.Backends.IWindowBackend, ThemedGtkWindowBackend>();
			Xwt.Toolkit.CurrentEngine.RegisterBackend <Xwt.Backends.IDialogBackend, ThemedGtkDialogBackend>();
		}

		internal static void SetupGtkTheme ()
		{
			if (Gtk.Settings.Default == null)
				return;

			DefaultTheme = GtkThemes.GetCurrent (Gtk.Settings.Default);
			string theme = IdeApp.Preferences.UserInterfaceThemeName;
			if (string.IsNullOrEmpty (theme))
				theme = DefaultTheme;
			ValidateGtkTheme (ref theme);
			GtkThemes.Apply (Gtk.Settings.Default, theme);
			LogGtkTheme (theme);
		}

		internal static void UpdateGtkTheme ()
		{
			if (DefaultTheme == null)
				SetupGtkTheme ();

			// GTK 3: the IDE uses the GTK theme and its dark variant ("Name:dark"); the bundled GTK 2 gtkrc
			// themes (Xamarin engine, Mac/Windows) are gone. "(Default)" (empty) is the theme GTK started with.
			string theme = IdeApp.Preferences.UserInterfaceThemeName;
			if (string.IsNullOrEmpty (theme))
				theme = DefaultTheme;
			if (Gtk.Settings.Default != null && GtkThemes.GetCurrent (Gtk.Settings.Default) != theme) {
				GtkThemes.Apply (Gtk.Settings.Default, theme);
				LogGtkTheme (theme);
			}

			// let Gtk realize the new theme
			// Style is being updated by DefaultWorkbench.OnStyleSet ()
			// This ensures that the theme and all styles have been loaded when
			// the Styles.Changed event is raised.
			//GLib.Timeout.Add (50, delegate { UpdateStyles(); return false; });
		}


		static void LogGtkTheme (string theme)
		{
			var dir = GtkThemes.FindThemeDirectory (theme, GtkThemes.GetSearchDirectories ());
			LoggingService.LogInfo ("GTK: Using Gtk theme {0} from {1}", theme, dir ?? "GTK (built in)");
		}

		internal static void UpdateStyles ()
		{
			if (Platform.IsLinux) {
				var bgColor = IdeApp.Workbench.RootWindow.GetStyleBackgroundColor (Gtk.StateType.Normal);
				UserInterfaceTheme = HslColor.Brightness (bgColor) < 0.5 ? Theme.Dark : Theme.Light;
			}

			if (UserInterfaceTheme == Theme.Dark)
				Xwt.Drawing.Context.SetGlobalStyle ("dark");
			else
				Xwt.Drawing.Context.ClearGlobalStyle ("dark");

			if (HighContrastThemeEnabled)
				Xwt.Drawing.Context.SetGlobalStyle ("contrast");
			else
				Xwt.Drawing.Context.ClearGlobalStyle ("contrast");

			Styles.LoadStyle ();
			UpdateXwtDefaults ();
			#if MAC
			UpdateMacWindows ();
			#endif
		}

		static void UpdateXwtDefaults ()
		{
			// Xwt default dialog icons
			Xwt.Toolkit.CurrentEngine.Defaults.MessageDialog.InformationIcon = ImageService.GetIcon ("gtk-dialog-info", Gtk.IconSize.Dialog);
			Xwt.Toolkit.CurrentEngine.Defaults.MessageDialog.WarningIcon = ImageService.GetIcon ("gtk-dialog-warning", Gtk.IconSize.Dialog);
			Xwt.Toolkit.CurrentEngine.Defaults.MessageDialog.ErrorIcon = ImageService.GetIcon ("gtk-dialog-error", Gtk.IconSize.Dialog);
			Xwt.Toolkit.CurrentEngine.Defaults.MessageDialog.QuestionIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog);
			Xwt.Toolkit.CurrentEngine.Defaults.MessageDialog.ConfirmationIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog);

			if (Platform.IsMac && UserInterfaceTheme == Theme.Dark) {
				// dark NSAppearance can not handle custom drawn images in dialogs
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.InformationIcon = ImageService.GetIcon ("gtk-dialog-info", Gtk.IconSize.Dialog).ToBitmap (GtkWorkarounds.GetScaleFactor ());
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.WarningIcon = ImageService.GetIcon ("gtk-dialog-warning", Gtk.IconSize.Dialog).ToBitmap (GtkWorkarounds.GetScaleFactor ());
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.ErrorIcon = ImageService.GetIcon ("gtk-dialog-error", Gtk.IconSize.Dialog).ToBitmap (GtkWorkarounds.GetScaleFactor ());
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.QuestionIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog).ToBitmap (GtkWorkarounds.GetScaleFactor ());
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.ConfirmationIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog).ToBitmap (GtkWorkarounds.GetScaleFactor ());
			} else {
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.InformationIcon = ImageService.GetIcon ("gtk-dialog-info", Gtk.IconSize.Dialog);
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.WarningIcon = ImageService.GetIcon ("gtk-dialog-warning", Gtk.IconSize.Dialog);
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.ErrorIcon = ImageService.GetIcon ("gtk-dialog-error", Gtk.IconSize.Dialog);
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.QuestionIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog);
				Xwt.Toolkit.NativeEngine.Defaults.MessageDialog.ConfirmationIcon = ImageService.GetIcon ("gtk-dialog-question", Gtk.IconSize.Dialog);
			}

			Xwt.Toolkit.CurrentEngine.Defaults.FallbackLinkColor = Styles.LinkForegroundColor;
			Xwt.Toolkit.NativeEngine.Defaults.FallbackLinkColor = Styles.LinkForegroundColor;
		}

		internal static string[] gtkThemeFallbacks = new string[] {
			GtkThemes.DefaultTheme // built into GTK 3, always available
		};

		static void ValidateGtkTheme (ref string theme)
		{
			if (!MonoDevelop.Ide.Gui.OptionPanels.IDEStyleOptionsPanelWidget.IsBadGtkTheme (theme))
				return;

			var themes = MonoDevelop.Ide.Gui.OptionPanels.IDEStyleOptionsPanelWidget.InstalledThemes;

			string fallback = gtkThemeFallbacks
				.Select (fb => themes.FirstOrDefault (t => string.Compare (fb, t, StringComparison.OrdinalIgnoreCase) == 0))
				.FirstOrDefault (t => t != null);

			string message = "Theme Not Supported";

			string detail;
			if (themes.Count > 0) {
				detail =
					"Your system is using the '{0}' GTK+ theme, which is known to be very unstable. MonoDevelop will " +
					"now switch to an alternate GTK+ theme.\n\n" +
					"This message will continue to be shown at startup until you set a alternate GTK+ theme as your " +
					"default in the GTK+ Theme Selector or MonoDevelop Preferences.";
			} else {
				detail =
					"Your system is using the '{0}' GTK+ theme, which is known to be very unstable, and no other GTK+ " +
					"themes appear to be installed. Please install another GTK+ theme.\n\n" +
					"This message will continue to be shown at startup until you install a different GTK+ theme and " +
					"set it as your default in the GTK+ Theme Selector or MonoDevelop Preferences.";
			}

			MessageService.GenericAlert (Gtk.Stock.DialogWarning, message, BrandingService.BrandApplicationName (detail), AlertButton.Ok);

			theme = fallback ?? themes.FirstOrDefault () ?? theme;
		}

#if MAC
		static Dictionary<NSWindow, NSObject> nsWindows = new Dictionary<NSWindow, NSObject> ();

		internal static NSAppearance GetAppearance ()
		{
			return IdeApp.Preferences.UserInterfaceTheme == Theme.Light
				? NSAppearance.GetAppearance (NSAppearance.NameAqua)
				: MacSystemInformation.OsVersion < MacSystemInformation.Mojave
					? NSAppearance.GetAppearance (NSAppearance.NameVibrantDark)
					: NSAppearance.GetAppearance (new NSString ("NSAppearanceNameDarkAqua"));
		}

		public static void ApplyTheme (NSWindow window)
		{
			if (!nsWindows.ContainsKey(window)) {
				nsWindows [window] = NSNotificationCenter.DefaultCenter.AddObserver (NSWindow.WillCloseNotification, OnClose, window);
				SetTheme (window);
			}
		}

		static void SetTheme (NSWindow window)
		{
			window.Appearance = GetAppearance ();

			if (IdeApp.Preferences.UserInterfaceTheme == Theme.Light) {
				window.StyleMask &= ~NSWindowStyle.TexturedBackground;
				window.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor.ToNSColor ();
				return;
			}

			if (window is NSPanel || window.ContentView.Class.Name != "GdkQuartzView") {
				window.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor.ToNSColor ();
				if (MacSystemInformation.OsVersion <= MacSystemInformation.Sierra)
					window.StyleMask |= NSWindowStyle.TexturedBackground;
			} else {
				object[] platforms = Mono.Addins.AddinManager.GetExtensionObjects ("/MonoDevelop/Core/PlatformService");
				if (platforms.Length > 0) {
					var platformService = (MonoDevelop.Ide.Desktop.PlatformService)platforms [0];
					var image = Xwt.Drawing.Image.FromResource (platformService.GetType().Assembly, "maintoolbarbg.png");

					window.IsOpaque = false;
					window.BackgroundColor = NSColor.FromPatternImage (image.ToBitmap().ToNSImage());
				}
				window.StyleMask |= NSWindowStyle.TexturedBackground;
			}
			if (MacSystemInformation.OsVersion >= MacSystemInformation.HighSierra && !window.IsSheet)
				window.TitlebarAppearsTransparent = true;
		}

		static void OnClose (NSNotification note)
		{
			var w = (NSWindow)note.Object;
			if (MacSystemInformation.OsVersion < MacSystemInformation.HighSierra)
				// Since HighSierra observers don't need to be removed manually, doing so
				// after a window has been released might even lead to a native crash
				// see: https://developer.apple.com/library/archive/releasenotes/Foundation/RN-Foundation/index.html#10_11NotificationCenter
				NSNotificationCenter.DefaultCenter.RemoveObserver(nsWindows[w]);
			nsWindows.Remove (w);

		}

		static void UpdateMacWindows ()
		{
			foreach (var w in nsWindows.Keys)
				SetTheme (w);
		}

		static void OnGtkWindowRealized (object s, EventArgs a)
		{
			var nsw = MonoDevelop.Components.Mac.GtkMacInterop.GetNSWindow ((Gtk.Window) s);
			if (nsw != null)
				ApplyTheme (nsw);
		}
#endif

		public static void ApplyTheme (this Gtk.Window window)
		{
			#if MAC
			window.Realized += OnGtkWindowRealized;
			if (window.IsRealized) {
				var nsw = MonoDevelop.Components.Mac.GtkMacInterop.GetNSWindow (window);
				if (nsw != null)
					ApplyTheme (nsw);
			}
			#endif
		}
	}

	public class ThemedGtkWindowBackend : Xwt.GtkBackend.WindowBackend
	{
		public override void Initialize ()
		{
			base.Initialize ();
			IdeTheme.ApplyTheme (Window);
		}
	}

	public class ThemedGtkDialogBackend : Xwt.GtkBackend.DialogBackend
	{
		public override void Initialize ()
		{
			base.Initialize ();
			IdeTheme.ApplyTheme (Window);
		}
	}
}

