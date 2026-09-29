#!/bin/bash
# Stops the regtest stack and deletes its volumes: chain, wallets, BTCPay and
# app databases. Nothing in them is worth keeping.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker compose -f "$here/compose.yml" down --volumes --remove-orphans
