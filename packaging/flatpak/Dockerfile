# Flatpak packaging container (M7, ADR 0022), selected with PM_PROFILE=flatpak ./scripts/pm <cmd>.
# Same pinned .NET SDK base as the development image (so the SBOM tool and a Release build can run here
# too) plus flatpak, flatpak-builder and the desktop/AppStream validators. The Flathub runtimes, the
# flatpak-builder state and the test installation live in the podman volume md-flatpak, never in the
# host's flatpak installation.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29

FROM ${DOTNET_SDK_IMAGE}

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl xz-utils zstd file git jq python3 shellcheck \
      flatpak flatpak-builder bubblewrap elfutils \
      appstream desktop-file-utils shared-mime-info \
      dbus dbus-x11 xvfb xauth x11-utils imagemagick \
 && rm -rf /var/lib/apt/lists/*

# Mount point of the md-flatpak volume (the volume copies these permissions on first use).
RUN mkdir -p /var/lib/md-flatpak && chmod 1777 /var/lib/md-flatpak

ENV FLATPAK_USER_DIR=/var/lib/md-flatpak/user

RUN git config --system --add safe.directory '*'

WORKDIR /src
