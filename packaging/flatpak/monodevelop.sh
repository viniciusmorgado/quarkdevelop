#!/bin/sh
# Flatpak launcher, installed as /app/bin/monodevelop (ADR 0022). The IDE runs on the .NET 10 SDK
# bundled in /app/lib/dotnet, which also builds user projects (MSBuild, NuGet: ADR 0008, ADR 0020).
# `flatpak run --command=mdtool io.github.viniciusmorgado.MonoDevelop` runs the command-line tool.
set -eu

tool=MonoDevelop
case "$(basename "$0")" in
	mdtool) tool=mdtool ;;
esac

export DOTNET_ROOT=/app/lib/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
export MONODEVELOP_LOCALE_PATH="${MONODEVELOP_LOCALE_PATH:-/app/lib/monodevelop/locale}"
# .NET global tools (`dotnet tool install -g`) and user-local workloads live in the home directory.
export PATH="/app/lib/dotnet:$PATH:${HOME}/.dotnet/tools"

exec /app/lib/dotnet/dotnet "/app/lib/monodevelop/bin/$tool.dll" "$@"
