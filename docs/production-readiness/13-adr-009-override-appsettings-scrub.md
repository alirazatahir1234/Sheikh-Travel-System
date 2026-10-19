# ADR-009 override — tracked `appsettings` placeholder scrub

**Date:** 2026-10-19  
**Decision:** Accept gate **fail** findings on this merge for the intentional secrets scrub.  
**Related:** [ADR-009](../architecture/) (no secrets in tracked config), [`12-secrets-rotation-no-history-rewrite.md`](12-secrets-rotation-no-history-rewrite.md)

## Why the gate fails (false positive for this change)

The quality gate scans the **diff against `origin/main`**. On `main`, tracked
`Backend/SheikhTravelSystem.API/appsettings.json` still contains live-shaped values.

This branch **replaces** those values with `__SET_IN_USER_SECRETS_OR_ENV__` / empty
placeholders. The scanner therefore matches:

| Finding | Why it fires |
|---------|----------------|
| Possible password in diff | Removed lines still contain the old password/JWT/Traccar values |
| Possible secret/connection string in diff | Removed connection string / API key lines |
| Sensitive config file: `appsettings.json` | Path allow-list (any change to this file) |
| Sensitive config file: `appsettings.Example.json` | New tracked **template** (placeholders only) |

**Working-tree truth (post-scrub):** tracked `appsettings.json` and
`appsettings.Example.json` contain **no** live secrets — only placeholders and
non-secret defaults. Verified by inspection on 2026-10-19.

Live overlays remain gitignored:

- `**/appsettings.Local.json`
- `**/appsettings.Development.json`

`Program.cs` loads `appsettings.Local.json` optionally after host config.

## Accepted findings (this merge)

### High — security (4) — **OVERRIDE ACCEPT**

Intentional scrub. Do not revert placeholders to silence the gate.

### Medium — impact (2) — **ACCEPT / informational**

| Symbol | Note |
|--------|------|
| `DriverAppController` | Added forgot/reset password + driver settlements read. Controllers stay thin (MediatR). |
| `VehiclesController` | Added bulk create. Controllers stay thin (MediatR). |

Blast-radius warnings are expected for shared API edges; no architecture violation.

## Merge checklist

- [x] Tracked `appsettings.json` is placeholders only
- [x] `appsettings.Example.json` is placeholders only (template for new envs)
- [x] Local/Development overlays gitignored
- [x] Rotation guidance documented in [`12-secrets-rotation-no-history-rewrite.md`](12-secrets-rotation-no-history-rewrite.md)
- [ ] Ops confirms Railway/host env still supplies real secrets after deploy
- [ ] Ops confirms previously exposed credentials were rotated (SQL, JWT, Traccar, Maps)

## Explicit non-goals

- No git history rewrite
- No committing `appsettings.Local.json` / `appsettings.Development.json`
