# 0029 — ASP.NET Core add-in in the Linux build

- Status: Accepted
- Date: 2026-09-26

## Context and Problem Statement

`MonoDevelop.AspNetCore` stayed in the repository outside the Linux build (ADR 0017: deferred, then kept to be
ported). `MonoDevelop.DotNetCore` leaves the projects with launch settings, those of the Web and Worker SDKs, to that
add-in (`DotNetCoreProjectExtension.SupportsObject`). Without the add-in, such a project had neither extension. The IDE
ran it as a generic .NET project, `dotnet bin/<configuration>/<framework>/<name>.exe`, a file the SDK does not create on
Linux, so Run and Debug failed ("The application to execute does not exist"). How should Web and Worker projects run?

## Considered Options

1. Port the add-in: an SDK-style project in `main/MonoDevelop.Linux.sln`, with its tests.
2. Let `MonoDevelop.DotNetCore` run these projects too until the add-in is ported. The program runs, but without the
   profiles of `launchSettings.json` (environment, URLs, browser), so not as with `dotnet run`.

## Decision Outcome

Option 1, chosen by the maintainer. `MonoDevelop.AspNetCore` and `MonoDevelop.AspNetCore.Tests` join the solution as
SDK-style `net10.0` projects with warning baselines (ADR 0018). Run and Debug use the profiles of `launchSettings.json`
as `dotnet run` does:

- The run configurations are the `Project` profiles, in the order of the file. The order came from a dictionary, whose
  order changes from one process to the next on .NET (randomized string hashes).
- Without a profile named after the project, the Default run configuration uses the first `Project` profile (the
  templates of .NET 7 and later have `http` and `https` only). The copy stays in memory: `launchSettings.json` changes
  only when a run configuration is edited, as before.
- The command runs `dotnet <output>.dll` in the project directory, with the environment variables of the profile and
  `ASPNETCORE_URLS` set to its `applicationUrl`. The URLs are part of the command so that the debugger (netcoredbg,
  ADR 0016), which starts the program itself, gets them too. The Run button starts the debugger.
- The browser opens when the application answers, with or without the debugger:
  `AspNetCoreProjectExtension.OnExecuteCommand` opens it instead of `AspNetCoreExecutionHandler`.
- A new port, for a profile the IDE creates or for a second project with the default ports, is never the one chosen
  for HTTP already. On Linux, a socket bound without listening does not keep another socket from binding the same
  port (`SO_REUSEADDR`), so HTTP and HTTPS got the same port.
- Not built:
  - the project template wizard and the conditions of the removed `.xft.xml` templates (the templates come from
    `dotnet new`, ADR 0026);
  - `MonoDevelop.AspNetCore.DevCertInstaller`, the macOS certificate installer.
- The development certificate check acts on macOS only, as upstream. On Linux the HTTPS profiles need a certificate
  that the user sets up with `dotnet dev-certs https`.
- Publish to Folder, scaffolding and file nesting build with the add-in.
- Tests run on NUnit 3 in the IDE test host (ADR 0015): 28 of 30 pass. The other two are ignored: one needs the
  browsers of macOS, the other downloads the scaffolding package versions (ignored upstream since 2019).
  `WebTemplate_RunsWithTheFirstLaunchProfile` runs a project of the ASP.NET Core Empty template for .NET 10
  (`main/tests/test-projects/aspnetcore-web`).

### Consequences

- Good: Web and Worker projects run and debug from the IDE with the launch profile, environment, URLs and browser that
  `dotnet run` uses.
- Bad: the add-in keeps the warnings of its legacy code (baseline).
- Bad: the IDE neither checks nor installs the HTTPS development certificate on Linux.
