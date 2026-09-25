# M0 evidence — baseline and spikes

All commands ran inside the dev container (`./scripts/pm`, image built from `Dockerfile`:
.NET SDK 10.0.401, runtime 10.0.12, GTK 3.24.41, no Mono).
Containerfile
| Task | Evidence | Result |
|---|---|---|
| T001–T003 container + specification layout | commit `eab706b582` | `dotnet --version` 10.0.401, `git status` works in container |
| T004 submodules | [inventory.md](inventory.md) § Submodules | 15/15 initialized |
| T005 inventory | [inventory.md](inventory.md) | static blocker counts |
| T006 GtkSharp 3 | [T006-gtk3-hello.png](T006-gtk3-hello.png), `spikes/gtk3-hello` | OK (window + Cairo under Xvfb) |
| T007 Mono.Addins | `spikes/addins-host`, `spikes/addins-plugin` | OK (in-process scan, XML + type extensions) |
| T008 Roslyn | `spikes/roslyn-ivt`, `spikes/roslyn-publicizer` | no IVT for MonoDevelop in 5.9; Publicizer works |
| T009 legacy baseline | [T009-legacy-baseline.md](T009-legacy-baseline.md) | see report |
| T010 DotDevelop | [T010-dotdevelop.md](T010-dotdevelop.md) | cherry-pick candidates identified |

Reproduce: see `specs/001-linux-dotnet10-migration/quickstart.md` § M0.

Spike outputs (abridged):

```text
gtk-version=3.24.41 runtime=10.0.12
gtk3-hello: OK

registered add-ins: SamplePlugin,1.0
command SamplePlugin.HelloCommand -> hello from an add-in loaded on .NET 10.0.12
tool node id=hello-tool type=TypeExtensionNode
addins-spike: OK

Microsoft.CodeAnalysis.Workspaces.dll 5.9.0.0: 119 IVT, MonoDevelop/Xamarin/VSMac-related=0, with MonoDevelop key=0
TOTAL IVT=408 MonoDevelop-related=0

GetAncestor -> C; IsInNonUserCode(comment) -> True
roslyn-publicizer: OK
```
