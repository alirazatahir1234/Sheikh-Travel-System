# GT06 / JIMI VG03 clock offset — history repair

**Problem (audit 26 Nov 2026):** the three JIMI VG03 trackers (Traccar ids 19, 23, 26) report
`fixTime` 5 hours behind `serverTime`. Playback shows morning trips (07:00–09:45 PKT) as
night trips (02:00–04:45 PKT). Example: BWY 217, Talwandi → Pasrur, shown 02:15, real 07:15.

## Order matters

1. **Fix the root cause first**, or new data keeps arriving wrong:
   - Traccar VM: check `traccar.xml` for `decoder.timezone` / `gt06.timezone`. Remove and restart Traccar.
   - If neither is set: set the trackers to GMT+0 (command from the JIMI manual).
   - Confirm: `GET /api/positions?deviceId=23` → `fixTime` and `serverTime` within seconds.
2. **Find the Traccar DB type**: `database.driver` in `traccar.xml` (MySQL, PostgreSQL or H2).
3. **Back up Traccar**, e.g. MySQL: `mysqldump traccar tc_positions tc_events > traccar_pre_clockfix.sql`.
   H2: stop Traccar and copy the `data/database.mv.db` file.
4. Run **`01_traccar_inspect.sql`** (read-only). Note `first_bad_fix` / `last_bad_fix` per device.
   Query B must show no normal days inside the bad period.
5. Run **`02_traccar_fix.sql`**. Check the final SELECT (`bad_rows = 0`, `fix_in_future = 0`),
   then `COMMIT;` by hand. Traccar's trips, stops and summary reports are computed from
   `tc_positions`, so they are correct right away.
   In `01` and `02`, replace `'2099-01-01 00:00:00'` (marked `ROOT_FIXED_AT_UTC`) with the UTC time
   of the root-cause fix, so correct data received afterwards is never touched.
6. Back up the SheikhGo database. Edit **`03_sheikhgo_fix.sql`**: set `@TenantId`,
   `@SafeguardLiveFromUtc` (when the API with the clock-skew safeguard went live in that environment;
   it stores corrected times from then on) and fill `@Window` with the values from step 4.
   Run once with `@Apply = 0` (dry run) and check the counts, then run again with `@Apply = 1`.

## Verify by hand

- ERP → GPS → History/Playback, BWY 217, 24 Nov 2026: the Talwandi → Pasrur run starts around 07:15.
- Mobile app → Vehicle → Playback, same day: same times.
- Trips list for 14–24 Nov: no trips between 00:00 and 05:00 for these vehicles.

## The API safeguard

Since `fix/gps-tracker-clock-skew`, the API detects trackers whose clock is whole hours off and
corrects times on every Traccar read (playback, history, trips, stops, reports, ingest). Look for
`Tracker clock skew detected for Traccar device {id}` in the API log. The scripts here are still
needed: Traccar's own UI and anything reading the Traccar DB directly see the raw times.

## Safety notes

- `02` is idempotent (each row is checked for the 5h lag). `03` refuses to run twice for the same window
  (`dbo.OpsGpsClockCorrections`, created by the script).
- Rows with a lag over 9.5 hours (tracker buffered for a long time) are left alone;
  query A reports them as `left_alone_rows`.
