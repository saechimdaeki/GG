#!/bin/bash
# Package an existing Unity macOS build with an ad-hoc signature.
set -euo pipefail
cd "$(dirname "$0")/.."

source_app=Builds/Mac/CatChaos.app
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
app="$scratch/CatChaos.app"
ditto "$source_app" "$app"

plist="$app/Contents/Info.plist"
exe=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$plist")
# Use an ASCII Mach-O filename so codesign resolves the sealed executable path.
if [[ "$exe" != CatChaos ]]; then
  mv "$app/Contents/MacOS/$exe" "$app/Contents/MacOS/CatChaos"
  /usr/libexec/PlistBuddy -c 'Set :CFBundleExecutable CatChaos' "$plist"
fi

# Sign nested code before signing the app.
find "$app/Contents/Frameworks" "$app/Contents/PlugIns" -type f \( -name '*.dylib' -o -name '*.bundle' \) -print0 |
  while IFS= read -r -d '' component; do
    codesign --force --sign - "$component"
  done
codesign --force --sign - "$app"
codesign --verify --deep --strict "$app"

zip="$scratch/CatChaos-mac.zip"
ditto -c -k --keepParent "$app" "$zip"
unzip -tq "$zip"
mkdir -p Builds/Release
mv "$zip" Builds/Release/CatChaos-mac.zip
openssl dgst -sha256 Builds/Release/CatChaos-mac.zip
