# 0023 — Logging and observability

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

Constitution principle VIII asks for structured logs, a log level set by an environment variable, the runtime and
SDK versions in the start-up log, and metrics and tracing where instrumentation already exists. `LoggingService`
broadcasts plain-text messages to `ILogger`s:
- the console, whose level comes from `MONODEVELOP_CONSOLE_LOG_LEVEL`;
- a file, from `MONODEVELOP_LOG_FILE`;
- the per-session IDE log;
- instrumentation.

Hundreds of call sites use its `LogInfo`/`LogError` methods, and add-ins implement `ILogger`. How do we get
machine-readable logs without rewriting the call sites?

## Considered Options

1. Keep `LoggingService` and its `ILogger` model. Add an optional structured interface, a JSON mode for the console
   logger, and environment variables for level and format.
2. Replace `LoggingService` with Microsoft.Extensions.Logging, with `ILoggerFactory` and the JSON console formatter.
3. Keep `LoggingService`, but forward every record to Microsoft.Extensions.Logging behind it.

## Decision Outcome

Option 1.
- `MD_LOG_LEVEL` sets the console level. It accepts `fatal`, `error`, `warn`, `info`, `debug`, `none`, or an
  `EnabledLoggingLevel` name, and takes precedence over `MONODEVELOP_CONSOLE_LOG_LEVEL`. Hosts keep a level
  set this way: `mdtool` no longer lowers it to its `-v` verbosity.
- `MD_LOG_FORMAT=json` makes the console logger write one JSON object per line: `timestamp` (ISO 8601, UTC),
  `level`, `message`, then the record's properties. Records go to standard output, as the text records do.
- `IStructuredLogger` (an `ILogger` that also takes `IReadOnlyList<KeyValuePair<string, object>>` properties) and
  `LoggingService.Log (level, message, properties)`. Loggers that are not structured get the message only.
- Start-up record: `Runtime.Initialize` logs one Info record with `event=startup`, `host`, `version`,
  `versionLabel`, `runtime`, `sdk` (the .NET SDK whose MSBuild is registered), `os` and `architecture`. Every
  host logs it: the IDE, mdtool and the test hosts.

Microsoft.Extensions.Logging (option 2) would replace an API used across the whole code base and by add-ins. A
forwarding bridge (option 3) adds dependencies to every host for no feature the JSON console mode lacks. It can
still be added later as another `IStructuredLogger`.

Metrics and tracing for `InstrumentationService` (`System.Diagnostics.Metrics`, `ActivitySource`) are task T126.
This ADR will be amended when that is done.

### Consequences

- Good: `MD_LOG_FORMAT=json MD_LOG_LEVEL=info dotnet main/build/bin/mdtool.dll … | jq -R 'fromjson?'` gives
  machine-readable records, and the text output is unchanged by default.
- Good: no new package dependency.
- Bad: only the console logger is structured. The IDE's session log file stays text.
- Bad: tools that print their own results on standard output (mdtool) interleave them with JSON records, so
  consumers read JSON lines leniently (`fromjson?`).
