# MonoDevelop development container (Linux-first, .NET 10 LTS, GTK3).
# Every build/test/run command in this repository is executed inside this image
# through ./scripts/pm (see docs/linux/setup.md).

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29
ARG UV_IMAGE=ghcr.io/astral-sh/uv:0.9.5@sha256:f459f6f73a8c4ef5d69f4e6fbbdb8af751d6fa40ec34b39a1ab469acd6e289b7

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
      git jq shellcheck python3 gettext \
      libgtk-3-0t64 libgtk-3-bin librsvg2-common adwaita-icon-theme \
      fonts-dejavu-core dbus-x11 at-spi2-core \
      xvfb xauth x11-utils imagemagick weston \
 && rm -rf /var/lib/apt/lists/*

COPY --from=uv /uv /uvx /usr/local/bin/

# netcoredbg (Samsung, MIT) — debug adapter for .NET programs (ADR 0016). Pinned + checksum.
ARG NETCOREDBG_VERSION=3.2.0-1092
ARG NETCOREDBG_SHA256=080eb3b2d2152465f599d3b33d1ee6e747794e11cc0a3773ec689f5e5f2c5afa
RUN curl -fsSL -o /tmp/netcoredbg.tgz \
      "https://github.com/Samsung/netcoredbg/releases/download/${NETCOREDBG_VERSION}/netcoredbg-linux-amd64.tar.gz" \
 && echo "${NETCOREDBG_SHA256}  /tmp/netcoredbg.tgz" | sha256sum -c - \
 && tar -xzf /tmp/netcoredbg.tgz -C /opt \
 && ln -s /opt/netcoredbg/netcoredbg /usr/local/bin/netcoredbg \
 && rm /tmp/netcoredbg.tgz \
 && netcoredbg --version | head -1

# actionlint — validates .github/workflows locally (CI cannot be exercised without a push).
ARG ACTIONLINT_VERSION=1.7.12
ARG ACTIONLINT_SHA256=8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8
RUN curl -fsSL -o /tmp/actionlint.tgz \
      "https://github.com/rhysd/actionlint/releases/download/v${ACTIONLINT_VERSION}/actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz" \
 && echo "${ACTIONLINT_SHA256}  /tmp/actionlint.tgz" | sha256sum -c - \
 && tar -xzf /tmp/actionlint.tgz -C /usr/local/bin actionlint \
 && rm /tmp/actionlint.tgz

# The repository marks itself as a safe git directory regardless of the mapped uid.
RUN git config --system --add safe.directory '*'

WORKDIR /src
