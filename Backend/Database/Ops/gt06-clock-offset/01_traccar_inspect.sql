-- =====================================================================
-- 01  Traccar — INSPECT ONLY (no writes)
-- GT06 / JIMI VG03 trackers report fixTime 5h behind serverTime.
-- Run against the TRACCAR database (not the SheikhGo SQL Server).
--
-- Syntax: MySQL / MariaDB / H2.  For PostgreSQL replace
--   TIMESTAMPDIFF(MINUTE, a, b)  ->  (EXTRACT(EPOCH FROM (b - a)) / 60)
--
-- Affected Traccar device ids (from the 26-Nov-2026 audit):
--   19 Jimi VG03 · 23 Ali BWY 217 · 26 JIMI VG03 - Hannan Khokhar
-- ROOT_FIXED_AT_UTC: replace '2099-01-01 00:00:00' with the UTC time the root cause was fixed
-- (decoder.timezone removed / trackers set to GMT+0). Rows received after that are correct even
-- if they were buffered for hours, so they must never be counted or shifted.
-- A row is "bad" when servertime - fixtime is 290..569 minutes
-- (5h clock error + up to ~4.5h of tracker buffering).
-- =====================================================================

-- A. Per device: how many rows are bad, and the bad window on the fixtime scale.
--    Copy first_bad_fix / last_bad_fix into 03_sheikhgo_fix.sql.
SELECT
    deviceid,
    COUNT(*)                                                                                   AS total_rows,
    SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569 THEN 1 ELSE 0 END) AS bad_rows,
    SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) < 290            THEN 1 ELSE 0 END) AS normal_rows,
    SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) >= 570           THEN 1 ELSE 0 END) AS left_alone_rows,
    MIN(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569 THEN fixtime END) AS first_bad_fix,
    MAX(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569 THEN fixtime END) AS last_bad_fix
FROM tc_positions
WHERE deviceid IN (19, 23, 26)
  AND servertime < '2099-01-01 00:00:00' /* ROOT_FIXED_AT_UTC */
GROUP BY deviceid
ORDER BY deviceid;

-- B. Per device per day: confirm "normal" rows are NOT mixed into the bad period.
--    Expected: every day from the start of the problem is (almost) all bad.
--    If you see days with many normal rows INSIDE the bad window, stop and ask —
--    the SheikhGo script shifts the whole window.
SELECT
    deviceid,
    CAST(servertime AS DATE)                                                                   AS received_day,
    SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) BETWEEN 290 AND 569 THEN 1 ELSE 0 END) AS bad_rows,
    SUM(CASE WHEN TIMESTAMPDIFF(MINUTE, fixtime, servertime) < 290            THEN 1 ELSE 0 END) AS normal_rows
FROM tc_positions
WHERE deviceid IN (19, 23, 26)
GROUP BY deviceid, CAST(servertime AS DATE)
ORDER BY deviceid, received_day;

-- C. Events that carry the bad position time (these get shifted too).
SELECT e.deviceid, e.type, COUNT(*) AS events_to_shift
FROM tc_events e
WHERE e.deviceid IN (19, 23, 26)
  AND EXISTS (
        SELECT 1 FROM tc_positions p
        WHERE p.id = e.positionid
          AND p.fixtime = e.eventtime
          AND p.servertime < '2099-01-01 00:00:00' /* ROOT_FIXED_AT_UTC */
          AND TIMESTAMPDIFF(MINUTE, p.fixtime, p.servertime) BETWEEN 290 AND 569)
GROUP BY e.deviceid, e.type
ORDER BY e.deviceid, e.type;
