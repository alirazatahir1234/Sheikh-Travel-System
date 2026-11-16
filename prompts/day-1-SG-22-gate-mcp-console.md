# Day 1 — SG-22: MCP console ko production se band karein

**Kyun pehle:** sabse chhota kaam (~1 ghanta) aur sabse bada security surface band
hota hai. Asli maqsad ye hai ke aapko workflow ki aadat pad jaye.

## 1. Terminal

```bash
cd ~/Projects/Sheikh-Travel-System
git checkout -b fix/SG-22-gate-mcp-console
claude
```

## 2. Plan mode on karein

`Shift+Tab` do baar dabayein. Prompt par "plan mode" likha dikhega.

## 3. Ye paste karein

```
Read docs/audit/audit.md section SG-22.

The /mcp console (14 servers, 117 registered tools, Connect Client for external IDEs)
is reachable inside the production ERP.

Make it unavailable when the environment is Production, and require a dedicated
Platform.Mcp.Access permission in every other environment. Every tool invocation
should write an audit event.

Before changing anything, tell me:
- which files in Backend/ you will touch
- whether anything in the product depends on /mcp at runtime
- how Frontend/sheikhgo-erp should hide the menu item when the API gates it
```

## 4. Check karein

- `cd Backend && dotnet build` pass ho
- Production config me chalayein -> /mcp 404 ya 403 de
- Development me permission ke bagair bhi 403 de

## 5. Commit

```bash
git add -A
git commit -m "fix(SG-22): gate MCP console behind environment and permission"
```
