# Skoven — Asset and License Register

_Last updated: 2026-11-02_
_Status: Production assets imported and active_

All assets used in a Store release must have an entry here with confirmed license status. Do not import or ship an asset without recording its provenance.

Skoven is a productivity tool with minimal art requirements. The asset surface is intentionally focused: high-legibility monospace and UI typography, dark ambient AR/VR spatial backgrounds, and Meta Horizon Store listing assets.

---

## Font assets (Active in project)

| Font | Source | Version / Hash | License | Usage in Skoven | Status |
|---|---|---|---|---|---|
| [JetBrains Mono](https://www.jetbrains.com/lp/mono/) | JetBrains | v2.304 | OFL 1.1 | Code diff viewer (`DiffPanel`), interactive terminal (`TerminalPanel`), and live agent activity streams (`AgentLogPanel`). Generated as dynamic SDF TMP font asset. | **Active** (`Assets/Skoven/Fonts/JetBrainsMono-*`) |
| [Inter](https://rsms.me/inter/) | Rasmus Andersson | v4.1 | OFL 1.1 | UI chrome, titles, panel headers, orchestrator chat stream (`ChatPanel`), and task board items (`TaskBoardPanel`). Generated as dynamic SDF TMP font asset. | **Active** (`Assets/Skoven/Fonts/Inter-*`) |

OFL 1.1 permits commercial and embedded application bundling. No per-seat royalties or fees required.

---

## Store Listing & Brand Assets (Active in project)

Authored specifically for Skoven via automated pipeline [`tools/generate_brand_art.py`](../tools/generate_brand_art.py). All assets strictly adhere to Meta Horizon Store Developer Dashboard specifications and VRC asset rules.

| Asset | Dimensions | Format | Parallax / Safe Area Spec | Location |
|---|---|---|---|---|
| **Spatial Tile 512 Background** | `512 × 512 px` | Solid `RGB` PNG | Opaque background layer (no alpha) | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Spatial Tile 512 Foreground** | `512 × 512 px` | `RGBA` PNG | Transparent background; non-transparent pixels strictly inside centered `392 × 392 px` (`60 px` margin) | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Spatial Tile 180 Background** | `180 × 180 px` | Solid `RGB` PNG | Opaque background layer | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Spatial Tile 180 Foreground** | `180 × 180 px` | `RGBA` PNG | Transparent background; non-transparent pixels strictly inside centered `138 × 138 px` (`21 px` margin) | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Landscape Cover** | `2560 × 1440 px` | `RGB` PNG | 16:9 Landscape Store Cover | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Hero Cover** | `3000 × 900 px` | `RGB` PNG | Panoramic banner | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Square Cover** | `1440 × 1440 px` | `RGB` PNG | 1:1 Square Cover | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Portrait Cover** | `1008 × 1440 px` | `RGB` PNG | 7:10 Portrait Cover | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **Mini Landscape** | `1080 × 360 px` | `RGB` PNG | 3:1 Small Banner | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |
| **App Icon** | `512 × 512 px` | `RGB` PNG | Standard 2D App Icon for Android/Quest launcher | `store_listing/assets/`, `Assets/Skoven/Art/Store/` |

---

## Environment asset candidates

Skoven needs 3 minimal static environments (Void, Studio, Exterior). These are background rooms, not game-quality scenes.

| Source | Type | License | Notes |
|---|---|---|---|
| [Kenney.nl — Furniture Kit](https://kenney.nl/assets/furniture-kit) | 3D props | CC0 | Studio environment furniture; no attribution required |
| [Quaternius — Ultimate Nature Pack](https://quaternius.com) | 3D nature | CC0 | Exterior environment elements |
| Custom / procedural | Skybox, lighting | Owned | Void and base environments procedurally generated; no external assets needed |

The Void environment requires no external assets — black skybox, minimal floor indicator, Unity default lighting. This should be the prototype environment.

---

## Audio candidates

| Source | Type | License | Notes |
|---|---|---|---|
| [Freesound.org](https://freesound.org) | UI SFX (confirm, beep, error) | CC0 (filter required) | Verify each clip is CC0 before import |
| [Sonniss GDC Audio Bundle 2024](https://sonniss.com/gameaudiogdc) | General SFX | Royalty-free commercial | Verify current year's terms cover Quest distribution |
| Procedurally generated | Subtle ambient tones | Owned | Preferred for ambient audio; no licensing overhead |

UI sound design should be minimal — confirmation tones, soft error sounds, ambient hum. Avoid anything that becomes fatiguing during 60-minute work sessions.

---

## Third-party code and packages

| Package | Source | License | Notes |
|---|---|---|---|
| Meta XR SDK (All-in-One) | Meta Platforms | [Meta Platform Technologies SDK License](https://developer.oculus.com/licenses/oculussdk/) | Required; review terms before Store submission |
| TextMeshPro | Unity Technologies | Unity Companion License | Bundled with Unity; review redistribution terms |
| Whisper.net / ONNX Runtime | Open source | MIT | Pending Stage 4 STT evaluation |
| OpenAI .NET SDK | OpenAI / community | MIT | Pending Stage 4 evaluation |
| ws (Node.js WebSocket) | Open source | MIT | Companion app WebSocket server |
| simple-git (Node.js) | Open source | MIT | Companion app Git operations |
| Electron | OpenJS Foundation | MIT | Companion app shell — pending framework decision |

---

## Imported assets (production record)

_Nothing imported yet. Add entries here when assets are brought into the project._

| Asset name | Source | Version / date | License | Notes | Verified by |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

---

## Attribution obligations

_Track any attribution-required assets here once selected._

| Asset | Author | License | Required credit text |
|---|---|---|---|
| — | — | — | — |

---

## Notes

- All assets must be cleared for **commercial redistribution** in a native Quest application before Stage 8.
- Do not import assets with NC (non-commercial) licenses into the production project.
- AI-generated art or audio requires separate license review — verify per tool and per asset before any Store release.
- LLM API output (code generated by agents) is governed by the LLM provider's terms of service. Review OpenAI and Anthropic ToS for commercial use of generated code before launch.
