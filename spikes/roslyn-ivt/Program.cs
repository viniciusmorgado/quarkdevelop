// Spike T004: which friend assemblies do Roslyn 5.x assemblies grant InternalsVisibleTo,
// and is MonoDevelop's public key (main/msbuild/MonoDevelop-Public.snk) among them?
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

static class Program
{
	static int Main (string[] args)
	{
		var snk = File.ReadAllBytes (args [0]);
		var mdKey = Convert.ToHexString (snk).ToLowerInvariant ();
		Console.WriteLine ($"MonoDevelop-Public.snk: {snk.Length} bytes");
		var dir = AppContext.BaseDirectory;
		int mdGrants = 0, total = 0;
		foreach (var dll in Directory.GetFiles (dir, "Microsoft.CodeAnalysis*.dll").OrderBy (f => f)) {
			var asm = Assembly.LoadFrom (dll);
			var ivts = asm.GetCustomAttributes<InternalsVisibleToAttribute> ().Select (a => a.AssemblyName).ToList ();
			total += ivts.Count;
			var md = ivts.Where (n => n.Contains ("MonoDevelop") || n.Contains ("Xamarin") || n.Contains ("VisualStudio.Mac") || n.ToLowerInvariant ().Contains (mdKey)).ToList ();
			var keyed = ivts.Count (n => n.ToLowerInvariant ().Contains (mdKey));
			mdGrants += md.Count;
			Console.WriteLine ($"{Path.GetFileName (dll)} {asm.GetName ().Version}: {ivts.Count} IVT, MonoDevelop/Xamarin/VSMac-related={md.Count}, with MonoDevelop key={keyed}");
			foreach (var n in md.Take (15))
				Console.WriteLine ("    " + n.Split (',') [0]);
		}
		Console.WriteLine ($"TOTAL IVT={total} MonoDevelop-related={mdGrants}");
		return 0;
	}
}
