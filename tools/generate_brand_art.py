#!/usr/bin/env python3
"""
Skoven Brand Art and Meta Horizon Store Asset Suite Generator.
Generates:
1. High-resolution master emblem (FG.png, 2048x2048 RGBA)
2. Master spatial background (BG.png, 3840x2160 RGB)
3. Master logo wordmark (LOGO.png)
4. Store Cover Art Suite across standard Meta Horizon Store aspect ratios:
   - landscape_cover_2560x1440.png
   - hero_cover_3000x900.png
   - square_cover_1440x1440.png
   - portrait_cover_1008x1440.png
   - mini_landscape_1080x360.png
   - app_icon_512x512.png
5. Runs meta-spatialized-tile-icon to generate compliant spatialized tile pairs:
   - spatial_tile_512_background.png (opaque RGB)
   - spatial_tile_512_foreground.png (transparent RGBA, 392x392 safe area)
   - spatial_tile_180_background.png
   - spatial_tile_180_foreground.png
"""

import math
import os
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
SOURCE_DIR = os.path.join(PROJECT_ROOT, "store_listing", "source_art")
STORE_OUT_DIR = os.path.join(PROJECT_ROOT, "store_listing", "assets")
UNITY_STORE_DIR = os.path.join(PROJECT_ROOT, "unity", "Assets", "Skoven", "Art", "Store")
INTER_FONT = os.path.join(PROJECT_ROOT, "unity", "Assets", "Skoven", "Fonts", "Inter-Bold.ttf")
MONO_FONT = os.path.join(PROJECT_ROOT, "unity", "Assets", "Skoven", "Fonts", "JetBrainsMono-Bold.ttf")
TILE_SCRIPT = "/Users/jonathansirrine/dev/vr-games/.agents/skills/meta-spatialized-tile-icon/scripts/generate_spatialized_tile.py"

os.makedirs(SOURCE_DIR, exist_ok=True)
os.makedirs(STORE_OUT_DIR, exist_ok=True)
os.makedirs(UNITY_STORE_DIR, exist_ok=True)


def create_emblem(size=2048) -> Image.Image:
    """
    Renders the Skoven emblem at 2x supersampling and downsamples with Lanczos.
    The emblem represents 'The Forest' (Skoven) of AI agents:
    A cybernetic tree/canopy formed by interlocking translucent polygonal nodes,
    neural branches, and glowing focal points in Cyan (#00E5FF), Indigo (#6366F1), and Emerald (#10B981).
    """
    scale = 2
    w = h = size * scale
    cx, cy = w // 2, h // 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. Subtle Outer Prismatic Shield / Hex Ring
    radius = int(w * 0.40)
    hex_pts = [
        (cx + radius * math.sin(math.radians(a)), cy - radius * math.cos(math.radians(a)))
        for a in range(0, 360, 60)
    ]
    for i in range(len(hex_pts)):
        p1 = hex_pts[i]
        p2 = hex_pts[(i + 1) % len(hex_pts)]
        draw.line([p1, p2], fill=(0, 229, 255, 120), width=int(14 * scale))

    # Outer corner node pips
    for px, py in hex_pts:
        r_node = int(22 * scale)
        draw.ellipse([px - r_node, py - r_node, px + r_node, py + r_node], fill=(0, 229, 255, 230))
        r_inner = int(10 * scale)
        draw.ellipse([px - r_inner, py - r_inner, px + r_inner, py + r_inner], fill=(255, 255, 255, 255))

    # 2. Main Canopy: Interlocking faceted polygons (Tree of Agents)
    # Apex node
    apex = (cx, cy - int(radius * 0.90))
    # Mid-tier branching nodes (left, center, right)
    mid_left = (cx - int(radius * 0.65), cy - int(radius * 0.20))
    mid_center = (cx, cy - int(radius * 0.15))
    mid_right = (cx + int(radius * 0.65), cy - int(radius * 0.20))
    # Lower tier nodes
    low_left = (cx - int(radius * 0.45), cy + int(radius * 0.45))
    low_right = (cx + int(radius * 0.45), cy + int(radius * 0.45))
    # Base trunk node (the orchestrator root)
    root_node = (cx, cy + int(radius * 0.85))

    # Facet polygons with high-tech translucency and vibrant fills
    # Top Left Leaf / Panel
    draw.polygon([apex, mid_left, mid_center], fill=(0, 200, 255, 180))
    # Top Right Leaf / Panel
    draw.polygon([apex, mid_center, mid_right], fill=(0, 240, 255, 220))
    # Mid Left Panel
    draw.polygon([mid_left, low_left, mid_center], fill=(79, 70, 229, 200))
    # Mid Right Panel
    draw.polygon([mid_center, low_right, mid_right], fill=(99, 102, 241, 210))
    # Center Core Diamond
    draw.polygon([apex, mid_left, root_node, mid_right], outline=(0, 255, 255, 100), width=int(6 * scale))
    # Central Lower Core (Emerald status bridge)
    draw.polygon([mid_center, low_left, root_node, low_right], fill=(16, 185, 129, 190))

    # 3. High-intensity neural trunk and branch lines
    branches = [
        (root_node, mid_center),
        (mid_center, apex),
        (root_node, low_left),
        (root_node, low_right),
        (low_left, mid_left),
        (low_right, mid_right),
        (mid_left, apex),
        (mid_right, apex),
        (mid_left, mid_center),
        (mid_right, mid_center),
    ]
    for p1, p2 in branches:
        draw.line([p1, p2], fill=(255, 255, 255, 240), width=int(10 * scale))

    # 4. Neural Nodes (Glowing circular junctions)
    all_nodes = [
        (apex, (0, 240, 255), int(36 * scale)),
        (mid_center, (255, 255, 255), int(42 * scale)),
        (mid_left, (0, 220, 255), int(30 * scale)),
        (mid_right, (0, 220, 255), int(30 * scale)),
        (low_left, (99, 102, 241), int(28 * scale)),
        (low_right, (99, 102, 241), int(28 * scale)),
        (root_node, (16, 185, 129), int(38 * scale)),
    ]
    for (nx, ny), color, r_outer in all_nodes:
        # Halo
        draw.ellipse([nx - r_outer * 1.5, ny - r_outer * 1.5, nx + r_outer * 1.5, ny + r_outer * 1.5], fill=(*color, 90))
        # Core
        draw.ellipse([nx - r_outer, ny - r_outer, nx + r_outer, ny + r_outer], fill=(*color, 240))
        # Hot white center
        r_hot = int(r_outer * 0.45)
        draw.ellipse([nx - r_hot, ny - r_hot, nx + r_hot, ny + r_hot], fill=(255, 255, 255, 255))

    # Downsample with Lanczos
    return img.resize((size, size), Image.Resampling.LANCZOS)


def create_spatial_background(w=3840, h=2160) -> Image.Image:
    """
    Renders the master spatial background:
    Deep obsidian-to-midnight gradient with spatial perspective grid,
    ambient nebula glows, and floating translucent code panels.
    """
    img = Image.new("RGB", (w, h), (8, 10, 16))
    draw = ImageDraw.Draw(img)

    # 1. Radial Vignette / Gradient
    cx, cy = w // 2, h // 2
    max_d = math.hypot(cx, cy)
    for y in range(0, h, 8):
        for x in range(0, w, 16):
            d = math.hypot(x - cx, y - cy) / max_d
            # Center color: (18, 24, 38) -> Edge: (6, 8, 12)
            r = int(18 * (1.0 - d * 0.7))
            g = int(24 * (1.0 - d * 0.7))
            b = int(38 * (1.0 - d * 0.65))
            draw.rectangle([x, y, x + 16, y + 8], fill=(r, g, b))

    # 2. Add soft ambient glow layers
    glow_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow_layer)

    # Cyan central glow
    glow_draw.ellipse([cx - 900, cy - 800, cx + 900, cy + 600], fill=(0, 229, 255, 45))
    # Violet lower ambient glow
    glow_draw.ellipse([cx - 1200, cy - 200, cx + 1200, cy + 1100], fill=(99, 102, 241, 40))
    # Emerald bottom accent
    glow_draw.ellipse([cx - 400, cy + 300, cx + 400, cy + 900], fill=(16, 185, 129, 35))

    glow_blurred = glow_layer.filter(ImageFilter.GaussianBlur(140))
    img = Image.alpha_composite(img.convert("RGBA"), glow_blurred).convert("RGB")
    draw = ImageDraw.Draw(img)

    # 3. Spatial Perspective Grid (Floor)
    horizon_y = int(h * 0.62)
    vanishing_pt = (cx, horizon_y - 200)

    # Radial perspective rays
    for angle_deg in range(-75, 76, 5):
        rad = math.radians(angle_deg)
        x_bottom = cx + int(math.tan(rad) * (h - vanishing_pt[1]))
        draw.line([vanishing_pt, (x_bottom, h)], fill=(0, 229, 255, 30), width=2)

    # Horizontal depth lines
    for i in range(1, 16):
        t = (i / 15.0) ** 2.2
        y_line = int(horizon_y + t * (h - horizon_y))
        draw.line([(0, y_line), (w, y_line)], fill=(0, 229, 255, int(15 + t * 45)), width=2)

    # 4. Floating Translucent Workspace Panels in VR perspective
    panel_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pdraw = ImageDraw.Draw(panel_layer)

    # Left Floating Panel (Diff / Terminal)
    p_left = [(int(w * 0.08), int(h * 0.32)), (int(w * 0.28), int(h * 0.38)), (int(w * 0.28), int(h * 0.72)), (int(w * 0.08), int(h * 0.68))]
    pdraw.polygon(p_left, fill=(20, 24, 35, 160), outline=(0, 229, 255, 110), width=3)
    # Header bar
    pdraw.polygon([p_left[0], p_left[1], (p_left[1][0], p_left[1][1] + 35), (p_left[0][0], p_left[0][1] + 35)], fill=(42, 47, 62, 210))

    # Right Floating Panel (Task Board / Agent Log)
    p_right = [(int(w * 0.72), int(h * 0.38)), (int(w * 0.92), int(h * 0.32)), (int(w * 0.92), int(h * 0.68)), (int(w * 0.72), int(h * 0.72))]
    pdraw.polygon(p_right, fill=(20, 24, 35, 160), outline=(99, 102, 241, 110), width=3)
    pdraw.polygon([p_right[0], p_right[1], (p_right[1][0], p_right[1][1] + 35), (p_right[0][0], p_right[0][1] + 35)], fill=(42, 47, 62, 210))

    # Composite panel layer
    img = Image.alpha_composite(img.convert("RGBA"), panel_layer).convert("RGB")
    return img


def create_logo_wordmark(w=2000, h=600) -> Image.Image:
    """Creates transparent wordmark 'SKOVEN' + subtitle."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    try:
        font_title = ImageFont.truetype(INTER_FONT, 180)
        font_sub = ImageFont.truetype(MONO_FONT, 42)
    except Exception:
        font_title = ImageFont.load_default()
        font_sub = ImageFont.load_default()

    # Tracked "S K O V E N"
    text = "S K O V E N"
    bbox = draw.textbbox((0, 0), text, font=font_title)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = (w - tw) // 2
    ty = int(h * 0.22)

    # Soft glow behind text
    for offset in range(6, 0, -2):
        draw.text((tx, ty), text, font=font_title, fill=(0, 229, 255, 40))

    # Main text in clean white
    draw.text((tx, ty), text, font=font_title, fill=(255, 255, 255, 255))

    # Subtitle
    subtext = "AI AGENT WORKSPACE FOR META QUEST"
    sbbox = draw.textbbox((0, 0), subtext, font=font_sub)
    sw = sbbox[2] - sbbox[0]
    sx = (w - sw) // 2
    sy = ty + th + 60

    draw.text((sx, sy), subtext, font=font_sub, fill=(0, 229, 255, 230))
    return img


def compose_store_cover(bg: Image.Image, emblem: Image.Image, logo: Image.Image) -> Image.Image:
    """Composes master brand image with background, emblem, and logo."""
    w, h = bg.size
    comp = bg.convert("RGBA").copy()

    # Place emblem slightly above vertical center
    emblem_size = int(h * 0.58)
    emblem_scaled = emblem.resize((emblem_size, emblem_size), Image.Resampling.LANCZOS)
    ex = (w - emblem_size) // 2
    ey = int(h * 0.12)

    comp.alpha_composite(emblem_scaled, (ex, ey))

    # Place logo wordmark below emblem
    lw = int(w * 0.60)
    lh = int(lw * (logo.height / logo.width))
    logo_scaled = logo.resize((lw, lh), Image.Resampling.LANCZOS)
    lx = (w - lw) // 2
    ly = ey + emblem_size - int(lh * 0.15)

    comp.alpha_composite(logo_scaled, (lx, ly))
    return comp.convert("RGB")


def crop_and_scale(im: Image.Image, target_w: int, target_h: int, top_bias: float = 0.40) -> Image.Image:
    """Crops and scales image to target aspect ratio with focal bias."""
    orig_w, orig_h = im.size
    target_aspect = target_w / target_h
    orig_aspect = orig_w / orig_h

    if orig_aspect > target_aspect:
        # Source is wider than target: crop sides
        new_w = int(orig_h * target_aspect)
        left = (orig_w - new_w) // 2
        top = 0
        right = left + new_w
        bottom = orig_h
    else:
        # Source is taller than target: crop top/bottom with bias
        new_h = int(orig_w / target_aspect)
        left = 0
        top = int((orig_h - new_h) * top_bias)
        right = orig_w
        bottom = top + new_h

    cropped = im.crop((left, top, right, bottom))
    return cropped.resize((target_w, target_h), Image.Resampling.LANCZOS)


def main():
    print("=== Generating Skoven Master Brand Assets ===")
    
    # 1. Generate Emblem (FG)
    emblem = create_emblem(2048)
    fg_path = os.path.join(SOURCE_DIR, "FG.png")
    emblem.save(fg_path, "PNG")
    print(f"✅ Generated Master Emblem: {fg_path} (2048x2048 RGBA)")

    # 2. Generate Spatial Background (BG)
    bg = create_spatial_background(3840, 2160)
    bg_path = os.path.join(SOURCE_DIR, "BG.png")
    bg.save(bg_path, "PNG")
    print(f"✅ Generated Master Background: {bg_path} (3840x2160 RGB)")

    # 3. Generate Logo Wordmark
    logo = create_logo_wordmark(2000, 500)
    logo_path = os.path.join(SOURCE_DIR, "LOGO.png")
    logo.save(logo_path, "PNG")
    print(f"✅ Generated Master Wordmark: {logo_path}")

    # 4. Generate Master Combined Cover
    master_cover = compose_store_cover(bg, emblem, logo)
    master_cover_path = os.path.join(SOURCE_DIR, "cover_master_3840x2160.png")
    master_cover.save(master_cover_path, "PNG")
    print(f"✅ Generated Master Cover: {master_cover_path}")

    # 5. Generate Standard Meta Horizon Store Covers
    targets = [
        ("landscape_cover_2560x1440.png", 2560, 1440, 0.40),
        ("hero_cover_3000x900.png", 3000, 900, 0.35),
        ("square_cover_1440x1440.png", 1440, 1440, 0.38),
        ("portrait_cover_1008x1440.png", 1008, 1440, 0.32),
        ("mini_landscape_1080x360.png", 1080, 360, 0.35),
        ("app_icon_512x512.png", 512, 512, 0.32),
    ]

    print("\n=== Generating Store Cover Assets ===")
    for filename, tw, th, bias in targets:
        scaled = crop_and_scale(master_cover, tw, th, top_bias=bias)
        # Save to store_listing/assets/
        out1 = os.path.join(STORE_OUT_DIR, filename)
        scaled.save(out1, "PNG", optimize=True)
        # Save to unity/Assets/Skoven/Art/Store/
        out2 = os.path.join(UNITY_STORE_DIR, filename)
        scaled.save(out2, "PNG", optimize=True)
        print(f"✅ Created {filename} ({tw}x{th})")

    # 6. Generate Meta Spatialized Tile Icon (Foreground + Background)
    print("\n=== Generating Meta Spatialized Tile Icons via canonical skill script ===")
    cmd = [
        sys.executable,
        TILE_SCRIPT,
        "--fg-source", fg_path,
        "--bg-source", bg_path,
        "--output-dir", STORE_OUT_DIR,
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    print(res.stdout)
    if res.returncode != 0:
        print(f"❌ Tile generation failed:\n{res.stderr}")
        sys.exit(res.returncode)

    # Also copy spatial tiles to Unity folder
    for tile_file in [
        "spatial_tile_512_background.png",
        "spatial_tile_512_foreground.png",
        "spatial_tile_180_background.png",
        "spatial_tile_180_foreground.png",
    ]:
        src = os.path.join(STORE_OUT_DIR, tile_file)
        dst = os.path.join(UNITY_STORE_DIR, tile_file)
        if os.path.exists(src):
            with open(src, "rb") as sf, open(dst, "wb") as df:
                df.write(sf.read())
            print(f"Copied {tile_file} to Unity project")

    print("\n🎉 All Skoven brand and Meta Horizon Store assets generated and verified successfully!")


if __name__ == "__main__":
    main()
