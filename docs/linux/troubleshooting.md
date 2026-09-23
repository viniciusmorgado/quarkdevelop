# Troubleshooting (Linux / .NET 10)

| Symptom | Cause | Fix |
|---|---|---|
| `pm: building localhost/md-dev:…` on every call | `Containerfile` changed (image tag = file hash) | expected once per change; old images can be removed with `podman image prune` |
| `run this inside the dev container` | a `scripts/*.sh` script was started on the host | prefix with `./scripts/pm` |
| `NU1301`/restore cannot reach a feed | only nuget.org is configured (ADR 0004) | check network/proxy inside the container: `./scripts/pm curl -I https://api.nuget.org/v3/index.json` |
| `NU1902/NU1903/NU1904` errors | a package has a known vulnerability (constitution VII) | upgrade it in `main/Directory.Packages.props` |
| Files owned by another uid after a container run | podman without `--userns=keep-id` | use `./scripts/pm`, which sets it |
| `git` "dubious ownership" inside the container | repository mounted with a different uid | the image sets `safe.directory '*'`; rebuild with `PM_REBUILD=1 ./scripts/pm true` |
| GTK: `cannot open display` | no display forwarded | use `./scripts/run.sh --headless` or see setup.md §4 |
