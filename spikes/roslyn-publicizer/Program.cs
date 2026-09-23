// Spike T004b: call Roslyn *internal* APIs used by MonoDevelop (Shared.Extensions,
// CSharp.Extensions) from .NET 10 via Krafs.Publicizer (compile time) and the CoreCLR
// IgnoresAccessChecksTo support that Publicizer emits (run time).
using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;

static class Program
{
	static int Main ()
	{
		var tree = CSharpSyntaxTree.ParseText ("class C { void M() { /* comment */ int x = 1; } }");
		var root = tree.GetRoot ();
		var local = root.DescendantNodes ().OfType<LocalDeclarationStatementSyntax> ().First ();
		// internal: Microsoft.CodeAnalysis.Shared.Extensions.SyntaxNodeExtensions.GetAncestor<T>
		var cls = local.GetAncestor<ClassDeclarationSyntax> ();
		// internal: Microsoft.CodeAnalysis.CSharp.Extensions.SyntaxTreeExtensions.IsInNonUserCode
		var commentPos = tree.ToString ().IndexOf ("comment");
		var inComment = tree.IsInNonUserCode (commentPos, CancellationToken.None);
		Console.WriteLine ($"GetAncestor -> {cls?.Identifier.Text}; IsInNonUserCode(comment) -> {inComment}");
		var ok = cls?.Identifier.Text == "C" && inComment;
		Console.WriteLine (ok ? "roslyn-publicizer: OK" : "roslyn-publicizer: FAIL");
		return ok ? 0 : 1;
	}
}
