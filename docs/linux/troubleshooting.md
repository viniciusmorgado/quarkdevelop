# Troubleshooting (Linux / .NET 10)

| Symptom | Cause | Fix |
|---|---|---|
| `dotnet scripts/<name>.cs -c Release` builds in Debug, or the script stops with `unknown argument` | `dotnet` read the options itself | put the script's arguments after `--`: `dotnet scripts/build.cs -- -c Release` |
| `A compatible .NET SDK was not found` | the .NET 10 SDK is not installed | install it: `global.json` asks for 10.0.100 or a later 10.0 feature band |
| Package management fails in the IDE (`MissingMethodException`, `TypeLoadException` in NuGet) | the .NET SDK is older than 10.0.400 and its NuGet older than 7.9 ([ADR 0020](../adr/0020-nuget-client-version.md)) | install SDK 10.0.400 or later; `dotnet scripts/setup.cs` warns about it |
| `msgfmt: not found` in `MonoDevelop.Translations` | gettext is not installed | install `gettext` (`dotnet scripts/setup.cs` lists the missing packages) |
| `NU1301`/restore cannot reach a feed | only nuget.org is configured (ADR 0004) | check the network or proxy: `curl -I https://api.nuget.org/v3/index.json` |
| `NU1902/NU1903/NU1904` errors | a package has a known vulnerability | upgrade it in `main/Directory.Packages.props` |
| `The IDE tests need a display`, or GTK tests ignored with `no display` | no display and no Xvfb | install `xvfb` and `xauth`; `dotnet scripts/test.cs` then runs them under Xvfb |
| GTK: `cannot open display` | the IDE was started without a display (e.g. over SSH) | `dotnet scripts/run.cs -- --headless` |
| `The .NET debugger (netcoredbg) was not found`, or `netcoredbg is not installed` from `debug.cs` | netcoredbg is not installed, or not on the IDE's `PATH` | `dotnet scripts/setup.cs` installs it in `~/.local/bin`; add that directory to `PATH` |

## Logs

| Variable | Effect |
|---|---|
| `MD_LOG_LEVEL=fatal\|error\|warn\|info\|debug\|none` | Console log level for the IDE, `mdtool` and test hosts. It takes precedence over `MONODEVELOP_CONSOLE_LOG_LEVEL` and over `mdtool -v` ([ADR 0023](../adr/0023-logging-and-observability.md)). |
| `MD_LOG_FORMAT=json` | Console records as one JSON object per line: `timestamp`, `level`, `message` and properties. |
| `MONODEVELOP_LOG_FILE`, `MONODEVELOP_FILE_LOG_LEVEL` | An extra text log file. |

Every host starts with a record like the one below, which names the runtime and the .NET SDK in use:

```bash
MD_LOG_FORMAT=json MD_LOG_LEVEL=info dotnet main/build/bin/mdtool.dll -q | jq -cR 'fromjson? | select(.event == "startup")'
# {"timestamp":"…","level":"info","message":"mdtool 8.6 Preview on .NET 10.0.12, .NET SDK 10.0.401, …","event":"startup","host":"mdtool","version":"8.6",…}
```

The IDE also writes a session log to `~/.cache/MonoDevelop/8.0/Logs` (`$XDG_CACHE_HOME/MonoDevelop/8.0/Logs`).
