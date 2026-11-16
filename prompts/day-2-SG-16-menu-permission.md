# Day 2 — SG-16: Vehicles menu ka permission galat hai

**Kyun:** Vehicles menu `Vehicle.Create` permission maangta hai, jabke baaki sab
`*.View` maangte hain. Matlab koi bhi read-only role (Accountant, Driver,
GPS Operator) se Vehicles menu gayab ho jata hai. Ek line ka fix.

## 1. Terminal

```bash
cd ~/Projects/Sheikh-Travel-System
git checkout main && git pull
git checkout -b fix/SG-16-vehicles-menu-permission
claude
```

## 2. Ye paste karein

```
Read docs/audit/audit.md section SG-16.

The Vehicles menu item at /vehicles is gated on Vehicle.Create. Every sibling item uses
a View permission (Driver.View, Maintenance.View, Fuel.View, GPS.View).

Find where the menu catalog is seeded or stored in Backend/, change it to Vehicle.View,
and write the migration if it lives in the database.

Then check every other menu item for the same mistake and list anything else gated on
a Create, Edit or Delete permission.
```

## 3. Check karein

- Platform > Menu Management kholein -> Vehicles ab `Vehicle.View` dikhaye
- Ho sake to ek Dispatcher user se login karke dekhein ke Vehicles menu nazar aata hai

## 4. Commit

```bash
git commit -am "fix(SG-16): menu visibility uses View permission not Create"
```
