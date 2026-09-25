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
./scripts/pm bash -lc 'git submodule status | wc -l'                            # 0 (all 15 replaced or removed, T148)
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
./scripts/pm ./scripts/lint.sh                       # shellcheck (maintained scripts) + actionlint
```

## M3 — Headless walking skeleton (US1, US2)

```bash
./scripts/pm ./scripts/build.sh                      # TreatWarningsAsErrors + per-project baselines (ADR 0018)
./scripts/pm dotnet main/build/bin/mdtool.dll                                    # lists "build"
./scripts/pm bash -lc 'dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Hello/Hello.csproj && dotnet main/tests/linux-smoke/Hello/bin/Debug/net10.0/Hello.dll | grep -q Hello'
./scripts/pm bash -lc '! dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Broken/Broken.csproj'
./scripts/pm dotnet main/build/bin/mdtool.dll build -p:Hello main/tests/linux-smoke/Smoke.sln
./scripts/pm dotnet main/build/bin/mdtool.dll build -t:Clean main/tests/linux-smoke/Hello/Hello.csproj
./scripts/pm ./scripts/audit.sh                      # fails on High/Critical advisories
```

## M4 — Tests & coverage (US1)

```bash
./scripts/pm ./scripts/test.sh                  # TRX in out/tests, coverage summary in out/coverage/Summary.txt
./scripts/pm cat out/coverage/Summary.txt       # Core ≥ 60 %; total: ratchet only (SC-003)
cat docs/evidence/M4/quarantine.md              # every excluded test has a reason; ≤ 15 %
```

## M5 — GUI, run & debug (US3, US4)

```bash
./scripts/pm xvfb-run -a dotnet run --project main/vendor/xwt/TestApps/Gtk3Test          # M5a: Xwt/GTK3 window
./scripts/pm xvfb-run -a dotnet main/build/bin/MonoDevelop.dll --smoke-test main/tests/linux-smoke/Smoke.sln   # exit 0
./scripts/pm xvfb-run -a dotnet test main/tests/Ide.Tests
./scripts/pm dotnet test main/src/addins/MonoDevelop.Debugger/MonoDevelop.Debugger.Tests --filter FullyQualifiedName~NetCoreDbg
./scripts/pm bash -lc 'export XDG_RUNTIME_DIR=$(mktemp -d); weston --backend=headless --socket=wayland-md & sleep 2; WAYLAND_DISPLAY=wayland-md GDK_BACKEND=wayland dotnet main/build/bin/MonoDevelop.dll --smoke-test main/tests/linux-smoke/Smoke.sln'
./scripts/pm ./scripts/inventory.sh --linux-sln out/inventory-linux.md && grep 'GTK2-only' out/inventory-linux.md   # target 0
```

## M6 — CI/CD (US6)

```bash
./scripts/pm bash -lc 'actionlint && time ./scripts/ci.sh'   # ≤ 15 min (SC-007); hosted run only after push authorization
```

## M7 — Flatpak (US5)

```bash
PM_PROFILE=flatpak ./scripts/pm ./scripts/package-flatpak.sh                   # out/monodevelop.flatpak (+ sha256, SBOM)
PM_PROFILE=flatpak ./scripts/pm bash -lc 'flatpak --user install -y out/monodevelop.flatpak && flatpak run io.github.viniciusmorgado.MonoDevelop --version'
```

## M8/M9 — Hardening & release

```bash
./scripts/pm ./scripts/audit.sh
./scripts/pm bash -lc 'MD_LOG_FORMAT=json dotnet main/build/bin/mdtool.dll 2>&1 | head -1 | jq .'
./scripts/pm ./scripts/test.sh && ./scripts/pm ./scripts/run.sh --headless --smoke-test main/tests/linux-smoke/Smoke.sln
./scripts/pm ./scripts/test.sh --all   # informational: includes quarantined tests
```
