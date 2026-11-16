# Day 4 — SG-01 pass 1: nuqsan dhoondein (koi code change nahi)

**Kyun:** ye sabse bara issue hai — compliance page doosri companies ke drivers ke
documents dikha raha hai. Lekin aaj sirf **dhoondna** hai, theek nahi karna.

Do pass me isliye ke pehle aapko pata chale problem kitni bari hai. Agar ek hi session
me dono karwaayein to diff itna bara ho jayega ke review namumkin hai.

## 1. Terminal

```bash
cd ~/Projects/Sheikh-Travel-System
git checkout main && git pull
git checkout -b fix/SG-01-compliance-tenant-isolation
claude
```

## 2. Ye paste karein

```
Pass 1 — investigation only, change nothing.

Read docs/audit/audit.md section SG-01.

Use an Explore subagent to find every data-access path in Backend/ that reads a
tenant-owned entity, and classify each one:
  A) filters on TenantId from ITenantContext          -> OK
  B) filters on a TenantId that came from the request -> AUTHORIZATION BUG
  C) no TenantId filter at all                        -> LEAK

Cover Dapper SQL strings, EF Core queries, stored procedure calls, and any raw
ExecuteReader. Include read models and report queries, not just repositories.

Output a markdown table: file, method, table(s) read, classification, and the WHERE
clause as it stands. Start with anything touching ComplianceDocuments or
DriverDocuments, because that is the confirmed leak. Write the table to
docs/audit/SG-01-sweep.md.

Do not change any code in this pass.
```

## 3. Aaj ka kaam

`docs/audit/SG-01-sweep.md` khud padhein. Dekhein kitni rows LEAK hain.

Agar 5 hain to kal ka kaam chhota hai. Agar 50 hain to kal ka kaam ek din ka nahi,
teen din ka hai — aur ab aapko pehle se pata hai.

```bash
git add docs/audit/SG-01-sweep.md
git commit -m "docs(SG-01): tenant isolation sweep"
```
