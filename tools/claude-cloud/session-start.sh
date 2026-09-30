#!/bin/bash
# SessionStart hook (.claude/settings.json). In a claude.ai cloud session it
# starts dockerd, which the container doesn't start on its own, so the regtest
# stack (tools/regtest) can run. Does nothing anywhere else.
#
# Never fails: a hook error must not stop the session from starting. What it
# prints is added to the session's context, so it prints one line.
set -uo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

if docker info >/dev/null 2>&1; then
  echo "dockerd: already running"
  exit 0
fi
if ! command -v dockerd >/dev/null 2>&1 || [ "$(id -u)" -ne 0 ]; then
  echo "dockerd: not started (not installed, or not root); the regtest stack can't run in this session"
  exit 0
fi

# The disk outlives the container, so a session killed mid-run leaves dockerd's
# and containerd's PID files behind. If a stale file now names an unrelated
# process, dockerd waits for a containerd that isn't there and gives up. Neither
# daemon is running here, so any such file is stale.
if ! pgrep -x dockerd >/dev/null 2>&1 && ! pgrep -x containerd >/dev/null 2>&1; then
  rm -f /var/run/docker.pid /var/run/docker/containerd/containerd.pid
fi

# Detached, and with no handle on the hook's stdout, so the hook can return
# while dockerd keeps running.
log="${TMPDIR:-/tmp}/dockerd.log"
setsid dockerd >"$log" 2>&1 </dev/null &

for _ in $(seq 1 30); do
  if docker info >/dev/null 2>&1; then
    echo "dockerd: started (log: $log)"
    exit 0
  fi
  sleep 1
done
echo "dockerd: not ready after 30s; see $log"
exit 0
