
using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin ("Debugger.VsCodeDebugProtocol", 
	Namespace = "MonoDevelop",
	Version = MonoDevelop.BuildInfo.Version,
	Category = "Debugging")]

[assembly:AddinName ("VsCode Debug Protocol support for MonoDevelop")]
[assembly:AddinDescription ("Support for Debugging over VsCode debug protocol")]
[assembly:AddinFlags (AddinFlags.Hidden)]

[assembly:AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("Ide", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("Debugger", MonoDevelop.BuildInfo.Version)]

// The protocol assembly is copied next to the add-in (see the csproj): make it resolvable for the add-in and for
// the add-ins that depend on it (Debugger.NetCoreDbg).
[assembly: ImportAddinAssembly ("Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.dll")]
