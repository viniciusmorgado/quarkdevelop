#!/usr/bin/env bash
# Shared helpers for the developer scripts. Source it; do not execute it.
# shellcheck shell=bash

MD_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MD_SLN="$MD_ROOT/main/MonoDevelop.Linux.sln"
MD_OUT="$MD_ROOT/out"
export MD_ROOT MD_SLN MD_OUT

md_log() {
	printf '\033[1;34m==>\033[0m %s\n' "$*" >&2
}

md_die() {
	printf '\033[1;31merror:\033[0m %s\n' "$*" >&2
	exit 1
}

# The developer scripts run inside the dev container (./scripts/pm <script>).
md_require_container() {
	if [[ ! -f /run/.containerenv && ! -f /.dockerenv && "${MD_ALLOW_HOST:-0}" != 1 ]]; then
		md_die "run this inside the dev container: ./scripts/pm $0 $*  (set MD_ALLOW_HOST=1 to override)"
	fi
}
