-- SG-01 Step 6a — review finding M2: data that the new GPS ingest scope will REJECT.
-- READ-ONLY. SELECT statements only; no INSERT/UPDATE/DELETE, no temp tables.
-- Run it yourself (Development first, then production) before deploying 6a.
-- Returns ids and tenant ids only — no coordinates, names, phones or CNICs.
-- Any non-zero count is a data fix to plan and review separately. Do NOT backfill as part of 6a.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; -- diagnostic only; avoid blocking live writers

-- 1. Vehicles with no tenant. Every ingest for these is rejected ("Position rejected.").
SELECT 'vehicle_null_tenant' AS Check_, COUNT(*) AS Rows_
FROM Vehicles v
WHERE v.IsDeleted = 0 AND v.TenantId IS NULL;

SELECT TOP 50 v.Id AS VehicleId
FROM Vehicles v
WHERE v.IsDeleted = 0 AND v.TenantId IS NULL
ORDER BY v.Id;

-- 2. GPS devices whose own tenant differs from the tenant of the vehicle they are linked to.
--    (Devices with TenantId NULL are allowed — Traccar provisioning starts them unowned.)
SELECT 'device_vehicle_tenant_mismatch' AS Check_, COUNT(*) AS Rows_
FROM GpsDevices d
INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
WHERE d.IsDeleted = 0
  AND d.TenantId IS NOT NULL
  AND (v.TenantId IS NULL OR d.TenantId <> v.TenantId);

SELECT TOP 50 d.Id AS GpsDeviceId, d.TenantId AS DeviceTenantId, v.Id AS VehicleId, v.TenantId AS VehicleTenantId
FROM GpsDevices d
INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
WHERE d.IsDeleted = 0
  AND d.TenantId IS NOT NULL
  AND (v.TenantId IS NULL OR d.TenantId <> v.TenantId)
ORDER BY d.Id;

-- 2b. Vehicles pointing at a device (Vehicles.GpsDeviceId) that is linked to a DIFFERENT vehicle.
--     Ingest requires d.VehicleId = the vehicle, so these are rejected too.
SELECT 'vehicle_device_link_mismatch' AS Check_, COUNT(*) AS Rows_
FROM Vehicles v
INNER JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
WHERE v.IsDeleted = 0
  AND (d.VehicleId IS NULL OR d.VehicleId <> v.Id);

-- 3. Drivers with no tenant (driver-scoped ingest is rejected for them).
SELECT 'driver_null_tenant' AS Check_, COUNT(*) AS Rows_
FROM Drivers dr
WHERE dr.IsDeleted = 0 AND dr.TenantId IS NULL;

-- 3b. Drivers on a booking whose tenant differs from the driver's tenant.
SELECT 'booking_driver_tenant_mismatch' AS Check_, COUNT(*) AS Rows_
FROM Bookings b
INNER JOIN Drivers dr ON dr.Id = b.DriverId AND dr.IsDeleted = 0
WHERE b.IsDeleted = 0
  AND (dr.TenantId IS NULL OR b.TenantId IS NULL OR dr.TenantId <> b.TenantId);

-- 3c. Bookings whose vehicle belongs to another tenant (explicit BookingId ingest is rejected).
SELECT 'booking_vehicle_tenant_mismatch' AS Check_, COUNT(*) AS Rows_
FROM Bookings b
INNER JOIN Vehicles v ON v.Id = b.VehicleId AND v.IsDeleted = 0
WHERE b.IsDeleted = 0
  AND (v.TenantId IS NULL OR b.TenantId IS NULL OR v.TenantId <> b.TenantId);
