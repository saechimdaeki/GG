#!/usr/bin/env bash
# Prints Markdown patch notes for tag $1 and game folder $2: feat/fix commits since the
# previous tag that touched that folder. Prints nothing when the game has no such commits.
# Optional arguments select the previous tag pattern and the revision being released.
set -euo pipefail
tag=$1 game=$2
pattern=${3:-v*}
revision=${4:-$tag}
prev=$(git describe --tags --match "$pattern" --abbrev=0 "$revision^" 2>/dev/null || true)
range=${prev:+$prev..}$revision
if [[ -f "$game/project.godot" ]]; then
  name=$(sed -n 's/^config\/name="\(.*\)"/\1/p' "$game/project.godot")
else
  name=$(head -1 "$game/README.md" | sed 's/^# //; s/ (.*//')
fi

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
