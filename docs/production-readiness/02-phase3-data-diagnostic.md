# Phase 3A — Tenant Data Diagnostic

| Field | Value |
|-------|--------|
| Phase | **3A — Diagnostic ONLY** COMPLETE |
| Date | 2026-09-29 |
| Scope | Read-only `SELECT` diagnostics. No application code, schema, migration, or data changes. |
| STOP | Do **not** start Phase 3B repository scoping until this report is approved. |

---

## 1. Database / schema observations

### Connection identity (as executed)

| Field | Value |
|-------|--------|
| `@@SERVERNAME` | `940aff6640cf` (Docker container `sheikh-sqlserver`) |
| `DB_NAME()` | `SheikhTravelSystemDB` |
| Host (client) | `127.0.0.1,1433` |
| `SYSTEM_USER` | `sa` |
| Queried at (UTC) | `2026-09-29 07:30` |

**Environment label: LOCAL / DEV Docker — not claimed as production.**

Connection came from local `appsettings.Development.json` (`ConnectionStrings:DefaultConnection`). Operators must re-run the SQL in §2 against the real production host and replace §3 results before treating this as a production gate.

### Tenants present

| Id | Name | Slug | IsActive |
|----|------|------|----------|
| 1 | Sheikh Travel | default | true |

**Single tenant only** in this database.

### TenantId columns

Confirmed via `INFORMATION_SCHEMA.COLUMNS` where `COLUMN_NAME = 'TenantId'`. Core tables from [`TenantSchemaMigration`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Migrations/TenantSchemaMigration.cs) are present with `TenantId INT NOT NULL` and default `((1))` on:

`Bookings`, `Customers`, `Drivers`, `DriverAllowanceRules`, `FuelLogs`, `Maintenance`, `Payments`, `PromoCodes`, `Routes`, `Vehicles`, …

Also present: `WorkOrders` (`TenantId` nullable), GPS/AI/WhatsApp/website tables (out of Phase 3A repair focus).

### Tables without TenantId (note for Phase 3B)

`VehicleTracking` has **no** `TenantId` column (isolation would be via `VehicleId` → `Vehicles`). Row count in this DB: **32,728**.

---

## 2. Diagnostic SQL used (read-only)

Every statement is `SELECT` only. No `UPDATE` / `DELETE` / `INSERT` / `ALTER` / `DROP` / `TRUNCATE`.

### Identity + schema

```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName,
       SYSTEM_USER AS DbUser, GETUTCDATE() AS UtcNow;

SELECT Id, Name, Slug, IsActive FROM Tenants ORDER BY Id;

SELECT TABLE_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME = 'TenantId'
ORDER BY TABLE_NAME;
```

### FuelLogs vs Vehicles

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN v.Id IS NOT NULL AND f.TenantId = v.TenantId THEN 1 ELSE 0 END) AS MatchingVehicleTenant,
  SUM(CASE WHEN v.Id IS NOT NULL AND f.TenantId <> v.TenantId THEN 1 ELSE 0 END) AS Mismatched,
  SUM(CASE WHEN v.Id IS NULL THEN 1 ELSE 0 END) AS MissingOrInvalidVehicle
FROM FuelLogs f
LEFT JOIN Vehicles v ON v.Id = f.VehicleId AND ISNULL(v.IsDeleted,0)=0
WHERE ISNULL(f.IsDeleted,0)=0;

SELECT f.TenantId, v.TenantId AS VehicleTenantId, COUNT(*) AS Cnt
FROM FuelLogs f
INNER JOIN Vehicles v ON v.Id = f.VehicleId AND ISNULL(v.IsDeleted,0)=0
WHERE ISNULL(f.IsDeleted,0)=0 AND f.TenantId <> v.TenantId
GROUP BY f.TenantId, v.TenantId;

SELECT TenantId, COUNT(*) AS Cnt
FROM FuelLogs WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### Maintenance vs Vehicles

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN v.Id IS NOT NULL AND m.TenantId = v.TenantId THEN 1 ELSE 0 END) AS MatchingVehicleTenant,
  SUM(CASE WHEN v.Id IS NOT NULL AND m.TenantId <> v.TenantId THEN 1 ELSE 0 END) AS Mismatched,
  SUM(CASE WHEN v.Id IS NULL THEN 1 ELSE 0 END) AS MissingOrInvalidVehicle
FROM Maintenance m
LEFT JOIN Vehicles v ON v.Id = m.VehicleId AND ISNULL(v.IsDeleted,0)=0
WHERE ISNULL(m.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt
FROM Maintenance WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### Payments vs Bookings

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN b.Id IS NOT NULL AND p.TenantId = b.TenantId THEN 1 ELSE 0 END) AS MatchingBookingTenant,
  SUM(CASE WHEN b.Id IS NOT NULL AND p.TenantId <> b.TenantId THEN 1 ELSE 0 END) AS Mismatched,
  SUM(CASE WHEN b.Id IS NULL THEN 1 ELSE 0 END) AS MissingOrInvalidBooking
FROM Payments p
LEFT JOIN Bookings b ON b.Id = p.BookingId AND ISNULL(b.IsDeleted,0)=0
WHERE ISNULL(p.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt
FROM Payments WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### Bookings (distribution + related-entity checks)

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN t.Id IS NULL THEN 1 ELSE 0 END) AS OrphanTenantId,
  SUM(CASE WHEN c.Id IS NOT NULL AND b.TenantId <> c.TenantId THEN 1 ELSE 0 END) AS MismatchVsCustomer,
  SUM(CASE WHEN c.Id IS NULL AND b.CustomerId IS NOT NULL THEN 1 ELSE 0 END) AS MissingCustomer,
  SUM(CASE WHEN v.Id IS NOT NULL AND b.TenantId <> v.TenantId THEN 1 ELSE 0 END) AS MismatchVsVehicle,
  SUM(CASE WHEN d.Id IS NOT NULL AND b.TenantId <> d.TenantId THEN 1 ELSE 0 END) AS MismatchVsDriver,
  SUM(CASE WHEN r.Id IS NOT NULL AND b.TenantId <> r.TenantId THEN 1 ELSE 0 END) AS MismatchVsRoute
FROM Bookings b
LEFT JOIN Tenants t ON t.Id = b.TenantId
LEFT JOIN Customers c ON c.Id = b.CustomerId AND ISNULL(c.IsDeleted,0)=0
LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND ISNULL(v.IsDeleted,0)=0
LEFT JOIN Drivers d ON d.Id = b.DriverId AND ISNULL(d.IsDeleted,0)=0
LEFT JOIN Routes r ON r.Id = b.RouteId AND ISNULL(r.IsDeleted,0)=0
WHERE ISNULL(b.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt
FROM Bookings WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### Routes (no parent provenance — human review if mixed)

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN t.Id IS NULL THEN 1 ELSE 0 END) AS OrphanTenantId,
  COUNT(DISTINCT r.TenantId) AS DistinctTenants
FROM Routes r
LEFT JOIN Tenants t ON t.Id = r.TenantId
WHERE ISNULL(r.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt
FROM Routes WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### DriverAllowanceRules (no parent provenance — human review if mixed)

```sql
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN t.Id IS NULL THEN 1 ELSE 0 END) AS OrphanTenantId,
  COUNT(DISTINCT r.TenantId) AS DistinctTenants
FROM DriverAllowanceRules r
LEFT JOIN Tenants t ON t.Id = r.TenantId
WHERE ISNULL(r.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt
FROM DriverAllowanceRules WHERE ISNULL(IsDeleted,0)=0
GROUP BY TenantId ORDER BY TenantId;
```

### Customers / Vehicles / Drivers / PromoCodes / WorkOrders

```sql
-- Customers
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN t.Id IS NULL THEN 1 ELSE 0 END) AS OrphanTenantId,
       COUNT(DISTINCT c.TenantId) AS DistinctTenants
FROM Customers c
LEFT JOIN Tenants t ON t.Id = c.TenantId
WHERE ISNULL(c.IsDeleted,0)=0;

SELECT TenantId, COUNT(*) AS Cnt FROM Customers WHERE ISNULL(IsDeleted,0)=0 GROUP BY TenantId ORDER BY TenantId;
SELECT TenantId, COUNT(*) AS Cnt FROM Vehicles WHERE ISNULL(IsDeleted,0)=0 GROUP BY TenantId ORDER BY TenantId;
SELECT TenantId, COUNT(*) AS Cnt FROM Drivers WHERE ISNULL(IsDeleted,0)=0 GROUP BY TenantId ORDER BY TenantId;

-- PromoCodes
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN t.Id IS NULL THEN 1 ELSE 0 END) AS OrphanTenantId,
       COUNT(DISTINCT p.TenantId) AS DistinctTenants
FROM PromoCodes p
LEFT JOIN Tenants t ON t.Id = p.TenantId
WHERE ISNULL(p.IsDeleted,0)=0;

-- WorkOrders spot-check
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN v.Id IS NOT NULL AND wo.TenantId = v.TenantId THEN 1 ELSE 0 END) AS MatchingVehicleTenant,
  SUM(CASE WHEN v.Id IS NOT NULL AND wo.TenantId <> v.TenantId THEN 1 ELSE 0 END) AS Mismatched,
  SUM(CASE WHEN v.Id IS NULL THEN 1 ELSE 0 END) AS MissingOrInvalidVehicle,
  SUM(CASE WHEN wo.TenantId IS NULL THEN 1 ELSE 0 END) AS NullTenantId
FROM WorkOrders wo
LEFT JOIN Vehicles v ON v.Id = wo.VehicleId AND ISNULL(v.IsDeleted,0)=0;
```

---

## 3. Results (counts by table)

Executed against **local Docker `SheikhTravelSystemDB`** (`940aff6640cf`). Soft-deleted rows excluded where `IsDeleted` applies.

### FuelLogs

| Metric | Count |
|--------|------:|
| Total | 4 |
| Matching Vehicle TenantId | 4 |
| Mismatched | **0** |
| Missing/invalid Vehicle | 0 |
| Tenant distribution | TenantId=1 → 4 |

### Maintenance

| Metric | Count |
|--------|------:|
| Total | 4 |
| Matching Vehicle TenantId | 4 |
| Mismatched | **0** |
| Missing/invalid Vehicle | 0 |
| Tenant distribution | TenantId=1 → 4 |

### Payments

| Metric | Count |
|--------|------:|
| Total | 5 |
| Matching Booking TenantId | 5 |
| Mismatched | **0** |
| Missing/invalid Booking | 0 |
| Tenant distribution | TenantId=1 → 5 |

### Bookings

| Metric | Count |
|--------|------:|
| Total | 5 |
| Orphan TenantId (not in Tenants) | 0 |
| Mismatch vs Customer | 0 |
| Missing Customer | 0 |
| Mismatch vs Vehicle | 0 |
| Mismatch vs Driver | 0 |
| Mismatch vs Route | 0 |
| Tenant distribution | TenantId=1 → 5 |

### Routes

| Metric | Count |
|--------|------:|
| Total | 6 |
| Orphan TenantId | 0 |
| Distinct Tenants | **1** |
| Tenant distribution | TenantId=1 → 6 |

### DriverAllowanceRules

| Metric | Count |
|--------|------:|
| Total | 4 |
| Orphan TenantId | 0 |
| Distinct Tenants | **1** |
| Tenant distribution | TenantId=1 → 4 |

### Customers / Vehicles / Drivers

| Table | Total (active) | Distinct Tenants | Distribution |
|-------|---------------:|-----------------:|--------------|
| Customers | 5 | 1 | TenantId=1 → 5 |
| Vehicles | 5 | 1 | TenantId=1 → 5 |
| Drivers | 4 | 1 | TenantId=1 → 4 |

### PromoCodes

| Metric | Count |
|--------|------:|
| Total | 0 |
| Orphan / mismatch | n/a |

### WorkOrders (spot-check)

| Metric | Count |
|--------|------:|
| Total | 0 |
| Mismatched | n/a |

### VehicleTracking

| Metric | Value |
|--------|-------|
| TenantId column | **Absent** |
| Row count | 32,728 |

---

## 4. Cross-tenant inconsistencies

**None found** in the queried database:

- Parent-provenanced tables (`FuelLogs`, `Maintenance`, `Payments`, `Bookings` related checks): **0 mismatches**.
- Top-level tables (`Routes`, `DriverAllowanceRules`, `Customers`): all rows belong to **TenantId = 1**; no orphan TenantIds; only one tenant exists in `Tenants`.

`TenantId = 1` here coincides with the only seeded tenant (`Sheikh Travel` / `default`). That does **not** prove historical multi-tenant pollution never existed in production — only that this DB is clean and single-tenant.

---

## 5. Records that can be safely repaired by parent provenance

| Table | Mismatched count | Repair approach (NOT executed) |
|-------|-----------------:|--------------------------------|
| FuelLogs | 0 | Would set `TenantId` from `Vehicles` |
| Maintenance | 0 | Would set `TenantId` from `Vehicles` |
| Payments | 0 | Would set `TenantId` from `Bookings` |
| Bookings (vs Customer/Vehicle/Driver/Route) | 0 | Would require case-by-case if mismatches appear in prod |

**No repair scripts were run.** No data was modified.

---

## 6. Records that require human review

| Table | Reason | This DB |
|-------|--------|---------|
| **Routes** | No parent FK provenance for automatic ownership repair | All 6 rows TenantId=1 — **no adjudication needed on this DB** |
| **DriverAllowanceRules** | No parent FK provenance | All 4 rows TenantId=1 — **no adjudication needed on this DB** |
| `VehicleTracking` | No TenantId; Phase 3B must scope via Vehicle join | Structural finding only |

If production shows Routes or DriverAllowanceRules with **multiple TenantIds** or orphan ids, **STOP and adjudicate manually** — do not auto-repair.

---

## 7. Recommendation for Phase 3 implementation

### Against this observed database → **Scenario A**

```text
Tenant 1
   ↓
All historical data belongs to Tenant 1
Zero parent mismatches
Routes / DriverAllowanceRules: single-tenant, no orphans
```

**Data-consistency view:** safe to proceed with **Phase 3B — Repository Tenant Scoping** (inject `ITenantContext`, filter/write `TenantId`) without a prior data-repair migration on this database.

### Production gate (mandatory before merge)

1. Re-run §2 SQL against **production** `ConnectionStrings:DefaultConnection`.
2. Paste production counts into a new section of this doc (or a sibling `02-phase3-data-diagnostic.production.md`).
3. If production returns **Scenario B** (mismatches, multi-tenant Routes/Rules, orphan TenantIds): **hold Phase 3B** until human adjudication; do not auto-fix Routes / DriverAllowanceRules.

### Phase 3B priority (unchanged from audit; do not start until approved)

1. DashboardRepository  
2. DriverAppRepository  
3. BookingRepository  
4. PaymentRepository  
5. FuelLogRepository  
6. MaintenanceRepository (legacy)  
7. RouteRepository  
8. DriverAllowanceRepository  
9. PricingRepository  
10. TrackingRepository  
11. CustomerRepository  
12. Additional gaps (e.g. VehicleTracking via vehicle join)

Leave already-correct Vehicle/Driver / MaintenanceModule paths alone unless diagnostics show new gaps.

### Phase 3C (later)

Governance tests: every tenant-sensitive repository uses `ITenantContext` (or explicit tenantId) + tenant-aware SQL source checks.

---

## Safety checklist

| Check | Status |
|-------|--------|
| Only SELECT statements | Yes |
| No migrations created | Yes |
| No application code modified | Yes |
| No TenantId values updated | Yes |
| Phase 3B not started | Yes |

---

## STOP

**Phase 3A complete.** Await approval (and production re-run confirmation if required) before Phase 3B.
