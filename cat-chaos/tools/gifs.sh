#!/bin/bash
# Capture reproducible gameplay loops from a built macOS player.
set -euo pipefail
cd "$(dirname "$0")/.."
app=Builds/Mac/CatChaos.app
exe=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist")
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p docs/media

capture() {
  local scene=$1 start=$2 duration=$3
  echo "캡처: $scene"
  "$app/Contents/MacOS/$exe" -screen-width 960 -screen-height 540 -screen-fullscreen 0 \
    -logFile "$work/$scene.log" --scene "$scene" --frames "$work/$scene" --at "$start" --seconds "$duration" >/dev/null
  ffmpeg -hide_banner -loglevel error -y -framerate 15 -i "$work/$scene/%04d.png" \
    -filter_complex 'scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle' \
    -loop 0 "docs/media/$scene.gif"
  grep '^\[frames\]' "$work/$scene.log"
}

if [[ $# -eq 0 ]]; then
  set -- intro play sprint chaos plant tv scratch kitchen bath robot robot-flip hide ending result favorite fever butterfly fishbowl trash tug cloth petcam box laundry wall
fi

for name in "$@"; do
  case "$name" in
      result) capture "$name" 6.3 3 ;;
      ending) capture "$name" 2 4.5 ;;
      hide) capture "$name" 0.9 7.3 ;;
      robot|robot-flip) capture "$name" 1.2 4.5 ;;
      favorite|fever|butterfly|fishbowl|trash) capture "$name" 0.8 4.8 ;;
      tug|cloth|petcam|laundry) capture "$name" 0.2 4 ;;
      scratch) capture "$name" 0.9 5.6 ;;
      *) capture "$name" 0.7 3.5 ;;
  esac
done
