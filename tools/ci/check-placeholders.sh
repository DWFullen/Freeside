#!/usr/bin/env bash
# Keeps PLACEHOLDER(<id>) markers in code and docs/plans/placeholders.md in step
# (docs/plans/phase-0.md, placeholder policy). Each table row in placeholders.md
# ends with an "In code" column:
#   yes  the id must appear as PLACEHOLDER(<id>) somewhere in the code
#   no   the id must not appear yet (set it to yes in the PR that adds it)
# Every PLACEHOLDER(<id>) in the code must have a row.
# Scanned: every file except .git, bin, obj, node_modules, docs/, *.md and tools/ci/.
#
# Usage: check-placeholders.sh [repo-root]   (default: current directory)
set -euo pipefail

root="${1:-.}"
registry="$root/docs/plans/placeholders.md"
if [[ ! -f "$registry" ]]; then
  echo "check-placeholders: missing $registry"
  exit 1
fi

failures=0
declare -A state=()
while IFS='|' read -r id value; do
  if [[ "$value" != yes && "$value" != no ]]; then
    echo "$registry: row '$id': 'In code' must be yes or no, found '$value'"
    failures=$((failures + 1))
  fi
  state["$id"]="$value"
done < <(awk -F'|' '/^\| `[a-z0-9-]+` \|/ {
    id = $2; gsub(/[ `]/, "", id)
    value = $(NF - 1); gsub(/ /, "", value)
    print id "|" value
  }' "$registry")

declare -A found=()
while IFS= read -r match; do
  location="${match%:*}"
  id="$(sed -E 's/.*PLACEHOLDER\(([a-z0-9-]+)\)$/\1/' <<<"$match")"
  case "${location#"$root"/}" in docs/* | tools/ci/*) continue ;; esac
  if [[ -z "${state[$id]+set}" ]]; then
    echo "$location: PLACEHOLDER($id) has no row in docs/plans/placeholders.md"
    failures=$((failures + 1))
  elif [[ "${state[$id]}" == no ]]; then
    echo "$location: PLACEHOLDER($id) is in code but its row says 'In code: no'"
    failures=$((failures + 1))
  fi
  found["$id"]=1
done < <(grep -rnoE --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=node_modules \
           --exclude='*.md' 'PLACEHOLDER\([a-z0-9-]+\)' "$root" || true)

for id in "${!state[@]}"; do
  if [[ "${state[$id]}" == yes && -z "${found[$id]+set}" ]]; then
    echo "$registry: row '$id' says 'In code: yes' but PLACEHOLDER($id) is not in the code"
    failures=$((failures + 1))
  fi
done

if [[ $failures -gt 0 ]]; then
  echo "check-placeholders: $failures problem(s)"
  exit 1
fi
echo "check-placeholders: ${#state[@]} registered id(s), ${#found[@]} in code, consistent"
