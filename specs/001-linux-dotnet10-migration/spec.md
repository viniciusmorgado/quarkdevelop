# Feature Specification: MonoDevelop on Linux with .NET 10 LTS and GTK3

**Feature Branch**: `dotnet-linux-migration`

**Created**: 2026-09-23

**Status**: Draft

**Input**: User description: "Migrate the archived MonoDevelop IDE (Mono / .NET Framework 4.7.2 /
Gtk# 2) so that the complete IDE builds, runs and can be debugged on Linux using .NET 10 LTS with a
GTK3 user interface. Linux-first; macOS and Windows are secondary and may break. Incremental,
reversible, verifiable migration with a walking skeleton first. C# is the first supported language.
Flatpak is the first distribution format. Everything runs inside a podman container."

## Clarifications

### Session 2026-09-23

- Q: What is the final scope — headless core only, or the full graphical IDE? → A: The full IDE
  with a GTK3 UI; the headless core is the first walking-skeleton step.
- Q: What happens to macOS/Windows-specific code? → A: Linux is the only focus; other platforms may
  break; their code is excluded from the Linux build (and may be removed when it blocks Linux work).
- Q: How are archived third-party dependencies handled? → A: Prefer a maintained package; otherwise
  vendor the source into this same repository with its origin recorded; no separate repositories.
- Q: Which languages must the first release support? → A: C# only; other languages are deferred.
- Q: Which distribution format comes first? → A: Flatpak; bundling older dependencies inside it is
  acceptable. Other formats are secondary.
- Q: May a community fork (DotDevelop) replace the current codebase? → A: No; only useful changes
  are cherry-picked, with their origin recorded.
- Q: Where do commands run? → A: Only inside the podman development container, never on the host.
- Q: What coverage targets apply? → A: Core ≥ 60%, whole Linux build ≥ 40% by M4, then a ratchet.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Contributor builds and tests the headless core on Linux (Priority: P1)

A contributor clones the repository on a Linux machine that has only podman and git, runs the
documented setup/build/test commands, and gets a successful build of the IDE's core (project
model, add-in system, command-line tool) and a test report — without installing Mono or any
other runtime on the host.

**Why this priority**: Nothing else can be migrated, verified or distributed until the core
compiles and its tests run on the target platform. It is the first slice of the walking skeleton.

**Independent Test**: From a fresh clone, run the documented build and test commands inside the
development container; the build finishes without errors and the test report lists executed,
passed, failed and quarantined tests with coverage figures.

**Acceptance Scenarios**:

1. **Given** a fresh clone and a host with only podman and git, **When** the contributor runs the
   documented setup and build commands twice in a row, **Then** both runs succeed and the second
   run makes no additional changes (idempotent).
2. **Given** a successful build, **When** the contributor runs the documented test command,
   **Then** every core test is executed and either passes or is listed in the quarantine record
   with a reason, and a coverage summary is produced.
3. **Given** the development container, **When** the contributor checks for a Mono runtime,
   **Then** none is present and the build still succeeds.

---

### User Story 2 - Command-line user builds C# projects with `mdtool` (Priority: P1)

A developer uses the IDE's command-line tool to build and clean modern SDK-style C# projects and
solutions targeting .NET 10, and relies on its exit code in scripts.

**Why this priority**: It proves the project model, the add-in system and the build engine work
end to end on the new runtime without any UI, and it gives the migration its first functional
regression check.

**Independent Test**: Build a sample console project and a sample solution with the tool; run the
produced program; build a deliberately broken project and observe a failing exit code.

**Acceptance Scenarios**:

1. **Given** a valid .NET 10 console project, **When** the user runs the tool's build command on
   it, **Then** the command exits with code 0 and the produced program runs and prints its output.
2. **Given** a project with a compile error, **When** the user builds it, **Then** the tool exits
   with a non-zero code and reports the error with file and line.
3. **Given** a solution containing several projects, **When** the user builds one project by name,
   **Then** only that project (and its dependencies) is built.
4. **Given** a previously built project, **When** the user runs the clean target, **Then** the
   build outputs are removed.

---

### User Story 3 - Developer edits and builds C# code in the graphical IDE (Priority: P2)

A developer launches MonoDevelop on a Linux desktop, opens an SDK-style C# solution, browses it in
the solution tree, edits C# files with syntax highlighting and code completion, builds, and sees
errors and warnings listed with navigation to the source.

**Why this priority**: This is the product's core value, but it depends on Story 1 and on the GTK3
foundation; it is delivered after the headless skeleton.

**Independent Test**: Launch the IDE (headless display in CI), open the sample solution, trigger a
build, and verify the error list; run a scripted smoke test that exits 0 on success.

**Acceptance Scenarios**:

1. **Given** a Linux desktop session, **When** the user starts MonoDevelop, **Then** the main
   window appears with the welcome page within the startup time budget.
2. **Given** an opened C# solution, **When** the user types in a C# file, **Then** keywords are
   highlighted and code completion offers members of the types in scope.
3. **Given** an opened solution with an error, **When** the user builds it, **Then** the error list
   shows the error and activating it opens the file at the reported line.
4. **Given** a repository-backed solution, **When** the user opens the version-control views,
   **Then** Git status and history of files are shown.

---

### User Story 4 - Developer debugs a .NET 10 program from the IDE (Priority: P2)

A developer sets a breakpoint in a C# console program, starts debugging from the IDE, stops at the
breakpoint, inspects local variables, steps over a line and continues to completion.

**Why this priority**: Debugging is a defining IDE capability; the previous debugger only
supported the Mono runtime, so without this the IDE cannot debug any program on the new platform.

**Independent Test**: An automated test drives a debug session against a sample program and checks
breakpoint hit, variable values and process exit code.

**Acceptance Scenarios**:

1. **Given** a breakpoint on a line of a sample program, **When** the user starts debugging,
   **Then** execution stops at that line.
2. **Given** a stopped session, **When** the user inspects locals, **Then** their current values
   are shown; **When** the user steps over, **Then** execution advances one line.
3. **Given** a running session, **When** the program ends, **Then** the IDE reports the exit code
   and returns to edit mode.

---

### User Story 5 - Linux user installs MonoDevelop as a Flatpak (Priority: P3)

A Linux user installs MonoDevelop from a Flatpak bundle produced by the project's release pipeline,
launches it from the desktop menu, and uses it with a .NET 10 SDK.

**Why this priority**: Distribution only matters once Stories 1–4 work; Flatpak is the chosen first
format because it ships its own GTK runtime and works across distributions.

**Independent Test**: Install the produced bundle in a clean environment, run the version command
and a headless build through it, and launch the IDE under a virtual display.

**Acceptance Scenarios**:

1. **Given** a clean Linux environment with Flatpak, **When** the user installs the bundle,
   **Then** MonoDevelop appears in the application menu with its icon and file associations.
2. **Given** the installed Flatpak, **When** the user builds the sample project from it, **Then**
   the build succeeds using a .NET 10 SDK.

---

### User Story 6 - Maintainer gets automated builds and releases (Priority: P3)

The maintainer pushes a change and the pipeline automatically builds, tests, measures coverage,
checks formatting and known vulnerabilities, runs the GUI smoke test and publishes versioned
artifacts; tagging a version publishes a release with the Flatpak bundle.

**Why this priority**: Keeps the migration from regressing once individual pieces work.

**Independent Test**: Trigger the pipeline on a commit and on a tag; inspect job results and
published artifacts.

**Acceptance Scenarios**:

1. **Given** a pushed commit, **When** the pipeline runs, **Then** it reports build, test, coverage,
   format, vulnerability and smoke-test results and stores artifacts named with version and commit.
2. **Given** a version tag, **When** the release job runs, **Then** a release with the Flatpak
   bundle, checksums and a software bill of materials is published.

### Edge Cases

- Opening a legacy (non SDK-style, .NET Framework) project: the IDE must still load it and report
  clearly when it cannot be built on this platform instead of crashing.
- No .NET SDK available to the IDE (e.g. inside Flatpak without SDK access): the IDE starts and
  shows an actionable message instead of failing silently.
- Add-in built for the old runtime or missing dependency: the add-in is skipped, the failure is
  logged, and the IDE keeps running.
- Test run started from a different working directory or tool (e.g. the test runner as entry
  program): add-in discovery still finds the IDE's add-ins.
- Wayland session vs X11 session: the IDE runs on both.
- Build cancelled by the user: the build stops promptly and the IDE remains responsive.
- Large solution (the IDE's own `main/MonoDevelop.Linux.sln`): the IDE stays responsive while
  loading — no main-loop stall longer than 1 second during load.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST build from a clean clone on Linux inside the reference
  container, with a single documented command, without Mono or any Mono component.
- **FR-002**: The core (project model, add-in system, build engine integration, command-line tool)
  MUST run on .NET 10 LTS.
- **FR-003**: The system MUST detect installed .NET SDKs and use one of them as the build runtime
  for user projects.
- **FR-004**: The add-in system MUST discover and load the IDE's add-ins from the installation's
  add-in directory, including extension points declared in add-in manifests.
- **FR-005**: The command-line tool MUST build and clean SDK-style C# projects and solutions,
  select a project by name, select a configuration, and return a non-zero exit code on failure.
- **FR-006**: The graphical IDE MUST start on Linux with a GTK3 user interface on X11 and Wayland.
- **FR-007**: The IDE MUST open, display, edit (with C# syntax highlighting and code completion)
  and build SDK-style C# solutions, and list build errors with navigation to source.
- **FR-008**: The IDE MUST debug .NET 10 programs: breakpoints, stepping, locals inspection,
  process exit reporting.
- **FR-009**: The IDE MUST show Git status, diff and history for files in Git repositories.
- **FR-010**: The IDE MUST add, update, remove and restore NuGet package references of SDK-style
  projects from nuget.org.
- **FR-011**: The IDE MUST discover, run and report results of NUnit, xUnit and MSTest tests in
  .NET 10 test projects (through the VSTest platform).
- **FR-012**: Components that only serve macOS or Windows, and legacy technologies without a
  .NET 10 / Linux equivalent (web forms, web references/WCF, the old GUI designer, Subversion,
  autotools), MUST be excluded from the Linux build and listed as breaking changes.
- **FR-013**: The project MUST produce a Flatpak bundle that installs the IDE with its desktop entry,
  icon and MIME/file associations, and launches it.
- **FR-014**: Logs MUST be structured, with level and format selectable at start-up, and start-up
  logs MUST record the IDE, runtime and SDK versions.
- **FR-015**: Every documented developer workflow (setup, restore, build, test, run, debug,
  package) MUST be available as an idempotent script runnable inside the reference container.
- **FR-016**: The build MUST NOT depend on any third-party package with a known High or Critical
  vulnerability, nor on unreachable package feeds or private repositories.
- **FR-017**: Third-party components that require local modifications MUST live in the same
  repository with their origin, source revision and license recorded.
- **FR-018**: The IDE's text and messages MUST remain localizable with the existing translations.

### Key Entities

- **Workspace / Solution / Project**: the user's code organization the IDE loads, builds and saves;
  must round-trip without losing information.
- **Add-in**: a unit of IDE functionality with a manifest declaring extension points and extensions;
  discovered at start-up from the add-in directory.
- **Target runtime / SDK**: an installed .NET SDK used to build and run user projects.
- **Debug session**: a running program under the debugger with breakpoints, threads and frames.
- **Quarantined test**: a test excluded from the gate, with a recorded reason, owner and date.
- **Evidence record**: stored output of a validation command proving a milestone criterion.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A contributor with only podman and git reaches a successful build from a fresh clone
  by following the setup guide, with zero manual steps beyond the documented commands.
- **SC-002**: By M4, every test project in the Linux solution executes on the target platform, and
  quarantined test cases are at most 15% of the test cases discovered (`dotnet test --list-tests`)
  across those projects, excluding tests already `[Ignore]`d upstream; each has a recorded reason,
  and the quarantine count per suite never increases after the suite is first converted.
- **SC-003**: By M4, line coverage of the core library is at least 60% and of the whole Linux
  solution at least 40%; afterwards coverage never decreases between accepted changes (ratchet).
  "Whole Linux solution" means the MonoDevelop product assemblies built by it: vendored third-party
  code (`main/vendor`), samples, test projects and test helpers are excluded (coverlet filter in
  `main/msbuild/Linux/Test.targets`); `scripts/test.sh` records it as the `total` line of the
  ratchet file `docs/evidence/M4/coverage-baseline.txt`.
- **SC-004**: The command-line build of the sample console project and the sample solution succeeds
  and the broken sample fails, in every pipeline run (the check is a pipeline step).
- **SC-005**: The graphical smoke test (start, open sample solution, build, zero errors, exit)
  passes under a virtual display on X11 and on a headless Wayland compositor; start-up to main
  window takes at most 10 seconds in the reference container on the maintainer's workstation
  (x86-64, measured and recorded as evidence).
- **SC-006**: An automated debug scenario (breakpoint hit, locals read, step, exit) passes.
- **SC-007**: A full pipeline run (build + tests + smoke) completes in at most 15 minutes of
  wall-clock time with a warm dependency cache, measured with `scripts/ci.sh` in the reference
  container (and on the hosted runner once pushing is authorized).
- **SC-008**: The Flatpak bundle installs in a clean environment and passes the version and
  headless build checks.
- **SC-009**: Zero High/Critical known vulnerabilities in the dependency graph of the Linux build.

## Assumptions

- The previous build (Mono + autotools + dead package feeds + a private repository) cannot be
  reproduced; the pre-migration baseline is the static inventory in `docs/evidence/M0/` plus the
  first run on the new platform (see research).
- C# is the only language supported in the first release; F#, VB.NET, IL assembler and text
  templating support are deferred.
- The product keeps the name "MonoDevelop" for now.
- The IDE builds user projects with a .NET SDK installed on the machine (or reachable from the
  Flatpak sandbox); bundling an SDK is a packaging decision recorded in an ADR.
- The reference machine for performance targets is the maintainer's workstation (x86-64, Linux);
  arm64 support is desirable but not required for the first release.
- Community forks (notably DotDevelop) are used only as a source of cherry-picked changes; the local
  codebase remains the base.
- Specification artifacts are written in English, the language of the codebase.
