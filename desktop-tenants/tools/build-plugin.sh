#!/bin/bash
# Compile Native/DesktopBridge.m into a universal macOS plugin bundle for Unity.
set -euo pipefail
cd "$(dirname "$0")/.."
out=Assets/Plugins/macOS/DesktopBridge.bundle
mkdir -p "$out/Contents/MacOS"
clang -bundle -fobjc-arc -O2 -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 \
  -framework Cocoa -framework QuartzCore -o "$out/Contents/MacOS/DesktopBridge" Native/DesktopBridge.m
cat > "$out/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>DesktopBridge</string>
  <key>CFBundleIdentifier</key><string>com.GG.DesktopBridge</string>
  <key>CFBundlePackageType</key><string>BNDL</string>
</dict></plist>
PLIST
echo "built $out"
