#!/bin/bash
# Waits until BTCPay's health endpoint reports that it is synchronized with
# NBXplorer and bitcoind. On timeout, prints the stack's state and fails.
#
#   REGTEST_BTCPAY_URL    default http://127.0.0.1:49392
#   REGTEST_WAIT_SECONDS  default 180
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
url="${REGTEST_BTCPAY_URL:-http://127.0.0.1:49392}"
timeout="${REGTEST_WAIT_SECONDS:-180}"
deadline=$((SECONDS + timeout))

until curl -fsS --max-time 5 "$url/api/v1/health" 2>/dev/null | grep -Eq '"synchronized": ?true'; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "BTCPay at $url is not synchronized after ${timeout}s." >&2
    docker compose -f "$here/compose.yml" ps >&2 || true
    docker compose -f "$here/compose.yml" logs --tail 40 btcpay nbxplorer >&2 || true
    exit 1
  fi
  sleep 2
done
echo "BTCPay is synchronized: $url"
