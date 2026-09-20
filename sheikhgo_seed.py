#!/usr/bin/env python3
"""
SheikhGo demo-tenant seed generator.

Emits CSVs you can bulk-load into SQL Server for a DEMO tenant.
No dependencies beyond the standard library.

    python3 sheikhgo_seed.py --out ./seed --region dubai --vehicles 32 --days 30

Everything it produces is SYNTHETIC. Load it only into a tenant clearly
labelled as a demo, never into a tenant that holds real customer data.
"""

import argparse
import csv
import math
import os
import random
from datetime import datetime, timedelta, timezone

# --------------------------------------------------------------------------
# Regions: depots and destination nodes. Coordinates are real road-network
# points so traces land on roads rather than in the sea.
# --------------------------------------------------------------------------
REGIONS = {
    "dubai": {
        "tz_offset": 4,
        "plate_fmt": lambda r: f"{r.choice('ABCDEFGHIJKLMNOPQRSTUVWX')} {r.randint(10000, 99999)}",
        "depots": [
            ("Al Quoz Depot",        25.1279, 55.2260),
            ("Jebel Ali Yard",       24.9857, 55.0600),
            ("Sharjah Industrial 12", 25.3100, 55.4400),
        ],
        "nodes": [
            ("Dubai Investment Park", 24.9800, 55.1700),
            ("Al Qusais",             25.2830, 55.3800),
            ("Dubai South",           24.8960, 55.1610),
            ("Ras Al Khor",           25.1860, 55.3400),
            ("Deira Port",            25.2700, 55.3080),
            ("Business Bay",          25.1860, 55.2620),
            ("Ajman Free Zone",       25.4050, 55.4700),
            ("Abu Dhabi ICAD",        24.3200, 54.5300),
            ("Al Ain Industrial",     24.2400, 55.7300),
            ("Hatta",                 24.8000, 56.1200),
        ],
    },
    "riyadh": {
        "tz_offset": 3,
        "plate_fmt": lambda r: f"{r.randint(1000, 9999)} {''.join(r.sample('ABDEGHJKLNRSTUVXZ', 3))}",
        "depots": [
            ("Sulay Depot",        24.6300, 46.8300),
            ("Al Kharj Road Yard", 24.5600, 46.8900),
            ("Exit 18 Logistics",  24.7600, 46.8600),
        ],
        "nodes": [
            ("Riyadh Dry Port",   24.6900, 46.8400),
            ("Al Olaya",          24.6900, 46.6850),
            ("Diriyah",           24.7370, 46.5750),
            ("King Khalid Airport", 24.9570, 46.6990),
            ("Al Kharj",          24.1480, 47.3050),
            ("Al Qassim Road",    25.0800, 46.4500),
            ("Industrial City 2", 24.5900, 46.7900),
            ("Dammam Highway",    24.7000, 47.1000),
        ],
    },
    "lahore": {
        "tz_offset": 5,
        "plate_fmt": lambda r: f"{r.choice(['LEA','LEB','LEC','LED','LZA'])}-{r.randint(1000, 9999)}",
        "depots": [
            ("Sundar Depot",     31.2800, 74.1300),
            ("Ravi Yard",        31.6100, 74.2900),
            ("Pasrur Yard",      32.2700, 74.6700),
        ],
        "nodes": [
            ("Multan Road",      31.4400, 74.2500),
            ("Sialkot Bypass",   32.4900, 74.5300),
            ("Gujranwala",       32.1600, 74.1900),
            ("Faisalabad Road",  31.4200, 73.0800),
            ("Allama Iqbal Airport", 31.5220, 74.4030),
            ("Raiwind",          31.2500, 74.2100),
            ("Kamoke",           31.9700, 74.2200),
        ],
    },
}

# vehicle_type, make/model pool, share of fleet, tank litres, l/100km
VEHICLE_MIX = [
    ("Truck",  [("Hino", "500 Series"), ("Isuzu", "FVR"), ("Mitsubishi Fuso", "Fighter"),
                ("Volvo", "FH16"), ("Mercedes-Benz", "Actros")], 0.34, 300, 31.0),
    ("Van",    [("Toyota", "HiAce"), ("Ford", "Transit"), ("Nissan", "Urvan"),
                ("Mercedes-Benz", "Sprinter")],                   0.25, 70,  11.5),
    ("Pickup", [("Toyota", "Hilux"), ("Nissan", "Navara"), ("Isuzu", "D-Max"),
                ("Mitsubishi", "L200")],                          0.19, 80,  10.8),
    ("Bus",    [("Toyota", "Coaster"), ("Higer", "KLQ6119"), ("King Long", "XMQ6900")],
                                                                  0.12, 200, 24.0),
    ("Car",    [("Toyota", "Corolla"), ("Honda", "Civic"), ("Hyundai", "Elantra"),
                ("Nissan", "Sunny")],                             0.10, 50,  7.4),
]

# Device models actually supported by SheikhGo, with plausible IMEI prefixes.
DEVICE_MODELS = [
    ("Teltonika", "FMB920", "35722"),
    ("Teltonika", "FMC130", "35722"),
    ("Teltonika", "FMB640", "35722"),
    ("Queclink",  "GV300W", "86825"),
    ("Queclink",  "GV58CEU", "86825"),
    ("Jimi IoT",  "VG03",   "86229"),
    ("Jimi IoT",  "JC450",  "86229"),
]

FIRST_NAMES = ["Imran", "Bilal", "Rashid", "Yousaf", "Kamran", "Adnan", "Faisal", "Zahid",
               "Naveed", "Tariq", "Junaid", "Saleem", "Waqar", "Asif", "Nadeem", "Shahid",
               "Haroon", "Irfan", "Mansoor", "Qasim", "Rehan", "Sohail", "Umair", "Zeeshan",
               "Abdullah", "Hamza", "Khalid", "Majid", "Noman", "Owais", "Danish", "Farhan"]
LAST_NAMES = ["Ahmed", "Khan", "Iqbal", "Hussain", "Raza", "Malik", "Sheikh", "Butt",
              "Chaudhry", "Qureshi", "Siddiqui", "Farooq", "Aslam", "Javed", "Mahmood",
              "Nawaz", "Rafiq", "Saeed", "Tariq", "Yousaf"]

# status -> share of fleet. Tuned so a screenshot looks like a working weekday.
STATUS_MIX = [("Moving", 0.44), ("Idle", 0.19), ("Parked", 0.22),
              ("Offline", 0.09), ("NeverSeen", 0.03), ("Scheduled", 0.03)]

ALERT_TYPES = [
    ("Overspeed",       0.30, "High"),
    ("HarshBraking",    0.16, "Medium"),
    ("IdleExceeded",    0.18, "Medium"),
    ("GeofenceExit",    0.12, "Medium"),
    ("GeofenceEntry",   0.08, "Low"),
    ("DeviceOffline",   0.08, "High"),
    ("MaintenanceDue",  0.05, "Medium"),
    ("FuelDrop",        0.025, "Critical"),
    ("SOS",             0.005, "Critical"),
]

MAINT_TYPES = ["Oil & Filter Change", "Brake Pad Replacement", "Tyre Rotation",
               "Annual Inspection", "Battery Replacement", "AC Service",
               "Gearbox Service", "Suspension Check"]


def hav_km(a_lat, a_lon, b_lat, b_lon):
    r = 6371.0
    p1, p2 = math.radians(a_lat), math.radians(b_lat)
    dp = math.radians(b_lat - a_lat)
    dl = math.radians(b_lon - a_lon)
    h = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(math.sqrt(h))


def bearing(a_lat, a_lon, b_lat, b_lon):
    p1, p2 = math.radians(a_lat), math.radians(b_lat)
    dl = math.radians(b_lon - a_lon)
    y = math.sin(dl) * math.cos(p2)
    x = math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl)
    return (math.degrees(math.atan2(y, x)) + 360) % 360


def build_leg(rnd, start, end, interval_s):
    """Interpolate start->end with a curved path, road-ish jitter and a speed profile.
    Returns [(frac_t, lat, lon, speed_kmh, heading, ignition)]."""
    dist = hav_km(*start, *end)
    cruise = rnd.uniform(52, 88) if dist > 15 else rnd.uniform(28, 52)
    duration_s = max(300, int((dist / cruise) * 3600))
    steps = max(4, duration_s // interval_s)

    # one control point perpendicular to the straight line => a curved route
    mid_lat = (start[0] + end[0]) / 2
    mid_lon = (start[1] + end[1]) / 2
    dx, dy = end[1] - start[1], end[0] - start[0]
    bow = rnd.uniform(-0.16, 0.16)
    ctrl = (mid_lat + dx * bow, mid_lon - dy * bow)

    pts = []
    for i in range(steps + 1):
        t = i / steps
        # quadratic bezier
        lat = (1 - t) ** 2 * start[0] + 2 * (1 - t) * t * ctrl[0] + t ** 2 * end[0]
        lon = (1 - t) ** 2 * start[1] + 2 * (1 - t) * t * ctrl[1] + t ** 2 * end[1]
        lat += rnd.gauss(0, 0.00045)
        lon += rnd.gauss(0, 0.00045)

        # trapezoid speed profile + traffic noise + occasional stop
        if t < 0.08:
            sp = cruise * (t / 0.08)
        elif t > 0.92:
            sp = cruise * ((1 - t) / 0.08)
        else:
            sp = cruise * rnd.uniform(0.80, 1.12)
        if rnd.random() < 0.04:
            sp = rnd.uniform(0, 6)          # signal / congestion
        sp = max(0.0, min(sp, 110.0))
        pts.append([t, lat, lon, round(sp, 1), 0.0, 1])

    for i in range(len(pts)):
        j = min(i + 1, len(pts) - 1)
        pts[i][4] = round(bearing(pts[i][1], pts[i][2], pts[j][1], pts[j][2]), 1)
    return pts, duration_s


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="./seed")
    ap.add_argument("--region", default="dubai", choices=sorted(REGIONS))
    ap.add_argument("--vehicles", type=int, default=32)
    ap.add_argument("--days", type=int, default=30)
    ap.add_argument("--trace-days", type=int, default=7,
                    help="days of full GPS position traces (older trips keep summaries only)")
    ap.add_argument("--interval", type=int, default=30, help="position interval, seconds")
    ap.add_argument("--tenant", default="DEMO")
    ap.add_argument("--seed", type=int, default=20260918)
    args = ap.parse_args()

    rnd = random.Random(args.seed)
    reg = REGIONS[args.region]
    tz = timezone(timedelta(hours=reg["tz_offset"]))
    now = datetime.now(tz).replace(microsecond=0)
    os.makedirs(args.out, exist_ok=True)

    def w(name, header, rows):
        p = os.path.join(args.out, name)
        with open(p, "w", newline="", encoding="utf-8") as f:
            cw = csv.writer(f)
            cw.writerow(header)
            cw.writerows(rows)
        return len(rows)

    # ---------------- drivers ----------------
    n_drivers = int(args.vehicles * 1.15)
    drivers = []
    used = set()
    for i in range(1, n_drivers + 1):
        while True:
            nm = f"{rnd.choice(FIRST_NAMES)} {rnd.choice(LAST_NAMES)}"
            if nm not in used:
                used.add(nm)
                break
        drivers.append({
            "id": i, "name": nm,
            "phone": f"+9715{rnd.randint(10, 69)}{rnd.randint(100000, 999999)}",
            "licence": f"DL-{rnd.randint(100000, 999999)}",
            "licence_expiry": (now + timedelta(days=rnd.randint(-20, 900))).date().isoformat(),
            "status": rnd.choices(["Active", "OnLeave", "Inactive"], [0.86, 0.09, 0.05])[0],
            "rating": round(rnd.uniform(3.4, 4.9), 1),
        })
    w("drivers.csv",
      ["driver_id", "tenant", "full_name", "phone", "licence_no", "licence_expiry", "status", "safety_rating"],
      [[d["id"], args.tenant, d["name"], d["phone"], d["licence"], d["licence_expiry"],
        d["status"], d["rating"]] for d in drivers])

    # ---------------- geofences ----------------
    geofences = []
    gid = 1
    for nm, la, lo in reg["depots"]:
        geofences.append([gid, args.tenant, nm, "Depot", round(la, 6), round(lo, 6), 350]); gid += 1
    for nm, la, lo in reg["nodes"][:6]:
        geofences.append([gid, args.tenant, f"{nm} Site", "CustomerSite",
                          round(la, 6), round(lo, 6), rnd.choice([250, 400, 600])]); gid += 1
    w("geofences.csv",
      ["geofence_id", "tenant", "name", "type", "center_lat", "center_lon", "radius_m"], geofences)

    # ---------------- vehicles + devices ----------------
    types = []
    for tname, models, share, tank, cons in VEHICLE_MIX:
        types += [(tname, models, tank, cons)] * max(1, round(share * args.vehicles))
    while len(types) < args.vehicles:
        types.append(types[rnd.randrange(len(types))])
    types = types[:args.vehicles]
    rnd.shuffle(types)

    statuses = []
    for s, share in STATUS_MIX:
        statuses += [s] * max(0, round(share * args.vehicles))
    while len(statuses) < args.vehicles:
        statuses.append("Parked")
    statuses = statuses[:args.vehicles]
    rnd.shuffle(statuses)

    vehicles, devices = [], []
    for i in range(args.vehicles):
        tname, models, tank, cons = types[i]
        make, model = rnd.choice(models)
        vid = i + 1
        brand, dmodel, prefix = rnd.choice(DEVICE_MODELS)
        imei = prefix + "".join(str(rnd.randint(0, 9)) for _ in range(15 - len(prefix)))
        depot = rnd.choice(reg["depots"])
        st = statuses[i]
        drv = drivers[i % len(drivers)]
        odo = rnd.randint(18_000, 460_000)
        vehicles.append({
            "id": vid, "plate": reg["plate_fmt"](rnd), "make": make, "model": model,
            "type": tname, "year": rnd.randint(2016, 2025), "tank": tank, "cons": cons,
            "status": st, "depot": depot, "driver": drv, "odo": odo, "imei": imei,
            "vin": "".join(rnd.choice("ABCDEFGHJKLMNPRSTUVWXYZ0123456789") for _ in range(17)),
        })
        last_seen = {
            "NeverSeen": "",
            "Offline": (now - timedelta(hours=rnd.randint(6, 70))).isoformat(),
        }.get(st, (now - timedelta(seconds=rnd.randint(3, 90))).isoformat())
        devices.append([vid, args.tenant, imei, brand, dmodel,
                        "Traccar", "NeverSeen" if st == "NeverSeen" else
                        ("Offline" if st == "Offline" else "Online"),
                        last_seen, rnd.randint(55, 100) if st != "NeverSeen" else "",
                        rnd.randint(8, 31) if st != "NeverSeen" else ""])

    w("vehicles.csv",
      ["vehicle_id", "tenant", "plate_no", "make", "model", "vehicle_type", "year", "vin",
       "tank_litres", "avg_l_per_100km", "home_depot", "current_status", "assigned_driver_id",
       "odometer_km", "device_imei"],
      [[v["id"], args.tenant, v["plate"], v["make"], v["model"], v["type"], v["year"], v["vin"],
        v["tank"], v["cons"], v["depot"][0], v["status"], v["driver"]["id"], v["odo"], v["imei"]]
       for v in vehicles])

    w("devices.csv",
      ["vehicle_id", "tenant", "imei", "brand", "model", "protocol", "conn_status",
       "last_seen_utc", "battery_pct", "gsm_signal"], devices)

    # ---------------- trips + positions + alerts + fuel ----------------
    trips, positions, alerts, fuel = [], [], [], []
    trip_id = 1
    alert_id = 1
    fuel_id = 1
    nodes = reg["nodes"]

    for v in vehicles:
        if v["status"] == "NeverSeen":
            continue                      # keep exactly one class of silent asset
        per_day = {"Truck": (2, 4), "Van": (3, 6), "Pickup": (2, 5),
                   "Bus": (2, 3), "Car": (2, 5)}[v["type"]]
        for d in range(args.days, -1, -1):
            day = (now - timedelta(days=d)).replace(hour=0, minute=0, second=0)
            if day.weekday() == 4 and rnd.random() < 0.6:   # lighter Friday
                continue
            trace = d <= args.trace_days
            start_clock = rnd.randint(6, 9)
            cursor = day + timedelta(hours=start_clock, minutes=rnd.randint(0, 50))
            loc = (v["depot"][1], v["depot"][2])
            loc_name = v["depot"][0]
            # on the current day keep dispatching until the clock catches up,
            # so live vehicles have a leg in flight right now
            legs = 14 if d == 0 else rnd.randint(*per_day)
            for _ in range(legs):
                if cursor >= now:
                    break
                dest = rnd.choice([n for n in nodes if n[0] != loc_name])
                dest_pt = (dest[1], dest[2])
                pts, dur = build_leg(rnd, loc, dest_pt, args.interval)
                dist = hav_km(*loc, *dest_pt) * rnd.uniform(1.10, 1.35)
                end_ts = cursor + timedelta(seconds=dur)
                # today: let the last leg of a Moving vehicle still be running
                in_progress = False
                if end_ts > now:
                    if d == 0 and cursor < now and v["status"] in ("Moving", "Idle"):
                        in_progress = True
                        pts = [p for p in pts
                               if cursor + timedelta(seconds=int(p[0] * dur)) <= now]
                        if len(pts) < 3:
                            break
                    else:
                        break
                drv = v["driver"]
                speeds = [p[3] for p in pts]
                max_sp = max(speeds)
                idle_min = sum(1 for s in speeds if s < 3) * args.interval / 60.0
                litres = dist * v["cons"] / 100.0

                trips.append([trip_id, args.tenant, v["id"], drv["id"],
                              cursor.isoformat(), "" if in_progress else end_ts.isoformat(),
                              loc_name, dest[0],
                              round(dist, 2), round(dur / 60.0, 1),
                              round(sum(speeds) / len(speeds), 1), round(max_sp, 1),
                              round(idle_min, 1), round(litres, 2),
                              "InProgress" if in_progress else "Completed"])

                if trace:
                    for p in pts:
                        ts = cursor + timedelta(seconds=int(p[0] * dur))
                        positions.append([v["id"], v["imei"], ts.isoformat(),
                                          round(p[1], 6), round(p[2], 6),
                                          p[3], p[4], rnd.randint(2, 90), 1, trip_id])

                # alerts derived from what actually happened on the trip
                if max_sp > 95 and rnd.random() < 0.7:
                    k = pts[max(range(len(pts)), key=lambda i: pts[i][3])]
                    alerts.append([alert_id, args.tenant, v["id"], drv["id"], "Overspeed", "High",
                                   (cursor + timedelta(seconds=int(k[0] * dur))).isoformat(),
                                   round(k[1], 6), round(k[2], 6),
                                   f"{round(max_sp)} km/h in an 80 km/h zone",
                                   rnd.choices(["Open", "Acknowledged", "Closed"], [0.2, 0.3, 0.5])[0]])
                    alert_id += 1
                if idle_min > 22 and rnd.random() < 0.55:
                    alerts.append([alert_id, args.tenant, v["id"], drv["id"], "IdleExceeded", "Medium",
                                   (cursor + timedelta(seconds=dur // 2)).isoformat(),
                                   round(pts[len(pts) // 2][1], 6), round(pts[len(pts) // 2][2], 6),
                                   f"Engine idle {round(idle_min)} min above 20 min threshold",
                                   rnd.choices(["Open", "Acknowledged", "Closed"], [0.25, 0.3, 0.45])[0]])
                    alert_id += 1
                for atype, prob, sev in ALERT_TYPES:
                    if atype in ("Overspeed", "IdleExceeded"):
                        continue
                    if rnd.random() < prob * 0.10:
                        k = pts[rnd.randrange(len(pts))]
                        alerts.append([alert_id, args.tenant, v["id"], drv["id"], atype, sev,
                                       (cursor + timedelta(seconds=int(k[0] * dur))).isoformat(),
                                       round(k[1], 6), round(k[2], 6), f"{atype} detected",
                                       rnd.choices(["Open", "Acknowledged", "Closed"], [0.3, 0.3, 0.4])[0]])
                        alert_id += 1

                trip_id += 1
                if in_progress:
                    break
                loc = dest_pt
                loc_name = dest[0]
                cursor = end_ts + timedelta(minutes=rnd.randint(20, 75))

            # refuel roughly every third day
            if rnd.random() < 0.34:
                lt = round(v["tank"] * rnd.uniform(0.45, 0.92), 1)
                ppl = {"dubai": 3.05, "riyadh": 2.33, "lahore": 272.0}[args.region]
                ppl = round(ppl * rnd.uniform(0.97, 1.04), 2)
                fuel.append([fuel_id, args.tenant, v["id"], v["driver"]["id"],
                             (day + timedelta(hours=rnd.randint(7, 19))).isoformat(),
                             lt, ppl, round(lt * ppl, 2),
                             rnd.choice(["ENOC", "ADNOC", "EPPCO", "Aldrees", "PSO", "Shell", "Total"]),
                             v["odo"] - rnd.randint(0, 9000), "FuelCard"])
                fuel_id += 1

    w("trips.csv",
      ["trip_id", "tenant", "vehicle_id", "driver_id", "start_utc", "end_utc", "origin", "destination",
       "distance_km", "duration_min", "avg_speed_kmh", "max_speed_kmh", "idle_min", "fuel_litres", "status"],
      trips)
    w("positions.csv",
      ["vehicle_id", "imei", "ts_utc", "lat", "lon", "speed_kmh", "heading", "hdop", "ignition", "trip_id"],
      positions)
    w("alerts.csv",
      ["alert_id", "tenant", "vehicle_id", "driver_id", "alert_type", "severity", "ts_utc",
       "lat", "lon", "message", "status"], alerts)
    w("fuel_entries.csv",
      ["fuel_id", "tenant", "vehicle_id", "driver_id", "ts_utc", "litres", "price_per_litre",
       "total_cost", "station", "odometer_km", "payment_method"], fuel)

    # ---------------- maintenance ----------------
    maint = []
    mid = 1
    for v in vehicles:
        for _ in range(rnd.randint(2, 5)):
            due_km = v["odo"] + rnd.randint(-4000, 9000)
            due_at = now + timedelta(days=rnd.randint(-45, 75))
            status = "Completed" if due_at < now - timedelta(days=3) else \
                     ("Overdue" if due_at < now else "Scheduled")
            maint.append([mid, args.tenant, v["id"], rnd.choice(MAINT_TYPES),
                          due_at.date().isoformat(), due_km, status,
                          round(rnd.uniform(180, 2400), 2),
                          rnd.choice(["Al Shirawi Workshop", "In-house Garage",
                                      "Fast Fit Service", "Authorised Dealer"])])
            mid += 1
    w("maintenance.csv",
      ["maintenance_id", "tenant", "vehicle_id", "service_type", "due_date", "due_odometer_km",
       "status", "cost", "workshop"], maint)

    open_alerts_today = sum(
        1 for a in alerts
        if a[10] == "Open" and datetime.fromisoformat(a[6]).date() == now.date())

    print(f"Region        : {args.region}")
    print(f"Tenant        : {args.tenant}")
    print(f"Vehicles      : {len(vehicles)}  (statuses: "
          + ", ".join(f"{s}={sum(1 for v in vehicles if v['status'] == s)}"
                      for s, _ in STATUS_MIX) + ")")
    print(f"Drivers       : {len(drivers)}")
    print(f"Geofences     : {len(geofences)}")
    print(f"Trips         : {len(trips)} over {args.days} days")
    print(f"Positions     : {len(positions)} ({args.trace_days}-day trace @ {args.interval}s)")
    print(f"Alerts        : {len(alerts)}  (open today: {open_alerts_today})")
    print(f"Fuel entries  : {len(fuel)}")
    print(f"Maintenance   : {len(maint)}")
    print(f"\nCSVs written to {os.path.abspath(args.out)}")
    print("Synthetic data — load into a DEMO tenant only.")


if __name__ == "__main__":
    main()
