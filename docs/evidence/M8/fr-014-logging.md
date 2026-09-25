# FR-014 — structured logs with the versions at start-up (T125, 2026-09-25)

Run in the dev container at `b2520b31b8`, after `./scripts/pm ./scripts/build.sh` (ADR 0023).

**JSON format, level `info`**: the start-up record carries the IDE, runtime and SDK versions.

```bash
./scripts/pm bash -lc 'MD_LOG_FORMAT=json MD_LOG_LEVEL=info dotnet main/build/bin/mdtool.dll -q | jq -cR "fromjson? | select(.event==\"startup\")"'
```

```json
{"timestamp":"2026-09-25T02:38:10.0590801+00:00","level":"info","message":"mdtool 8.6 Preview on .NET 10.0.12, .NET SDK 10.0.401, Ubuntu 24.04.5 LTS X64","event":"startup","host":"mdtool","version":"8.6","versionLabel":"8.6 Preview","runtime":".NET 10.0.12","sdk":"10.0.401","os":"Ubuntu 24.04.5 LTS","architecture":"X64"}
```

Every line of that run is a JSON object (`jq -cR 'fromjson? | {level, event}' | sort | uniq -c`):

```text
      1 {"level":"info","event":"startup"}
      6 {"level":"info","event":null}
      1 {"level":"warn","event":null}
```

**Level selection**: with `MD_LOG_LEVEL=warn` the same command prints no start-up record (0 lines), because it is
logged at `info`.

**Text format** (default), level `info`:

```text
INFO [2026-09-25 02:43:01Z]: mdtool 8.6 Preview on .NET 10.0.12, .NET SDK 10.0.401, Ubuntu 24.04.5 LTS X64
```

The IDE writes the same record with `host` = `MonoDevelop` at start-up. Metrics and traces (T126) are
covered by the `InstrumentationTelemetry` tests in Core.Tests, which run in every gate.
