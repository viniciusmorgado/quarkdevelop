# 0022 — Flatpak packaging

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

Flatpak is the first distribution format (spec clarification, FR-013, SC-008). The IDE is a set of
framework-dependent .NET 10 assemblies (`main/build/{bin,AddIns,data,locale}`) that P/Invoke GTK 3 and
its stack (ADR 0011, ADR 0013). At run time it also needs a full .NET SDK: it hosts the SDK's MSBuild
through MSBuildLocator (ADR 0008), loads the SDK's NuGet client (ADR 0020, SDK 10.0.4xx or newer) and
builds, runs and tests user projects with it. Inside a Flatpak sandbox the host's `/usr` is not
visible. Which runtime, which SDK, which permissions, and how is the bundle built without network
access during the build?

## Decision Drivers

- The IDE must build the sample projects from inside the sandbox (US5, SC-008) with the same SDK the
  CI uses (10.0.401 today).
- MSBuild and NuGet are loaded **in process** from the SDK directory: the SDK must be a local
  directory, not a program reachable only through a process boundary.
- Reproducible, offline flatpak-builder build; everything runs in podman (constitution I).
- Least privilege where it does not break the IDE; Flathub conventions where possible.

## Considered Options

SDK access:

1. **Bundle the SDK** from `org.freedesktop.Sdk.Extension.dotnet10` into `/app/lib/dotnet`.
2. Use the host SDK through `flatpak-spawn --host` (`--talk-name=org.freedesktop.Flatpak`).
3. Ship only the .NET runtime (the extension's `install.sh`) and ask users to install an SDK extension
   at run time.

Build of the IDE inside flatpak-builder:

A. **Binary module**: build in the dev container (`./scripts/build.sh -c Release`), copy `main/build`
   in as a `dir` source.
B. Build from source in flatpak-builder with generated NuGet sources (`flatpak-dotnet-generator`).

## Decision Outcome

Options 1 and A.

- **App id** `io.github.viniciusmorgado.MonoDevelop` (the fork's GitHub namespace), command
  `monodevelop`; `mdtool` is the same launcher under a second name
  (`flatpak run --command=mdtool io.github.viniciusmorgado.MonoDevelop build …`).
- **Runtime** `org.gnome.Platform//51` (GTK 3.24, GLib, Pango, Cairo, gdk-pixbuf, libgit2's C
  dependencies), SDK `org.gnome.Sdk//51`, both on freedesktop-sdk 26.08. GNOME 51 is the newest
  runtime on Flathub (2026-09) and pairs with the `26.08` branch of the .NET extension, the only
  branch that ships SDK **10.0.401** (the `25.08` branch has 10.0.300, older than the NuGet 7.9 the IDE
  compiles against).
- **.NET SDK** (option 1): the manifest copies the whole extension (`/usr/lib/sdk/dotnet10/lib`:
  host, shared runtimes including ASP.NET Core, `sdk/10.0.401`, `packs/`, `sdk-manifests/`,
  `templates/`) into `/app/lib/dotnet`. The extension's own `install-sdk.sh` is not used: it leaves out
  `packs/` (targeting packs; no project builds without them), `sdk-manifests/` and `templates/`. The
  launcher sets `DOTNET_ROOT=/app/lib/dotnet` and puts it first on `PATH`, so the IDE, MSBuildLocator,
  the out-of-process builder (`dotnet exec`) and user programs all use it.
- **Workloads and tools**: `/app` is read-only, so the manifest writes the SDK's `userlocal` marker
  (`metadata/workloads/10.0.400/userlocal`): `dotnet workload install` then installs into
  `~/.dotnet`. Global tools go to `~/.dotnet/tools` (on the launcher's `PATH`). NuGet packages use
  `~/.nuget/packages`, shared with a host SDK. A different SDK (another feature band, a preview) is
  not supported inside the sandbox; users who need one run the IDE from source (`scripts/run.sh`).
- **Build** (option A): `packaging/flatpak/io.github.viniciusmorgado.MonoDevelop.yml` takes
  `main/build` (without `tests/` and `samples/`) as a `dir` source, keeps only the `unix`, `linux` and
  `linux-x64` (or `linux-arm64`) native assets of the NuGet `runtimes/` folders, and installs the
  launcher, desktop entry, AppStream metainfo, MIME types, license and hicolor PNG icons (16–512 px).
  `strip: false`, `no-debuginfo: true`: the managed assemblies and Microsoft's native host are shipped
  as built. The IDE build must be made with `ContinuousIntegrationBuild=true` (deterministic source and
  PDB paths, as in CI); `package-flatpak.sh` builds it that way and refuses a bundle containing the
  checkout path. flatpak-builder needs no network (the Flathub runtime, SDK and extension are installed
  with `--install-deps-from=flathub` before the build).
- **Permissions** (`finish-args`):
  - `--share=network`: NuGet restore, package search, `dotnet new`/workload downloads.
  - `--socket=wayland`, `--socket=fallback-x11`, `--share=ipc`, `--device=dri`: the GTK 3 UI on
    Wayland, X11 only when there is no Wayland session (ipc for X11 shared memory).
  - `--filesystem=home`: projects are opened, built and run in place, anywhere in the home directory;
    the IDE uses GTK file dialogs, not the document portal, so a narrower grant (`xdg-documents`,
    portal-only) would hide most existing projects and `~/.nuget`/`~/.dotnet`. `host` is not granted:
    projects outside the home directory are out of reach (documented).
  - `--env=DOTNET_ROOT=/app/lib/dotnet`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`.
  - No `--talk-name=org.freedesktop.Flatpak` (no `flatpak-spawn --host`), no device or system bus
    access.
- **Desktop integration** from the upstream files: `main/monodevelop.desktop` →
  `packaging/flatpak/io.github.viniciusmorgado.MonoDevelop.desktop` (icon renamed to the app id,
  deprecated `Encoding` key and GNOME Bugzilla keys dropped, `Keywords` added);
  `main/monodevelop.appdata.xml` → `…metainfo.xml` (AppStream 1.0: `developer`, `launchable`,
  `content_rating`, screenshots from `docs/evidence/M5`, `releases`); `main/monodevelop.xml` → `…xml`
  with only `application/x-sln` and `application/x-csproj` (`text/x-csharp` comes from shared-mime-info;
  the legacy MonoDevelop/SharpDevelop, VB.NET and ASP.NET types are dropped); icons from
  `main/theme-icons/GNOME/monodevelop-<size>.png` (`monodevelop.svg` is a 48 px design and is not
  installed as a scalable icon).
- **Packaging container**: `PM_PROFILE=flatpak ./scripts/pm …` builds `packaging/flatpak/Containerfile`
  (the dev image's pinned .NET SDK base + flatpak 1.14, flatpak-builder 1.4, appstream,
  desktop-file-utils, Xvfb). The only extra podman option is `--security-opt unmask=/proc/*`:
  bubblewrap mounts a new `/proc` for each sandbox, which the kernel refuses while podman masks
  paths under `/proc`. Found by elimination (2026-09-24, rootless podman, `--userns=keep-id`):
  `--cap-add SYS_ADMIN` breaks bubblewrap ("Unexpected capabilities but not setuid"), `seccomp=unconfined`
  alone does not help ("Can't mount proc on /newroot/proc"), and `/dev/fuse` is unnecessary with
  `flatpak-builder --disable-rofiles-fuse`. The Flathub runtimes, the flatpak-builder state, the build
  directory and the test installation live in the `md-flatpak` volume (`FLATPAK_USER_DIR`), never in
  the host's flatpak installation. `flatpak run` in the container additionally needs a D-Bus session
  (`dbus-run-session`) and a system bus address (pointed at the session bus) — the test script does it.
- **Scripts**: `scripts/package-flatpak.sh` (validators, flatpak-builder, `flatpak build-bundle
  --runtime-repo=<Flathub>`, sha256, CycloneDX SBOM) → `out/monodevelop.flatpak`,
  `out/monodevelop.flatpak.sha256`, `out/monodevelop.cdx.json` (the names `release.yml` publishes);
  `scripts/test-flatpak.sh` installs the bundle into a fresh installation and runs the version, headless
  build and IDE smoke checks (T124, `docs/evidence/M7/`). Both run in the flatpak profile; the IDE build
  they use comes from the dev profile (or is built by `package-flatpak.sh` with the same SDK when
  missing).
- **SBOM**: CycloneDX .NET tool 6.2.0 (local tool, `dotnet-tools.json`) over
  `main/MonoDevelop.Linux.sln` with test projects and development-only packages excluded (the NuGet
  packages whose assemblies ship in the bundle), plus the bundled .NET SDK as a component.

### Not included

- netcoredbg: the IDE's netcoredbg engine is not in the build yet (T112); when it is, the adapter can
  be added to the manifest as a pinned, checksummed archive source (as in the `Containerfile`).
- An external terminal for "Run in external console" (no host terminal in the sandbox).
- Compiled translations (`main/po` is not compiled yet; the IDE runs in English).
- Flathub submission: `flatpak-builder-lint` reports `finish-args-home-filesystem-access` (needs a
  Flathub exception, usual for IDEs) and unreachable/unmirrored screenshots (the URLs point at the
  default branch; Flathub mirrors screenshots itself). Building from source with generated NuGet
  sources (option B) is also a Flathub requirement and a later improvement.
- arm64 bundle: the manifest supports `aarch64`, but only x86_64 is built and tested.

### Consequences

- Good: one self-contained bundle; builds inside the sandbox use exactly the SDK the IDE was tested
  with, and MSBuild/NuGet load in process as outside the sandbox.
- Good: no build-time network, no NuGet source list to maintain; the Release build is the same one
  CI tests.
- Bad: size — about 150 MB compressed, about 720 MB installed (the SDK is ~620 MB of it), plus the
  GNOME 51 runtime (shared with other GNOME apps).
- Bad: SDK updates (security patches) require a new bundle; bump the extension branch and rebuild.
- Bad: GNOME 50+ loads images out of process (glycin, `flatpak-spawn --sandbox` through the Flatpak
  portal `org.freedesktop.portal.Flatpak`, which ships with flatpak itself). Without a session bus that
  can activate it, every icon of the IDE fails to load. Desktop sessions have it; the container test
  gets it through `dbus-run-session`, with the system bus address exported to the activation
  environment (`dbus-update-activation-environment`); the test fails on any "Error loading icon".
- Option 2 rejected: MSBuild and NuGet cannot be loaded in process from the host through
  `flatpak-spawn`; it would need `org.freedesktop.Flatpak` (a sandbox escape) and a host SDK of the
  right version. Option 3 rejected: an IDE that cannot build out of the box fails US5.
