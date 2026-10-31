# Flowstate — Developer Setup

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
cd flowstate/companion
cp .env.example .env
```

Edit `.env`:
- Set `OPENAI_API_KEY` (or your preferred provider key — see comments)
- Set `FLOWSTATE_DEFAULT_PROJECT` to the absolute path of a git repo to work on
- Leave `FLOWSTATE_PORT=8765` unless that port is in use

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
║        Flowstate Companion App            ║
║  Voice-driven AI coding — Meta Quest      ║
╚═══════════════════════════════════════════╝

[Flowstate] LLM provider: OpenAI / model: gpt-4o
[Flowstate] Companion app listening on ws://localhost:8765
[Flowstate] Connect your Quest to the same Wi-Fi network and use this machine's IP address.
```

### 4. Find your machine's local IP

```bash
# macOS
ipconfig getifaddr en0

# Windows
ipconfig | findstr "IPv4"
```

Note this IP — you'll enter it in the Quest app's setup wizard (e.g., `192.168.1.42`).

### 5. Build for production

```bash
npm run build
npm start
```

---

## LLM provider configuration

Edit `FLOWSTATE_LLM_PROVIDER` and the corresponding key in `.env`:

| Provider | Key variable | Example model |
|---|---|---|
| OpenAI | `OPENAI_API_KEY` | `gpt-4o` |
| Anthropic | `ANTHROPIC_API_KEY` | `claude-3-5-sonnet-20241022` |
| OpenRouter | `OPENROUTER_API_KEY` | `anthropic/claude-3.5-sonnet` |
| Ollama (local) | _(none)_ | `llama3.1:8b` |
| Custom endpoint | `FLOWSTATE_CUSTOM_LLM_API_KEY` | your model name |

OpenRouter gives access to 100+ models with a single API key — recommended if you want to switch models without reconfiguring. Get a key at https://openrouter.ai/keys.

To use Ollama locally (free, offline):
```bash
brew install ollama        # macOS
ollama pull llama3.1:8b   # or codellama:13b, deepseek-coder-v2, etc.
ollama serve
```
Then set `FLOWSTATE_LLM_PROVIDER=ollama` in `.env`.

---

## Testing the companion app without a Quest

Use the `passthrough` STT mode to send pre-transcribed text via a WebSocket client:

```bash
# In .env:
FLOWSTATE_STT_BACKEND=passthrough

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

1. Open Unity Hub → Add → select `flowstate/unity/`
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

Follow `Assets/Flowstate/Scenes/SCENE-SETUP.md` → Font import section.

### 5. Create and configure the main scene

Follow `Assets/Flowstate/Scenes/SCENE-SETUP.md` for the full hierarchy and MessageRouter wiring.

### 6. Build and install on Quest

```bash
# Enable Developer Mode on Quest 3:
# Settings → About headset → tap "Build Number" 7 times

# Connect Quest via USB-C
adb devices   # confirm device appears

# Build in Unity:
# File → Build Settings → Android → Build And Run
# OR build to APK then:
adb install -r path/to/Flowstate.apk
```

---

## Troubleshooting

**Companion app can't connect / Quest can't reach the WebSocket:**
- Confirm both devices are on the same Wi-Fi network (Quest guest networks are often isolated)
- Check macOS firewall: System Settings → Network → Firewall → allow incoming on port 8765
- Try `nc -zv <your-ip> 8765` from the Quest shell via ADB: `adb shell nc -zv 192.168.x.x 8765`

**"No microphone devices found" on Quest:**
- Ensure the Android Manifest includes `RECORD_AUDIO` permission (Meta XR SDK adds this automatically)
- Confirm mic permission is granted: Quest Settings → Apps → Flowstate → Permissions

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
