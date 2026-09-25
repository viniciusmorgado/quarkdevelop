# M3 evidence — mdtool on .NET 10 (quickstart § M3)

Generated inside the dev container: SDK 10.0.401, runtime 10.0.12, mono: absent, base commit b61597c405.

```text
$ dotnet main/build/bin/mdtool.dll -q
- build: Project build tool
- dbgen: Parser database generation tool
- project-export: Project conversion tool
exit code: 0

$ dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Hello/Hello.csproj
exit code: 0

$ dotnet main/tests/linux-smoke/Hello/bin/Debug/net10.0/Hello.dll
Hello, MonoDevelop!
exit code: 0

$ dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Broken/Broken.csproj
     <repo>/main/tests/linux-smoke/Broken/Program.cs(2,27): error CS0103: The name 'undefinedSymbol' does not exist in the current context
   <repo>/main/tests/linux-smoke/Broken/Program.cs(2,27): error CS0103: The name 'undefinedSymbol' does not exist in the current context
<repo>/main/tests/linux-smoke/Broken/Program.cs(2,27) : error CS0103: The name 'undefinedSymbol' does not exist in the current context
exit code: 1

$ dotnet main/build/bin/mdtool.dll build -p:Hello main/tests/linux-smoke/Smoke.sln
exit code: 0

$ dotnet main/build/bin/mdtool.dll build -c:Release main/tests/linux-smoke/Smoke.sln
exit code: 0

$ dotnet main/build/bin/mdtool.dll build -t:Clean main/tests/linux-smoke/Hello/Hello.csproj
exit code: 0

$ dotnet main/build/bin/mdtool.dll build main/tests/linux-smoke/Missing.csproj
exit code: 1

```
