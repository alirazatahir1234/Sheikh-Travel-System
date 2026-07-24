# Screenshot & Marketing Asset Guide

Capture on a **physical device** (or high-quality emulator) with demo tenant data. Prefer light theme unless dark is brand-standard.

## Required set (minimum)

| # | Screen | Notes |
|---|--------|-------|
| 1 | Login | Brand “SheikhGo Driver” visible |
| 2 | Dashboard | KPIs / today’s trips |
| 3 | Trip list | Mix of statuses |
| 4 | Trip detail + lifecycle actions | Accept / Arrived visible |
| 5 | Navigation map | Route line + ETA chip |
| 6 | Live / GPS | Map with tracking indicator |
| 7 | Attendance | Check-in UI |
| 8 | Earnings | Period cards |

Optional: fuel upload, inspection checklist, notifications, documents.

## Device sizes

**Google Play**
- Phone: 1080×1920 or higher (portrait)
- 7" tablet (optional): 1200×1920
- Feature graphic: 1024×500

**App Store**
- 6.7" iPhone: 1290×2796
- 6.5" iPhone: 1284×2778 (if still required by ASC)
- iPad 12.9" (if you support iPad): 2048×2732

## Brand rules
- No fake star ratings or “#1” badges on screenshots  
- Do not show real passenger PII; use demo names  
- Blur map house numbers if needed for privacy demos  

## App icons
Launcher icons live under:
- `android/app/src/main/res/mipmap-*/ic_launcher.png`
- `ios/Runner/Assets.xcassets/AppIcon.appiconset/`

Regenerate with:

```bash
cd Frontend/sheikh-driver
dart run flutter_launcher_icons
```

(Requires `flutter_launcher_icons` config in `pubspec.yaml` and a source `assets/icon/app_icon.png`.)

## Folder for exports

Place final PNGs in `store/screenshots/` (git-ignore large binaries if preferred; keep a checklist here).
