# Quickstart & Validation Scenarios

**Host prerequisites**: `podman` (rootless) and `git`. Nothing else is installed on the host.
Every command below runs from the repository root. `./scripts/pm` builds the dev image on first use.

```bash
./scripts/pm dotnet --info | head -3          # .NET SDK 10.0.x inside the container
./scripts/pm bash -lc '! command -v mono'      # no Mono anywhere
```

## M0 — Baseline & spikes

```bash
./scripts/pm ./scripts/inventory.sh && test -s docs/evidence/M0/inventory.md
./scripts/pm bash -lc 'git submodule status | grep -c "^ "'                     # 15 initialized
./scripts/pm bash -lc 'cd spikes/gtk3-hello && dotnet build -c Release && xvfb-run -a dotnet bin/Release/net10.0/gtk3-hello.dll'   # "gtk3-hello: OK"
./scripts/pm bash -lc 'cd spikes && dotnet build addins-plugin -c Release && dotnet addins-host/bin/Release/net10.0/AddinsHost.dll'   # "addins-spike: OK"
./scripts/pm bash -lc 'cd spikes/roslyn-publicizer && dotnet run -c Release'     # "roslyn-publicizer: OK"
```

## M1 — Specification artifacts

```bash
test -f docs/constitution.md
! grep -q "NEEDS CLARIFICATION" specs/001-linux-dotnet10-migration/spec.md
ls docs/adr/*.md | wc -l                                                         # ≥ 17
```

## M2 — Toolchain

```bash
./scripts/pm dotnet --version                                                    # honours global.json (10.0.x)
./scripts/pm ./scripts/setup.sh && ./scripts/pm ./scripts/build.sh && ./scripts/pm ./scripts/build.sh   # idempotent
./scripts/pm bash -lc 'shellcheck scripts/*.sh scripts/pm scripts/git-commit'
```

## M3 — Headless walking skeleton (US1, US2)

```bash
./scripts/pm dotnet build main/MonoDevelop.Linux.sln -c Debug -warnaserror
./scripts/pm dotnet main/build/bin/mdtool.dll                                    # lists "build"
./scripts/pm bash -lc 'dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Hello/Hello.csproj && dotnet main/tests/linux-smoke/Hello/bin/Debug/net10.0/Hello.dll | grep -q Hello'
./scripts/pm bash -lc '! dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Broken/Broken.csproj'
./scripts/pm dotnet main/build/bin/mdtool.dll build -p:Hello main/tests/linux-smoke/Smoke.sln
./scripts/pm dotnet main/build/bin/mdtool.dll build -t:Clean main/tests/linux-smoke/Hello/Hello.csproj
./scripts/pm dotnet list main/MonoDevelop.Linux.sln package --vulnerable --include-transitive   # no High/Critical
```

## M4 — Tests & coverage (US1)

```bash
./scripts/pm ./scripts/test.sh                  # TRX in out/tests, coverage summary in out/coverage/Summary.txt
./scripts/pm cat out/coverage/Summary.txt       # Core ≥ 60 %, total ≥ 40 %
cat docs/evidence/M4/quarantine.md              # every excluded test has a reason; ≤ 15 %
```

## M5 — GUI, run & debug (US3, US4)

```bash
./scripts/pm bash -lc 'cd main/vendor/xwt/samples && xvfb-run -a dotnet run'          # M5a: Xwt/GTK3 window
./scripts/pm xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test main/tests/linux-smoke/Smoke.sln   # exit 0
./scripts/pm dotnet test main/tests/Ide.Tests --filter "Category=Smoke|Category=Debugger"
./scripts/pm bash -lc 'grep -rlE "ExposeEvent|Gdk\.GC|SizeRequested" main/src --include=*.cs | wc -l'   # target 0 in the Linux solution
```

## M6 — CI/CD (US6)

The workflow `.github/workflows/ci.yml` runs the M3–M5 commands inside the same container image.
Locally the equivalent is `./scripts/pm ./scripts/ci.sh`.

## M7 — Flatpak (US5)

```bash
./scripts/pm-flatpak ./scripts/package-flatpak.sh                               # out/monodevelop.flatpak
./scripts/pm-flatpak bash -lc 'flatpak --user install -y out/monodevelop.flatpak && flatpak run com.monodevelop.MonoDevelop --version'
```

## M8/M9 — Hardening & release

```bash
./scripts/pm dotnet list main/MonoDevelop.Linux.sln package --vulnerable --include-transitive
./scripts/pm bash -lc 'MD_LOG_FORMAT=json dotnet main/build/bin/mdtool.dll 2>&1 | head -1 | jq .'
./scripts/pm ./scripts/test.sh --all && ./scripts/pm xvfb-run -a ./scripts/run.sh --smoke-test
```
