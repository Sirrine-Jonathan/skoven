#!/usr/bin/env bash
# Skoven — Build development APK and deploy to connected Quest 3
#
# Usage:
#   ./scripts/build-and-deploy.sh          # build + deploy
#   ./scripts/build-and-deploy.sh --build  # build only, no deploy
#   ./scripts/build-and-deploy.sh --deploy # deploy last build, no rebuild
#
# Requirements:
#   - Unity 6000.6.3f1 installed via Unity Hub
#   - Quest 3 connected via USB with USB Debugging enabled
#   - adb available on PATH (bundled with Unity Android Player)

set -euo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)/unity"
BUILD_DIR="$(cd "$(dirname "$0")/.." && pwd)/builds"
APK_PATH="$BUILD_DIR/Skoven-dev.apk"
PACKAGE="com.sirrine.skoven"
DEVICE="2G0YC1ZFCN01JN"

# ADB bundled with Unity (fallback to system adb)
ADB_UNITY="/Applications/Unity/Hub/Editor/6000.6.3f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
ADB=$(command -v adb 2>/dev/null || echo "$ADB_UNITY")

BUILD=true
DEPLOY=true

for arg in "$@"; do
  case "$arg" in
    --build)  DEPLOY=false ;;
    --deploy) BUILD=false ;;
  esac
done

mkdir -p "$BUILD_DIR"

# ── Build ──────────────────────────────────────────────────────────────────
if [ "$BUILD" = true ]; then
  echo ""
  echo "▶ Building Skoven development APK..."
  echo "  Project: $PROJECT_PATH"
  echo "  Output:  $APK_PATH"
  echo ""

  "$UNITY" \
    -quit \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_PATH" \
    -buildTarget Android \
    -executeMethod SkovenBuild.BuildDevelopmentApk \
    -customBuildPath "$APK_PATH" \
    -logFile "$BUILD_DIR/build.log" \
    "$@"

  BUILD_EXIT=$?

  if [ $BUILD_EXIT -ne 0 ]; then
    echo ""
    echo "✗ Build failed (exit $BUILD_EXIT). Last 40 lines of build log:"
    echo "──────────────────────────────────────────────"
    tail -40 "$BUILD_DIR/build.log"
    echo "──────────────────────────────────────────────"
    echo "  Full log: $BUILD_DIR/build.log"
    exit $BUILD_EXIT
  fi

  echo "✓ Build succeeded: $APK_PATH"
  ls -lh "$APK_PATH"
fi

# ── Deploy ─────────────────────────────────────────────────────────────────
if [ "$DEPLOY" = true ]; then
  echo ""
  echo "▶ Deploying to Quest 3 ($DEVICE)..."

  # Confirm device is still connected
  if ! "$ADB" -s "$DEVICE" get-state 2>/dev/null | grep -q "device"; then
    echo "✗ Device $DEVICE not found. Check USB connection and try again."
    exit 1
  fi

  # Install (replace existing)
  "$ADB" -s "$DEVICE" install -r "$APK_PATH"

  echo "✓ Installed $PACKAGE on device $DEVICE"
  echo ""
  echo "▶ Launching app..."
  "$ADB" -s "$DEVICE" shell monkey \
    -p "$PACKAGE" \
    -c android.intent.category.LAUNCHER 1

  echo ""
  echo "✓ Done. Put on the headset — Skoven should be launching."
  echo ""
  echo "  To stream device logs:"
  echo "  adb -s $DEVICE logcat -s Unity:V"
fi
