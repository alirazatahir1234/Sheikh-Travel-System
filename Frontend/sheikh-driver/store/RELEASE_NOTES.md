# SheikhGo Tracking — Release Notes

## 1.0.5 (build 28) — 2026-11-12

### Fixes
- Keep prod API on HTTPS `https://20-174-1-230.sslip.io/api` (do not ship cleartext `:5082` — carriers often return HTTP 302 on mobile data while Wi‑Fi still works)
- Harden TLS error detection so `sslip.io` hostnames are not mislabeled "Secure connection failed"
- `build_prod.sh` refuses cleartext `http://` API_BASE_URL overrides for release builds

## 1.0.4 (build 24) — 2026-10-22

### Fixes & polish
- Biometric lock: `FlutterFragmentActivity` + AppCompat; resume lock paused when app backgrounded/hidden
- Offline queue UI revision and cold-start drain
- Attendance JSON/GPS parsing fixes
- Dark mode contrast for legal/privacy/terms, AI, and More screens
- Version/env clutter cleanup in Settings
- Arabic (AR) localization expansion
- Background GPS copy review (store justification strings)

## 1.0.3 (build 23) — 2026-10-22

### Fixes
- Production API `https://joke-rebates-titans-duties.trycloudflare.com/api` (Cloudflare quick tunnel; replaces unreachable sslip / retired Railway)
- `build_prod.sh` refuses `railway.app` and `trycloudflare.com` bake-ins
- Auth-path 404 → “Cannot reach login API…” (not generic “resource not found”)
- Login footer shows API host for support screenshots
- Staff login email normalized to lowercase; in-app version matches pubspec
- Login FailResponse / Maps key inject / 401·429 UX from 1.0.2+22

## 1.0.2 (build 22) — 2026-10-22

### Fixes
- Login FailResponse (`success:false` + HTTP 200) no longer treated as success
- Default production API → `https://joke-rebates-titans-duties.trycloudflare.com/api` (live VM quick-tunnel)
- Release build script injects Maps key from `android/maps.properties`
- Clearer 401 (auth paths) / 429 rate-limit messages; emulator block copy for testers

## 1.0.0 (build 1) — 2026-07-19

### Highlights
- Trip lifecycle: Accept → Arrived → Onboard → Enroute → Complete  
- In-app Google Maps navigation with ETA and external map deep links  
- Attendance, fuel receipts (OCR assist), inspections, documents  
- Earnings summary (today / week / month / pending / paid)  
- Push notifications with categories, archive, and deep links  
- Offline outbox with auto-sync for key actions  
- Background GPS with Android foreground service & iOS background modes  
- Production security: cert pinning hooks, integrity checks, device registration  
- Crashlytics + Analytics for release diagnostics  

### Known limitations
- Turn-by-turn voice guidance uses external map apps  
- Store privacy/terms URLs must be hosted publicly before submission  
- Release signing keystore must be configured per environment (see RELEASE_CHECKLIST.md)
