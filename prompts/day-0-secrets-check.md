# Day 0 — secrets check (10 minute, sabse pehle)

Repo root me `secrets/` folder hai. Agar woh git me track ho raha hai to ye audit
ke har issue se zyada urgent hai — commit history me secret hamesha ke liye reh jata hai.

## 1. Khud check karein

```bash
cd ~/Projects/Sheikh-Travel-System
git ls-files secrets | head -20
git ls-files | grep -iE "appsettings\.(Production|Staging)|\.env$|\.pfx$"
```

## 2. Agar output KHALI hai

Sab theek hai. Day 1 par chale jayein.

## 3. Agar output me files hain

Rukein. Ye karein, isi tarteeb se:

**Pehle rotate karein, delete baad me.** File git history me pehle se hai —
delete karne se woh history se nahi jati. Jo bhi us file me tha woh ab compromised
samjhein:

- Database connection string -> SQL user ka password badlein
- JWT signing key -> naya key generate karein (sab users logout ho jayenge, theek hai)
- Traccar admin token -> Traccar me regenerate karein
- Meta / WhatsApp app secret -> Meta dashboard me regenerate karein
- Azure credentials -> portal me rotate karein

**Phir tracking band karein:**

```bash
git rm -r --cached secrets
echo "secrets/" >> .gitignore
echo "appsettings.Production.json" >> .gitignore
echo "appsettings.Staging.json" >> .gitignore
echo ".env" >> .gitignore
echo "*.pfx" >> .gitignore
git commit -m "chore: stop tracking secrets, rotate keys"
```

**History se poori tarah hatana** (optional, baad me): `git filter-repo` ya BFG
se hota hai, lekin woh poori history rewrite karta hai — agar repo kisi aur ke
paas bhi hai to pehle baat kar lein. Rotate kar lene ke baad ye urgent nahi rehta.

## 4. Claude se madad chahiye ho to

```
Read docs/audit/audit.md if it is relevant.

Secrets were committed to this repo under secrets/. I have already rotated the keys.
Help me stop tracking them: update .gitignore, git rm --cached the right paths, and
list every other file in the working tree that looks like it holds a credential.

Do not delete anything. Do not rewrite history. Just show me the commands and the list.
```
