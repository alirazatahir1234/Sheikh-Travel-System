# MCP Review — High Findings (pre-push)

**Scope:** Working tree vs `origin/main` (Ali’s MCP inspector, 6 HIGH).  
**Mode:** Report only — no scrub, unstage, commit, or push applied by this document pass.  
**Date context:** 2026-10-24

---

## Summary

| # | Severity | Category | File | Verdict |
|---|----------|----------|------|---------|
| 1 | HIGH | Security / ADR-009 | `docker-compose.db.env` (inferred) | Real — DB password on added lines |
| 2 | HIGH | Security / ADR-009 | `secrets/local.env` (inferred) | Real — API keys on added lines |
| 3 | HIGH | Security / ADR-009 | `docker-compose.db.env` | Real — sensitive config file in change set |
| 4 | HIGH | Security / ADR-009 | `secrets/local.env` | Real — sensitive config file in change set |
| 5 | HIGH | Architecture / ADR-003 | `IAiAssistantGateway.cs` | **False positive** |
| 6 | HIGH | Architecture / ADR-003 | `QueryAiAssistantCommand.cs` | **False positive** |

**Safe to push?** **NO** until findings 1–4 are out of the commit path (untracked + gitignored). Findings 5–6 do not require code changes.

---

## Findings 1–2 — “Possible secret/connection string in diff”

MCP JSON did **not** name files; evidence was: *“Matched sensitive pattern on added (+) lines only.”*

### Identified sources (from inspecting the same two Added files)

These are the only working-tree files that contain secret-like `KEY=value` material matching typical connection-string / password / API-key scanners. No other staged/unstaged **code** diffs currently show new connection-string / password / API-key assignments on `+` lines.

#### Finding 1 (inferred file: `docker-compose.db.env`)

| Item | Detail |
|------|--------|
| Match type | SQL / DB **password** env var (`MSSQL_SA_PASSWORD=…`) |
| What it is | Local Docker SQL Server SA password (value redacted; non-empty) |
| Why HIGH | Added (+) line looks like a live credential in the diff |

#### Finding 2 (inferred file: `secrets/local.env`)

| Item | Detail |
|------|--------|
| Match type | **API keys** (Google Maps) |
| Keys present (names only) | `GOOGLE_MAPS_API_KEY_WEB`, `GOOGLE_MAPS_API_KEY_ANDROID`, `GOOGLE_MAPS_API_KEY_IOS`, `GOOGLE_MAPS_API_KEY_BACKEND`, `GOOGLE_MAPS_API_KEY`, `GOOGLE_MAPS_KEY`, plus non-secret-looking `GOOGLE_MAPS_MAP_ID` |
| What it is | Local Maps API key material (values redacted; non-empty) |
| Why HIGH | Added (+) lines match secret/API-key patterns |

**Unknown vs known:** Exact MCP regex is unknown; file attribution for 1–2 is **inferred** from content + git status (`A` / sensitive files). Confidence high that 1–2 are the same files as 3–4 (duplicate signal: pattern match + sensitive filename).

---

## Findings 3–4 — “Sensitive config/key file appears in changed files”

| # | File | Issue |
|---|------|--------|
| 3 | `docker-compose.db.env` | Entire file treated as sensitive config; was **Added** in the review change set |
| 4 | `secrets/local.env` | Entire file treated as sensitive config; was **Added** in the review change set |

### `.gitignore` gap (known)

Current `.gitignore` has:

- `.env`
- `.env.*`

That does **not** ignore:

- `docker-compose.db.env` (name does not match `.env` / `.env.*`)
- `secrets/` or `secrets/local.env` (`local.env` ≠ `.env.*`)

`git check-ignore` does **not** cover these paths today.

---

## Findings 5–6 — Architecture / ADR-003 (SQL/Dapper in Application)

Reviewer message: *“Application change adds SQL/Dapper — prefer Infrastructure repository (ADR-003)”*

### 5 — `Backend/SheikhTravelSystem.Application/Common/Interfaces/IAiAssistantGateway.cs`

- **What it is:** Interface + DTOs (`AiAssistantQueryRequest` / `AiAssistantQueryResponse`).
- **SQL/Dapper?** **No.** No `using Dapper`, no `SqlConnection`, no SQL strings.
- **Verdict:** **False positive** — leave alone.
- Likely scanner noise: Application path + name `QueryAsync` / “assistant” tooling heuristics.

### 6 — `Backend/SheikhTravelSystem.Application/Features/AI/Commands/QueryAiAssistantCommand.cs`

- **What it is:** MediatR command + handler that validates tenant/user/question and calls `IAiAssistantGateway.QueryAsync(...)`.
- **SQL/Dapper?** **No.** Handler is orchestration only.
- **Where SQL actually lives:** Infrastructure (e.g. `AssistantReportingTools.cs`, AI tool engine) — correct ADR-003 placement.
- **Verdict:** **False positive** — leave alone.

---

## Recommended fix approach (do not apply in this pass)

### Must fix before live push (1–4)

1. **Keep local copies on disk** (needed for local Docker / Maps). Do **not** delete Ali’s secret values.
2. **Ensure they are not staged/committed:**
   - If still staged: `git rm --cached docker-compose.db.env secrets/local.env` (keeps files on disk).
   - Confirm with `git status` / `git ls-files` that they are untracked and not in the index.
3. **Extend `.gitignore`** (recommended entries):
   - `docker-compose.db.env`
   - `secrets/` (or at least `secrets/**`)
   - Optionally keep existing `.env` / `.env.*`
4. **Never commit secrets.** Never force-push.
5. **Do not scrub** tracked `appsettings.json` in this push unless you are intentionally doing the deferred credential scrub — see below.

### Leave alone (5–6)

- No move of SQL; Application layer is already correct.
- Optional later: tune MCP inspector to ignore `QueryAsync` method names / Application interfaces.

### Deferred (do not block solely on this if unchanged in the diff)

- Tracked `Backend/SheikhTravelSystem.API/appsettings.json` on **main** still contains ConnectionStrings / Password / ApiKey-like keys (known deferred scrub).
- This change set does **not** modify `appsettings*.json` (no new introduction in current WT/staged diff).
- Prefer: unstage secret files only for this push; scrub appsettings in a dedicated follow-up.

### Intentional feature work (non-blocking)

Smart Ops / AI assistant / fuel-tenant / WhatsApp menu consolidation are expected feature changes in the large file set — not blockers for the six HIGHs above.

---

## Completeness / blockers

| Blocker | Status |
|---------|--------|
| Secrets out of commit path | **Required** before push |
| `.gitignore` covers `secrets/` + `docker-compose.db.env` | **Required** (gap today) |
| ADR-003 Application SQL | Not a real blocker (false positives) |
| appsettings live secrets on tracked main | Known deferred; not newly introduced in this diff |

**Final call for Ali’s Azure + pipeline push:** **NO** until `docker-compose.db.env` and `secrets/local.env` are untracked, gitignored, and absent from any commit that will be pushed.

---

## Note on workspace state at documentation time

At the time this doc was written, those two files appeared as **untracked** (`??`) rather than staged `A` (index empty for both). Disk copies still present. If a prior agent turn unstaged them, re-verify before push; still add `.gitignore` entries so they cannot be re-added accidentally. This documentation pass did not scrub, unstage, commit, or push.
