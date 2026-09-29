#!/usr/bin/env bash
# Runs the repo-check scripts against fixtures and asserts their exit codes.
# Usage: tools/ci/tests/run.sh
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ci="$(dirname "$here")"
fixtures="$here/fixtures"
failures=0

# expect <exit code> <script> <argument>
expect() {
  local want="$1" script="$2" arg="$3" got=0
  "$ci/$script" "$arg" >/dev/null 2>&1 || got=$?
  if [[ "$got" -eq "$want" ]]; then
    echo "ok    $script ${arg#"$fixtures"/} (exit $got)"
  else
    echo "FAIL  $script ${arg#"$fixtures"/}: expected exit $want, got $got"
    failures=$((failures + 1))
  fi
}

expect 0 check-action-pins.sh "$fixtures/pins/pass.workflow"
expect 1 check-action-pins.sh "$fixtures/pins/tag.workflow"
expect 1 check-action-pins.sh "$fixtures/pins/branch.workflow"
expect 1 check-action-pins.sh "$fixtures/pins/short-sha.workflow"
expect 1 check-action-pins.sh "$fixtures/pins/no-comment.workflow"
expect 1 check-action-pins.sh "$fixtures/pins/docker-tag.workflow"

expect 0 check-placeholders.sh "$fixtures/placeholders/pass"
expect 1 check-placeholders.sh "$fixtures/placeholders/unlisted"
expect 1 check-placeholders.sh "$fixtures/placeholders/stale-yes"
expect 1 check-placeholders.sh "$fixtures/placeholders/premature"
expect 1 check-placeholders.sh "$fixtures/placeholders/bad-value"

if [[ "$failures" -gt 0 ]]; then
  echo "$failures fixture case(s) failed"
  exit 1
fi
echo "all fixture cases passed"
