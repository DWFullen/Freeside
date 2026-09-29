#!/bin/bash
# Starts the regtest stack, mines past initial block download and waits until
# BTCPay reports it is synchronized. Safe to run again on a running stack.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker compose -f "$here/compose.yml" up --detach --wait

# A new chain stays in initial block download until it has a recent block, and
# coinbase outputs need 100 confirmations before the wallet can spend them.
# So mine up to height 101, and always at least one block, which also ends
# initial block download on a stack restarted after a day or more.
height="$("$here/bitcoin-cli.sh" getblockcount)"
blocks=$((101 - height))
[ "$blocks" -ge 1 ] || blocks=1
"$here/bitcoin-cli.sh" -generate "$blocks" >/dev/null
echo "Mined $blocks block(s); height is now $("$here/bitcoin-cli.sh" getblockcount)"

"$here/wait.sh"
