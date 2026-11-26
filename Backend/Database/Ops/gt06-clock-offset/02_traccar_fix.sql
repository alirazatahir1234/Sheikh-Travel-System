-- =====================================================================
-- 02  Traccar — FIX (writes)  ·  add 5h to fixtime / devicetime / eventtime
-- Run against the TRACCAR database, AFTER:
--   * the root cause is fixed (decoder.timezone removed or trackers set to GMT+0),
--   * a backup of tc_positions and tc_events exists (see README).
--
-- Safe to re-run: every update is guarded by the per-row lag
-- (servertime - fixtime between 290 and 569 minutes). After the shift the
-- lag is < 270 minutes, so a shifted row never matches again, and rows
-- recorded after the root-cause fix (lag ~0) are never touched.
--
-- ROOT_FIXED_AT_UTC: replace '2099-01-01 00:00:00' below (4 places) with the UTC time the root cause
-- was fixed, so a correct-clock upload buffered 5-9h after the fix is never shifted.
--
-- Syntax: MySQL / MariaDB / H2.  For PostgreSQL:
--   TIMESTAMPDIFF(MINUTE, a, b)  ->  (EXTRACT(EPOCH FROM (b - a)) / 60)
--   TIMESTAMPADD(HOUR, 5, x)     ->  (x + INTERVAL '5 hours')
--   START TRANSACTION            ->  BEGIN
-- H2 (embedded, Traccar's default): STOP Traccar first and use the H2 shell.
-- =====================================================================

START TRANSACTION;

-- 1) Events FIRST — matched to their position by the still-unshifted fixtime.
--    Events with no position (deviceOnline/Offline etc.) use server time and are left alone.
UPDATE tc_events
SET eventtime = TIMESTAMPADD(HOUR, 5, eventtime)
WHERE deviceid IN (19, 23, 26)
  AND EXISTS (
        SELECT 1 FROM tc_positions p
        WHERE p.id = tc_events.positionid
          AND p.fixtime = tc_events.eventtime
          AND p.servertime < '2099-01-01 00:00:00' /* ROOT_FIXED_AT_UTC */
          AND TIMESTAMPDIFF(MINUTE, p.fixtime, p.servertime) BETWEEN 290 AND 569);

-- 2) Positions. devicetime is shifted only when it carries the same error.
UPDATE tc_positions
SET devicetime = CASE
                   WHEN TIMESTAMPDIFF(MINUTE, devicetime, servertime) BETWEEN 290 AND 569
                   THEN TIMESTAMPADD(HOUR, 5, devicetime)
                   ELSE devicetime
                 END,
    fixtime    = TIMESTAMPADD(HOUR, 5, fixtime)
WHERE deviceid IN (19, 23, 26)
  AND servertime < '2099-01-01 00:00:00' /* ROOT_FIXED_AT_UTC */
  AND TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569;

-- 3) Check before committing: bad_rows must now be 0 for every device.
SELECT deviceid,
       SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569 THEN 1 ELSE 0 END) AS bad_rows,
       SUM(CASE WHEN fixtime > TIMESTAMPADD(MINUTE, 5, servertime) THEN 1 ELSE 0 END)                AS fix_in_future
FROM tc_positions
WHERE deviceid IN (19, 23, 26)
  AND servertime < '2099-01-01 00:00:00' /* ROOT_FIXED_AT_UTC */
GROUP BY deviceid;

-- bad_rows = 0 and fix_in_future = 0  ->  COMMIT;
-- anything else                       ->  ROLLBACK;
-- (Run one of them by hand — this script deliberately does not commit.)
