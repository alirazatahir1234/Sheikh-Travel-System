# GPS Operator V1 — Field Validation Checklist

Use a **physical Android/iOS device** (not macOS for field test) with LAN API:

```bash
flutter run --dart-define=API_BASE_URL=http://<YOUR-LAN-IP>:5082/api
```

Assign the test user the platform role **`GPS_OPERATOR`** (ERP Users → roles). Restart API once so `GpsOperatorRoleTemplateMigration` applies.

## Auth & shell

- [ ] Splash appears, then login (or dashboard if session restores)
- [ ] Login as GPS Operator → bottom nav: **Dashboard · Live Map · Vehicles · Alerts · More**
- [ ] Reports accessible from **More** (not bottom tab)
- [ ] GPS commands, Incident center, Geofences, AI insights in More
- [ ] Session expiry → Session expired screen → Sign in again
- [ ] Forgot password opens guided admin-reset flow

## Map & alerts (control room)

- [ ] Live map connection dot (green/amber/red) reflects refresh age
- [ ] Map layers toggle, vehicle search, expanded sheet with Navigate / Commands
- [ ] Nearest geofence label on vehicle sheet when fences exist
- [ ] Alerts severity lanes show counts; bulk select → mark read / resolve
- [ ] Vehicle detail Alerts tab lists last 7 days with ACK

## Export & AI

- [ ] History playback → Share summary exports text
- [ ] Export GPX from share menu
- [ ] Export CSV from share menu
- [ ] Header keeps Share + Refresh; statistics/layers under overflow menu
- [ ] Playback shows vehicle name + plate above date control
- [ ] Date range uses one preset dropdown (Today/Yesterday/24h/3d/7d/Custom)
- [ ] Trip summary card above map (distance, duration, stops, avg/max)
- [ ] Color-coded route, start/finish markers, follow vehicle
- [ ] Timeline slider has thicker track/thumb and is easy to scrub
- [ ] Footer shows labeled Time / Speed / Distance + playback status/progress
- [ ] Event timeline chips jump playback (stop, overspeed, geofence, fuel, SOS)
- [ ] Map legend explains marker colors
- [ ] More → AI insights runs scoped server query; Copilot link still works

## Friend’s vehicle monitoring

- [ ] Dashboard KPIs show Online / Moving / Parked / Idle / Offline
- [ ] Vehicles list shows the tracker vehicle with speed / ignition / last update
- [ ] Vehicle detail → Overview matches live fields
- [ ] Live Map updates within ~20s refresh; Follow centers on selected vehicle
- [ ] Live panel shows Details + Playback actions
- [ ] Playback 24h loads route + distance; Play / Pause / speed (1x–4x) work
- [ ] Alerts list opens; acknowledge/resolve if permission allows
- [ ] More → GPS trips / Fuel analytics / Mileage open without crash
- [ ] Vehicle → Device health shows IMEI / battery / online; commands sheet if permitted

## Regression

- [ ] Driver / Dispatcher / Fleet Manager shells still resolve correctly for those roles
- [ ] Menu Management / ERP GPS still work (mobile does not replace them)

## Notes

| Item | Value |
|------|--------|
| Vehicle plate / name | |
| Tracker IMEI | |
| API base URL | |
| Tester | |
| Date | |
| Pass / Fail | |
