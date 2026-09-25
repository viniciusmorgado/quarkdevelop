namespace MonoDevelopTests
open System
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open MonoDevelop.FSharp
open MonoDevelop.Ide.Editor
open MonoDevelop.Ide.TypeSystem
open MonoDevelop.Core
open MonoDevelop.Core.Text
open MonoDevelop.Ide
open MonoDevelop.Core.ProgressMonitoring
open System.Threading.Tasks
open System.Runtime.CompilerServices

module FixtureSetup =
    let firstRun = ref true

    let toTask computation : Task = Async.StartImmediateAsTask computation :> _

    [<AsyncStateMachine(typeof<Task>)>]
    let initialiseMonoDevelopAsync() = toTask <| async {
        if !firstRun then
            firstRun := false
            //Environment.SetEnvironmentVariable ("MONO_ADDINS_REGISTRY", "/tmp")
            //Environment.SetEnvironmentVariable ("XDG_CONFIG_HOME", "/tmp")
            MonoDevelop.FSharp.MDLanguageService.DisableVirtualFileSystem()
            // GTK, the add-in registry and the runtime are initialized by the test host (FSharpTestsSetUp), without
            // the workbench: its shell is a mock (IdeUnitTests.GuiTestHost). The services the tests use, as IdeTestBase:
            do! Runtime.GetService<DesktopService> ()
            do! Runtime.GetService<MonoDevelop.Ide.Fonts.FontService> ()
            do! Runtime.GetService<TypeSystemService> ()
    }

module TestHelpers =
    let filename = if Platform.IsWindows then "c:\\test.fsx" else "test.fsx"

    let parseAndCheckFile source =
        async {
            try
                let checker = FSharpChecker.Create()
                let sourceText = SourceText.ofString source
                // .NET: the assemblies of the running runtime, as the language service does (GetScriptCheckerOptions)
                let! projOptions, _errors = checker.GetProjectOptionsFromScript(filename, sourceText, otherFlags = [| "--targetprofile:netcore" |], assumeDotNetFramework = false)
                let! parseResults, checkAnswer = checker.ParseAndCheckFileInProject(filename, 0, sourceText , projOptions)

                // Construct new typed parse result if the task succeeded
                let results =
                  match checkAnswer with
                  | FSharpCheckFileAnswer.Succeeded(checkResults) ->
                      ParseAndCheckResults(Some checkResults, Some parseResults)
                  | FSharpCheckFileAnswer.Aborted ->
                      ParseAndCheckResults(None, Some parseResults)
                if parseResults.Errors.Length > 0 then
                    printfn "%A" parseResults.Errors
                return results
            with exn ->
                printf "%A" exn
                return ParseAndCheckResults(None, None) }

    let createDocWithParseResults source compilerDefines (parseFile:string -> ParseAndCheckResults) =
        let results = parseFile source

        results.CheckResults |> Option.iter(fun r -> if r.Errors.Length > 0 then printfn "%A" r.Errors)
        let options = ParseOptions(FileName = filename, Content = StringTextSource(source))

        let parsedDocument =
            ParsedDocument.create options results [compilerDefines] (Some (new DocumentLocation(1,1))) "" |> Async.RunSynchronously

        let doc = TextEditorFactory.CreateNewReadonlyDocument(StringTextSource(source), filename, "text/fsharp")
        let editor = MonoDevelop.Ide.Editor.TextEditorFactory.CreateNewEditor (doc)

        TestDocument(filename, parsedDocument, editor)

    let createDoc source compilerDefines =
        createDocWithParseResults source compilerDefines (fun source -> parseAndCheckFile source |> Async.RunSynchronously)

    let createDocWithoutParsing source compilerDefines =
        createDocWithParseResults source compilerDefines (fun _ -> ParseAndCheckResults(None, None))

    let getAllSymbols source =
        async {
          let! results = parseAndCheckFile source
          return! results.GetAllUsesOfAllSymbolsInFile()
        } |> Async.RunSynchronously
