// Spike T005: Mono.Addins 1.4.1 on CoreCLR (.NET 10), mimicking MonoDevelop's layout:
// host in bin/, add-ins discovered from an AddIns directory via an .addins file,
// extension points declared in XML manifests and via attributes.
using System;
using System.IO;
using System.Linq;
using Mono.Addins;


namespace AddinsSpike
{
	[TypeExtensionPoint ("/Host/Commands")]
	public interface ICommand
	{
		string Run ();
	}

	static class Program
	{
		static int Main (string[] args)
		{
			var baseDir = AppContext.BaseDirectory;
			var configDir = Path.Combine (Path.GetTempPath (), "addins-spike-registry-" + Environment.ProcessId);
			var addinsDir = args.Length > 0 ? args [0] : Path.Combine (baseDir, "AddIns");
			Console.WriteLine ($"runtime={Environment.Version} base={baseDir} addins={addinsDir}");

			AddinManager.AddinLoadError += (s, a) => Console.WriteLine ($"LOAD ERROR {a.AddinId}: {a.Message} {a.Exception}");
			AddinManager.AddinLoaded += (s, a) => Console.WriteLine ($"loaded {a.AddinId}");
			AddinManager.Initialize (configDir, addinsDir);
			var sw = System.Diagnostics.Stopwatch.StartNew ();
			AddinManager.Registry.Update (new ConsoleProgressStatus (true));
			Console.WriteLine ($"registry update took {sw.ElapsedMilliseconds} ms");

			var addins = AddinManager.Registry.GetAddins ();
			Console.WriteLine ("registered add-ins: " + string.Join (", ", addins.Select (a => a.Id)));

			var nodes = AddinManager.GetExtensionObjects<ICommand> ("/Host/Commands");
			foreach (var c in nodes)
				Console.WriteLine ($"command {c.GetType ().FullName} -> {c.Run ()}");

			var xmlNodes = AddinManager.GetExtensionNodes ("/Host/Tools");
			foreach (ExtensionNode n in xmlNodes)
				Console.WriteLine ($"tool node id={n.Id} type={n.GetType ().Name}");

			var ok = nodes.Length > 0 && xmlNodes.Count > 0;
			Console.WriteLine (ok ? "addins-spike: OK" : "addins-spike: FAIL");
			return ok ? 0 : 1;
		}
	}
}
