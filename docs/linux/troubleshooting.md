# Troubleshooting (Linux / .NET 10)

| Symptom | Cause | Fix |
|---|---|---|
| `pm: building localhost/md-dev:…` on every call | `Dockerfile` changed (image tag = file hash) | expected once per change; old images can be removed with `podman image prune` |
| `run this inside the dev container` | a `scripts/*.sh` script was started on the host | prefix with `./scripts/pm` |
| `NU1301`/restore cannot reach a feed | only nuget.org is configured (ADR 0004) | check network/proxy inside the container: `./scripts/pm curl -I https://api.nuget.org/v3/index.json` |
| `NU1902/NU1903/NU1904` errors | a package has a known vulnerability | upgrade it in `main/Directory.Packages.props` |
| Files owned by another uid after a container run | podman without `--userns=keep-id` | use `./scripts/pm`, which sets it |
| `git` "dubious ownership" inside the container | repository mounted with a different uid | the image sets `safe.directory '*'`; rebuild with `PM_REBUILD=1 ./scripts/pm true` |
| GTK: `cannot open display` | no display forwarded | use `./scripts/run.sh --headless` or see setup.md §4 |

## Logs

| Variable | Effect |
|---|---|
| `MD_LOG_LEVEL=fatal\|error\|warn\|info\|debug\|none` | Console log level for the IDE, `mdtool` and test hosts. It takes precedence over `MONODEVELOP_CONSOLE_LOG_LEVEL` and over `mdtool -v` ([ADR 0023](../adr/0023-logging-and-observability.md)). |
| `MD_LOG_FORMAT=json` | Console records as one JSON object per line: `timestamp`, `level`, `message` and properties. |
| `MONODEVELOP_LOG_FILE`, `MONODEVELOP_FILE_LOG_LEVEL` | An extra text log file. |

Every host starts with a record like the one below, which names the runtime and the .NET SDK in use:

```bash
MD_LOG_FORMAT=json MD_LOG_LEVEL=info ./scripts/pm dotnet main/build/bin/mdtool.dll -q | jq -cR 'fromjson? | select(.event == "startup")'
# {"timestamp":"…","level":"info","message":"mdtool 8.6 Preview on .NET 10.0.12, .NET SDK 10.0.401, …","event":"startup","host":"mdtool","version":"8.6",…}
```

The IDE also writes a session log to `~/.cache/MonoDevelop/8.0/Logs` (`$XDG_CACHE_HOME/MonoDevelop/8.0/Logs`).
