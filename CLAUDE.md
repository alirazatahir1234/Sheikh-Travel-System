# Sheikh Travel System — working rules

One git repository, three parts:

- `Backend/`                   .NET 8 API          (see Backend/CLAUDE.md)
- `Frontend/sheikhgo-erp/`     Angular ERP         (see its CLAUDE.md)
- `Frontend/sheikh-driver/`    Flutter app         (see its CLAUDE.md)

Multi-tenant SaaS. Tenants are fleet operators. Cross-tenant data leakage is a P0 incident.

## The audit driving current work

`docs/audit/audit.md` — issue ids SG-01 .. SG-49.
Read the relevant section before starting an issue. `docs/audit/playbook.md` has longer
prompts and background for later issues.

## Because this is one repo

- One branch per issue, even when the fix spans Backend and Frontend:
  `fix/SG-01-compliance-tenant-isolation`
- Commit messages carry the id: `fix(SG-01): scope compliance queries to tenant`
- An API contract change (enum, DTO, endpoint) is not done until the ERP and the Flutter
  app are updated in the same branch. Say which parts you touched.

## Always

- Plan mode for anything touching more than three files. Show me the plan first.
- Change only what I asked for. Do not reformat, rename or "improve" untouched code.
- When you finish, give me: files changed, the migration (if any), the test you added,
  and the exact steps to verify it by hand.
- Never commit anything under `secrets/`, any `appsettings.Production.json`, `.env`,
  or a `.pfx`. If you find a secret in the working tree, stop and tell me — do not
  "clean it up" by deleting it, it needs rotating.
