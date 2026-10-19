# Secrets rotation (no git history rewrite)

**Date:** 2026-10-16  
**Control:** Rotate credentials that were previously committed; stop tracking live secrets.  
**Explicit non-goal:** Do **not** rewrite git history unless an owner asks later.

## What changed in repo

- Tracked `Backend/SheikhTravelSystem.API/appsettings.json` now contains only `__SET_IN_USER_SECRETS_OR_ENV__` / empty placeholders (plus non-secret defaults).
- `appsettings.Example.json` is the copy-paste template for new environments.
- `appsettings.Local.json` and `appsettings.Development.json` remain gitignored; `Program.cs` loads `appsettings.Local.json` optionally after the default host config so local secrets override placeholders.
- Live values must come from Railway / Azure env vars, user secrets, or a local overlay that is never committed.

## Rotate these (ops / owner)

History still contains older commits with live-shaped values. **Rotation is the control**, not a history purge.

| Secret | Action |
|--------|--------|
| SQL `sa` / connection string | Change SQL password; update Railway `ConnectionStrings__DefaultConnection` and local overlay |
| JWT signing secret | Generate a new long random secret; update `JwtSettings__Secret`; expect all sessions to re-login |
| Traccar password | Change Traccar user password; update `Traccar__Password` |
| Google Maps server key | Regenerate in Cloud Console; restrict by IP / API; update `Geocoding__GoogleMapsApiKey` |
| SMTP / Azure storage / OCR keys | Rotate if they were ever committed or shared |

## Local setup

```bash
cp Backend/SheikhTravelSystem.API/appsettings.Example.json \
   Backend/SheikhTravelSystem.API/appsettings.Local.json
# Fill real values in appsettings.Local.json (never commit)
```

Or use `dotnet user-secrets` under `SheikhTravelSystem.API`.

## Redeploy checklist

1. Set env vars on Railway (or host) for connection string, JWT, Traccar, Maps, storage.
2. Confirm `MultiTenancy__AllowAnonymousDefaultTenant=false` in production.
3. Hit `/health` (or the project health endpoint) after deploy.
4. Confirm Maps key regenerated + restricted in Google Cloud Console (manual).

## Why no history rewrite

Force-pushing a rewritten history breaks clones, Railway deploys, and shared branches. Until an owner explicitly requests a BFG/filter-repo pass, treat historical exposure as mitigated by **rotation + stop tracking**.
