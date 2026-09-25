namespace global

open NUnit.Framework

[<assembly: IdeUnitTests.GuiTestContext>]
do ()

/// Initializes the IDE test host (GTK, the runtime, Xwt; IdeUnitTests.GuiTestHost) once, on the test thread, before
/// any fixture of the assembly (global namespace). GuiUnit did this before NUnit 3 (ADR 0015).
[<SetUpFixture>]
type FSharpTestsSetUp() =
    [<OneTimeSetUp>]
    member x.InitializeIdeTestHost() =
        // before the language service starts, as the GuiUnit set-up did (FixtureSetup)
        MonoDevelop.FSharp.MDLanguageService.DisableVirtualFileSystem()
        IdeUnitTests.GuiTestHost.EnsureInitialized()
