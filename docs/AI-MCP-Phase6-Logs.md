# Phase 6 — Logs MCP / Operational Diagnostics (STOP for review)

**Status:** Complete. Phase 7 (conversation context) documented in `docs/AI-MCP-Phase7-ConversationContext.md`.  
**Date:** 2026-10-26  
**Repos:** SheikhGo-MCP `0.17.0` + Sheikh-Travel-System AI bridge allowlist / router prompt / elevated permission gates + `McpToolExecutions` migration.

---

## 1. Goal

Expose **read-only operational diagnostics** to authorized admins/developers via Logs MCP tools. Prefer querying existing sinks (DB tables, live pings, bounded Serilog files). Never invent root causes when logs are missing. Keep diagnostics **separate from Analytics KPIs**. Never execute shell commands, change production settings, or auto-remediate.

---

## 2. Logging sources — exist vs missing

### Exist (queryable or live)

| Source | Store | What Phase 6 can answer |
|--------|-------|-------------------------|
| Assistant failure markers | `AiChatMessages` ⋈ `AiChatSessions` | Failed MCP / TOOL_ERROR wording, provider timeout / rate-limit text heuristics |
| AI usage | `AiUsageLedger` | Call/token/cost rollups by feature/provider (not revenue) |
| Auth / security events | `AuditEvents` + `AuditEventDefinitions` | Login failed, lockout, Success=0, severity, **CorrelationId** |
| Legacy audit (Security MCP) | `AuditLogs` | `list_audit_events` (Platform.Audit.* gate — not Logs) |
| Live SQL connectivity | Database MCP ping | Connected / configured (not historical timeouts) |
| Live Traccar | Optional `/api/server` | Connectivity (passwords never returned) |
| GPS freshness sample | `VehicleCurrentLocation` | Fresh vs stale within 30m |
| Serilog rolling files | `Logs/log-.txt` (Day, retain 30) | Bounded `search_serilog_logs` (file read only; redacted) |
| MCP tool executions | `McpToolExecutions` | Structured tool name / success / duration / correlationId / sessionId (`search_mcp_executions`) |

### Missing / unavailable (structured `available=false`)

| Capability | Dependency |
|------------|------------|
| Background job failure ledger | Job run table (hosted services today → ILogger → files; use `search_serilog_logs` as fallback) |
| Centralized observability | Seq / Application Insights Serilog sink |
| End-to-end chat→DeepInfra CorrelationId | Propagate one id through HTTP / DeepInfra (MCP executions already stamp per-call CorrelationId) |

**Inspection notes:** `appsettings.json` Serilog `WriteTo` = Console + File (`path: Logs/log-.txt`, `rollingInterval: Day`, `retainedFileCountLimit: 30`). No Seq / Application Insights. No Hangfire.

---

## 3. Tools implemented (contracts)

| Tool | Inputs | Outputs |
|------|--------|---------|
| `get_logs_domain` | (none) | Catalog, sources exist/missing, filters, redaction policy, limits |
| `get_log_source_status` | (none) | Existing vs unavailable matrices |
| `get_diagnostic_summary` | `tenantId`, period? | Counts (assistant errors, auth failures, AI usage) + live connectivity + unavailable list |
| `get_ai_assistant_errors` | `tenantId`, period?, `limit?`, `sessionId?`, `component?`, `severity?` | Redacted events; soft correlator = sessionId; capped |
| `get_ai_usage_summary` | `tenantId`, period? | Feature/provider rollup |
| `get_integration_diagnostics` | `tenantId` | DB + Traccar + GPS freshness; points to `search_serilog_logs` for historical app errors |
| `search_auth_failures` | `tenantId`, period?, `severity?`, `eventKey?`, `limit?` | AuditEvents auth/authz/security failures; CorrelationId when present |
| `get_events_by_correlation_id` | `tenantId`, `correlationId`, `limit?` | Related AuditEvents or empty (never invent) |
| `search_serilog_logs` | `query?`, period?, `severity?`, `limit?`, `timeoutMs?` | Redacted line matches from rolling files; `available=false` only if log dir missing |
| `search_mcp_executions` | `tenantId`, period?, `toolName?`, `success?`, `correlationId?`, `sessionGuid?`, `limit?` | Structured execution rows (sanitized args/error) |

All tools: `ok` / `available`, `group: "logs"`, `readOnly`, `empty`, `warnings[]`, `doNotInvent` when unavailable. Lookback max **90 days**; default **7 days**; event limit max **100**. Serilog: max **2MB/file**, timeout capped **15s**, no shell.

---

## 4. Permission / redaction model

| Gate | Tools |
|------|-------|
| `Ai.ViewProviderHealth` **or** `Ai.Manage` **or** Platform SuperAdmin | All Phase 6 Logs tools |
| Standard `Ai.Chat` only | Denied (fail-closed) |
| `Platform.Audit.View` / `Platform.AuditLogs.View` | Security `list_audit_events` only (not required for Logs auth search) |

**Redaction (aggressive, tested):** passwords, connection strings, API keys, Bearer/JWT, refresh tokens, emails → `[EMAIL]`, phones → `[PHONE]`, secret-looking keys. Message/content previews truncated (≤400 / ≤300). Serilog lines and MCP args/errors sanitized at write and again on read.

**Hard rules:** `remediation: false`, `shellExecution: false`. Bridge never enables writes for Logs. Serilog tool reads files only under allowed roots (`SHEIKHGO_SERILOG_LOG_DIR` or `Backend/SheikhTravelSystem.API/Logs`).

---

## 5. Correlation ID handling

| Layer | Behavior |
|-------|----------|
| `AuditEvents.CorrelationId` | Queried by `get_events_by_correlation_id` / returned on auth failures when writers set it |
| `McpToolExecutions.CorrelationId` | Stamped per tool call by `SheikhGoMcpBridge` (loop sets ambient id for the turn) |
| `McpToolExecutions.SessionId` | AiChatSessions Id when chat-scoped (`AiMcpToolCallingLoop` / `AiChatGateway`) |
| Ai chat messages | No CorrelationId column — soft correlator `sessionId` on `get_ai_assistant_errors` |
| DeepInfra HTTP | Not yet stamped end-to-end (documented as partial `chat_to_mcp_correlation_propagation`) |
| Missing id | Empty result + guidance — **do not invent** related events |

Preserve ids across responses when present; never fabricate trails.

---

## 6. Unavailable capabilities + dependencies

See §2 table and MCP `get_logs_domain` / `get_log_source_status`. LLM prompt instructs: if `available=false`, say diagnostics could not be retrieved — do not invent root causes.

---

## 7. Tests

### SheikhGo-MCP
- `tests/phase6-logs.test.ts` — redaction, time filter (90d), empty, query failure, large volume cap, missing correlation, auth failures, diagnostic summary, tenant params
- `tests/phase6-serilog-mcp-exec.test.ts` — Serilog missing dir / redaction / empty / limits; MCP executions correlationId / redaction / empty / missing table / sessionGuid
- `tests/logs-status-maps.test.ts` — Phase 6 catalog, no Analytics overlap, Serilog + MCP executions as existing

### Sheikh-Travel-System
- `McpAllowlistTests` — Phase 6 tool names allowlisted (incl. `search_serilog_logs`, `search_mcp_executions`); permission gates
- `McpToolExecutionLoggerTests` — args/error sanitization

---

## 8. Files changed

### SheikhGo-MCP (`0.17.0`)
- `src/logs/serilog-file-search.ts` (new)
- `src/logs/redact.ts`
- `src/logs/status-maps.ts`
- `src/logs/logs-queries.ts`
- `src/config/config.ts` (Serilog MCP config / `SHEIKHGO_SERILOG_LOG_DIR`)
- `src/mcp/tools.ts`
- `scripts/export-tool-catalog.mjs`
- `package.json` → `0.17.0`
- `src/knowledge/agent-playbook.md`
- `tests/phase6-logs.test.ts`
- `tests/phase6-serilog-mcp-exec.test.ts` (new)
- `tests/logs-status-maps.test.ts`

### Sheikh-Travel-System
- `Backend/.../Migrations/McpToolExecutionsMigration.cs` (new)
- `Backend/.../Migrations/DatabaseMigrationRegistry.cs`
- `Backend/.../Mcp/McpAllowlist.cs`
- `Backend/.../Mcp/SheikhGoMcpBridge.cs` (execution logging + correlationId)
- `Backend/.../Mcp/McpToolExecutionLogger.cs` / `McpExecutionAmbient.cs` (new)
- `Backend/.../Ai/AiMcpToolCallingLoop.cs` (+ chatSessionId ambient)
- `Backend/.../Ai/AiChatGateway.cs` (passes sessionId)
- `Backend/.../DependencyInjection.cs`
- `Backend/.../Tests/AI/McpAllowlistTests.cs`
- `Backend/.../Tests/AI/McpToolExecutionLoggerTests.cs` (new)
- `Backend/.../Tests/AI/AiMcpToolCallingLoopTests.cs`
- `docs/AI-MCP-Phase6-Logs.md` (this file)
- `docs/AI-MCP-Bridge.md` (Logs Phase 6 note)
- ERP `mcp-catalog.json` assets (via export)

---

## 9. Commands

```bash
cd /Users/alirazatahir/Projects/SheikhGo-MCP && npm run typecheck && npm test && npm run export:catalog && npm run build

dotnet test /Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj \
  --filter "FullyQualifiedName~McpAllowlist|FullyQualifiedName~McpToolExecution|FullyQualifiedName~AiMcpToolCallingLoop"
```

Optional: `SHEIKHGO_SERILOG_LOG_DIR=/path/to/API/Logs` when MCP runs off the API host.

---

## 10. STOP for review

**Phase 6 complete** (Serilog file search + MCP execution table). Conversation context continues in **Phase 7** — see `docs/AI-MCP-Phase7-ConversationContext.md`.
