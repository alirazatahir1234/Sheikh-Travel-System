# Day 5 — SG-01 pass 2: ab theek karein

**Pehle:** kal wali `docs/audit/SG-01-sweep.md` khuli rakhein.

## 1. Terminal

```bash
cd ~/Projects/Sheikh-Travel-System
git checkout fix/SG-01-compliance-tenant-isolation
claude
```

## 2. Ye paste karein

```
Pass 2 — fix.

Using docs/audit/SG-01-sweep.md, fix every row classified LEAK or AUTHORIZATION BUG.

Rules:
- TenantId comes from ITenantContext only.
- Add the filter to the inner query too, not just the outer select. Joins count.
- Where a query is legitimately platform-scope (Tenants, Plans, Modules, MenuCatalog),
  leave it and mark it with a // PLATFORM-SCOPE comment explaining why.
- Do not change behaviour beyond adding the filter. No refactoring.

Then propose a guard that makes this harder to regress, and tell me the trade-offs
before implementing it.

For each fixed method add an xUnit test: seed two tenants, query as tenant A,
assert zero rows from tenant B.

In your summary give me the full SQL of every query you changed, before and after.
```

## 3. Review — ye step skip mat karein

SQL isliye maangte hain ke sabse aam ghalti ye hoti hai: bahar wale SELECT par filter
lag jata hai aur andar wale join par reh jata hai. Har SQL khud padhein.

## 4. Check karein

- `cd Backend && dotnet test` pass ho
- Hannan Khokhar Travels select karke /fleet/compliance kholein ->
  sirf Munna Driver ke documents dikhein, 39 nahi
- sweep file me koi LEAK row baqi na ho

## 5. Commit, phir hostile review

```bash
git commit -am "fix(SG-01): scope all tenant-owned queries to the tenant context"
```

Phir ye chalayein:

```
Spawn a subagent to review the diff on this branch as a hostile reviewer looking for
tenant filters we missed, especially on inner joins and secondary queries inside the
same handler.
```
