# Skoven — Developer Setup

How to get the companion app running and the Unity project open. Follow this before building or testing anything.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Node.js | ≥ 18 | `node --version` to check |
| npm | ≥ 9 | Comes with Node |
| Unity Hub | Latest | Download from unity.com/download |
| Unity Editor | 6000.0.23f1 (Unity 6 LTS) | Install via Unity Hub |
| Meta XR SDK | 71.0.0 | Installed via Unity Package Manager |
| Android Build Support | Matching Unity version | Install via Unity Hub → Add modules |
| Meta Quest 3 | Developer mode enabled | Settings → About → tap Build Number 7x |
| ADB | Latest platform-tools | For sideloading builds |

---

## Companion app (macOS / Windows)

The companion app runs on your development machine and bridges the Quest to the LLM and your git repositories.

### 1. Configure environment

```bash
cd skoven/companion
cp .env.example .env
```

Edit `.env`:
- Set `OPENAI_API_KEY` (or your preferred provider key — see comments)
- Set `SKOVEN_DEFAULT_PROJECT` to the absolute path of a git repo to work on
- Leave `SKOVEN_PORT=8765` unless that port is in use

### 2. Install dependencies

```bash
npm install
```

### 3. Run in development mode

```bash
npm run dev
```

You should see:
```
╔═══════════════════════════════════════════╗
║        Skoven Companion App            ║
║  Voice-driven AI agent workspace      ║
╚═══════════════════════════════════════════╝

[Skoven] LLM provider: OpenAI / model: gpt-4o
[Skoven] Companion app listening on ws://localhost:8765
[Skoven] Connect your Quest to the same Wi-Fi network and use this machine's IP address.
```

### 4. Connect Quest to Companion

There are two supported connection paths:

#### Option A: USB Tethering (Recommended for Development)
Zero network latency, no Wi-Fi guest isolation issues, and no firewall configuration required:
```bash
adb -s 2G0YC1ZFCN01JN reverse tcp:8765 tcp:8765
```
The Quest app automatically connects to `ws://127.0.0.1:8765` over the USB cable.

#### Option B: Wi-Fi (Untethered)
```bash
# Find your host machine's local IP:
# macOS
ipconfig getifaddr en0

# Windows
ipconfig | findstr "IPv4"
```
The Quest client automatically falls back to your host's local IP (e.g. `192.168.1.119:8765`) if `127.0.0.1` is unavailable.

### 5. Build for production

```bash
npm run build
npm start
```

---

## In-VR Controls & Interaction

Skoven implements 6DOF controller tracking and laser pointer interaction aligned with the studio's `meta-quest-controller-input` skill:

| Action | Control | Visual / Audio Feedback |
|---|---|---|
| **Speak to Orchestrator** | Hold **Right Controller Grip** | On-screen Talk button turns red with pulsing recording dot (`🔴 Listening...`); audio streams in real-time to Whisper |
| **Talk Button (Click)** | Point laser ray at **[🎙️ Push to Talk]** & pull **Trigger** | Toggles voice capture with click chime and haptic pulse |
| **Reposition Panels** | Point laser ray at any panel & hold **Left Controller Grip** | Grabs panel; move & rotate freely in 3D AR space; release to place (automatically saves layout to PlayerPrefs) |
| **Click Buttons / Approvals** | Point laser ray at button & pull **Trigger** (or tap **A / X**) | Button scales up `1.035×` on hover (emerald beam); button presses with tactile haptic impulse (`0.85f`) and procedural click chime |
| **Laser Beams** | Point with either hand | Left Pointer: Teal/Cyan (`#4ED4E0`); Right Pointer: Blue (`#4E80FF`); Hover: Emerald (`#4EC94E`) |

### Connection Status Indicator

The header on the central **Skoven Orchestrator** panel displays live connectivity:
- `● Connected to Host` (Green): WebSocket connected to companion app; voice requests will process.
- `○ Offline (Companion port 8765)` (Red): Companion server is not running or unreachable. If you attempt to speak while offline, an in-VR banner alerts you to run `npm start`.

---

## LLM provider configuration

Edit `SKOVEN_LLM_PROVIDER` and the corresponding key in `.env`:

| Provider | Key variable | Example model |
|---|---|---|
| OpenAI | `OPENAI_API_KEY` | `gpt-4o` |
| Anthropic | `ANTHROPIC_API_KEY` | `claude-3-5-sonnet-20241022` |
| OpenRouter | `OPENROUTER_API_KEY` | `anthropic/claude-3.5-sonnet` |
| Ollama (local) | _(none)_ | `llama3.1:8b` |
| Custom endpoint | `SKOVEN_CUSTOM_LLM_API_KEY` | your model name |

OpenRouter gives access to 100+ models with a single API key — recommended if you want to switch models without reconfiguring. Get a key at https://openrouter.ai/keys.

To use Ollama locally (free, offline):
```bash
brew install ollama        # macOS
ollama pull llama3.1:8b   # or codellama:13b, deepseek-coder-v2, etc.
ollama serve
```
Then set `SKOVEN_LLM_PROVIDER=ollama` in `.env`.

---

## Testing the companion app without a Quest

Use the `passthrough` STT mode to send pre-transcribed text via a WebSocket client:

```bash
# In .env:
SKOVEN_STT_BACKEND=passthrough

# In a separate terminal, connect with wscat:
npx wscat -c ws://localhost:8765

# Send a handshake:
{"type":"handshake","deviceId":"test-device"}

# Then send a transcript:
{"type":"transcript","text":"add a hello world function to utils.ts"}
```

---

## Unity project (Quest app)

### 1. Open the project

1. Open Unity Hub → Add → select `skoven/unity/`
2. Unity Hub will prompt to install Unity 6000.0.23f1 if not present — do so with Android Build Support + OpenXR
3. Open the project — first import will take several minutes

### 2. Install packages

Unity will auto-install packages from `Packages/manifest.json`. If Meta XR SDK fails to resolve:
1. Window → Package Manager → `+` → Add package by name
2. Add `com.meta.xr.sdk.all` version `71.0.0` from the Meta XR scoped registry

### 3. Configure XR

1. Edit → Project Settings → XR Plug-in Management → Android tab
2. Enable **OpenXR**
3. Click the warning triangle → Fix All
4. OpenXR → Add interaction profiles:
   - Meta Quest Touch Pro Controller Profile
   - Meta Quest Touch Controller Profile

### 4. Import fonts

Follow `Assets/Skoven/Scenes/SCENE-SETUP.md` → Font import section.

### 5. Create and configure the main scene

Follow `Assets/Skoven/Scenes/SCENE-SETUP.md` for the full hierarchy and MessageRouter wiring.

### 6. Build and install on Quest

```bash
# Quest 3 device serial: 2G0YC1ZFCN01JN (connected via USB Debugging)
# Confirm connection:
adb devices -l

# Build and deploy in one step:
./scripts/build-and-deploy.sh

# Build only (no deploy):
./scripts/build-and-deploy.sh --build

# Deploy last build without rebuilding:
./scripts/build-and-deploy.sh --deploy

# Stream Unity logs from device:
adb -s 2G0YC1ZFCN01JN logcat -s Unity:V
```

The script calls Unity in batchmode, writes the APK to `builds/Skoven-dev.apk`, installs it with `adb install -r`, and launches the app automatically.

---

## Troubleshooting

**Companion app can't connect / Quest can't reach the WebSocket:**
- Confirm both devices are on the same Wi-Fi network (Quest guest networks are often isolated)
- Check macOS firewall: System Settings → Network → Firewall → allow incoming on port 8765
- Try `nc -zv <your-ip> 8765` from the Quest shell via ADB: `adb shell nc -zv 192.168.x.x 8765`

**"No microphone devices found" on Quest:**
- Ensure the Android Manifest includes `RECORD_AUDIO` permission (Meta XR SDK adds this automatically)
- Confirm mic permission is granted: Quest Settings → Apps → Skoven → Permissions

**STT returns empty string:**
- Check `OPENAI_API_KEY` is valid and has Whisper API access
- Try `passthrough` mode to isolate whether the issue is audio capture or transcription

**LLM returns malformed JSON:**
- Some models (especially smaller Ollama models) ignore `json_object` format instructions
- The intent parser will retry without `response_format` and strip markdown fences
- Switch to a larger model if intent parsing is consistently wrong

**Build fails with IL2CPP error:**
- Ensure Android NDK is installed in Unity Hub (matching the Unity version)
- Player Settings → Other → Scripting Backend: IL2CPP, Target Architecture: ARM64 only
