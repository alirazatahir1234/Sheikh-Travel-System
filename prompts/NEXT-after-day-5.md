# Day 5 ke baad

5 din ho gaye to workflow aa gaya. Ab order ye hai:

| Agla | Issue | Kya | Kahan |
|---|---|---|---|
| 6  | SG-06 | Global tenant switcher header me — 3 screens unblock hoti hain | Backend + erp |
| 7  | SG-05 | SUPER_ADMIN role ko asli permissions dena | Backend |
| 8  | SG-07 | Audit trail readable banana | Backend + erp |
| 9  | SG-11 + SG-42 | Suspended companies + cross-tenant user list | Backend + erp |
| 10 | SG-02 + SG-03 + SG-14 | Invoice + payment chain (sabse bara, 1-2 hafte) | Backend + erp |
| 11 | SG-08 | MFA | Backend + erp |
| 12 | SG-04 | GPS staleness (Live / Stale / Offline) | Backend + erp + driver |

In sab ke poore prompts `docs/audit/playbook.md` me hain, "Prompt pack" wale section me.
Woh tab kholein jab ye 5 din ho jayein — pehle nahi.

## Jo audit me nahi tha lekin dekhna hoga

Audit sirf ERP ka hai. `Frontend/sheikh-driver` wahi API use karta hai, isliye
SG-01 (tenant leak), SG-09 (API host) aur SG-04 (staleness) us par bhi lagte hain.
SG-01 aur SG-09 Backend me theek hote hi app ko bhi fayda hoga; SG-04 ke liye app me
alag kaam chahiye.

## Har issue par wohi 5 steps

1. `git checkout -b fix/SG-xx-...`
2. `claude` -> `Shift+Tab` `Shift+Tab`
3. prompt paste
4. plan padhein, approve karein
5. diff dekhein, build chalayein, commit
