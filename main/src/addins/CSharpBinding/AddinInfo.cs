
using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin ("CSharpBinding", 
                 Namespace = "MonoDevelop",
                 Version = MonoDevelop.BuildInfo.Version,
                 Category = "Language bindings")]

[assembly:AddinName ("CSharp Language Binding")]
[assembly:AddinDescription ("CSharp Language Binding")]

[assembly:AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("Ide", MonoDevelop.BuildInfo.Version)]
// Linux: the C# project model (project type, compiler parameters, language binding) is the CSharpBinding.Core add-in.
[assembly:AddinDependency ("CSharpBinding.Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("Refactoring", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("SourceEditor2", MonoDevelop.BuildInfo.Version)]
// Linux: no UnitTesting dependency until that add-in is ported (T101), and no Cocoa/WPF TextEditor add-in (ADR 0012).
// The Autotools (removed) and ASP.NET (ADR 0017) modules are not built.