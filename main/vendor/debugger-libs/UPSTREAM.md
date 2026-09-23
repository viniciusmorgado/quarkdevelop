# Vendored: Mono.Debugging (debugger-libs)

| | |
|---|---|
| Upstream | https://github.com/mono/debugger-libs (`Mono.Debugging/`) |
| Source commit | `aae0e23e345caa1f3652b0e6b7d9e5bf49037ef3` (submodule main/external/debugger-libs, "[Mono.Debugging] Refactored code to avoid deadlocks in DebuggerSession when using breakpoints", 2020-01-22) |
| License | MIT (LICENSE; Novell, Xamarin) |
| Vendored on | 2026-09-23 (task T110, ADR 0005) |

Only `Mono.Debugging` is vendored: the debugger-independent client API (`DebuggerSession`, breakpoints,
`ObjectValue`, backtraces) used by `MonoDevelop.Debugger` and the Debug Adapter Protocol client
(`MonoDevelop.Debugger.VsCodeDebugProtocol`, netcoredbg in ADR 0016).

Not vendored:

- `Mono.Debugger.Soft` and `Mono.Debugging.Soft`: the Mono soft debugger, used only by the
  `MonoDevelop.Debugger.Soft` add-in, which is excluded from the Linux build (ADR 0017).
- `Mono.Debugging.Win32`, `CorApi`, `CorApi2` (and `eula.rtf`, which covers them): Windows only (ADR 0017).
- `UnitTests`, solutions, `Mono.Debugging.settings`, `Makefile.am`, `ChangeLog`, `Mono.Debugging.nuspec`.
- The NRefactory 5 expression evaluator (`Mono.Debugging.Evaluation/NRefactoryExpressionEvaluator.cs`,
  `NRefactoryExpressionEvaluatorVisitor.cs`, `NRefactoryExpressionResolverVisitor.cs`,
  `NRefactoryExtensions.cs`, `LambdaBodyOutputVisitor.cs`): NRefactory is removed from the Linux build
  (ADR 0019). Upstream later ported this evaluator to Roslyn; that port can be vendored if a
  Mono.Debugging based evaluator is needed again.

Because nothing else in the Linux build uses the other debugger-libs projects, the
`main/external/debugger-libs` submodule is removed. The legacy `Main.sln` and the excluded add-ins
(`MonoDevelop.Debugger.Soft`, `MonoDevelop.Debugger.Win32`) still point to the old path.

## Local patches

Listed per commit in `git log -- main/vendor/debugger-libs`; summary:

- **Build:** SDK-style `net10.0` project (`Mono.Debugging.csproj`) built with the repository props
  (ADR 0003), source glob, upstream strong-name key `mono.debugging.snk` (public signing, identity
  kept). Output goes to `main/build/bin/` as before. Analyzer findings are baselined in
  `main/msbuild/Linux/warning-baselines/Mono.Debugging.props`.
- **Expression evaluation:** `DebuggerSession`'s default evaluator is the new internal
  `Mono.Debugging.Evaluation/DefaultExpressionEvaluator.cs` instead of `NRefactoryExpressionEvaluator`.
  It only resolves plain local, parameter and member names (the `ExpressionEvaluator` base behaviour)
  and returns expressions unresolved. Debuggers that evaluate expressions themselves (DAP adapters)
  are not affected; sessions can still supply evaluators through `GetExpressionEvaluator`.
- **No .NET Remoting (ADR 0009):** `RemotingServices.Disconnect` calls are removed
  (`RemoteFrameObject.DisconnectAll`, the `ObjectValue` finalizer, which only did that, is removed),
  `RemoteFrameObject.InitializeLifetimeService` is removed, and `RemoteFrameObject` and
  `UpdateCallbackProxy` no longer derive from `MarshalByRefObject`. All objects live in-process.
- **No formatter serialization (SYSLIB0051):** the `(SerializationInfo, StreamingContext)`
  constructors of `DebuggerException`, `EvaluatorException`, `EvaluatorAbortedException`,
  `NotSupportedExpressionException` and `ImplicitEvaluationDisabledException` are removed.
- **`SourceLocation` checksums:** `HashAlgorithm.Create (string)` (obsolete, SYSLIB0045) is replaced by
  the same lookup through `CryptoConfig.CreateFromName`.
- **`BreakpointStore.realpath`:** the path is passed as NUL-terminated UTF-8 bytes (the Unix string
  marshalling, made explicit for CA2101).
