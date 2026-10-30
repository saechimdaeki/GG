#!/usr/bin/env bash
# Prints Markdown patch notes for tag $1 and game folder $2: feat/fix commits since the
# previous tag that touched that folder. Prints nothing when the game has no such commits.
# Used by release.yml for the release body and the per-game patch-notes issue.
set -euo pipefail
tag=$1 game=$2
prev=$(git describe --tags --abbrev=0 "$tag^" 2>/dev/null || true)
range=${prev:+$prev..}$tag
name=$(sed -n 's/^config\/name="\(.*\)"/\1/p' "$game/project.godot")

section() {  # $1 = heading, $2 = conventional-commit type
  local lines
  # drop the type prefix, and the game name the subject often starts with
  lines=$(git log "$range" --pretty='%s' -- "$game/" | sed -nE "s/^$2(\([^)]*\))?!?: (${name} )?(.*)/- \3/p")
  [ -n "$lines" ] && printf '### %s\n\n%s\n\n' "$1" "$lines"
  return 0
}

body=$(section "✨ 새로 생긴 것" feat; section "🔧 고친 것" fix)
[ -z "$body" ] && exit 0
echo "$body"
echo
echo "📦 다운로드: https://github.com/${GITHUB_REPOSITORY:-saechimdaeki/GG}/releases/tag/$tag"
echo
echo "_${prev:-처음}부터 $tag 까지의 변경_"
