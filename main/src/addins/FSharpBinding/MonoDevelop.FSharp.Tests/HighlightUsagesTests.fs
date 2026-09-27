namespace MonoDevelopTests
open NUnit.Framework
open FsUnit
open MonoDevelop.FSharp.MonoDevelop
open MonoDevelop.FSharp.Shared
open System.Threading.Tasks
open System.Runtime.CompilerServices

[<TestFixture>]
type HighlightUsagesTests() =

    let assertUsages (source:string, expectedCount) =
        let offset = source.IndexOf "|"
        let source = source.Replace("|", "")
        let doc = TestHelpers.createDoc source ""
        let line, col, lineStr = doc.Editor.GetLineInfoFromOffset offset

        match Parsing.findIdents col lineStr SymbolLookupKind.ByLongIdent with
        | None -> Assert.Fail "Could not find ident"
        | Some(colu, ident) -> let symbolUse = doc.Ast.GetSymbolAtLocation(line, col, lineStr) |> Async.RunSynchronously
                               match symbolUse with
                               | Some symbol ->
                                   let references = doc.Ast.GetUsesOfSymbolInFile(symbol.Symbol) |> Async.RunSynchronously
                                   references.Length |> should equal expectedCount
                               | None -> Assert.Fail "No symbol found"

    [<SetUp;AsyncStateMachine(typeof<Task>)>]
    member x.``run before test``() =
        FixtureSetup.initialiseMonoDevelopAsync()

    [<Test>]
    member x.``Highlight usages from declaration``() =
        let source =
          """
          let ast|ring = "astring"
          let b = astring
          """
        assertUsages(source, 2)

    [<Test>]
    member x.``Highlight usages from usage``() =
        let source =
          """
          let astring = "astring"
          let b = astr|ing
          """
        assertUsages(source, 2)
