#!/bin/bash
# Setup script for the Freeside cloud environment at claude.ai/code.
#
# This file is the reviewed copy. The live copy is pasted into:
#   claude.ai/code -> environment selector -> settings icon -> Setup script
# Change both together.
#
# Environment settings that go with this script
# ---------------------------------------------
# Network access: Custom, with "Also include default list of common package
#   managers" checked, plus these hosts:
#     builds.dotnet.microsoft.com   dotnet-install.sh default feed #1
#     ci.dot.net                    dotnet-install.sh default feed #2
#     aka.ms                        version resolution for --channel
#   The Trusted list has dotnet.microsoft.com and dot.net without a wildcard,
#   so none of these are covered by default. Feed hosts are from
#   dotnet/install-scripts src/dotnet-install.sh, get_feeds_to_use().
# Environment variables (readable by anyone using the environment: no secrets):
#     DOTNET_ROOT=/usr/share/dotnet
#     DOTNET_CLI_TELEMETRY_OPTOUT=1
#     DOTNET_NOLOGO=1
# API credentials: none. Never mainnet credentials, xpubs/descriptors or Strike
#   production keys in this environment (AGENTS.md §2, invariants 4 and 8).
#   A Strike sandbox key, if one exists (D8), goes under API credentials, not
#   environment variables.
#
# Constraints (claude.ai/code docs, "Configure cloud environments")
# ------------------------------------------------------------------
# - Must exit 0, or the session will not start.
# - Must finish in about 5 minutes, or the environment is not cached.
# - The cache is a filesystem snapshot: installed files and pulled images
#   persist; running processes (Postgres, docker compose) do not.
# - GitHub Release downloads from repos not attached to the session get 403
#   from the GitHub proxy. Install tools from vendor hosts or apt repos.
#
# Phase 0 additions (not yet present):
# - `docker compose -f <regtest compose file> pull` so BTCPay regtest images
#   are cached. Start the stack per session from a SessionStart hook in
#   .claude/settings.json, gated on CLAUDE_CODE_REMOTE=true.
# - OpenTofu, from its apt repository (add the host to the network list).

set -euo pipefail

DOTNET_ROOT=/usr/share/dotnet
DOTNET_CHANNEL=10.0

if ! "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL%%.*}\."; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT"
fi

ln -sf "$DOTNET_ROOT/dotnet" /usr/local/bin/dotnet
dotnet --list-sdks
