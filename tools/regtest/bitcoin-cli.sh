#!/bin/bash
# Runs bitcoin-cli inside the regtest bitcoind container, against the regtest
# chain and its "default" wallet. Examples:
#   tools/regtest/bitcoin-cli.sh getblockchaininfo
#   tools/regtest/bitcoin-cli.sh -generate 1
#   tools/regtest/bitcoin-cli.sh sendtoaddress bcrt1q... 0.001
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

exec docker compose -f "$here/compose.yml" exec -T bitcoind bitcoin-cli -datadir=/data "$@"
