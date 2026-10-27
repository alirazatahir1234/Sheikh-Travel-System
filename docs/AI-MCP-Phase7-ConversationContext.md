# Phase 7 — Conversation Context, Bounded Memory, Optional RAG (STOP for review)

**Status:** Complete. Phase 8 documented in `docs/AI-MCP-Phase8-ProductionReadiness.md`.  
**Date:** 2026-10-26  
**Repos:** Sheikh-Travel-System AI gateway + Angular chat (SheikhGo-MCP unchanged; reuse Phases 1–6 tools).

---

## 1. Goal

Give SheikhGo AI Assistant **reliable multi-turn context** without treating prior chat answers as live fleet truth:

1. **Conversation** — recent messages, clarifications/follow-ups, extractive summary for long chats  
2. **Business** — authenticated user, authorized tenant/company, roles/permissions, timezone, approved KPI notes  
3. **Live retrieved data** — current-turn tool / MCP results only  

Optional **RAG** for manuals/policies/procedures — **deferred** (no corpus in repo).

---

## 2. What was inspected / reused

| Area | Finding |
|------|---------|
| `AiChatSessions` / `AiChatMessages` | Phase 1 SQL memory already tenant+user scoped |
| `AiChatGateway` | History `TakeLast(20)` only; no summary; mixed system prompt |
| `AiAssistantGateway` | Stateless — Angular default path had **no** session persistence |
| Angular `ai-chat` | `useAssistant=true` → `/assistant/query` without `sessionId` |
| RAG / doc store | **None** (no manuals/policies vector store or file corpus for AI) |
| Phases 4–6 MCP | Remain source of truth for live KPIs / logs |

---

## 3. Three context types (code)

| Type | Builder section | Source of truth |
|------|-----------------|-----------------|
| Conversation | `=== Conversation context ===` | `ContextSummary` + bounded recent messages |
| Business | `=== Business context ===` | JWT user, tenantId, roles, AI permissions, company branding/timezone, approved KPI notes |
| Live data | `=== Live retrieved data ===` | Current-turn `IAiTool` / MCP prefetch only |

Implemented in:

- `IAiConversationContextBuilder` / `AiConversationContextBuilder`
- Wired into `AiChatGateway` + `AiAssistantGateway` system / seed prompts

**Stale facts rule:** prior assistant text is labeled memory-only; live numbers require tools/MCP this turn.

---

## 4. Follow-up resolution

- Heuristic detects anaphora / continuation (`those`, `which of those`, `compare with last month`, …).
- With authorized memory → inject conversation section + instruct model to resolve refs then **re-query tools**.
- Without memory → concise clarification (`mode=clarify`); do not invent the prior set.
- Examples supported in product intent: maintenance list → highest fuel among those → compare last month (via conversation + Analytics/Fleet tools).

---

## 5. Persistence + bounds

| Mechanism | Behavior |
|-----------|----------|
| Store | Existing `AiChatSessions` / `AiChatMessages` |
| New columns | `ContextSummary`, `SummaryUpdatedAt` (`AiConversationContextPhase7Migration`) |
| Isolation | Load/ensure always `TenantId` + `UserId`; cross-user never reuses session |
| Tenant change | Owned session with different tenant → **new** session + notice |
| Recent cap | `Ai:ConversationContext:MaxRecentMessages` (default **16**, clamp 4–40) |
| Token budget | `MaxApproxTokens` (default **6000**, chars/4 heuristic) |
| Summarization | After `SummarizeAfterMessages` (default **24**), older turns folded extractively into `ContextSummary` (`SummaryMaxChars` 2000) |
| MCP loop | Still respects `Ai:McpBridge:MaxHistoryMessages` |

Assistant path now always creates/continues a session and returns `sessionId`. Angular passes it on subsequent turns.

---

## 6. RAG

**Deferred / unavailable.**

- No manuals/policies/procedures corpus in Sheikh-Travel-System or configured vector store.
- `DeferredAiRagRetriever` (`IAiRagRetriever.IsAvailable = false`).
- Docs must not invent a doc store. Live metrics stay on SQL/MCP.

When a corpus exists later: passages + sources, permission checks, label as untrusted, never replace live tools.

---

## 7. Tests

| Suite | Coverage |
|-------|----------|
| `AiConversationContextPhase7Tests` | Follow-up detect/clarify, three-layer separation, truncation, token budget, extractive summary, missing context, tenant-change contract, RAG deferred, duplicate user trim |
| Existing `AiAssistantGatewayTests` | Updated for memory/context DI + session-aware MCP loop |

```bash
dotnet test Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj \
  --filter "FullyQualifiedName~AiConversationContextPhase7|FullyQualifiedName~AiAssistantGateway"
```

---

## 8. Key paths

- `Backend/.../AiConversationContextOptions.cs`
- `Backend/.../IAiConversationMemoryService.cs` / `IAiConversationContextBuilder.cs` / `IAiRagRetriever`
- `Backend/.../Conversation/AiConversationMemoryService.cs`
- `Backend/.../Conversation/AiConversationContextBuilder.cs`
- `Backend/.../Conversation/DeferredAiRagRetriever.cs`
- `Backend/.../Migrations/AiConversationContextPhase7Migration.cs`
- `Backend/.../Ai/AiChatGateway.cs` / `AiAssistantGateway.cs`
- `Frontend/.../ai-platform.service.ts` / `ai-chat.component.ts`
- `docs/AI-MCP-Phase7-ConversationContext.md` (this file)
- Config: `Ai:ConversationContext` in `appsettings.json`

---

## 9. Remaining gaps

1. Currency code not yet loaded into business context (timezone/company name are).  
2. Summarization is extractive (no LLM fold) — may be coarse for very long threads.  
3. FAB / other surfaces that call assistant without UI session still get a **new** server session each cold start unless they persist `sessionId`.  
4. RAG corpus + permissioned retrieval not built.  
5. End-to-end live DeepInfra follow-up chain not automated in CI (unit heuristics + gateway wiring covered).

---

## 10. Explicit stop

**Phase 7 complete.** Production readiness / eval / security continues in **Phase 8** — see `docs/AI-MCP-Phase8-ProductionReadiness.md`.
