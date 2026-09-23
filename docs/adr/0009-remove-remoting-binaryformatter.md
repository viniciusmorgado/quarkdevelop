# 0009 — Remove .NET Remoting and BinaryFormatter

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

.NET Remoting does not exist on .NET 5+; `BinaryFormatter` throws on .NET 9+. MonoDevelop uses
Remoting in 39–43 files (Core `RemotingService`, `ProcessHostController`, `RemoteProcessObject`,
formatter sinks, instrumentation, mdhost, GtkCore designer, NUnit runner bases) and
`BinaryFormatter` in 11 files.

## Decision Outcome

- Delete the Remoting infrastructure in Core; out-of-process features use the existing
  `RemoteProcessConnection`/`BinaryMessage` protocol or StreamJsonRpc 2.x (InstrumentationService).
- `CallContext.LogicalSetData/GetData` → `AsyncLocal<T>`.
- `MarshalByRefObject` base classes kept only where harmless; no cross-domain calls remain.
- Every `BinaryFormatter` use is replaced (JSON/XML/custom) or its feature excluded.
- `mdhost` and the Stetic designer (Remoting-based) are excluded (ADR 0017).

### Consequences

- Good: compliance with constitution VII; no PlatformNotSupportedException at run time.
- Bad: some out-of-process scenarios must be re-implemented when their add-ins are ported.
