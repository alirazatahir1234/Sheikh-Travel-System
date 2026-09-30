# SheikhGo Backend — secrets & local configuration

Never commit real passwords, JWT signing keys, API keys, or connection strings.
Bind required secrets via **environment variables** or `dotnet user-secrets`. Do **not** stage
[`SheikhTravelSystem.API/appsettings.json`](SheikhTravelSystem.API/appsettings.json) for feature
work — ADR-009 flags that path (and any `appsettings*.json` template) whenever it appears in a diff.

## Required secrets (Package 1 Phase 0)

| Purpose | Configuration key | Environment variable |
|---------|-------------------|----------------------|
| SQL Server | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| JWT signing | `JwtSettings:Secret` (min 32 chars) | `JwtSettings__Secret` |
| Traccar (when enabled) | `Traccar:Password` | `Traccar__Password` |
| Google Maps (server) | `GoogleMaps:ApiKey` or `Geocoding:GoogleMapsApiKey` | `GoogleMaps__ApiKey` or `Geocoding__GoogleMapsApiKey` |

Non-Development hosts **fail fast at startup** if these are missing or still placeholders (`ProductionSecretsValidator`).

Host env vars override any values present in tracked `appsettings.json`. Prefer env / user-secrets for
all environments. A tracked-file placeholder scrub is deferred to a separate ADR-009 override merge
(removal hunks of prior credentials fail the scanner even when the intent is cleanup).

## Local Development (user-secrets)

From `Backend/SheikhTravelSystem.API`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-local-sql-connection-string>"
dotnet user-secrets set "JwtSettings:Secret" "<random-32+-char-secret>"
dotnet user-secrets set "Traccar:Password" "<traccar-password>"
dotnet user-secrets set "GoogleMaps:ApiKey" "<sheikhgo-backend-maps-key>"
# Optional portal OTP bypass (Development only — ignored in Production/Staging):
dotnet user-secrets set "PortalAuth:DevMode" "true"
dotnet user-secrets set "PortalAuth:DevOtpCode" "<six-digit-code>"
```

Alternatively, copy [`docs/local-dev-config.example.json`](docs/local-dev-config.example.json) into a
gitignored `SheikhTravelSystem.API/appsettings.Development.json` and add connection/JWT/Maps values
locally (do not commit that file). Prefer user-secrets when possible.

## Production / Railway / App Service

Set the environment variables in the table above on the host. Do **not** rely on committed
`appsettings.json` for secret values.

Also rotate any credential that was previously committed to git history (SQL password, JWT secret,
Traccar password, Maps key) — scrubbing the repo does **not** rotate the live credential.

## PortalAuth OTP

- `PortalAuth:DevMode` / `DevOtpCode` work **only** when `ASPNETCORE_ENVIRONMENT=Development`.
- Staging and Production always generate a random OTP and use the SMS path, even if DevMode is set in configuration.

## Related

- Progress log: [`docs/production-readiness/01-security-package-progress.md`](../docs/production-readiness/01-security-package-progress.md)
