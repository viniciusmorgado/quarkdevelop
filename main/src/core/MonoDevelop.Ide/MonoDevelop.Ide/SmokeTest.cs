//
// SmokeTest.cs
//
// Copyright (c) 2026 MonoDevelop contributors
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using MonoDevelop.Core;
using MonoDevelop.Core.Logging;
using MonoDevelop.Projects;

namespace MonoDevelop.Ide
{
	/// <summary>
	/// <c>--smoke-test [solution or project]</c> (task T103, specs/001-linux-dotnet10-migration/contracts/smoke-test.md): start
	/// the IDE, open the solution, build it, write out/smoke/{ide.log,screenshot.png} and exit with
	/// 0 (built with no errors and no unhandled exception), 1 (build errors) or 2 (start-up/load failure or timeout).
	/// MD_SMOKE_OPEN=&lt;file&gt; (relative to the solution's directory) opens that file in the editor before the screenshot,
	/// and fails the run if the IDE's workspace sees errors in it or in its project. MD_SMOKE_GOTO=&lt;method&gt; then goes
	/// to the definition of that partial method, which a source generator implements (T147).
	/// </summary>
	sealed class SmokeTest
	{
		public const string DefaultSolution = "main/tests/linux-smoke/Smoke.sln";
		public const int ExitSuccess = 0, ExitBuildErrors = 1, ExitFailure = 2;

		const int MainWindowTimeoutSeconds = 60;

		readonly FilePath solution;
		readonly FilePath outputDirectory;
		readonly Stopwatch clock = Stopwatch.StartNew ();
		FileLogger logger;
		Timer watchdog;
		int unhandledExceptions;

		SmokeTest (FilePath solution, FilePath outputDirectory)
		{
			this.solution = solution;
			this.outputDirectory = outputDirectory;
		}

		public int UnhandledExceptions => unhandledExceptions;

		/// <summary>
		/// Takes the solution out of <paramref name="args"/> (the smoke test opens it itself) and starts the log and
		/// the watchdog (MD_SMOKE_TIMEOUT seconds, default 600). The output directory is MD_SMOKE_OUT or out/smoke;
		/// MD_SMOKE_NO_BUILD=1 stops after loading the solution.
		/// </summary>
		public static SmokeTest Start (ref string[] args)
		{
			var sln = args.FirstOrDefault (a => a.EndsWith (".sln", StringComparison.OrdinalIgnoreCase) || a.EndsWith (".csproj", StringComparison.OrdinalIgnoreCase));
			args = args.Where (a => !ReferenceEquals (a, sln)).ToArray ();
			var output = Environment.GetEnvironmentVariable ("MD_SMOKE_OUT");
			var test = new SmokeTest (
				Path.GetFullPath (sln ?? DefaultSolution),
				Path.GetFullPath (string.IsNullOrEmpty (output) ? Path.Combine ("out", "smoke") : output));

			Directory.CreateDirectory (test.outputDirectory);
			test.logger = new FileLogger (test.outputDirectory.Combine ("ide.log"), false) { Name = "SmokeTestLogger" };
			LoggingService.AddLogger (test.logger);

			var previousHandler = LoggingService.UnhandledErrorOccurred;
			LoggingService.UnhandledErrorOccurred = (reportCrashes, ex, willShutDown) => {
				Interlocked.Increment (ref test.unhandledExceptions);
				return previousHandler != null ? previousHandler (reportCrashes, ex, willShutDown) : reportCrashes;
			};

			if (!int.TryParse (Environment.GetEnvironmentVariable ("MD_SMOKE_TIMEOUT"), out int timeout) || timeout <= 0)
				timeout = 600;
			test.watchdog = new Timer (_ => test.Exit (ExitFailure, $"timeout after {timeout} s"), null, TimeSpan.FromSeconds (timeout), Timeout.InfiniteTimeSpan);
			LoggingService.LogInfo ("Smoke test: solution {0}, output {1}", test.solution, test.outputDirectory);
			return test;
		}

		/// <summary>Runs on the main thread once the IDE is initialized.</summary>
		public async Task RunAsync ()
		{
			try {
				var mainWindowDeadline = TimeSpan.FromSeconds (MainWindowTimeoutSeconds);
				while (IdeApp.Workbench?.RootWindow?.Visible != true) {
					if (clock.Elapsed > mainWindowDeadline) {
						Exit (ExitFailure, "the main window did not appear");
						return;
					}
					await Task.Delay (100);
				}
				var processStart = Process.GetCurrentProcess ().StartTime;
				LoggingService.LogInfo ("Smoke test: main window shown {0:F1} s after the process started", (DateTime.Now - processStart).TotalSeconds);
				// X11 (":99") or Wayland ("wayland-0"): the Wayland smoke (T104) checks which backend GTK used
				LoggingService.LogInfo ("Smoke test: GDK display {0}", Gdk.Display.Default?.Name);

				// T106: the longest gap between main-loop ticks while the solution loads (a stall freezes the UI)
				var probe = MainLoopStallProbe.Start ();
				bool opened = await IdeApp.Workspace.OpenWorkspaceItem (solution);
				// the type system keeps loading the projects in the background after the workspace is open
				await Task.Delay (5000);
				LoggingService.LogInfo ("Smoke test: longest main loop stall while loading (and 5 s after): {0} ms", probe.Stop ());
				if (!opened) {
					Exit (ExitFailure, "could not open " + solution);
					return;
				}
				// a project is opened wrapped in a solution of its own
				var sln = IdeApp.Workspace.GetAllSolutions ().FirstOrDefault ();
				if (sln == null) {
					Exit (ExitFailure, solution + " did not load as a solution");
					return;
				}
				LoggingService.LogInfo ("Smoke test: loaded {0} ({1} projects)", sln.Name, sln.GetAllProjects ().Count ());

				if (Environment.GetEnvironmentVariable ("MD_SMOKE_NO_BUILD") == "1") {
					string loadedOpenFailure = await OpenRequestedFileAsync (sln);
					SaveScreenshot ();
					if (loadedOpenFailure != null)
						Exit (ExitFailure, "MD_SMOKE_OPEN: " + loadedOpenFailure);
					else
						Exit (unhandledExceptions > 0 ? ExitFailure : ExitSuccess, "loaded (MD_SMOKE_NO_BUILD)");
					return;
				}

				var result = await IdeApp.ProjectOperations.Build (sln).Task;
				int errors = result?.ErrorCount ?? 1;
				LoggingService.LogInfo ("Smoke test: build finished with {0} errors, {1} warnings", errors, result?.WarningCount ?? 0);
				if (result != null) {
					foreach (var error in result.Errors.Where (e => !e.IsWarning))
						LoggingService.LogError ("Smoke test: build error {0}({1},{2}): {3} {4}", error.FileName, error.Line, error.Column, error.ErrorNumber, error.ErrorText);
				}

				// let the error list and the status bar update before the screenshot
				await Task.Delay (1000);

				// T105: activating an error in the Errors pad must open the file with the caret on the error line
				string navigationFailure = errors > 0 ? await CheckErrorNavigationAsync () : null;
				// T112: MD_SMOKE_DEBUG=1 debugs the startup project (netcoredbg) to a breakpoint on the first line of Program.cs
				string debugFailure = errors == 0 && Environment.GetEnvironmentVariable ("MD_SMOKE_DEBUG") == "1" ? await CheckDebuggingAsync (sln) : null;
				// T138: MD_SMOKE_OPEN=<file> shows that file of the solution in the editor
				string openFailure = navigationFailure == null && debugFailure == null ? await OpenRequestedFileAsync (sln) : null;
				SaveScreenshot ();

				if (navigationFailure != null)
					Exit (ExitFailure, "error list navigation: " + navigationFailure);
				else if (openFailure != null)
					Exit (ExitFailure, "MD_SMOKE_OPEN: " + openFailure);
				else if (debugFailure != null)
					Exit (ExitFailure, "debugging: " + debugFailure);
				else if (errors > 0)
					Exit (ExitBuildErrors, errors + " build errors");
				else if (unhandledExceptions > 0)
					Exit (ExitFailure, unhandledExceptions + " unhandled exceptions were logged");
				else
					Exit (ExitSuccess, "success");
			} catch (Exception e) {
				LoggingService.LogError ("Smoke test failed", e);
				Exit (ExitFailure, e.Message);
			}
		}

		/// <summary>Returns null when the first row of the Errors pad opens its file at its line, or what went wrong.</summary>
		async Task<string> CheckErrorNavigationAsync ()
		{
			var pad = IdeApp.Workbench.GetPad<MonoDevelop.Ide.Gui.Pads.ErrorListPad> ();
			if (pad == null)
				return "no Errors pad";
			pad.BringToFront ();
			var task = ((MonoDevelop.Ide.Gui.Pads.ErrorListPad)pad.Content).ActivateFirstRow ();
			if (task == null)
				return "the Errors pad is empty";

			var deadline = clock.Elapsed + TimeSpan.FromSeconds (30);
			while (true) {
				var editor = IdeApp.Workbench.ActiveDocument?.Editor;
				if (editor != null && IdeApp.Workbench.ActiveDocument.FileName == task.FileName && editor.CaretLine == task.Line) {
					LoggingService.LogInfo ("Smoke test: error list navigation opened {0} at line {1}", task.FileName.FileName, task.Line);
					// let the editor draw the document before the screenshot
					await Task.Delay (1000);
					return null;
				}
				if (clock.Elapsed > deadline) {
					var document = IdeApp.Workbench.ActiveDocument;
					return $"expected {task.FileName}:{task.Line}, the active document is {document?.FileName.ToString () ?? "none"}:{document?.Editor?.CaretLine}";
				}
				await Task.Delay (100);
			}
		}

		/// <summary>
		/// Sets a breakpoint on the first line of the startup project's Program.cs with the Toggle Breakpoint command, runs
		/// the Debug command and waits until the debugger is paused there. Returns null on success, or what went wrong.
		/// The debugger add-in is not referenced by the IDE core: its commands are dispatched by id and
		/// DebuggingService.IsPaused is read by reflection.
		/// </summary>
		async Task<string> CheckDebuggingAsync (Solution sln)
		{
			var project = sln.StartupItem as Project ?? sln.GetAllProjects ().FirstOrDefault ();
			var program = project?.Files.FirstOrDefault (f => f.FilePath.FileName == "Program.cs");
			if (program == null)
				return "the startup project has no Program.cs";
			await IdeApp.Workbench.OpenDocument (program.FilePath, project, 1, 1);
			// the editor is created after the document opens: wait for it, as the error navigation check does
			var editorDeadline = clock.Elapsed + TimeSpan.FromSeconds (30);
			while (IdeApp.Workbench.ActiveDocument?.FileName != program.FilePath || IdeApp.Workbench.ActiveDocument.Editor == null) {
				if (clock.Elapsed > editorDeadline)
					return "could not open " + program.FilePath;
				await Task.Delay (100);
			}
			if (!IdeApp.CommandService.DispatchCommand ("MonoDevelop.Debugger.DebugCommands.ToggleBreakpoint"))
				return "the Toggle Breakpoint command is not available";
			if (!IdeApp.CommandService.DispatchCommand ("MonoDevelop.Debugger.DebugCommands.Debug"))
				return "the Debug command is not available";

			var isPaused = AppDomain.CurrentDomain.GetAssemblies ()
				.Select (a => a.GetType ("MonoDevelop.Debugger.DebuggingService"))
				.FirstOrDefault (t => t != null)?.GetProperty ("IsPaused");
			if (isPaused == null)
				return "the debugger add-in is not loaded";
			var deadline = clock.Elapsed + TimeSpan.FromSeconds (60);
			while (!(bool)isPaused.GetValue (null)) {
				if (clock.Elapsed > deadline)
					return "the debugger did not stop at the breakpoint";
				await Task.Delay (100);
			}
			var active = IdeApp.Workbench.ActiveDocument;
			if (active?.FileName != program.FilePath || active.Editor?.CaretLine != 1)
				return $"stopped, but the active document is {active?.FileName.ToString () ?? "none"}:{active?.Editor?.CaretLine}";
			LoggingService.LogInfo ("Smoke test: the debugger stopped at the breakpoint {0}:1", program.FilePath.FileName);
			// let the debug pads (call stack, locals) fill before the screenshot
			await Task.Delay (3000);
			return null;
		}

		/// <summary>
		/// Opens the file named by MD_SMOKE_OPEN (relative to the solution's directory, off when unset) and waits for the
		/// editor to show it, with the semantic highlighting of the C# binding. A C# file is also parsed with the
		/// language version of its project in the IDE's workspace: syntax errors there would be false errors in the
		/// editor (T138). Its project is then compiled in the workspace: an error there that the build does not have (a
		/// type from the global usings that the SDK generates, T146) is a false error too. Returns null on success, or
		/// what went wrong.
		/// </summary>
		async Task<string> OpenRequestedFileAsync (Solution sln)
		{
			var requested = Environment.GetEnvironmentVariable ("MD_SMOKE_OPEN");
			if (string.IsNullOrEmpty (requested))
				return null;
			var file = sln.BaseDirectory.Combine (requested).FullPath;
			if (!File.Exists (file))
				return file + " does not exist";
			var project = sln.GetAllProjects ().FirstOrDefault (p => p.Files.GetFile (file) != null);
			await IdeApp.Workbench.OpenDocument (file, project, 1, 1);
			var deadline = clock.Elapsed + TimeSpan.FromSeconds (30);
			while (IdeApp.Workbench.ActiveDocument?.FileName != file || IdeApp.Workbench.ActiveDocument.Editor == null) {
				if (clock.Elapsed > deadline)
					return "could not open " + file;
				await Task.Delay (100);
			}
			LoggingService.LogInfo ("Smoke test: opened {0}", file.FileName);

			if (project != null && file.HasExtension (".cs")) {
				var roslynProject = await IdeApp.TypeSystemService.GetCodeAnalysisProjectAsync (project);
				if (roslynProject?.ParseOptions is CSharpParseOptions options) {
					var tree = CSharpSyntaxTree.ParseText (await File.ReadAllTextAsync (file), options, file);
					var syntaxErrors = tree.GetDiagnostics ().Where (d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList ();
					LoggingService.LogInfo ("Smoke test: {0} parses as C# {1} with {2} syntax errors",
						file.FileName, options.LanguageVersion.ToDisplayString (), syntaxErrors.Count);
					foreach (var error in syntaxErrors)
						LoggingService.LogError ("Smoke test: syntax error {0}", error);
					if (syntaxErrors.Count > 0)
						return $"{syntaxErrors.Count} syntax errors in {file.FileName}";
				}
				string semanticFailure = await CheckWorkspaceErrorsAsync (project, file);
				if (semanticFailure != null)
					return semanticFailure;
				string gotoFailure = await GoToGeneratedDefinitionAsync (project);
				if (gotoFailure != null)
					return gotoFailure;
			}
			// let the editor draw the document and the C# binding classify it before the screenshot
			await Task.Delay (3000);
			return null;
		}

		/// <summary>
		/// Returns null when the workspace compilation of <paramref name="project"/> has no errors, or what they are. The
		/// workspace reloads a project after a restore or a build: the check is repeated for up to 30 s.
		/// </summary>
		async Task<string> CheckWorkspaceErrorsAsync (Project project, FilePath file)
		{
			var deadline = clock.Elapsed + TimeSpan.FromSeconds (30);
			TimeSpan? firstCompilation = null;
			while (true) {
				var start = clock.Elapsed;
				var roslynProject = await IdeApp.TypeSystemService.GetCodeAnalysisProjectAsync (project);
				var document = roslynProject?.Documents.FirstOrDefault (d => (FilePath)d.FilePath == file);
				var compilation = roslynProject != null ? await roslynProject.GetCompilationAsync () : null;
				firstCompilation ??= clock.Elapsed - start;
				var errors = compilation?.GetDiagnostics ().Where (d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList ();
				if ((document != null && errors?.Count == 0) || clock.Elapsed > deadline) {
					int count = errors?.Count ?? -1;
					// T147: the documents of the source generators (Regex, System.Text.Json...) are part of that compilation
					int generated = roslynProject != null ? (await roslynProject.GetSourceGeneratedDocumentsAsync ()).Count () : -1;
					LoggingService.LogInfo ("Smoke test: {0} compiles in the workspace with {1} errors, {2} in {3} ({4} source-generated documents, first compilation {5:F1} s)",
						project.Name, count, errors?.Count (e => (FilePath)e.Location.SourceTree?.FilePath == file) ?? -1, file.FileName,
						generated, firstCompilation.Value.TotalSeconds);
					foreach (var error in errors ?? Enumerable.Empty<Microsoft.CodeAnalysis.Diagnostic> ())
						LoggingService.LogError ("Smoke test: workspace error {0}", error);
					if (document == null)
						return $"{file.FileName} is not a document of {project.Name} in the workspace";
					return count == 0 ? null : $"{count} errors in the workspace compilation of {project.Name}";
				}
				await Task.Delay (1000);
			}
		}

		/// <summary>
		/// T147: MD_SMOKE_GOTO=&lt;method&gt; goes to the definition of that partial method of <paramref name="project"/>, whose
		/// implementation a source generator writes. The editor must open a read-only copy of the generated document.
		/// Returns null on success (or when MD_SMOKE_GOTO is not set), or what went wrong.
		/// </summary>
		async Task<string> GoToGeneratedDefinitionAsync (Project project)
		{
			var name = Environment.GetEnvironmentVariable ("MD_SMOKE_GOTO");
			if (string.IsNullOrEmpty (name))
				return null;
			var roslynProject = await IdeApp.TypeSystemService.GetCodeAnalysisProjectAsync (project);
			var compilation = roslynProject != null ? await roslynProject.GetCompilationAsync () : null;
			var method = compilation?.GetSymbolsWithName (name).OfType<Microsoft.CodeAnalysis.IMethodSymbol> ().FirstOrDefault ();
			if (method?.PartialImplementationPart == null)
				return $"MD_SMOKE_GOTO: no implemented partial method {name} in {project.Name}";

			IdeApp.ProjectOperations.JumpToDeclaration (method.PartialImplementationPart, project);
			var deadline = clock.Elapsed + TimeSpan.FromSeconds (10);
			while (IdeApp.Workbench.ActiveDocument?.FileName.IsChildPathOf (TypeSystem.SourceGeneratedFiles.RootDirectory) != true
				|| IdeApp.Workbench.ActiveDocument.Editor == null) {
				if (clock.Elapsed > deadline)
					return $"MD_SMOKE_GOTO: go to definition of {name} opened no source-generated file";
				await Task.Delay (100);
			}
			var editor = IdeApp.Workbench.ActiveDocument.Editor;
			var line = editor.GetLineByOffset (editor.CaretOffset);
			LoggingService.LogInfo ("Smoke test: go to definition of {0} opened {1} at line {2}, read-only: {3}",
				name, IdeApp.Workbench.ActiveDocument.FileName.FileName, line?.LineNumber ?? 0, editor.IsReadOnly);
			return null;
		}

		void SaveScreenshot ()
		{
			try {
				var window = IdeApp.Workbench.RootWindow.GdkWindow;
				var pixbuf = gdk_pixbuf_get_from_window (window.Handle, 0, 0, window.Width, window.Height);
				if (pixbuf == IntPtr.Zero)
					throw new InvalidOperationException ("gdk_pixbuf_get_from_window returned null");
				using (var image = new Gdk.Pixbuf (pixbuf))
					image.Save (outputDirectory.Combine ("screenshot.png"), "png");
			} catch (Exception e) {
				LoggingService.LogError ("Smoke test: could not save the screenshot", e);
			}
		}

		int exited;

		void Exit (int code, string reason)
		{
			if (Interlocked.Exchange (ref exited, 1) != 0)
				return;
			watchdog?.Dispose ();
			LoggingService.LogInfo ("Smoke test: exit code {0} ({1}) after {2:F1} s", code, reason, clock.Elapsed.TotalSeconds);
			LoggingService.RemoveLogger (logger.Name);
			logger.Dispose ();
			// No IdeApp.Exit: it can ask to save files or wait for the UI; the result is already written.
			Environment.Exit (code);
		}

		/// <summary>Measures the longest interval between 50 ms GLib timeouts on the main loop.</summary>
		sealed class MainLoopStallProbe
		{
			readonly Stopwatch clock = Stopwatch.StartNew ();
			long last, longest;
			bool running = true;

			public static MainLoopStallProbe Start ()
			{
				var probe = new MainLoopStallProbe ();
				GLib.Timeout.Add (50, probe.Tick);
				return probe;
			}

			bool Tick ()
			{
				long now = clock.ElapsedMilliseconds;
				longest = Math.Max (longest, now - last);
				last = now;
				return running;
			}

			public long Stop ()
			{
				Tick ();
				running = false;
				return longest;
			}
		}

		[DllImport ("libgdk-3.so.0")]
		static extern IntPtr gdk_pixbuf_get_from_window (IntPtr window, int x, int y, int width, int height);
	}
}
