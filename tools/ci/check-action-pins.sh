#!/usr/bin/env bash
# Fails if a GitHub Actions `uses:` reference is not pinned to a full commit SHA
# (AGENTS.md §7.4). Allowed forms:
#   owner/repo[/path]@<40-hex commit SHA> # vX.Y.Z   (the comment names the version)
#   ./path/to/local/action
#   docker://image@sha256:<64-hex digest>
#
# Usage: check-action-pins.sh            scan .github/workflows and .github/actions
#        check-action-pins.sh FILE...    scan the given files (used by the tests)
set -euo pipefail

files=()
if [[ $# -gt 0 ]]; then
  files=("$@")
else
  while IFS= read -r -d '' f; do files+=("$f"); done < <(
    find .github/workflows .github/actions -type f \( -name '*.yml' -o -name '*.yaml' \) -print0 2>/dev/null | sort -z)
fi

failures=0
report() { echo "$1:$2: $3"; failures=$((failures + 1)); }

for file in "${files[@]}"; do
  mapfile -t lines < "$file"
  for i in "${!lines[@]}"; do
    line="${lines[$i]}"
    lineno=$((i + 1))
    [[ "$line" =~ ^[[:space:]]*(-[[:space:]]+)?uses:[[:space:]]*(.*)$ ]] || continue
    rest="${BASH_REMATCH[2]}"
    comment=""
    [[ "$rest" == *"#"* ]] && comment="${rest#*#}"
    ref="$(sed -E "s/[[:space:]]*#.*$//; s/^[\"']//; s/[\"'][[:space:]]*$//; s/[[:space:]]+$//" <<<"$rest")"

    case "$ref" in
      ./*) ;;
      docker://*)
        [[ "$ref" =~ @sha256:[0-9a-f]{64}$ ]] || report "$file" "$lineno" "docker image not pinned by digest: $ref" ;;
      *)
        if [[ ! "$ref" =~ ^[A-Za-z0-9._-]+/[A-Za-z0-9._/-]+@[0-9a-f]{40}$ ]]; then
          report "$file" "$lineno" "not pinned to a full commit SHA: $ref"
        elif [[ ! "$comment" =~ ^[[:space:]]*v[0-9] ]]; then
          report "$file" "$lineno" "SHA pin needs a '# vX.Y.Z' comment naming the version: $ref"
        fi ;;
    esac
  done
done

if [[ $failures -gt 0 ]]; then
  echo "check-action-pins: $failures unpinned reference(s)"
  exit 1
fi
echo "check-action-pins: ${#files[@]} file(s), all references pinned"
