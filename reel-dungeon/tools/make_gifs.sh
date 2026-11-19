#!/bin/bash
# Records README GIFs from the built macOS app: docs/media/<name>.gif
#   bash tools/make_gifs.sh            (after BuildScript.BuildMac)
# Each clip: the game runs with captureFramerate 12 and writes N frames, ffmpeg turns them into an 800px GIF.
set -euo pipefail
cd "$(dirname "$0")/.."
APP="Builds/Mac/ReelDungeon.app/Contents/MacOS/Reel Dungeon"
OUT="$PWD/docs/media"
TMP="$(mktemp -d)"
mkdir -p "$OUT"

clip() { # name start-seconds frames args...
  local name=$1 at=$2 frames=$3; shift 3
  "$APP" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
    --screenshot "$TMP/$name.png" --at "$at" --capture-frames "$frames" "$@" >/dev/null 2>&1
  ffmpeg -loglevel error -y -framerate 12 -i "$TMP/$name.%03d.png" \
    -vf "scale=800:-1:flags=neighbor,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=none" \
    -loop 0 "$OUT/$name.gif"
  echo "$OUT/$name.gif ($frames frames)"
}

clip title     0.5 36  --title
clip combat    0.8 96  --chapter 1 --node Combat --nostory --notips --charge --autocommit
clip skills    0.8 96  --chapter 2 --node Combat --nostory --notips --recruit gambler,skeleton --unlockall --charge --autocommit
clip enemy-skill 0.8 84 --chapter 2 --enemy "진흙 괴물" --node Combat --nostory --notips --recruit wizard --autocommit
clip boss-phase2 0.8 108 --chapter 1 --node Boss --nostory --notips --recruit wizard,dwarf,angel --autoturns 3 --autocommit
clip event-result 0.2 30 --chapter 1 --event coin_ghost --node Event --pick 1 --nostory --notips
clip story     0.4 48  --chapter 1 --node Combat --notips
rm -rf "$TMP"
