# AI Assistant ↔ SheikhGo MCP Bridge (ADR-AI-001)

Opt-in glue in the SheikhGo **backend** that lets AI Assistant / chat / copilot call **allowlisted read-only** SheikhGo MCP tools via stdio (Database + Bookings/Fleet/GPS/SmartOps/Analytics + elevated Logs/Audit), then polish answers with **Qwen via DeepInfra** when configured.

## Scope

| Included (AI paths) | Excluded (stays on normal API / repos) |
|---------------------|----------------------------------------|
| Database live SELECT helpers + `get_approved_entity_counts` | All ERP CRUD controllers & EF/Dapper repositories |
| Bookings, Fleet, GPS/Traccar, SmartOps, **Analytics** MCP tools | Architecture / code / git analyzers (`list_modules`, `search_code`, …) |
| **Logs** (AI diagnostics) + **list_audit_events** when caller has elevated permissions | File scanners `scan_secrets` / `audit_api_auth` / `audit_permissions` (Cursor/dev only) |
| Deterministic **McpDatabaseRecordRouter** for “show me records / list vehicles / subscription modules (SQL)” | `scaffold_*`, `draft_adr`, controlled writes |
| Server-injected `tenantId`; permission-filtered tool discovery | Client/LLM-supplied `tenantId` |
| Lat/lng from SheikhGo GPS tables | Google Maps / Maps API keys |

**Analytics ≠ Logs.** KPIs use Analytics tools; AI/provider/integration diagnostics use **Phase 6 Logs** tools (require `Ai.ViewProviderHealth` or `Ai.Manage`) including `search_serilog_logs` and `search_mcp_executions`. See `docs/AI-MCP-Phase6-Logs.md`. Audit events require `Platform.Audit.View` / `Platform.AuditLogs.View` or `Ai.Manage`.

**Phase 7 — Conversation context.** Bounded SQL memory + three-layer context (conversation / business / live tools). Prior chat answers are never live KPIs. RAG deferred (no corpus). See `docs/AI-MCP-Phase7-ConversationContext.md`.

**Phase 8 — Production readiness.** Eval suite, security/reliability tests, turn correlation (`X-Correlation-Id`), UI retry. See `docs/AI-MCP-Phase8-ProductionReadiness.md`.

**Literal “everywhere” is not done in one pass.** REST ERP data access remains on repositories so the app does not break under MCP stdio. Maximize assistant/chat coverage first.

## What was wired (Database MCP)

| Path | Behavior |
|------|----------|
| `POST /api/ai/chat` (`AiChatGateway`) | Keyword `IAiTool`s first. If miss **or** question looks like a record list → **Database MCP prefetch** via `IMcpDatabaseRecordRouter`. DeepInfra also gets allowlisted MCP `tools` for Qwen tool-calling. |
| `POST /api/ai/assistant/query` (`AiAssistantGateway`) | Allowlisted reporting `IAiTool`s first. On miss/fail → shared `IAiMcpToolCallingLoop` (same DeepInfra↔MCP loop as chat, `mode=mcp+llm`), then MCP router fallback (`mode=mcp_database`). |
| `POST /api/ai/copilot/ask` (`AiCopilotService`) | Rule keywords first; else MCP router for list/record questions. |
| `GET /api/ai/mcp/tools` | Inspect allowlist + bridge enabled flag. |
| `McpAllowlist` | Database + Bookings/Fleet/GPS/SmartOps + Analytics; Logs + Audit elevated by permission. Architecture `list_modules` and file security scanners remain **denied**. |

### Record-router intents → MCP tools

Examples: list/show vehicles → `list_vehicles`; drivers → `list_drivers`; bookings/trips/routes; fuel records; fleet status; live positions; list/describe tables; “show me records from X” / subscription modules → curated `run_readonly_query` (tenant-scoped).

Subscription **entitlements with plan licensing** still prefer `GetTenantSubscriptionModules` (`IAiTool` + platform repos). MCP SQL is used when that tool is not selected (or as chat prefetch for generic module/table questions). Chat skips MCP prefetch when `GetTenantSubscriptionModules` already succeeded.

## What remains on repositories (out of scope for MCP)

- Vehicle/Driver/Booking/Trip **create/update/delete** and all MediatR command handlers  
- Platform admin module toggles (`SetTenantModules`, etc.)  
- Keyword `IAiTool`s still run first for many analytics phrases; Phase 3–5 expose matching Analytics MCP tools to the LLM tool loop (see `docs/AI-MCP-Phase5-Analytics.md`)  
- Mobile/driver APIs, SignalR, Traccar write-back, payments, WhatsApp bots  
- File-based Architecture MCP tools and file security scanners (never exposed to tenant AI)

## Config (no secrets in repo)

```json
"Ai": {
  "McpBridge": {
    "Enabled": false,
    "Command": "node",
    "Args": ["/absolute/path/to/SheikhGo-MCP/dist/index.js"],
    "RepositoryPath": "/absolute/path/to/Sheikh-Travel-System"
  }
}
```

Env overrides:

- `SHEIKHGO_MCP_COMMAND` / `SHEIKHGO_MCP_ARGS`
- `SHEIKHGO_REPOSITORY`
- `SHEIKHGO_DB_CONNECTION` (required for live Database MCP SELECT)
- `DEEPINFRA_API_KEY` (existing)

The API process **forces** `SHEIKHGO_ALLOW_WRITES=false` on the MCP child.

## Flow (`POST /api/ai/chat`)

1. Keyword `IAiTool` engine runs (hybrid).
2. If bridge enabled and tools miss / record intent → `IMcpDatabaseRecordRouter` calls allowlisted Database MCP tools (stdio).
3. If `Ai:McpBridge:Enabled` + DeepInfra: send OpenAI-style `tools` (allowlisted MCP schemas) to Qwen.
4. On `tool_calls` → execute via MCP stdio → feed `role=tool` results → up to 3 rounds.
5. Final wording from Qwen. Fallback to tool summaries / MCP prefetch / rule copilot.

Inspect allowlist: `GET /api/ai/mcp/tools`.

## Maps keys

Untouched. GPS MCP tools use DB lat/lng only; bridge does not read or pass Geocoding/Google Maps settings.
