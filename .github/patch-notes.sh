#!/usr/bin/env bash
# Prints Markdown patch notes for tag $1: feat/fix commit subjects since the previous tag.
# Used by release.yml for both the release body and the patch-notes issue.
set -euo pipefail
tag=$1
prev=$(git describe --tags --abbrev=0 "$tag^" 2>/dev/null || true)
range=${prev:+$prev..}$tag

section() {  # $1 = heading, $2 = conventional-commit type
  local lines
  lines=$(git log "$range" --pretty='%s' | sed -nE "s/^$2(\([^)]*\))?!?: (.*)/- \2/p")
  [ -n "$lines" ] && printf '## %s\n\n%s\n\n' "$1" "$lines"
  return 0
}

section "✨ 새로 생긴 것" feat
section "🔧 고친 것" fix
echo "## 📦 다운로드"
echo
echo "https://github.com/${GITHUB_REPOSITORY:-saechimdaeki/GG}/releases/tag/$tag"
echo
echo "- Mac: \`<게임>-mac.zip\` 풀고 **우클릭 → 열기**"
echo "- Windows: \`<게임>-windows.exe\` (SmartScreen → 추가 정보 → 실행)"
echo
echo "_${prev:-처음}부터 $tag 까지의 변경_"
