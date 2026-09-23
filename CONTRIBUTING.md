# Contributing

This fork migrates MonoDevelop to .NET 10 LTS and GTK3 on Linux. The rules below come from the
project constitution (`docs/constitution.md`); read it once before contributing.

## Environment

- Host requirements: **podman** (rootless) and **git**. Nothing else.
- Every command runs in the development container: `./scripts/pm <command>` (see
  [docs/linux/setup.md](docs/linux/setup.md)). Scripts refuse to run on the host.

## Workflow

1. **Spec first.** Work items live in `specs/001-linux-dotnet10-migration/tasks.md`. New features or
   architectural changes go through the spec flow (spec → plan → tasks) and, when they decide
   something structural, an ADR in `docs/adr/` (MADR format).
2. **Small, reversible commits.** One task per commit by default. Every commit message ends with a
   `Tasks: Tnnn[, Tnnn]` trailer (or `Tasks: none` for chores). Use `./scripts/pm ./scripts/git-commit -m "…"`;
   it enforces the trailer and the project's commit identity.
3. **Keep the Linux solution green.** `./scripts/pm ./scripts/build.sh --check` and
   `./scripts/pm ./scripts/test.sh` must pass before a commit.
4. **Tests with behaviour changes.** A change in behaviour ships with a test in the same commit.
   Failing legacy tests are quarantined only with `[Category("Quarantine")]` and an entry in
   `docs/evidence/M4/quarantine.md`.
5. **Evidence.** When a task's proof is a command, store its output under `docs/evidence/Mx/`.

## Converting a legacy project

```bash
./scripts/pm python3 scripts/convert-legacy-csproj.py main/path/Project.csproj --write
# resolve the TODO(convert) comments, add the project to main/MonoDevelop.Linux.sln, then:
./scripts/pm ./scripts/warnings-baseline.sh main/path/Project.csproj
```

- Projects are SDK-style `net10.0` with explicit `Compile` items (ADR 0003) and central package
  versions (`main/Directory.Packages.props`, ADR 0004).
- Warnings are errors, except legacy IDs listed in the project's generated baseline
  (`main/msbuild/Linux/warning-baselines/`, ADR 0018). Security rules are never baselined.
- Formatting (`dotnet format whitespace`) is enforced on files added by this fork; reformat legacy
  files one project at a time in a dedicated commit (`./scripts/pm ./scripts/format.sh <files>`).

## Code style

Follow `.editorconfig` (tabs, Mono style: space before parentheses in calls and declarations).
New files carry the MIT license header used across the code base.
