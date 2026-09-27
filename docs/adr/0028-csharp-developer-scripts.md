# 0028 — Developer scripts in C# on the host

- Status: Accepted
- Date: 2026-09-25

## Context and Problem Statement

The build, test, run, CI and packaging scripts were fifteen Bash scripts and a shared library (`scripts/*.sh`,
`scripts/lib.sh`). They ran in a podman development container (`scripts/pm`, `Dockerfile`,
`.devcontainer/devcontainer.json`). The workflows no longer used them: `ci.yml` and `release.yml` run every step
inline, in containers they build themselves. GitHub listed Shell, HTML, C and Makefile next to C# and F#. The
maintainer wants the repository in C# and F# only, with builds on the host. .NET 10 runs a single C# file as a
program without a project file (`dotnet app.cs`, a file-based app). Which language and environment should the
developer scripts use?

## Decision Drivers

- One language for the product and its tooling.
- Nothing but the .NET SDK to run the scripts, and no container runtime on the developer's machine.
- The scripts keep every check, option and special case of the Bash versions.
- The workflows stay self-contained and unchanged.

## Considered Options

1. C# file-based apps (`dotnet scripts/<name>.cs`), run on the host.
2. Keep the Bash scripts and the podman container.
3. One console project (`scripts/Scripts.csproj`) with a command per script.
4. C# file-based apps inside the podman container.

## Decision Outcome

Option 1, chosen by the maintainer.

- Each `scripts/<name>.sh` becomes `scripts/<name>.cs` with the same name, options and outputs: `setup`, `restore`,
  `build`, `format`, `lint`, `test`, `audit`, `check-assemblies`, `run`, `debug`, `ci`, `netfx-refasm`,
  `warnings-baseline`, `package-flatpak` and `test-flatpak`. `lib.sh` is gone. Each script carries the few helpers it
  uses (the repository root from `EntryPointFileDirectoryPath`, logging, running a process), so every script stays one
  file. `[CallerFilePath]` is not used for the root: a script compiled with `ContinuousIntegrationBuild=true` (as
  `package-flatpak.cs` runs `build.cs`) gets `/_/` source paths, and `dotnet` keeps that build when the variable is
  gone. The list of C# files added by this fork (`md_new_cs_files`) is `NewCSharpFiles` in `build.cs` and `format.cs`.
- Arguments for a script go after `--`, as in `dotnet scripts/build.cs -- -c Release --check`: before it, `dotnet`
  reads options such as `-c`, `-v` or `--no-build` itself. Scripts that call other scripts pass `--` too.
- `dotnet` runs a script as a child process, and a script cannot `exec` the program it starts. `run.cs` and
  `debug.cs`, which used `exec`, leave Ctrl+C to the IDE or netcoredbg and pass SIGTERM and SIGHUP on to it, so the
  terminal comes back only when that program ends.
- `scripts/Directory.Build.props` sets `TreatWarningsAsErrors` for the scripts, as in the rest of the repository
  ([ADR 0018](0018-warning-policy.md)). `dotnet scripts/lint.cs` builds every script and checks its formatting; being
  new C# files, the scripts are also covered by the format check of CI. File-based apps enable the trimming and
  platform analyzers: `ci.cs` and `test-flatpak.cs` declare `SupportedOSPlatform ("linux")` for
  `File.SetUnixFileMode`. The scripts have no shebang line and no `#:` directives (`dotnet format` reports them), and
  they use no NuGet package.
- The host provides the .NET 10 SDK, git, gettext (the build compiles the translations with `msgfmt`,
  [ADR 0014](0014-localization-ngettext.md)) and GTK 3. The SDK must be 10.0.400 or later, as the image's 10.0.401 was:
  the IDE loads the SDK's NuGet, which must be at least the 7.9 it is compiled against
  ([ADR 0020](0020-nuget-client-version.md)). The headless tests and smoke tests also need Xvfb, Weston and
  ImageMagick. The Flatpak scripts need flatpak, flatpak-builder, the desktop and AppStream validators and D-Bus.
  `dotnet scripts/setup.cs` checks them, warns about an older SDK and lists the missing packages. It installs the
  netcoredbg and actionlint releases that the image pinned (same versions and SHA-256) into
  `~/.cache/monodevelop/tools`, linked from `~/.local/bin`. It never replaces a program or link there that it did not
  create.
- What the host changes, compared with the container:
  - The headless runs use Xvfb, even on a desktop. `test.cs` runs the tests under `xvfb-run` whenever it is
    installed. Before, it did so only when there was no display, which was the normal case in the container. The
    GUI smoke tests, `run.cs --headless` and `test.cs` remove `WAYLAND_DISPLAY` and set `GDK_BACKEND=x11`: GDK tries
    Wayland first, and without `WAYLAND_DISPLAY` it falls back to the desktop's `wayland-0` socket.
  - The Flatpak store (the Flathub runtimes, the flatpak-builder state and the test installation) moves from the
    `md-flatpak` volume to `~/.cache/monodevelop/flatpak` (`MD_FLATPAK_STORE`). `test-flatpak.cs` runs in its own
    D-Bus session and gives the app a home directory of its own, so it never uses the desktop's session bus or the
    data of an installed IDE in `~/.var/app`.
  - netcoredbg and actionlint come from `PATH` or `~/.local/bin`.
  - `setup.cs` reports an installed Mono instead of failing: the host is not a reference environment, and neither
    the build nor the IDE uses Mono.
- `scripts/pm`, `Dockerfile` (the development image) and `.devcontainer/devcontainer.json` are removed. The ci
  workflow builds its image from the Dockerfile in `ci.yml`, and the release workflow its Flatpak image from the
  Dockerfile it writes to `out/flatpak-files`; neither changes. `package-flatpak.cs` still reads the Flatpak files
  from `out/flatpak-files`, which the "Flatpak files" step of the release workflow writes.

### Other files that were not C# or F#

- Makefile: `main/tests/test-projects/console-project-with-makefile` and the quarantined
  `MakefileTests.MakefileSynchronization`, which needed the removed Autotools add-in
  ([ADR 0017](0017-linux-exclusions.md), T142).
- HTML, CSS and JavaScript: the `fsharp-aspnetcoremvc11` fixture (netcoreapp1.1, a retired framework) and its
  quarantined test, `Template tests.Can build netcoreapp11 MVC web app`.
- HTML: the W3C software license of the XML schemas is now plain text,
  `main/src/addins/Xml/schemas/W3C-License.txt`.
- Shell: `build_libgit2.sh` of the Git add-in, which built libgit2 from a submodule that no longer exists (the native
  library comes from the LibGit2Sharp.NativeBinaries package).
- C: `XmlChar.cs` started with an Emacs `Mode: C` line, which GitHub reads as the file's language. It now says
  `csharp`.

The only non-.NET code left is test data: the Razor page of `main/tests/test-projects/aspnetcore-razor-class-lib`
(for the ASP.NET Core tests, which are not in the Linux build yet) and two empty `.js` files of the wildcard tests.

### Consequences

- Good: one language. The scripts get the compiler, the analyzers and the format check of the rest of the code.
- Good: building and testing need only the .NET SDK and a few system packages; no container runtime.
- Bad: the first run of a script compiles it, which takes a few seconds. Later runs use the cached build, which
  `dotnet` rebuilds when the file changes.
- Bad: the host needs the system packages that the image had. `setup.cs` lists them but does not install them.
- Bad: local runs no longer use the image of CI, and the SDK is no longer pinned: the host's SDK, GTK and system
  libraries can make a local result differ. CI stays the reference.
- Bad: the `--` before the script's arguments is easy to forget.
- Option 2 rejected: the maintainer wants no shell code in the repository, and the development image duplicated the
  images that the workflows build. Option 3 rejected: a project for all the commands adds a project file and a
  command dispatcher, while each file-based script can be read and run on its own. Option 4 rejected: the maintainer
  chose to drop the container.
