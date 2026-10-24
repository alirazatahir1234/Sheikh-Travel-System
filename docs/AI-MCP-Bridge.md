# AI Assistant ↔ SheikhGo MCP Bridge (ADR-AI-001)

Opt-in glue in the SheikhGo **backend** that lets `/api/ai/chat` call **allowlisted read-only** SheikhGo MCP tools via stdio, then polish answers with **Qwen via DeepInfra**.

## Scope

| Included | Excluded |
|----------|----------|
| Database (live SELECT helpers) | Architecture / code / git analyzers |
| Bookings, Fleet, GPS/Traccar, SmartOps | `scaffold_*`, `draft_adr`, writes |
| Server-injected `tenantId` | Client/LLM-supplied `tenantId` |
| Lat/lng from SheikhGo GPS tables | Google Maps / Maps API keys |

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
- `SHEIKHGO_DB_CONNECTION` (optional; live MCP SELECT)
- `DEEPINFRA_API_KEY` (existing)

The API process **forces** `SHEIKHGO_ALLOW_WRITES=false` on the MCP child.

## Flow (`POST /api/ai/chat`)

1. Existing keyword `IAiTool` engine still runs (hybrid).
2. If `Ai:McpBridge:Enabled` + DeepInfra: send OpenAI-style `tools` (allowlisted MCP schemas) to Qwen.
3. On `tool_calls` → execute via MCP stdio → feed `role=tool` results → up to 3 rounds.
4. Final wording from Qwen. Fallback to pre-bridge LLM polish if MCP/tools unavailable.

Inspect allowlist: `GET /api/ai/mcp/tools`.

## Maps keys

Untouched. GPS MCP tools use DB lat/lng only; bridge does not read or pass Geocoding/Google Maps settings.
