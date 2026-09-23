# Review B — technical feasibility of T016–T054 (condensed)

Method: scratch SDK-style copies of Core and the MSBuild builder compiled against net10.0 in the
dev container (Mono.Addins 1.4.1, Microsoft.Build compile-only, Microsoft.CodeAnalysis.Common 5.9).

**Blocking issues found and their resolution**

| # | Issue | Resolution |
|---|---|---|
| 1 | `GenerateAssemblyInfo=false` silently drops every `InternalsVisibleTo` item | GenerateAssemblyInfo=true with each legacy attribute disabled (T028) — verified in generated AssemblyInfo |
| 2 | "DotNetCore.Core referenced from Core's runtime" is a dependency cycle | SDK discovery stays in Core; DotNetCore.Core only extends via extension points (T054) |
| 3 | `-warnaserror` impossible (~1.6k warnings at latest-recommended) | per-project warning baseline + TreatWarningsAsErrors (T029, ADR 0018) |
| 4 | nothing initializes the add-in runtime under `dotnet test` | `TestHostSetup` `[SetUpFixture]` + `.addins` in test output (T038) |

**Other findings adopted**: Core needs only Mono.Addins(.Setup), Cecil, Mono.Unix, MSBuild
(compile-only), Locator, CodeAnalysis.Common, Newtonsoft, ObjectPool, CodeDom,
ConfigurationManager (T033); `MonoDevelop.Core.addin.xml` imports framework assemblies (T037);
STSAuthHelper is used by RequestHelper (guarded instead of excluded); `ProcessHostConsole` must be
extracted (T034); `InstrumentationService` BinaryFormatter + `Activator.GetObject` (T034);
`Debug.Listeners`, `RegistryHive.DynData`, SYSLIB0051/0003 (T036); `CodePagesEncodingProvider`
(T046); .NET execution handler (T043); builder: `SetApartmentState`, `MSBUILD_EXE_PATH` override,
`AssemblyResolve`, exe.config toolsets (T059/T060); keep class name `CSharpProject` and use plain
`CSharpCodeProvider` in CSharpBinding.Core (T053); `Broken` sample must not be in the Linux solution
(T058); Core.Tests does not reference Ide/CSharpBinding (feasible); Xwt-dependent tests quarantined
(T040).
