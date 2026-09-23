# MonoDevelop development container (Linux-first, .NET 10 LTS, GTK3).
# Every build/test/run command in this repository is executed inside this image
# through ./scripts/pm (see docs/linux/setup.md).

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-noble
ARG UV_IMAGE=ghcr.io/astral-sh/uv:0.9.5

FROM ${UV_IMAGE} AS uv

FROM ${DOTNET_SDK_IMAGE}

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    UV_TOOL_BIN_DIR=/usr/local/bin \
    UV_TOOL_DIR=/opt/uv-tools \
    UV_PYTHON_INSTALL_DIR=/opt/uv-python

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl unzip xz-utils file \
      git jq shellcheck python3 \
      libgtk-3-0t64 libgtk-3-bin librsvg2-common adwaita-icon-theme \
      fonts-dejavu-core dbus-x11 at-spi2-core \
      xvfb xauth x11-utils imagemagick \
 && rm -rf /var/lib/apt/lists/*

COPY --from=uv /uv /uvx /usr/local/bin/

# The repository marks itself as a safe git directory regardless of the mapped uid.
RUN git config --system --add safe.directory '*'

WORKDIR /src
