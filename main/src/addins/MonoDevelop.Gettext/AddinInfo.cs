
using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin ("Gettext",
	Namespace = "MonoDevelop",
	Version = MonoDevelop.BuildInfo.Version,
	EnabledByDefault = false,
	Category = "IDE extensions")]

[assembly:AddinName ("Gettext Translations Support")]
[assembly:AddinDescription ("Provides support for adding/editing PO files")]

[assembly:AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("Ide", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("DesignerSupport", MonoDevelop.BuildInfo.Version)]
// Autotools and Deployment are excluded from the Linux build (ADR 0017): no Makefile handler, no deploy files.
