#!/usr/bin/env python3
"""Assemble SheikhGo Nexus mobile promo from existing iOS Simulator capture.

Offline edit: trims usable footage from ios-walkthrough.mov, adds VO / titles /
captions / light BGM. Does not require API :5082.

Claims: splash, login, dashboard, Live Map, Vehicles/Trips (More). Eco Nexus
lines only on open + Live Map. No invented EV/SOS partner claims.
"""
from __future__ import annotations

import json
import os
import subprocess
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PROD = Path(__file__).resolve().parent
DEMO = PROD.parent
REPO = Path("/Users/alirazatahir/Projects/Sheikh-Travel-System")
SRC_MOV = DEMO / "_segments" / "ios-walkthrough.mov"
SRC_MP4 = DEMO / "sheikhgo-nexus-ios-walkthrough.mp4"
OUT_MP4 = DEMO / "sheikhgo-nexus-mobile-promo.mp4"
OUT_SRT = DEMO / "sheikhgo-nexus-mobile-promo.srt"
OUT_VTT = DEMO / "sheikhgo-nexus-mobile-promo.vtt"
MUSIC = REPO / "docs" / "demo" / "audio-src" / "mixkit-motivating-mornings-33.mp3"
VOICE = "en-US-GuyNeural"
# Portrait deliverable (keeps phone framing, lean encode)
W, H, FPS = 720, 1560, 24
FFMPEG_THREADS = os.environ.get("PROMO_FFMPEG_THREADS", "1")
REUSE_VO = os.environ.get("PROMO_REUSE_VO", "1") != "0"
FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
FONT_REG = "/System/Library/Fonts/Supplemental/Arial.ttf"


@dataclass
class Scene:
    idx: int
    start: float
    end: float
    title: str
    subtitle: str
    vo: str
    # Absolute times in source MOV
    src_start: float
    src_end: float

    @property
    def dur(self) -> float:
        return self.end - self.start


# Usable MOV window ~74–98s (home→splash→login→dashboard→Live Map→More).
SCENES: list[Scene] = [
    Scene(
        1,
        0,
        8,
        "Every journey matters",
        "Eco Nexus · intelligent workplace",
        "Every journey matters. Eco Nexus brings your entire fleet into one intelligent workplace.",
        74.6,
        79.2,
    ),
    Scene(
        2,
        8,
        14,
        "Owners track · Drivers run trips",
        "Work email or phone",
        "Sign in with work email or phone — owners track the fleet, drivers run trips.",
        79.8,
        84.0,
    ),
    Scene(
        3,
        14,
        24,
        "Fleet command overview",
        "Dashboard · status · alerts",
        "The command dashboard mirrors Nexus priorities — fleet status and what needs attention now.",
        84.0,
        91.5,
    ),
    Scene(
        4,
        24,
        36,
        "Live Map",
        "Track every vehicle real-time",
        "Track every vehicle real-time. Live map is a first-class tab for fleet managers — same live tracking hub as the web.",
        91.5,
        94.2,  # stop before More tab transition
    ),
    Scene(
        5,
        36,
        46,
        "Vehicles · Trips · More",
        "Role-based shell",
        "Open More for Vehicles, Trips, geofences, and GPS tools — the role-based shell shows what your permissions allow.",
        94.5,
        96.5,  # More grid with Vehicles/Trips — before Alerts drill-in
    ),
    Scene(
        6,
        46,
        54,
        "Book a Demo",
        "sheikhgo.com · Dubai & beyond",
        "SheikhGo Nexus — web ERP and iOS fleet app, one platform. Book a demo at sheikhgo.com.",
        74.6,
        78.5,  # reuse splash/brand for close
    ),
]


def run(cmd: list[str], **kw) -> None:
    subprocess.run(cmd, check=True, **kw)


def ffmpeg_base() -> list[str]:
    return [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-threads",
        FFMPEG_THREADS,
    ]


def ffprobe_dur(path: Path) -> float:
    out = subprocess.check_output(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            str(path),
        ],
        text=True,
    ).strip()
    return float(out)


def source_video() -> Path:
    if SRC_MOV.exists():
        return SRC_MOV
    if SRC_MP4.exists():
        return SRC_MP4
    raise FileNotFoundError("No ios-walkthrough.mov or .mp4 found under website/demo")


def render_overlay(scene: Scene, out: Path) -> Path:
    out.parent.mkdir(parents=True, exist_ok=True)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    header_font = ImageFont.truetype(FONT_BOLD, 22)
    title_font = ImageFont.truetype(FONT_BOLD, 36)
    sub_font = ImageFont.truetype(FONT_REG, 24)

    header = "SHEIKHGO NEXUS — MOBILE"
    box_fill = (0, 0, 0, 72)

    def text_box(text: str, font, y: int, fill=(255, 255, 255, 245)) -> None:
        # Wrap long titles for portrait width
        words = text.split()
        lines: list[str] = []
        cur = ""
        for w in words:
            trial = f"{cur} {w}".strip()
            bbox = draw.textbbox((0, 0), trial, font=font)
            if bbox[2] - bbox[0] > W - 72 and cur:
                lines.append(cur)
                cur = w
            else:
                cur = trial
        if cur:
            lines.append(cur)
        y0 = y
        for line in lines:
            bbox = draw.textbbox((0, 0), line, font=font)
            tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
            x = (W - tw) // 2
            pad = 10
            draw.rounded_rectangle(
                (x - pad, y0 - pad, x + tw + pad, y0 + th + pad),
                radius=10,
                fill=box_fill,
            )
            draw.text((x, y0), line, font=font, fill=fill)
            y0 += th + 14

    text_box(header, header_font, 48, fill=(255, 255, 255, 230))
    text_box(scene.title, title_font, H - 220, fill=(255, 232, 150, 255))
    if scene.subtitle:
        text_box(scene.subtitle, sub_font, H - 120, fill=(200, 255, 235, 255))
    img.save(out)
    return out


def make_vo(scene: Scene, out: Path) -> None:
    raw = PROD / "vo" / f"scene{scene.idx:02d}-raw.mp3"
    text_marker = PROD / "vo" / f"scene{scene.idx:02d}-text.txt"
    raw.parent.mkdir(parents=True, exist_ok=True)
    prior = text_marker.read_text(encoding="utf-8") if text_marker.exists() else ""
    if REUSE_VO and prior == scene.vo and out.exists() and abs(ffprobe_dur(out) - scene.dur) < 0.5:
        print(f"  reuse VO scene {scene.idx}")
        return

    run(
        [
            "edge-tts",
            "--voice",
            VOICE,
            "--write-media",
            str(raw),
            "-t",
            scene.vo,
        ]
    )
    text_marker.write_text(scene.vo, encoding="utf-8")
    dur = ffprobe_dur(raw)
    max_dur = scene.dur - 0.3
    fit = PROD / "vo" / f"scene{scene.idx:02d}-fit.mp3"
    if dur > max_dur:
        rate = min(dur / max_dur, 1.35)
        run(
            [
                *ffmpeg_base(),
                "-i",
                str(raw),
                "-filter:a",
                f"atempo={rate:.4f}",
                str(fit),
            ]
        )
        dur = ffprobe_dur(fit)
    else:
        fit.write_bytes(raw.read_bytes())
    pad = scene.dur - dur
    if pad > 0.05:
        run(
            [
                *ffmpeg_base(),
                "-i",
                str(fit),
                "-f",
                "lavfi",
                "-i",
                f"anullsrc=r=24000:cl=mono,atrim=0:{pad:.3f}",
                "-filter_complex",
                "[0:a][1:a]concat=n=2:v=0:a=1",
                str(out),
            ]
        )
    else:
        out.write_bytes(fit.read_bytes())


def extract_padded_clip(src: Path, scene: Scene, out: Path) -> None:
    """Trim source clip; freeze last frame if shorter than scene.dur."""
    src_dur = max(0.2, scene.src_end - scene.src_start)
    need = scene.dur
    # Scale to portrait canvas, slight lift
    vf_base = (
        f"scale={W}:{H}:force_original_aspect_ratio=decrease,"
        f"pad={W}:{H}:(ow-iw)/2:(oh-ih)/2:black,"
        f"eq=brightness=0.04:contrast=1.03:saturation=1.04,"
        f"fps={FPS},format=yuv420p"
    )
    raw = PROD / "segments" / f"s{scene.idx:02d}-raw.mp4"
    raw.parent.mkdir(parents=True, exist_ok=True)
    run(
        [
            *ffmpeg_base(),
            "-ss",
            f"{scene.src_start:.3f}",
            "-i",
            str(src),
            "-t",
            f"{src_dur:.3f}",
            "-vf",
            vf_base,
            "-an",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "22",
            "-pix_fmt",
            "yuv420p",
            str(raw),
        ]
    )
    got = ffprobe_dur(raw)
    if got + 0.05 >= need:
        run(
            [
                *ffmpeg_base(),
                "-i",
                str(raw),
                "-t",
                f"{need:.3f}",
                "-c",
                "copy",
                str(out),
            ]
        )
        return
    # Freeze a mid-clip frame (avoids transition tails like Alerts)
    freeze = need - got
    still = PROD / "segments" / f"s{scene.idx:02d}-still.jpg"
    still_at = max(0.05, min(got * 0.45, got - 0.08))
    run(
        [
            *ffmpeg_base(),
            "-ss",
            f"{still_at:.3f}",
            "-i",
            str(raw),
            "-frames:v",
            "1",
            str(still),
        ]
    )
    hold = PROD / "segments" / f"s{scene.idx:02d}-hold.mp4"
    run(
        [
            *ffmpeg_base(),
            "-loop",
            "1",
            "-i",
            str(still),
            "-t",
            f"{freeze:.3f}",
            "-vf",
            f"fps={FPS},format=yuv420p",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "22",
            "-pix_fmt",
            "yuv420p",
            "-an",
            str(hold),
        ]
    )
    lst = PROD / "segments" / f"s{scene.idx:02d}-concat.txt"
    lst.write_text(f"file '{raw}'\nfile '{hold}'\n")
    run(
        [
            *ffmpeg_base(),
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(lst),
            "-c",
            "copy",
            str(out),
        ]
    )


def burn_overlay(clip: Path, overlay: Path, out: Path) -> None:
    run(
        [
            *ffmpeg_base(),
            "-i",
            str(clip),
            "-i",
            str(overlay),
            "-filter_complex",
            "[0:v][1:v]overlay=0:0:format=auto",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "21",
            "-pix_fmt",
            "yuv420p",
            "-an",
            str(out),
        ]
    )


def mux_vo(video: Path, vo: Path, out: Path) -> None:
    run(
        [
            *ffmpeg_base(),
            "-i",
            str(video),
            "-i",
            str(vo),
            "-c:v",
            "copy",
            "-c:a",
            "aac",
            "-b:a",
            "128k",
            "-shortest",
            str(out),
        ]
    )


def ts(seconds: float) -> str:
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    ms = int(round((seconds - int(seconds)) * 1000))
    if ms == 1000:
        s += 1
        ms = 0
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def write_captions(scenes: list[Scene]) -> None:
    srt_lines: list[str] = []
    vtt_lines = ["WEBVTT", ""]
    for i, sc in enumerate(scenes, 1):
        start, end = sc.start, sc.end
        srt_lines.append(str(i))
        srt_lines.append(f"{ts(start)} --> {ts(end)}")
        srt_lines.append(sc.vo)
        srt_lines.append("")
        vtt_lines.append(f"{ts(start).replace(',', '.')} --> {ts(end).replace(',', '.')}")
        vtt_lines.append(sc.vo)
        vtt_lines.append("")
    OUT_SRT.write_text("\n".join(srt_lines), encoding="utf-8")
    OUT_VTT.write_text("\n".join(vtt_lines), encoding="utf-8")


def main() -> None:
    src = source_video()
    print(f"Source: {src}")
    segs = PROD / "segments"
    segs.mkdir(parents=True, exist_ok=True)
    parts: list[Path] = []

    for scene in SCENES:
        print(f"Scene {scene.idx}: {scene.title} ({scene.dur:.1f}s)")
        clip = segs / f"s{scene.idx:02d}-clip.mp4"
        extract_padded_clip(src, scene, clip)
        overlay = PROD / "overlays" / f"scene{scene.idx:02d}.png"
        render_overlay(scene, overlay)
        burned = segs / f"s{scene.idx:02d}-burn.mp4"
        burn_overlay(clip, overlay, burned)
        vo = PROD / "vo" / f"scene{scene.idx:02d}.mp3"
        make_vo(scene, vo)
        mixed = segs / f"s{scene.idx:02d}.mp4"
        mux_vo(burned, vo, mixed)
        parts.append(mixed)

    lst = PROD / "concat.txt"
    lst.write_text("\n".join(f"file '{p}'" for p in parts))
    master = segs / "master-no-music.mp4"
    run(
        [
            *ffmpeg_base(),
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(lst),
            "-c",
            "copy",
            str(master),
        ]
    )

    total = ffprobe_dur(master)
    fade_start = max(0.5, total - 3.0)
    if MUSIC.exists():
        run(
            [
                *ffmpeg_base(),
                "-i",
                str(master),
                "-i",
                str(MUSIC),
                "-filter_complex",
                (
                    f"[1:a]volume=0.12,afade=t=in:st=0:d=1.5,"
                    f"atrim=0:{total:.3f},afade=t=out:st={fade_start:.3f}:d=3[m];"
                    f"[0:a][m]amix=inputs=2:duration=first:dropout_transition=2,"
                    f"volume=1.15[a]"
                ),
                "-map",
                "0:v",
                "-map",
                "[a]",
                "-c:v",
                "copy",
                "-c:a",
                "aac",
                "-b:a",
                "160k",
                "-movflags",
                "+faststart",
                str(OUT_MP4),
            ]
        )
    else:
        run(
            [
                *ffmpeg_base(),
                "-i",
                str(master),
                "-c",
                "copy",
                "-movflags",
                "+faststart",
                str(OUT_MP4),
            ]
        )

    write_captions(SCENES)
    meta = {
        "title": "SheikhGo Nexus — Mobile app promo",
        "durationSec": round(ffprobe_dur(OUT_MP4), 2),
        "source": str(src.name),
        "ecoNexusBeats": [1, 4],
        "claims": [
            "splash",
            "login",
            "dashboard",
            "live map",
            "vehicles",
            "trips",
            "role shell",
        ],
        "outputs": [OUT_MP4.name, OUT_VTT.name, OUT_SRT.name],
    }
    (PROD / "build-meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"Wrote {OUT_MP4} ({meta['durationSec']}s)")
    print(f"Captions: {OUT_VTT.name}, {OUT_SRT.name}")


if __name__ == "__main__":
    main()
