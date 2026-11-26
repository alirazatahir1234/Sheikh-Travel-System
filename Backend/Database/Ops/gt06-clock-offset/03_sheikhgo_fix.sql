-- =====================================================================
-- 03  SheikhGo SQL Server — shift our stored copies of the bad GPS times
--
-- Tables touched (all scoped to @TenantId):
--   GpsPositions      RecordedAt, CreatedAt   (ingest writes both = fix time)
--   VehicleTracking   [Timestamp], CreatedAt  (ingest writes both = fix time)
--   GpsAlertEvents    [Timestamp]             (per-row guard: CreatedAt is real receive time)
--   GpsTrips          StartTime, EndTime, LocalTripDate
-- Not touched: VehicleCurrentLocation / GpsDevices.LastSeenAt — the next ping
--   after the root-cause fix overwrites them.
--
-- Our tables have no "received at" column for positions, so the window per
-- device comes from 01_traccar_inspect.sql (query A: first_bad_fix / last_bad_fix,
-- taken BEFORE running 02). A log table blocks applying the same window twice.
--
-- Default is a DRY RUN: everything runs inside a transaction, counts are
-- printed, then it rolls back. Set @Apply = 1 to commit.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply           bit     = 0;                          -- 0 = dry run, 1 = commit
DECLARE @TenantId        int     = NULL;                       -- REQUIRED: tenant that owns the trackers
DECLARE @OffsetHours     int     = 5;
DECLARE @WindowsTimeZone sysname = N'Pakistan Standard Time';  -- for GpsTrips.LocalTripDate
-- REQUIRED: UTC time the API with the clock-skew safeguard went live in THIS environment.
-- From then on the API stores corrected times, so those rows must not be shifted again.
-- Shifting stops 1h before this point (uncorrected rows before it are always ≥5h older).
-- If the safeguard was never deployed here, use the time you run this script.
DECLARE @SafeguardLiveFromUtc datetime2 = NULL;

DECLARE @Window TABLE (
    TraccarDeviceId int       NOT NULL PRIMARY KEY,
    FirstBadFixUtc  datetime2 NOT NULL,
    LastBadFixUtc   datetime2 NOT NULL);

-- Fill from 01_traccar_inspect.sql query A (UTC, fixtime scale). Example values only:
INSERT INTO @Window (TraccarDeviceId, FirstBadFixUtc, LastBadFixUtc) VALUES
--  (23, '2026-10-01T00:00:00', '2026-11-26T00:39:39'),
--  (19, '...', '...'),
--  (26, '...', '...');
    (0, '1900-01-01', '1900-01-01');   -- placeholder row, rejected below

IF @TenantId IS NULL
    THROW 50001, 'Set @TenantId.', 1;
IF @SafeguardLiveFromUtc IS NULL
    THROW 50005, 'Set @SafeguardLiveFromUtc (when the clock-skew safeguard went live here).', 1;
IF EXISTS (SELECT 1 FROM @Window WHERE TraccarDeviceId = 0 OR LastBadFixUtc < FirstBadFixUtc)
    THROW 50002, 'Fill @Window from 01_traccar_inspect.sql (remove the placeholder row).', 1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OpsGpsClockCorrections', N'U') IS NULL
    CREATE TABLE dbo.OpsGpsClockCorrections (
        Id               int IDENTITY(1,1) PRIMARY KEY,
        TenantId         int       NOT NULL,
        GpsDeviceId      int       NOT NULL,
        TraccarDeviceId  int       NOT NULL,
        OffsetHours      int       NOT NULL,
        FirstBadFixUtc   datetime2 NOT NULL,
        LastBadFixUtc    datetime2 NOT NULL,
        PositionsShifted int       NOT NULL,
        TrackingShifted  int       NOT NULL,
        AlertsShifted    int       NOT NULL,
        TripsShifted     int       NOT NULL,
        AppliedAtUtc     datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AppliedBy        sysname   NOT NULL DEFAULT SUSER_SNAME());

-- Resolve our GpsDevices rows — tenant-scoped.
DECLARE @Dev TABLE (
    GpsDeviceId     int       NOT NULL PRIMARY KEY,
    TraccarDeviceId int       NOT NULL,
    FirstBadFixUtc  datetime2 NOT NULL,
    LastBadFixUtc   datetime2 NOT NULL);

INSERT INTO @Dev (GpsDeviceId, TraccarDeviceId, FirstBadFixUtc, LastBadFixUtc)
SELECT d.Id, w.TraccarDeviceId, w.FirstBadFixUtc,
       -- Never reach rows the safeguard already stored with corrected times.
       CASE WHEN w.LastBadFixUtc < DATEADD(HOUR, -1, @SafeguardLiveFromUtc)
            THEN w.LastBadFixUtc ELSE DATEADD(HOUR, -1, @SafeguardLiveFromUtc) END
FROM @Window w
JOIN GpsDevices d ON d.TraccarDeviceId = w.TraccarDeviceId
                 AND d.TenantId = @TenantId
                 AND d.IsDeleted = 0;

IF (SELECT COUNT(*) FROM @Dev) <> (SELECT COUNT(*) FROM @Window)
    THROW 50003, 'Not every Traccar device id maps to exactly one GpsDevices row for this tenant.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.OpsGpsClockCorrections c
    JOIN @Dev x ON x.GpsDeviceId = c.GpsDeviceId
    WHERE c.TenantId = @TenantId
      AND c.FirstBadFixUtc <= x.LastBadFixUtc
      AND c.LastBadFixUtc  >= x.FirstBadFixUtc)
    THROW 50004, 'An overlapping correction was already applied for one of these devices. Refusing to shift twice.', 1;

-- Vehicles these trackers fed during the bad window (captured BEFORE times move).
DECLARE @Veh TABLE (GpsDeviceId int NOT NULL, VehicleId int NOT NULL, PRIMARY KEY (GpsDeviceId, VehicleId));
INSERT INTO @Veh (GpsDeviceId, VehicleId)
SELECT DISTINCT p.GpsDeviceId, p.VehicleId
FROM GpsPositions p
JOIN @Dev x ON x.GpsDeviceId = p.GpsDeviceId
WHERE p.TenantId = @TenantId
  AND p.RecordedAt BETWEEN x.FirstBadFixUtc AND x.LastBadFixUtc;

DECLARE @Counts TABLE (GpsDeviceId int NOT NULL, Tbl sysname NOT NULL, Cnt int NOT NULL);

-- 1) GpsPositions
UPDATE p
SET p.RecordedAt = DATEADD(HOUR, @OffsetHours, p.RecordedAt),
    p.CreatedAt  = CASE WHEN p.CreatedAt = p.RecordedAt
                        THEN DATEADD(HOUR, @OffsetHours, p.CreatedAt) ELSE p.CreatedAt END
OUTPUT inserted.GpsDeviceId, N'GpsPositions', 1 INTO @Counts
FROM GpsPositions p
JOIN @Dev x ON x.GpsDeviceId = p.GpsDeviceId
WHERE p.TenantId = @TenantId
  AND p.RecordedAt BETWEEN x.FirstBadFixUtc AND x.LastBadFixUtc;

-- 2) VehicleTracking (no TenantId column — scoped through Vehicles)
UPDATE t
SET t.[Timestamp] = DATEADD(HOUR, @OffsetHours, t.[Timestamp]),
    t.CreatedAt   = CASE WHEN t.CreatedAt = t.[Timestamp]
                         THEN DATEADD(HOUR, @OffsetHours, t.CreatedAt) ELSE t.CreatedAt END
OUTPUT inserted.GpsDeviceId, N'VehicleTracking', 1 INTO @Counts
FROM VehicleTracking t
JOIN @Dev x      ON x.GpsDeviceId = t.GpsDeviceId
JOIN Vehicles v  ON v.Id = t.VehicleId AND v.TenantId = @TenantId
WHERE t.[Timestamp] BETWEEN x.FirstBadFixUtc AND x.LastBadFixUtc;

-- 3) GpsAlertEvents — CreatedAt is GETUTCDATE() at insert, so guard per row on the 5h gap.
UPDATE a
SET a.[Timestamp] = DATEADD(HOUR, @OffsetHours, a.[Timestamp])
OUTPUT vh.GpsDeviceId, N'GpsAlertEvents', 1 INTO @Counts
FROM GpsAlertEvents a
JOIN @Veh vh     ON vh.VehicleId = a.VehicleId
JOIN @Dev x      ON x.GpsDeviceId = vh.GpsDeviceId
JOIN Vehicles v  ON v.Id = a.VehicleId AND v.TenantId = @TenantId
WHERE a.[Timestamp] BETWEEN x.FirstBadFixUtc AND x.LastBadFixUtc
  AND DATEDIFF(MINUTE, a.[Timestamp], a.CreatedAt) BETWEEN 290 AND 569;

-- 4) GpsTrips (+ recompute the local trip date)
UPDATE g
SET g.StartTime     = DATEADD(HOUR, @OffsetHours, g.StartTime),
    g.EndTime       = DATEADD(HOUR, @OffsetHours, g.EndTime),
    g.LocalTripDate = CAST((DATEADD(HOUR, @OffsetHours, g.StartTime) AT TIME ZONE 'UTC')
                            AT TIME ZONE @WindowsTimeZone AS date)
OUTPUT inserted.GpsDeviceId, N'GpsTrips', 1 INTO @Counts
FROM GpsTrips g
JOIN @Dev x      ON x.GpsDeviceId = g.GpsDeviceId
JOIN Vehicles v  ON v.Id = g.VehicleId AND v.TenantId = @TenantId
WHERE g.StartTime BETWEEN x.FirstBadFixUtc AND x.LastBadFixUtc;

-- Log
INSERT INTO dbo.OpsGpsClockCorrections
    (TenantId, GpsDeviceId, TraccarDeviceId, OffsetHours, FirstBadFixUtc, LastBadFixUtc,
     PositionsShifted, TrackingShifted, AlertsShifted, TripsShifted)
SELECT @TenantId, x.GpsDeviceId, x.TraccarDeviceId, @OffsetHours, x.FirstBadFixUtc, x.LastBadFixUtc,
       (SELECT COUNT(*) FROM @Counts c WHERE c.GpsDeviceId = x.GpsDeviceId AND c.Tbl = N'GpsPositions'),
       (SELECT COUNT(*) FROM @Counts c WHERE c.GpsDeviceId = x.GpsDeviceId AND c.Tbl = N'VehicleTracking'),
       (SELECT COUNT(*) FROM @Counts c WHERE c.GpsDeviceId = x.GpsDeviceId AND c.Tbl = N'GpsAlertEvents'),
       (SELECT COUNT(*) FROM @Counts c WHERE c.GpsDeviceId = x.GpsDeviceId AND c.Tbl = N'GpsTrips')
FROM @Dev x;

SELECT c.TraccarDeviceId, c.GpsDeviceId, c.FirstBadFixUtc, c.LastBadFixUtc,
       c.PositionsShifted, c.TrackingShifted, c.AlertsShifted, c.TripsShifted
FROM dbo.OpsGpsClockCorrections c
JOIN @Dev x ON x.GpsDeviceId = c.GpsDeviceId
WHERE c.TenantId = @TenantId
  AND c.FirstBadFixUtc = x.FirstBadFixUtc;

IF @Apply = 1
BEGIN
    COMMIT TRANSACTION;
    PRINT 'Committed.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'Dry run — rolled back. Set @Apply = 1 to commit.';
END
