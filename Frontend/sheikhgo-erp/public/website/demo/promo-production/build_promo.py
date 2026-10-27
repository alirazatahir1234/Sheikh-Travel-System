#!/usr/bin/env python3
"""Assemble SheikhGo Nexus 120s promo (12-scene brief).

Visual policy (2026-10 re-render):
- Brighter grading, light overlays (UI readable behind text zones)
- Stable stills — no Ken Burns / zoompan / screen shake
- Copy: VIDEO-COPY-BANK.md (§A Eco Nexus on open/GPS + §B original VO)
- CTA: https://www.sheikhgo.com
"""
from __future__ import annotations

import json
import os
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]  # public/
WEB = ROOT / "website"
BRAND = ROOT / "brand" / "sheikhgo-logo-white.png"
PROD = Path(__file__).resolve().parent
OUT_MP4 = WEB / "demo" / "sheikhgo-nexus-promo-120s.mp4"
OUT_SRT = WEB / "demo" / "sheikhgo-nexus-promo-120s.srt"
OUT_VTT = WEB / "demo" / "sheikhgo-nexus-promo-120s.vtt"
MUSIC = Path(
    "/Users/alirazatahir/Projects/Sheikh-Travel-System/docs/demo/audio-src/mixkit-motivating-mornings-33.mp3"
)
VOICE = "en-US-GuyNeural"
W, H, FPS = 1280, 720, 30
FFMPEG_THREADS = os.environ.get("PROMO_FFMPEG_THREADS", "1")
REUSE_VO = os.environ.get("PROMO_REUSE_VO", "1") != "0"
START_SCENE = int(os.environ.get("PROMO_START_SCENE", "1"))
TTS_RETRIES = int(os.environ.get("PROMO_TTS_RETRIES", "4"))


@dataclass
class Scene:
    idx: int
    start: float
    end: float
    title: str
    vo: str
    images: list[Path]
    broll: Path | None = None

    @property
    def dur(self) -> float:
        return self.end - self.start


# §A Eco Nexus on scenes 1–2; §B SheikhGo Nexus VO for 3–12 (VIDEO-COPY-BANK.md)
SCENES: list[Scene] = [
    Scene(
        1,
        0,
        8,
        "Every journey matters",
        "Every journey matters. Eco Nexus brings your entire fleet into one intelligent workplace.",
        [WEB / "gps-alerts" / "hero-bg.jpg", WEB / "product" / "live-map.png", BRAND],
    ),
    Scene(
        2,
        8,
        20,
        "Real-time GPS",
        "Track every vehicle real-time. Live maps, geofencing, trip playback, and instant GPS and power alerts keep dispatch in control.",
        [
            WEB / "nexus" / "modules" / "dashboard.png",
            WEB / "nexus" / "modules" / "live-tracking.png",
            WEB / "nexus" / "modules" / "gps-alerts.png",
        ],
    ),
    Scene(
        3,
        20,
        32,
        "Health · Drivers · Maintenance · AI assign",
        "See vehicle health and driver performance together. Predictive maintenance signals and AI-assisted trip assignment help you act before small issues become downtime.",
        [
            WEB / "nexus" / "modules" / "maintenance.png",
            WEB / "nexus" / "modules" / "drivers.png",
            WEB / "nexus" / "modules" / "vehicles.png",
            WEB / "nexus" / "modules" / "trips.png",
        ],
    ),
    Scene(
        4,
        32,
        44,
        "Fuel intelligence",
        "Fuel intelligence surfaces unusual consumption patterns. Flag suspected anomalies, investigate receipts, and protect margin without guessing.",
        [WEB / "nexus" / "modules" / "fuel.png", WEB / "gps-alerts" / "scene-health.jpg"],
    ),
    Scene(
        5,
        44,
        54,
        "EV fleet",
        "Manage electric vehicles alongside your fleet. Monitor battery, range, and charging status in the same operations view.",
        [WEB / "nexus" / "modules" / "vehicles.png", WEB / "sheikhgo-fleet-hero.png"],
    ),
    Scene(
        6,
        54,
        66,
        "Road safety alerts",
        "Road safety alerts cover speed limits, route deviation, night driving, and sudden speed drops — so managers see risk while trips are still live.",
        [
            WEB / "gps-alerts" / "scene-road-speed.jpg",
            WEB / "gps-alerts" / "scene-safety.jpg",
            WEB / "gps-alerts" / "scene-tracking.jpg",
        ],
    ),
    Scene(
        7,
        66,
        74,
        "Document tracker",
        "Document tracker keeps registration, insurance, and fitness expiry visible — with reminders before vehicles go out of compliance.",
        [WEB / "nexus" / "modules" / "drivers.png", WEB / "nexus" / "modules" / "vehicles.png"],
    ),
    Scene(
        8,
        74,
        82,
        "Customer ratings",
        "Customer ratings capture politeness and on-time performance, giving operations a fair view of service quality trip by trip.",
        [WEB / "nexus" / "modules" / "customers.png"],
    ),
    Scene(
        9,
        82,
        94,
        "AI Assistant · Sample demo data",
        "Ask the AI assistant practical fleet questions — like which routes drive the highest fuel costs. Sample demo data in this preview; production uses your tenant with Ollama or DeepInfra when you configure it.",
        [WEB / "nexus" / "modules" / "ai-assistant.png", WEB / "nexus" / "fleet-intelligence-dashboard.jpg"],
    ),
    Scene(
        10,
        94,
        102,
        "SOS · Pakistan & Dubai",
        "Driver SOS workflows alert your team with live location context. Regional recovery partners support Pakistan and Dubai operations — without guaranteed arrival times.",
        [WEB / "nexus" / "modules" / "notifications.png", WEB / "gps-alerts" / "scene-safety.jpg"],
    ),
    Scene(
        11,
        102,
        110,
        "Reporting · Secure video",
        "Reporting and secure video tie trips, alerts, and operations into audit-ready views for managers and compliance.",
        [WEB / "nexus" / "modules" / "reports.png", WEB / "product" / "reports.png"],
    ),
    Scene(
        12,
        110,
        120,
        "Book a Demo",
        "Explore tracking and features on sheikhgo.com. Book a demo and see SheikhGo Nexus built for fleets in Dubai and beyond.",
        [WEB / "nexus" / "fleet-intelligence-dashboard.jpg", BRAND],
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


FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
FONT_REG = "/System/Library/Fonts/Supplemental/Arial.ttf"


def render_text_overlay(scene: Scene, out: Path) -> Path:
    out.parent.mkdir(parents=True, exist_ok=True)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    header_font = ImageFont.truetype(FONT_BOLD, 22)
    title_font = ImageFont.truetype(FONT_BOLD, 34)
    sub_font = ImageFont.truetype(FONT_REG, 22)

    header = "SHEIKHGO — SMARTER FLEET MANAGEMENT"
    subtitle = ""
    if scene.idx == 12:
        subtitle = "Book a Demo · https://www.sheikhgo.com · Dubai office"
    elif scene.idx == 1:
        subtitle = "Eco Nexus · intelligent workplace"
    elif scene.idx == 2:
        subtitle = "Track every vehicle real-time"
    elif scene.idx == 9:
        subtitle = "Sample demo data shown"
    elif scene.idx == 4:
        subtitle = "Suspected anomalies — investigate"
    elif scene.idx == 10:
        subtitle = "No guaranteed ETAs"

    box_fill = (0, 0, 0, 58)

    def text_box(text: str, font, y: int, fill=(255, 255, 255, 245)) -> None:
        bbox = draw.textbbox((0, 0), text, font=font)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
        x = (W - tw) // 2
        pad = 10
        draw.rounded_rectangle(
            (x - pad, y - pad, x + tw + pad, y + th + pad),
            radius=8,
            fill=box_fill,
        )
        draw.text((x, y), text, font=font, fill=fill)

    text_box(header, header_font, 36, fill=(255, 255, 255, 235))
    text_box(scene.title, title_font, H - 120, fill=(255, 232, 150, 255))
    if subtitle:
        text_box(subtitle, sub_font, H - 68)

    img.save(out)
    return out


def _pad_vo_to_scene(src: Path, scene: Scene, out: Path) -> float:
    dur = ffprobe_dur(src)
    max_dur = scene.dur - 0.35
    fit = PROD / "vo" / f"scene{scene.idx:02d}-fit.mp3"
    if dur > max_dur:
        rate = min(dur / max_dur, 1.35)
        run([*ffmpeg_base(), "-i", str(src), "-filter:a", f"atempo={rate:.4f}", str(fit)])
        dur = ffprobe_dur(fit)
    else:
        fit.write_bytes(src.read_bytes())
    pad = scene.dur - dur
    if pad > 0.05:
        padded = PROD / "vo" / f"scene{scene.idx:02d}.mp3"
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
                str(padded),
            ]
        )
        out.write_bytes(padded.read_bytes())
    else:
        out.write_bytes(fit.read_bytes())
    return min(dur, max_dur)


def make_vo(scene: Scene, out: Path) -> float:
    raw = PROD / "vo" / f"scene{scene.idx:02d}-raw.mp3"
    legacy = PROD / "vo" / f"scene{scene.idx:02d}.mp3"
    text_marker = PROD / "vo" / f"scene{scene.idx:02d}-text.txt"
    raw.parent.mkdir(parents=True, exist_ok=True)
    prior = text_marker.read_text(encoding="utf-8") if text_marker.exists() else ""
    if REUSE_VO and prior == scene.vo and out.exists() and abs(ffprobe_dur(out) - scene.dur) < 0.4:
        print(f"  reuse VO scene {scene.idx}")
        return min(ffprobe_dur(out), scene.dur - 0.35)

    last_err: Exception | None = None
    for attempt in range(1, TTS_RETRIES + 1):
        try:
            if raw.exists() and raw.stat().st_size == 0:
                raw.unlink()
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
            if raw.exists() and raw.stat().st_size > 1000:
                last_err = None
                break
            raise RuntimeError(f"edge-tts produced empty file for scene {scene.idx}")
        except Exception as exc:  # noqa: BLE001
            last_err = exc
            print(f"  TTS attempt {attempt}/{TTS_RETRIES} failed: {exc}")
            time.sleep(2 * attempt)

    if last_err is not None:
        fallback = raw if raw.exists() and raw.stat().st_size > 1000 else None
        if fallback is None and legacy.exists() and legacy.stat().st_size > 1000:
            fallback = legacy
        if fallback is None and out.exists() and out.stat().st_size > 1000:
            print(f"  keep existing VO scene {scene.idx} after TTS failure")
            text_marker.write_text(scene.vo, encoding="utf-8")
            return min(ffprobe_dur(out), scene.dur - 0.35)
        if fallback is None:
            raise last_err
        print(f"  fallback VO from {fallback.name} for scene {scene.idx}")
        text_marker.write_text(scene.vo, encoding="utf-8")
        return _pad_vo_to_scene(fallback, scene, out)

    text_marker.write_text(scene.vo, encoding="utf-8")
    return _pad_vo_to_scene(raw, scene, out)


def stable_clip(img: Path, dur: float, out: Path, dark: float = 0.12) -> None:
    """Hold a still frame — no zoompan / Ken Burns / shake."""
    eq_b = max(-0.08, min(0.10, 0.06 - dark))
    vf = (
        f"scale={W}:{H}:force_original_aspect_ratio=increase,"
        f"crop={W}:{H},"
        f"eq=brightness={eq_b:.3f}:contrast=1.04:saturation=1.05,"
        f"fps={FPS},format=yuv420p"
    )
    run(
        [
            *ffmpeg_base(),
            "-loop",
            "1",
            "-i",
            str(img),
            "-t",
            f"{dur:.3f}",
            "-vf",
            vf,
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "20",
            "-pix_fmt",
            "yuv420p",
            "-an",
            str(out),
        ]
    )


def logo_clip(dur: float, out: Path) -> None:
    run(
        [
            *ffmpeg_base(),
            "-f",
            "lavfi",
            "-i",
            f"color=c=0x12253a:s={W}x{H}:d={dur:.3f}:r={FPS}",
            "-i",
            str(BRAND),
            "-filter_complex",
            "[0:v]format=yuv420p[bg];[1:v]scale=420:-1[lg];[bg][lg]overlay=(W-w)/2:(H-h)/2-30",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "20",
            "-an",
            str(out),
        ]
    )


def concat_videos(parts: list[Path], out: Path) -> None:
    lst = PROD / "concat.txt"
    lst.write_text("\n".join(f"file '{p}'" for p in parts))
    run([*ffmpeg_base(), "-f", "concat", "-safe", "0", "-i", str(lst), "-c", "copy", str(out)])


def build_scene_video(scene: Scene, out: Path) -> None:
    parts_dir = PROD / "segments"
    parts_dir.mkdir(parents=True, exist_ok=True)
    imgs = scene.images
    if scene.idx == 1 and len(imgs) >= 3:
        d1, d2, d3 = scene.dur * 0.45, scene.dur * 0.35, scene.dur * 0.20
        p1 = parts_dir / f"s{scene.idx}a.mp4"
        p2 = parts_dir / f"s{scene.idx}b.mp4"
        p3 = parts_dir / f"s{scene.idx}c.mp4"
        stable_clip(imgs[0], d1, p1, dark=0.10)
        stable_clip(imgs[1], d2, p2, dark=0.14)
        logo_clip(d3, p3)
        concat_videos([p1, p2, p3], out)
        return
    if scene.idx == 12 and len(imgs) >= 2:
        d1, d2 = scene.dur * 0.65, scene.dur * 0.35
        p1 = parts_dir / f"s{scene.idx}a.mp4"
        p2 = parts_dir / f"s{scene.idx}c.mp4"
        stable_clip(imgs[0], d1, p1, dark=0.14)
        logo_clip(d2, p2)
        concat_videos([p1, p2], out)
        return
    per = scene.dur / len(imgs)
    parts = []
    for i, img in enumerate(imgs):
        p = parts_dir / f"s{scene.idx}{chr(97 + i)}.mp4"
        if img.suffix.lower() == ".png" and "logo" in img.name:
            logo_clip(per, p)
        else:
            stable_clip(img, per, p, dark=0.12)
        parts.append(p)
    concat_videos(parts, out)


def overlay_text(scene: Scene, inp: Path, out: Path) -> None:
    text_png = PROD / "overlays" / f"scene{scene.idx:02d}.png"
    render_text_overlay(scene, text_png)
    wm = PROD / "wm.png"
    if not wm.exists():
        run([*ffmpeg_base(), "-i", str(BRAND), "-vf", "scale=140:-1", str(wm)])
    run(
        [
            *ffmpeg_base(),
            "-i",
            str(inp),
            "-i",
            str(text_png),
            "-i",
            str(wm),
            "-filter_complex",
            "[0:v][1:v]overlay=0:0[v1];[v1][2:v]overlay=W-w-24:24",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "20",
            "-an",
            str(out),
        ]
    )


def mux_scene(scene: Scene, vid: Path, vo: Path, out: Path) -> None:
    run(
        [
            *ffmpeg_base(),
            "-i",
            str(vid),
            "-i",
            str(vo),
            "-map",
            "0:v",
            "-map",
            "1:a",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "20",
            "-c:a",
            "aac",
            "-b:a",
            "192k",
            "-shortest",
            str(out),
        ]
    )


def _ts_srt(t: float) -> str:
    h = int(t // 3600)
    m = int((t % 3600) // 60)
    sec = int(t % 60)
    ms = int(round((t - int(t)) * 1000))
    return f"{h:02d}:{m:02d}:{sec:02d},{ms:03d}"


def _ts_vtt(t: float) -> str:
    return _ts_srt(t).replace(",", ".")


def build_captions(scenes: list[Scene]) -> None:
    srt: list[str] = []
    vtt: list[str] = ["WEBVTT", ""]
    for i, s in enumerate(scenes, 1):
        srt.extend([str(i), f"{_ts_srt(s.start)} --> {_ts_srt(s.end)}", s.vo, ""])
        vtt.extend([str(i), f"{_ts_vtt(s.start)} --> {_ts_vtt(s.end)}", s.vo, ""])
    OUT_SRT.write_text("\n".join(srt), encoding="utf-8")
    OUT_VTT.write_text("\n".join(vtt), encoding="utf-8")


def main() -> None:
    PROD.mkdir(parents=True, exist_ok=True)
    (PROD / "vo").mkdir(exist_ok=True)
    (PROD / "segments").mkdir(exist_ok=True)
    scene_finals: list[Path] = []
    meta = []
    for scene in SCENES:
        final = PROD / "segments" / f"scene{scene.idx:02d}-final.mp4"
        if scene.idx < START_SCENE:
            if not final.exists():
                raise SystemExit(f"Missing prior final for scene {scene.idx}; cannot resume")
            print(f"Scene {scene.idx}… skip (resume from {START_SCENE})")
            scene_finals.append(final)
            meta.append(
                {
                    "scene": scene.idx,
                    "vo_sec": None,
                    "target": scene.dur,
                    "vo": scene.vo,
                    "skipped": True,
                }
            )
            continue
        print(f"Scene {scene.idx}…")
        base = PROD / "segments" / f"scene{scene.idx:02d}-base.mp4"
        titled = PROD / "segments" / f"scene{scene.idx:02d}-titled.mp4"
        vo_path = PROD / "vo" / f"scene{scene.idx:02d}-vo.mp3"
        build_scene_video(scene, base)
        overlay_text(scene, base, titled)
        vo_dur = make_vo(scene, vo_path)
        mux_scene(scene, titled, vo_path, final)
        for p in (base, titled):
            if p.exists():
                p.unlink()
        scene_finals.append(final)
        meta.append({"scene": scene.idx, "vo_sec": vo_dur, "target": scene.dur, "vo": scene.vo})

    concat = PROD / "video-narration.mp4"
    concat_videos(scene_finals, concat)

    run(
        [
            *ffmpeg_base(),
            "-i",
            str(concat),
            "-stream_loop",
            "-1",
            "-i",
            str(MUSIC),
            "-filter_complex",
            "[1:a]volume=0.14,afade=t=in:st=0:d=2,afade=t=out:st=115:d=5[m];"
            "[0:a][m]amix=inputs=2:duration=first:dropout_transition=2[aout]",
            "-map",
            "0:v",
            "-map",
            "[aout]",
            "-t",
            "120",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "18",
            "-c:a",
            "aac",
            "-b:a",
            "192k",
            "-movflags",
            "+faststart",
            str(OUT_MP4),
        ]
    )
    build_captions(SCENES)
    (PROD / "build-meta.json").write_text(json.dumps(meta, indent=2))
    dur = ffprobe_dur(OUT_MP4)
    print(f"Done: {OUT_MP4} ({dur:.2f}s)")
    print(f"Captions: {OUT_SRT.name} + {OUT_VTT.name}")


if __name__ == "__main__":
    main()
