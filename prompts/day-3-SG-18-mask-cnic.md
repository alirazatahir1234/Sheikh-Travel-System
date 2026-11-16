# Day 3 — SG-18: CNIC numbers plain text me dikh rahe hain

**Kyun:** /customers par har customer ka pura 13-digit CNIC saaf nazar aa raha hai,
aur CSV export me bhi chala jata hai. Ye PII hai — kisi bhi enterprise ya government
client ki security review isi par atak jayegi.

Backend aur Frontend dono change honge, lekin **ek hi branch** — ye monorepo hai.

## 1. Terminal

```bash
cd ~/Projects/Sheikh-Travel-System
git checkout main && git pull
git checkout -b fix/SG-18-mask-cnic
claude
```

## 2. Pehla prompt (Backend)

```
Read docs/audit/audit.md section SG-18.

Full 13-digit CNIC numbers render in plain text in the customer list and in the CSV
export, with no masking and no separate permission.

In Backend/: add a Customer.ViewPII permission. List endpoints return CNIC masked to
the last 4 unless the caller holds it. Unmasked retrieval is a separate endpoint that
writes an audit event naming the customer and the actor. Exclude CNIC from the default
CSV export.

Make sure CNIC never reaches Serilog — add a destructuring policy if needed.

Also check whether CNIC is exposed on driver records or in any report, and tell me.
```

## 3. Doosra prompt (Frontend, usi session me)

```
Now Frontend/sheikhgo-erp: update the customers list to show the masked value with a
reveal action for users holding Customer.ViewPII. The reveal must call the audited
unmask endpoint — do not un-hide a value that is already in the DOM.
Remove CNIC from the default export button.
```

## 4. Check karein

- /customers par CNIC `*********1234` jaisa dikhe
- Export CSV me CNIC column na ho
- Reveal karne par audit event bane

## 5. Commit

```bash
git add -A
git commit -m "fix(SG-18): mask CNIC and gate reveal behind Customer.ViewPII"
```
