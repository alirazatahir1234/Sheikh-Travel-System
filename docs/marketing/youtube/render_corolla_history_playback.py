#!/usr/bin/env python3
"""Render SheikhGo Nexus-style GPS history playback video (PIL + ffmpeg)."""

from __future__ import annotations

import json
import math
import shutil
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
ROUTE_JSON = ROOT / "_tmp_corolla_route.json"
FRAMES = ROOT / "_frames_bw2217"
OUT_MP4 = ROOT / "SheikhGo_Nexus_GPS_History_Playback_LEB-8825_Corolla.mp4"
LOGO = Path(
    "/Users/alirazatahir/.cursor/projects/"
    "Users-alirazatahir-Projects-Sheikh-Travel-System/assets/"
    "SheikhGoLogo_White_background-b4cfd7ff-f3e4-4ce3-ae54-874ec36ef9cf.png"
)
BGM = Path(
    "/Users/alirazatahir/Projects/Sheikh-Travel-System/docs/demo/audio-src/"
    "mixkit-motivating-mornings-33.mp3"
)

W, H = 1920, 1080
FPS = 30


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
        if bold
        else "/System/Library/Fonts/Supplemental/Arial.ttf"
    )
    return ImageFont.truetype(path, size)


def hav_km(a: tuple[float, float], b: tuple[float, float]) -> float:
    r = 6371.0
    lat1, lon1 = math.radians(a[0]), math.radians(a[1])
    lat2, lon2 = math.radians(b[0]), math.radians(b[1])
    dlat, dlon = lat2 - lat1, lon2 - lon1
    x = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
    return 2 * r * math.asin(math.sqrt(x))


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def interpolate_route(points: list[dict], samples: int = 240) -> list[dict]:
    if len(points) < 2:
        return points
    seg_lens = [
        max(hav_km((points[i]["lat"], points[i]["lng"]), (points[i + 1]["lat"], points[i + 1]["lng"])), 0.001)
        for i in range(len(points) - 1)
    ]
    total = sum(seg_lens)
    out: list[dict] = []
    for i in range(samples):
        target = (i / (samples - 1)) * total
        acc = 0.0
        for s, sl in enumerate(seg_lens):
            if acc + sl >= target or s == len(seg_lens) - 1:
                t = 0.0 if sl == 0 else min(1.0, (target - acc) / sl)
                p0, p1 = points[s], points[s + 1]
                out.append(
                    {
                        "lat": lerp(p0["lat"], p1["lat"], t),
                        "lng": lerp(p0["lng"], p1["lng"], t),
                        "speed": lerp(float(p0["speed"]), float(p1["speed"]), t),
                        "odo": lerp(float(p0["odo"]), float(p1["odo"]), t)
                        if p0.get("odo") is not None and p1.get("odo") is not None
                        else None,
                        "t": p1["t"] if t > 0.5 else p0["t"],
                    }
                )
                break
            acc += sl
    return out


def project(lat: float, lng: float, bounds: tuple, box: tuple[int, int, int, int]) -> tuple[int, int]:
    min_lat, max_lat, min_lng, max_lng = bounds
    x0, y0, x1, y1 = box
    pad = 0.04
    lat_span = max(max_lat - min_lat, 0.01)
    lng_span = max(max_lng - min_lng, 0.01)
    min_lat -= lat_span * pad
    max_lat += lat_span * pad
    min_lng -= lng_span * pad
    max_lng += lng_span * pad
    nx = (lng - min_lng) / (max_lng - min_lng)
    ny = (max_lat - lat) / (max_lat - min_lat)
    return int(x0 + nx * (x1 - x0)), int(y0 + ny * (y1 - y0))


def rounded_rect(draw: ImageDraw.ImageDraw, xy, radius: int, fill):
    draw.rounded_rectangle(xy, radius=radius, fill=fill)


def draw_logo(base: Image.Image, size: int = 150, margin: int = 36) -> None:
    logo = Image.open(LOGO).convert("RGBA")
    logo.thumbnail((size, size), Image.Resampling.LANCZOS)
    # Soft plate behind white-bg logo
    plate = Image.new("RGBA", (logo.width + 24, logo.height + 24), (255, 255, 255, 235))
    plate_draw = ImageDraw.Draw(plate)
    plate_draw.rounded_rectangle((0, 0, plate.width - 1, plate.height - 1), radius=18, fill=(255, 255, 255, 235))
    plate.paste(logo, (12, 12), logo)
    base.alpha_composite(plate, (margin, margin))


def caption_bar(draw: ImageDraw.ImageDraw, text: str, y: int = H - 120) -> None:
    tw = draw.textlength(text, font=font(36, True))
    pad_x, pad_y = 28, 16
    box = (W // 2 - tw / 2 - pad_x, y, W // 2 + tw / 2 + pad_x, y + 36 + pad_y * 2)
    rounded_rect(draw, box, 14, (8, 18, 36, 210))
    draw.text((W // 2 - tw / 2, y + pad_y), text, fill=(240, 248, 255, 255), font=font(36, True))


def make_title_card(subtitle: str) -> Image.Image:
    img = Image.new("RGBA", (W, H), (6, 16, 32, 255))
    draw = ImageDraw.Draw(img)
    # Atmosphere
    for i in range(12):
        alpha = 18 - i
        draw.ellipse(
            (W // 2 - 420 - i * 30, H // 2 - 220 - i * 20, W // 2 + 420 + i * 30, H // 2 + 160 + i * 20),
            outline=(20, 80, 120, max(alpha, 4)),
            width=2,
        )
    draw_logo(img, size=180, margin=48)
    title = "SheikhGo NEXUS"
    tw = draw.textlength(title, font=font(72, True))
    draw.text(((W - tw) / 2, H // 2 - 40), title, fill=(255, 255, 255, 255), font=font(72, True))
    sw = draw.textlength(subtitle, font=font(34))
    draw.text(((W - sw) / 2, H // 2 + 50), subtitle, fill=(170, 210, 235, 255), font=font(34))
    return img


def make_map_frame(
    waypoints: list[dict],
    trail: list[dict],
    marker: dict,
    bounds: tuple,
    caption: str,
    stats: dict,
) -> Image.Image:
    img = Image.new("RGBA", (W, H), (8, 20, 38, 255))
    draw = ImageDraw.Draw(img)

    # Subtle grid
    for x in range(0, W, 80):
        draw.line((x, 0, x, H), fill=(18, 40, 64, 255), width=1)
    for y in range(0, H, 80):
        draw.line((0, y, W, y), fill=(18, 40, 64, 255), width=1)

    map_box = (120, 140, W - 120, H - 160)
    rounded_rect(draw, map_box, 28, (12, 30, 52, 255))
    # Inner glow border
    draw.rounded_rectangle(map_box, radius=28, outline=(40, 120, 170, 180), width=2)

    # Full route (dim)
    full_xy = [project(p["lat"], p["lng"], bounds, map_box) for p in waypoints]
    if len(full_xy) > 1:
        draw.line(full_xy, fill=(40, 90, 120, 120), width=4)

    # Animated trail
    trail_xy = [project(p["lat"], p["lng"], bounds, map_box) for p in trail]
    if len(trail_xy) > 1:
        draw.line(trail_xy, fill=(0, 210, 180, 255), width=6)
        # Soft glow
        glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        gdraw = ImageDraw.Draw(glow)
        gdraw.line(trail_xy, fill=(0, 210, 180, 90), width=14)
        glow = glow.filter(ImageFilter.GaussianBlur(6))
        img = Image.alpha_composite(img, glow)
        draw = ImageDraw.Draw(img)

    mx, my = project(marker["lat"], marker["lng"], bounds, map_box)
    draw.ellipse((mx - 16, my - 16, mx + 16, my + 16), fill=(255, 90, 60, 255), outline=(255, 255, 255, 255), width=3)
    draw.ellipse((mx - 5, my - 5, mx + 5, my + 5), fill=(255, 255, 255, 255))

    draw_logo(img, size=120, margin=40)

    # Top info chip
    chip = f"Toyota Corolla  ·  LEB-8825  ·  History playback"
    cw = draw.textlength(chip, font=font(28, True))
    rounded_rect(draw, (W / 2 - cw / 2 - 24, 48, W / 2 + cw / 2 + 24, 100), 16, (255, 255, 255, 230))
    draw.text((W / 2 - cw / 2, 60), chip, fill=(12, 30, 48, 255), font=font(28, True))

    # Side stats
    panel = (W - 420, 160, W - 140, 430)
    rounded_rect(draw, panel, 18, (6, 14, 28, 210))
    lines = [
        ("DATE", stats["date"]),
        ("DISTANCE", stats["distance"]),
        ("MAX SPEED", stats["max_speed"]),
        ("POINTS", stats["points"]),
    ]
    y = 180
    for label, value in lines:
        draw.text((W - 390, y), label, fill=(120, 170, 200, 255), font=font(18, True))
        draw.text((W - 390, y + 24), value, fill=(240, 250, 255, 255), font=font(30, True))
        y += 62

    # Live speed
    speed_txt = f"{int(round(marker['speed']))} km/h"
    sw = draw.textlength(speed_txt, font=font(40, True))
    rounded_rect(draw, (150, 170, 150 + sw + 40, 240), 14, (0, 180, 150, 220))
    draw.text((170, 185), speed_txt, fill=(255, 255, 255, 255), font=font(40, True))

    caption_bar(draw, caption)
    return img


def make_stats_card(stats: dict) -> Image.Image:
    img = Image.new("RGBA", (W, H), (6, 16, 32, 255))
    draw = ImageDraw.Draw(img)
    draw_logo(img, size=150, margin=48)
    title = "History playback — real track data"
    tw = draw.textlength(title, font=font(48, True))
    draw.text(((W - tw) / 2, 220), title, fill=(255, 255, 255, 255), font=font(48, True))
    cards = [
        ("Vehicle", "Toyota Corolla"),
        ("Plate", "LEB-8825"),
        ("Date", stats["date"]),
        ("Distance", stats["distance"]),
        ("Max speed", stats["max_speed"]),
        ("Track points", stats["points"]),
    ]
    for i, (k, v) in enumerate(cards):
        col = i % 3
        row = i // 3
        x0 = 220 + col * 520
        y0 = 360 + row * 220
        rounded_rect(draw, (x0, y0, x0 + 460, y0 + 170), 22, (14, 34, 58, 255))
        draw.text((x0 + 36, y0 + 36), k.upper(), fill=(120, 170, 200, 255), font=font(22, True))
        draw.text((x0 + 36, y0 + 80), v, fill=(245, 250, 255, 255), font=font(36, True))
    caption_bar(draw, "One workspace for live GPS, history, and fleet ops")
    return img


def make_cta_card() -> Image.Image:
    img = Image.new("RGBA", (W, H), (6, 16, 32, 255))
    draw = ImageDraw.Draw(img)
    draw_logo(img, size=200, margin=W // 2 - 100)
    # Move logo up-center manually
    img = Image.new("RGBA", (W, H), (6, 16, 32, 255))
    draw = ImageDraw.Draw(img)
    logo = Image.open(LOGO).convert("RGBA")
    logo.thumbnail((220, 220), Image.Resampling.LANCZOS)
    plate = Image.new("RGBA", (logo.width + 28, logo.height + 28), (255, 255, 255, 240))
    pd = ImageDraw.Draw(plate)
    pd.rounded_rectangle((0, 0, plate.width - 1, plate.height - 1), radius=22, fill=(255, 255, 255, 240))
    plate.paste(logo, (14, 14), logo)
    img.alpha_composite(plate, ((W - plate.width) // 2, 220))
    draw = ImageDraw.Draw(img)
    line1 = "SheikhGo NEXUS"
    line2 = "Replay routes. Control the fleet."
    line3 = "sheikhgo.com  ·  Book a Demo"
    for text, y, size, color in [
        (line1, 520, 56, (255, 255, 255, 255)),
        (line2, 600, 36, (170, 210, 235, 255)),
        (line3, 700, 34, (0, 210, 180, 255)),
    ]:
        f = font(size, True)
        tw = draw.textlength(text, font=f)
        draw.text(((W - tw) / 2, y), text, fill=color, font=f)
    return img


def write_frames(frames: list[Image.Image]) -> None:
    if FRAMES.exists():
        shutil.rmtree(FRAMES)
    FRAMES.mkdir(parents=True, exist_ok=True)
    for i, frame in enumerate(frames):
        frame.convert("RGB").save(FRAMES / f"frame_{i:05d}.png")


def encode_video(frame_count: int) -> None:
    duration = frame_count / FPS
    cmd = [
        "ffmpeg",
        "-y",
        "-framerate",
        str(FPS),
        "-i",
        str(FRAMES / "frame_%05d.png"),
    ]
    if BGM.exists():
        cmd += [
            "-i",
            str(BGM),
            "-filter_complex",
            f"[1:a]volume=0.22,afade=t=in:st=0:d=1.5,afade=t=out:st={max(duration - 2, 0)}:d=2[a]",
            "-map",
            "0:v",
            "-map",
            "[a]",
            "-shortest",
        ]
    cmd += [
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        "-crf",
        "18",
        "-preset",
        "medium",
        "-c:a",
        "aac",
        "-b:a",
        "160k",
        "-movflags",
        "+faststart",
        str(OUT_MP4),
    ]
    subprocess.run(cmd, check=True)


def main() -> None:
    raw = json.loads(ROUTE_JSON.read_text())
    waypoints = raw["points"]
    # Prefer distinct waypoints if densely duplicated
    distinct: list[dict] = []
    prev = None
    for p in waypoints:
        key = (round(p["lat"], 5), round(p["lng"], 5))
        if key != prev:
            distinct.append(p)
            prev = key
    if len(distinct) >= 8:
        waypoints = distinct

    path_km = sum(
        hav_km((waypoints[i]["lat"], waypoints[i]["lng"]), (waypoints[i + 1]["lat"], waypoints[i + 1]["lng"]))
        for i in range(len(waypoints) - 1)
    )
    max_speed = max(float(p["speed"]) for p in waypoints)
    odo_delta = None
    if waypoints[0].get("odo") is not None and waypoints[-1].get("odo") is not None:
        odo_delta = float(waypoints[-1]["odo"]) - float(waypoints[0]["odo"])

    stats = {
        "date": "25 Sep 2026",
        "distance": f"{(odo_delta if odo_delta and odo_delta > 1 else path_km):.0f} km",
        "max_speed": f"{max_speed:.0f} km/h",
        "points": str(raw.get("sourcePointCountDay") or len(waypoints)),
    }

    bounds = (
        min(p["lat"] for p in waypoints),
        max(p["lat"] for p in waypoints),
        min(p["lng"] for p in waypoints),
        max(p["lng"] for p in waypoints),
    )

    anim = interpolate_route(waypoints, samples=270)
    frames: list[Image.Image] = []

    # Title ~3s
    title = make_title_card("One workspace for live GPS, fleet ops, and history playback")
    frames.extend([title] * (FPS * 3))

    # Vehicle intro ~4s
    intro = make_title_card("Toyota Corolla · LEB-8825 · GPS history replay")
    frames.extend([intro] * (FPS * 4))

    captions = [
        (0.00, "Live GPS history playback"),
        (0.25, "Replay the route — every kilometer"),
        (0.55, "Speed, path, and location in one view"),
        (0.80, "Built for dispatch and fleet control"),
    ]

    # Map animation ~9s
    map_n = FPS * 9
    for i in range(map_n):
        t = i / (map_n - 1)
        idx = min(int(t * (len(anim) - 1)), len(anim) - 1)
        trail = anim[: idx + 1]
        marker = anim[idx]
        caption = captions[0][1]
        for start, text in captions:
            if t >= start:
                caption = text
        frames.append(make_map_frame(waypoints, trail, marker, bounds, caption, stats))

    # Stats ~5s
    stats_card = make_stats_card(stats)
    frames.extend([stats_card] * (FPS * 5))

    # CTA ~4s
    cta = make_cta_card()
    frames.extend([cta] * (FPS * 4))

    write_frames(frames)
    encode_video(len(frames))

    meta = {
        **{k: raw.get(k) for k in ("vehicle", "plate", "vehicleId", "matchNote", "day", "sourcePointCountDay")},
        "sampledWaypoints": len(waypoints),
        "pathKmApprox": round(path_km, 2),
        "odoDeltaKm": round(odo_delta, 2) if odo_delta is not None else None,
        "maxSpeedKmh": max_speed,
        "dateRange": [waypoints[0]["t"], waypoints[-1]["t"]],
        "output": str(OUT_MP4),
        "durationSec": round(len(frames) / FPS, 1),
        "frameCount": len(frames),
        "logo": str(LOGO),
    }
    (ROOT / "SheikhGo_Nexus_GPS_History_Playback_LEB-8825_Corolla.meta.json").write_text(
        json.dumps(meta, indent=2)
    )
    print(json.dumps(meta, indent=2))


if __name__ == "__main__":
    main()
