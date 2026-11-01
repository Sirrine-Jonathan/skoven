#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APK_PATH="$SCRIPT_DIR/Builds/Skoven.apk"
PACKAGE_NAME="com.sirrine.skoven"
ACTIVITY_NAME="com.unity3d.player.UnityPlayerGameActivity"

echo "=== Checking ADB Devices ==="
adb devices -l

QUEST_SERIAL=$(adb devices -l | grep -E "Quest_3|eureka|2G0YC1ZFCN01JN" | awk '{print $1}' | head -n 1)

if [ -z "$QUEST_SERIAL" ]; then
    echo "⚠️ Quest not currently detected in adb devices."
    echo "Waiting up to 15 seconds for Quest to connect..."
    for i in {1..15}; do
        QUEST_SERIAL=$(adb devices -l | grep -E "Quest_3|eureka|2G0YC1ZFCN01JN" | awk '{print $1}' | head -n 1)
        if [ -n "$QUEST_SERIAL" ]; then
            echo "✅ Quest connected: $QUEST_SERIAL"
            break
        fi
        sleep 1
    done
fi

if [ -z "$QUEST_SERIAL" ]; then
    echo "❌ Quest not detected. Please connect your Quest 3 via USB or Wi-Fi with USB debugging enabled."
    exit 1
fi

if [ ! -f "$APK_PATH" ]; then
    echo "⚠️ APK not found at $APK_PATH. Building APK first via Unity CLI..."
    mkdir -p "$SCRIPT_DIR/Builds"
    cd "$SCRIPT_DIR/unity"
    unity run . -l "$SCRIPT_DIR/unity_apk_build.log" --no-tail -- -executeMethod SkovenBuild.BuildAPK -customBuildPath "$APK_PATH"
fi

if [ ! -f "$APK_PATH" ]; then
    echo "❌ Build did not produce $APK_PATH. Check $SCRIPT_DIR/unity_apk_build.log for details."
    exit 1
fi

echo "📦 Installing $APK_PATH to Meta Quest ($QUEST_SERIAL)..."
adb -s "$QUEST_SERIAL" install -r "$APK_PATH"

echo "🚀 Launching $PACKAGE_NAME on Quest..."
adb -s "$QUEST_SERIAL" shell am start -n "$PACKAGE_NAME/$ACTIVITY_NAME"

echo "✅ Skoven started! Put on your headset."
