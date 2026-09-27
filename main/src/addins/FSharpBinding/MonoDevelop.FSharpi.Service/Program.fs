namespace MonoDevelop.FSharpInteractive
open System
open System.Diagnostics
open System.IO
open System.Reflection
open Newtonsoft.Json
open FSharp.Compiler.Interactive.Shell
open MonoDevelop.FSharp.Shared
/// Wrapper for fsi with support for returning completions
module CompletionServer =
    let run (argv: string[]) =
        let inStream = Console.In
        let outStream = Console.Out
        let server = "MonoDevelop" + Guid.NewGuid().ToString("n")

        let editorPid = if argv.Length > 0 then Some (Int32.Parse argv.[0]) else None
        // This flag makes fsi send the SERVER-PROMPT> prompt
        // once it's output the header
        let fsiServerArg = sprintf "--fsi-server:%s " server
        // Make System.ValueTuple available to FSI
        let executingFolder = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
        let valueTuplePath = Path.Combine(executingFolder, "System.ValueTuple.dll")
        let argv = [| "--readline-"; fsiServerArg |]

        let serializer = JsonSerializer.Create()


        let (|Input|_|) (command: string) =
            if command.StartsWith("input ") then
                Some(command.[6..])
            else
                None

        let (|Tooltip|_|) (command: string) =
            if command.StartsWith("tooltip ") then
                Some(command.[8..])
            else
                None

        let (|Completion|_|) (command: string) =
            if command.StartsWith("completion ") then
                let input = command.[11..]
                let splitIndex = input.IndexOf(" ")
                if (splitIndex = -1) then
                    None
                else
                    let colStr = input.[0..splitIndex]
                    let success, col = Int32.TryParse colStr
                    if success then
                        Some (col, input.[splitIndex..])
                    else
                        None
            else
                None

        let (|ParameterHints|_|) (command: string) =
            if command.StartsWith("parameter-hints ") then
                let input = command.[16..]
                let splitIndex = input.IndexOf(" ")
                if (splitIndex = -1) then
                    None
                else
                    let colStr = input.[0..splitIndex]
                    let success, col = Int32.TryParse colStr
                    if success then
                        Some (col, input.[splitIndex..])
                    else
                        None
            else
                None

        let writeOutput (s:string) =
            async {
                do! outStream.WriteLineAsync s
            }

        let writeData commandType obj =
            async {
                let json = JsonConvert.SerializeObject obj
                do! Console.Error.WriteLineAsync (commandType + " " + json)
            }

        // No print transformer for System.Drawing.Image (sent to the pad as "image <base64>"): System.Drawing is
        // Windows-only on .NET, so no image can reach it on Linux.
        // FSharp.Compiler.Interactive.Settings.dll, the library that gives scripts the fsi object, was a prebuilt
        // .NET Framework binary: the session uses the settings of FSharp.Compiler.Service, without that library.
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

        let fsiSession = FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, outStream, true)

        // Add a watch on the editor PID. If it goes away we will self terminate.
        let editorProcess = editorPid |> Option.bind(fun pid -> Some (Process.GetProcessById pid))

        let rec main(currentInput) =
            editorProcess 
            |> Option.iter(fun editor ->
                if editor.HasExited then 
                    Process.GetCurrentProcess().Kill())

            let parseInput() =
                async {
                    let! command = inStream.ReadLineAsync()

                    match command with
                    | Input input ->
                        if input.EndsWith(";;") then
                            let result, warnings = fsiSession.EvalInteractionNonThrowing (currentInput + "\n" + input)
                            match result with
                            | Choice1Of2 () -> ()
                            | Choice2Of2 exn -> do! writeOutput (exn |> string)
                            for w in warnings do
                                do! writeOutput (sprintf "%s at %d,%d" w.Message w.StartLineAlternate w.StartColumn)

                            if not (input.StartsWith "#silentCd") then
                                do! writeOutput "SERVER-PROMPT>"
                            return ""
                        else
                            match currentInput with
                            | "" -> return input
                            | _ -> return currentInput + "\n" + input
                    | Tooltip filter ->
                        let! tooltip = Completion.getCompletionTooltip filter
                        do! writeData "tooltip" tooltip
                        return currentInput
                    | Completion context ->
                        let col, lineStr = context
                        let! results = Completion.getCompletions(fsiSession, lineStr, col)
                        do! writeData "completion" results
                        return currentInput
                    | ParameterHints context ->
                        let col, lineStr = context
                        let! results = Completion.getParameterHints(fsiSession, lineStr, col)
                        do! writeData "parameter-hints" results
                        return currentInput
                    | _ -> do! writeOutput (sprintf "Could not parse command - %s" command)
                           return currentInput
                }
            let currentInput =
                try
                    parseInput() |> Async.RunSynchronously
                with
                | exn ->
                    writeOutput (exn |> string) |> Async.RunSynchronously
                    currentInput
            main(currentInput)

        Console.SetOut outStream
        main("")

        0 // return an integer exit code

/// Newtonsoft.Json is the one of the IDE (build/bin): an installation has one copy of each assembly. .NET resolves only
/// what the deps.json lists, so it is looked up next to this process and then in the IDE's bin directory, before any
/// code that uses it is compiled (CompletionServer.run).
module Program =
    open System.Runtime.Loader

    let private resolveFrom (directories: string list) (context: AssemblyLoadContext) (name: AssemblyName) =
        directories
        |> List.map (fun directory -> Path.Combine(directory, name.Name + ".dll"))
        |> List.tryFind File.Exists
        |> Option.map context.LoadFromAssemblyPath
        |> Option.toObj

    [<EntryPoint>]
    let main argv =
        let ideBin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "bin"))
        AssemblyLoadContext.Default.add_Resolving(Func<_, _, _>(resolveFrom [ AppContext.BaseDirectory; ideBin ]))
        CompletionServer.run argv

