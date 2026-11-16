# Prompts

Ek din, ek file. Upar se shuru karein:

  day-0  secrets check (10 minute — sabse pehle)
  day-1  SG-22  MCP console band
  day-2  SG-16  menu permission
  day-3  SG-18  CNIC mask
  day-4  SG-01  tenant leak — sirf dhoondna
  day-5  SG-01  tenant leak — theek karna
  NEXT   iske baad kya

Har issue par wohi 5 steps:

1. `git checkout -b fix/SG-xx-...`
2. `claude` chalayein, phir `Shift+Tab` do baar (plan mode)
3. prompt paste karein
4. plan padhein, approve karein
5. diff dekhein, build chalayein, commit karein

Ye ek hi repo hai — branch aur commit bhi ek hi, chahe fix Backend aur Frontend
dono ko chhuye.
