-- SG-01 diagnostic — READ ONLY. Run it yourself against the target DB; it changes nothing.
-- Purpose: tell apart "compliance rows stamped with the wrong TenantId" (data repair needed)
-- from "page ran under the wrong tenant context" (fixed by SG-06).
-- Expected after a clean history: every count = 0.

-- 1. Driver documents whose TenantId differs from the owning driver's TenantId
SELECT c.TenantId AS DocTenantId, d.TenantId AS DriverTenantId, COUNT(*) AS Docs
FROM ComplianceDocuments c
JOIN Drivers d ON c.EntityType = N'Driver' AND c.EntityId = d.Id
WHERE c.IsDeleted = 0 AND (d.TenantId IS NULL OR c.TenantId <> d.TenantId)
GROUP BY c.TenantId, d.TenantId;

-- 2. Vehicle documents whose TenantId differs from the owning vehicle's TenantId
SELECT c.TenantId AS DocTenantId, v.TenantId AS VehicleTenantId, COUNT(*) AS Docs
FROM ComplianceDocuments c
JOIN Vehicles v ON c.EntityType = N'Vehicle' AND c.EntityId = v.Id
WHERE c.IsDeleted = 0 AND (v.TenantId IS NULL OR c.TenantId <> v.TenantId)
GROUP BY c.TenantId, v.TenantId;

-- 3. Orphans: documents pointing at a driver/vehicle that does not exist
SELECT c.EntityType, c.TenantId, COUNT(*) AS Docs
FROM ComplianceDocuments c
LEFT JOIN Drivers d ON c.EntityType = N'Driver' AND c.EntityId = d.Id
LEFT JOIN Vehicles v ON c.EntityType = N'Vehicle' AND c.EntityId = v.Id
WHERE c.IsDeleted = 0
  AND ((c.EntityType = N'Driver' AND d.Id IS NULL) OR (c.EntityType = N'Vehicle' AND v.Id IS NULL))
GROUP BY c.EntityType, c.TenantId;

-- 4. Per-tenant document count vs owning-entity tenant (compare with what /fleet/compliance shows)
SELECT c.TenantId, t.Name AS Company, COUNT(*) AS Docs
FROM ComplianceDocuments c
LEFT JOIN Tenants t ON t.Id = c.TenantId
WHERE c.IsDeleted = 0
GROUP BY c.TenantId, t.Name
ORDER BY c.TenantId;
