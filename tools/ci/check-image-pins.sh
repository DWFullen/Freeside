#!/usr/bin/env bash
# Fails if a docker compose `image:` is not pinned by tag and digest
# (AGENTS.md §7.4). Required form, which Dependabot keeps up to date:
#   image: name:tag@sha256:<64-hex digest>
#
# Usage: check-image-pins.sh            scan compose files in the repo
#        check-image-pins.sh FILE...    scan the given files (used by the tests)
set -euo pipefail

files=()
if [[ $# -gt 0 ]]; then
  files=("$@")
else
  while IFS= read -r -d '' f; do files+=("$f"); done < <(
    find . -path ./.git -prune -o -path ./tools/ci/tests -prune -o -type f \
      \( -name 'compose*.yml' -o -name 'compose*.yaml' -o -name 'docker-compose*.yml' -o -name 'docker-compose*.yaml' \) \
      -print0 | sort -z)
fi

failures=0
report() { echo "$1:$2: $3"; failures=$((failures + 1)); }

for file in "${files[@]}"; do
  mapfile -t lines < "$file"
  for i in "${!lines[@]}"; do
    line="${lines[$i]}"
    [[ "$line" =~ ^[[:space:]]*image:[[:space:]]*(.*)$ ]] || continue
    ref="$(sed -E "s/[[:space:]]*#.*$//; s/^[\"']//; s/[\"'][[:space:]]*$//; s/[[:space:]]+$//" <<<"${BASH_REMATCH[1]}")"
    [[ "$ref" =~ ^[a-z0-9._/-]+:[A-Za-z0-9._-]+@sha256:[0-9a-f]{64}$ ]] \
      || report "$file" "$((i + 1))" "image not pinned as name:tag@sha256:<digest>: $ref"
  done
done

if [[ $failures -gt 0 ]]; then
  echo "check-image-pins: $failures unpinned image(s)"
  exit 1
fi
echo "check-image-pins: ${#files[@]} file(s), all images pinned"
