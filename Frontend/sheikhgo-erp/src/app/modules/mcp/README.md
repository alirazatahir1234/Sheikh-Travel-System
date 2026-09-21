# MCP Console (sheikhgo-erp)

Operator console for **SheikhGo-MCP**, embedded in ERP at `/mcp`.

## What it is

- Nested layout under the existing ERP shell (auth, rail, notifications unchanged)
- Catalog-driven UI from `public/assets/mcp/mcp-catalog.json` (served as `/assets/mcp/mcp-catalog.json`)
- Bundled fallback in `shared/mcp-catalog.fallback.ts` if the asset request fails
- Prompt dock that **copies** prompts for Cursor/Claude (no browser→stdio MCP in v1)
- Permission: `Ai.View` (same as AI Management)

## Routes

| Path | Page |
| --- | --- |
| `/mcp` | Dashboard (hero, server grid, chat dock, right rail) |
| `/mcp/servers` | Filterable tool-group cards |
| `/mcp/tools` | Tools by group |
| `/mcp/chat` | Full chat dock |
| `/mcp/connections` | Cursor / Claude setup |
| `/mcp/settings` | Catalog meta + clear local activity |

## Refresh catalog

From the MCP repo:

```bash
cd /Users/alirazatahir/Projects/SheikhGo-MCP
npm run export:catalog
```

Writes into:

- `Frontend/sheikhgo-erp/public/assets/mcp/mcp-catalog.json`
- `Frontend/sheikhgo-erp/src/assets/mcp/mcp-catalog.json`
- `Frontend/sheikhgo-erp/src/app/modules/mcp/shared/mcp-catalog.fallback.ts`

## Local verify

```bash
cd Frontend/sheikhgo-erp && npm start
# sign in → open /mcp (or menu: MCP Console)
```
