# SheikhGo Driver — Release Notes

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
