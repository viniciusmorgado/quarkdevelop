# Contract: `mdtool` command line (preserved)

Entry: `dotnet main/build/bin/mdtool.dll [-v|-q] [--no-reg-update] <tool> [tool args]`
(installed launcher: `mdtool`).

| Invocation | Behavior | Exit code |
|---|---|---|
| `mdtool` (no args) or `-h` | lists available tools (includes `build`) | 0 |
| `mdtool build [file]` | builds the solution/project `file` (or the single solution in cwd) with the default configuration | 0 ok / 1 build failed |
| `mdtool build -t:Clean file` | cleans outputs | 0 / 1 |
| `mdtool build -c:Release file` | builds the named solution configuration | 0 / 1 |
| `mdtool build -p:NAME file.sln` | builds only project NAME (and its references) of the solution | 0 / 1 |
| `mdtool build <missing file>` | prints an error | 1 |

Errors are printed as `file(line,col): error CODE: message`. Argument syntax (`-p:`/`--project:`,
`-t:`/`--target:`, `-c:`/`--configuration:`) is unchanged from MonoDevelop 8.6
(`main/src/core/MonoDevelop.Core/MonoDevelop.Projects/BuildTool.cs`). The `-r:` (Mono runtime
prefix) option is accepted and ignored with a warning on .NET.
