#!/bin/bash
# Rebuild (setup + mac build) and capture evidence screenshots: tools/shots.sh [scene ...]
# Scenes: intro play chaos kitchen bath robot tv fishbowl butterfly fever wall tug cloth petcam box laundry ending result.  NOBUILD=1 skips the rebuild.
set -e
cd "$(dirname "$0")/.."
U="/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity"
L=${TMPDIR:-/tmp}/catchaos; mkdir -p "$L"
if [ -z "$NOBUILD" ]; then
  "$U" -batchmode -quit -projectPath "$PWD" -executeMethod Setup.Run -logFile "$L/setup.log" || { grep -E "error CS|Exception" "$L/setup.log"; exit 1; }
  "$U" -batchmode -quit -projectPath "$PWD" -executeMethod Setup.BuildMac -logFile "$L/build.log" || { grep -E "error CS|\[build\]" "$L/build.log"; exit 1; }
fi
B="Builds/Mac/CatChaos.app/Contents/MacOS/냥난리"
D=$(date +%Y-%m-%d)
mkdir -p production/qa/evidence
for s in "${@:-play}"; do
  "$B" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$L/play-$s.log" --scene "$s" $EXTRA --shot "$PWD/production/qa/evidence/$s${SUFFIX}-$D.png" --at ${AT:-3} >/dev/null 2>&1 &
  pid=$!; for i in $(seq 40); do kill -0 $pid 2>/dev/null || break; sleep 1; done; kill $pid 2>/dev/null || true
  grep -E "Exception|\[shot\]|\[cat\]|\[house\]" "$L/play-$s.log" | head -8
done
