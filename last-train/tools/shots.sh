#!/bin/bash
# Rebuild (setup + mac build) and capture evidence screenshots: tools/shots.sh [scene ...]
set -e
cd "$(dirname "$0")/.."
U="/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity"
L=${TMPDIR:-/tmp}/lasttrain; mkdir -p "$L"
if [ -z "$NOBUILD" ]; then
  "$U" -batchmode -quit -projectPath "$PWD" -executeMethod Setup.Run -logFile "$L/setup.log" || { grep "error CS" "$L/setup.log"; exit 1; }
  "$U" -batchmode -quit -projectPath "$PWD" -executeMethod Setup.BuildMac -logFile "$L/build.log" || { grep -E "error CS|\[build\]" "$L/build.log"; exit 1; }
fi
B="Builds/Mac/LastTrain.app/Contents/MacOS/막차까지"
D=$(date +%Y-%m-%d)
for s in "${@:-night}"; do
  "$B" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$L/play-$s.log" --scene "$s" --seed 7 $EXTRA $( [ "$s" != cards ] && echo "--nocards 1") --shot "$PWD/production/qa/evidence/$s${SUFFIX}-$D.png" --at ${AT:-3} >/dev/null 2>&1 &
  pid=$!; for i in $(seq 40); do kill -0 $pid 2>/dev/null || break; sleep 1; done; kill $pid 2>/dev/null || true
  grep -E "Exception|\[shot\]" "$L/play-$s.log" | head -5
done
