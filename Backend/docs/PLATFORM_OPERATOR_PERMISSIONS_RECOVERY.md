# Platform operator permissions recovery (SG-05)

If `/health` reports `platform-operator-permissions` unhealthy, no active user in the platform tenant (`TenantId = 1`) holds `Platform.Tenants.Manage` through `RolePermissions`. Authorization no longer grants power from the `SUPER_ADMIN` role name alone.

## One-statement recovery

Run against the application database (after confirming you intend to elevate the named user):

```sql
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
INNER JOIN UserRoles ur ON ur.RoleId = r.Id
INNER JOIN Users u ON u.Id = ur.UserId AND u.IsActive = 1 AND u.IsDeleted = 0
WHERE r.TenantId = 1
  AND r.Code = N'SUPER_ADMIN'
  AND r.IsActive = 1
  AND p.PermissionCode = N'Platform.Tenants.Manage'
  AND NOT EXISTS (
    SELECT 1 FROM RolePermissions rp
    WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );
```

Then have the operator **log out and log in again** (or refresh the token) so JWT permission claims reload.

## Prefer the migration

`PlatformOperatorPermissionsMigration` grants the full permission catalog to platform-tenant `SUPER_ADMIN` and is idempotent. Prefer re-running pending migrations on startup (or via Migration Manager) over hand-editing production data when possible.

## Related permissions

| Code | Purpose |
| --- | --- |
| `Platform.Tenants.View` | Company directory / tenant stats |
| `Platform.Tenants.Manage` | Provision, suspend, billing, menu catalog |
| `Platform.Tenants.DataRead` | Cross-tenant act-as / admin vehicles / platform GPS reads |
| `Platform.Tenants.AuditReadAll` | Cross-tenant audit (SG-07) |
| `Platform.Tenants.UsersReadAll` | Cross-tenant users (SG-42) |
| `Platform.Tenants.Impersonate` | Seeded for SUPER_ADMIN only; no endpoint yet |
