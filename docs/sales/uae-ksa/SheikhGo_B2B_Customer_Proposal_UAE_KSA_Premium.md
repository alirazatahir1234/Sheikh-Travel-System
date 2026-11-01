# SheikhGo — UAE & Saudi Arabia Market Proposal (Premium)

**Market label:** **UAE & Saudi Arabia Market Proposal**  
**Document:** Customer proposal (GCC)  
**Version:** 1.0 — Premium edition  
**Date:** October 2026  
**Prepared by:** SheikhGo Technologies  
**Audience:** Prospective fleet operators in the UAE and Kingdom of Saudi Arabia  
**Internal use:** Ali / SheikhGo sales only — do not auto-send to clients  

**Contact**

| Channel | Details |
|--------|---------|
| Email | info@sheikhgo.com |
| Website | [sheikhgo.com](https://sheikhgo.com) |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

> **Status of this document:** Introductory commercial overview — **not** a binding quotation or SLA. Pricing is **UAE / GCC-level** (AED / SAR / USD), **indicative / subject to quote**. Taxes (VAT), hosting region, SLA, hardware, SIMs, and custom work are confirmed in discovery and SOW.

---

## 1. Positioning

### Smarter fleets. Better-controlled operations.

One connected platform for vehicles, drivers, trips, and fleet visibility — built for Gulf fleet operators who need live GPS, dispatch discipline, and office–field alignment.

**SheikhGo** unifies ERP workflows with on-road activity: vehicles, drivers, assignments, GPS, fuel, maintenance, bookings, and reports in one operational picture.

| Signal | Meaning |
|--------|---------|
| Live GPS | Vehicle visibility, trip history, playback |
| One platform | Fleet and transport workflows in one ERP |
| Field + office | Driver / fleet mobile apps connected to Nexus |

### Designed for fleet-driven businesses

- **Transport & logistics** — trips, driver accountability, fuel, operational reporting  
- **Car rental & leasing** — utilization, documents, maintenance, rental / booking workflows  
- **Staff & corporate transport** — routes, attendance evidence, GPS visibility  
- **Travel & tour / airport transfer** — bookings connected with vehicles, drivers, and customers  

---

## 2. Phase 1 — Core GPS + Fleet

Live GPS (Traccar; Teltonika / Queclink / Jimi catalog), vehicle & driver management, fuel, maintenance & workshop, trips / bookings / routes, driver mobile app, reports & analytics, access control & audit.

Operating flow: **Plan → Execute → Control → Review**.

Includes live map, geofences, alerts, trip history, and **route / history playback**.

### GPS hardware note — 4G trackers (recommended for UAE / KSA)

| Class | Examples (catalog-backed) | Notes |
|-------|---------------------------|--------|
| **2G / GT06-class** | Jimi **VG03**, Jimi/Concox **GT06** family | Lower device cost; adequate for basic tracking where 2G coverage remains |
| **4G / LTE (recommended)** | Teltonika **FMC920**, **FMC130**, **FMM130**, **FMC001** (LTE / Cat-M class in catalog) | **Higher device + SIM/data cost** vs VG03 / GT06 class; better longevity as networks migrate off 2G; preferred for UAE/KSA deployments |

SheikhGo supports both classes via Traccar. **Ali’s preferred offer for GCC: 4G / LTE trackers** — quote device, install, and data separately; expect a **material premium** versus entry 2G hardware.

---

## 3. Phase 2 — Operations modules

| Module | What it delivers | Product status |
|--------|------------------|----------------|
| Settlements & payments | Invoice-linked payments, settlement workflows | In product |
| Pricing rules | Booking / trip pricing configuration | In product |
| Driver allowance / wallets | Allowance rules and wallet surfaces | In product (maturing) |
| Customer CRM + bookings | Customers, bookings, operational records | In product |
| Notifications center | In-app inbox, email; SMS / push config-gated | In product |
| Shareable live-tracking links | Token links for customers (`/t/{token}`) | In product (MVP+) |
| Smart trip assignment | Suggest → confirm | Hardened MVP |
| Document / compliance tracker | Docs + reminder jobs | Partially implemented |
| Customer portal / hub | Self-serve surfaces | Partial — per SOW |
| Vehicle rental module | Rates, availability, rentals in ERP | In product (partial) |
| Website CMS | Public site features/pages from Nexus | In product |

> **Deploy note:** Website Cloudflare / public media Blob continues. **Mobile-app Cloudflare/Blob migration is cancelled** — not part of this proposal.

---

## 4. Premium section

Premium is sold as add-ons or Phase 3 packs — **not** implied in a basic GPS-only licence unless quoted.

### 4.1 Flagship — SheikhGo AI Assistant (Premium)

In-ERP AI Assistant for operations questions (fleet, GPS, trips, alerts, maintenance helpers) via allowlisted tools and optional LLM provider when configured. Module: **AI — SheikhGo AI Assistant**. Depth depends on tenant enablement, provider keys, and tool allowlists.

**Not claimed:** fully autonomous decisions without human oversight; unlimited free LLM usage.

### 4.2 Premium & advanced modules

| Capability | Status |
|------------|--------|
| GPS history playback & signed exports | In product |
| SmartOps / Smart Alerts (rule-based) | In product |
| Predictive maintenance (interval/odometer — **not** ML) | In product |
| Fatigue detection | In product |
| Accident risk scoring | Partial |
| Driver performance scoring | Partial (rules MVP) |
| Fuel fraud detection | Partial — needs OBD / fuel sensor |
| SOS auto-dispatch | Partial |
| Premium GPS alert add-ons | In product (add-ons) |
| WhatsApp inbox + automation | In product; Meta setup per tenant |
| Website CMS / Rental / SheikhGo Fleet app | In product (rental partial) |
| EV / anomaly / OCR / AI recommendation APIs | Partial |

### 4.3 Roadmap — do not sell as live without SOW

- Full **ML** predictive maintenance  
- Production WhatsApp Cloud at every tenant without Meta onboarding  
- Complete white-label customer marketplace  
- Direct Jimi adapter independent of Traccar  
- Separate “GoFleet AI” SKU (not in repo — position as Fleet app + Premium AI Assistant)  
- **Cancelled:** Mobile-app Cloudflare / Blob migration  

---

## 5. Technology & deployment

Angular Nexus ERP · ASP.NET Core / SignalR · Flutter Driver + Fleet · SQL Server · Traccar + supported devices · Docker / cloud / IIS.

**Options:** (1) SheikhGo Cloud · (2) Client Cloud (Azure/AWS/GCP) · (3) On-premise.  
**Journey:** Discover → Configure → Data → Connect GPS → Pilot → Roll out. Typical cloud: **~3–5 weeks**.

---

## 6. Commercial models (UAE / KSA · AED / SAR / USD)

1. **Monthly subscription** — per-vehicle or tiered SaaS with managed hosting (primary GCC model)  
2. **Monthly rental with GPS hardware** — software + **4G device** rental + SIM/data + support  
3. **Perpetual / on-premise** — quoted for enterprises with local hosting requirements  

Indicative **GCC list bands** (software only; **subject to quote**; VAT extra):

| Plan | Indicative monthly | Fleet band | Approx. USD guide |
|------|--------------------|------------|-------------------|
| Starter | **AED 2,200 – 3,200** / mo (~**SAR 2,250 – 3,275**) | ~1–25 vehicles | ~USD 600 – 870 |
| Pro | **AED 3,500 – 5,500** / mo (~**SAR 3,575 – 5,625**) | ~26–50 vehicles | ~USD 950 – 1,500 |
| Enterprise | **from AED 6,000+** / mo + scale | 51+ vehicles | from ~USD 1,630+ |

Per-vehicle software guide (volume-dependent): ~**AED 70 – 140 / vehicle / month** (~**USD 19 – 38** / vehicle / month), or ~**SAR 70 – 145** / vehicle / month.

**Hardware (indicative, separate from software):**

| Item | Guide |
|------|--------|
| 2G / VG03–GT06 class device | Lower cost band — quote locally |
| **4G / LTE (FMC920 / FMC130 class)** | **Higher cost** — typically a clear premium vs 2G class (device + LTE SIM); quote per unit + install |
| Install + activation | Per vehicle / site visit |

Pakistan PKR website bands do **not** apply to this document.

---

## 7. Phased pricing with ROI (indicative · subject to quote)

Assumptions: mid-size fleet **~40 vehicles**, SheikhGo Cloud, **4G trackers** billed separately.

### Phase 1 — Core GPS + Fleet

| Item | Indicative |
|------|------------|
| Software (Pro-class) | ~**AED 3,500 – 5,500 / month** (~**USD 950 – 1,500**) |
| Per-vehicle guide | ~**AED 70 – 140 / vehicle / month** |
| Implementation (one-time cloud) | ~**AED 8,000 – 25,000** (scope-dependent) |
| 4G hardware + install (one-time) | Quoted separately — **higher than 2G/VG03 class** |
| Timeline | ~3–5 weeks typical |

**ROI (Phase 1):** fuel visibility (industry often targets 3–8% when GPS + logs enforced — results vary); theft/misuse reduction; dispatcher labor reclaim; fewer missed services. *Illustrative:* AED 150k/mo fuel × 4% ≈ AED 6k/mo — often covers Phase 1 software on a mid fleet.

### Phase 2 — Ops modules

| Item | Indicative |
|------|------------|
| Ops uplift | ~**+15–35%** of Phase 1 software, or packaged in Pro/Enterprise |
| Rental + CMS | Module add-on quotes |
| WhatsApp fair-use | By message volume |

**ROI:** faster booking→invoice; fewer “where is my car?” calls; fewer empty miles; fewer compliance fines.

### Phase 3 — Premium AI & intelligence

| Item | Indicative |
|------|------------|
| **AI Assistant (premium)** | ~**AED 900 – 2,500 / month** / tenant **or** ~**AED 25 – 60 / vehicle / month** + LLM pass-through |
| SmartOps + predictive (rules) + fatigue + driver score | ~**AED 600 – 1,800 / month** or Enterprise-included |
| Premium GPS alert add-ons | Per catalog |
| Accident risk / SOS / fuel fraud packs | Quoted; fraud may need OBD hardware |

**ROI:** faster managerial answers from live data; planned maintenance vs roadside failure; safety culture; fuel-fraud ROI only with supporting telemetry.

### Example 12-month TCO sketch (software only · 40 vehicles · illustrative)

| Phase | Year-1 software (indicative) |
|-------|------------------------------|
| Phase 1 | ~AED 42k – 66k (3.5k–5.5k × 12) |
| Phase 2 uplift (+25%) | ~AED 10.5k – 16.5k |
| Phase 3 AI + intelligence | ~AED 18k – 45k |
| **Total software sketch** | **~AED 70k – 128k / year** (~**USD 19k – 35k** illustrative) |

4G hardware, SIMs, install, VAT, and on-premise change TCO materially.

---

## 8. Why SheikhGo

- Live fleet visibility with playback and geofences  
- Accountability across drivers, assignments, and events  
- Fuel, maintenance, and trip cost in one picture  
- Premium AI Assistant when the fleet is ready  
- GCC-appropriate **4G** hardware path (higher cost, future-ready vs 2G class)  

---

## 9. Next step

Book a walkthrough. We will review fleet size, recommend **4G** vs 2G hardware honestly, demo Premium AI Assistant if desired, and issue a formal AED/SAR/USD quote.

| | |
|--|--|
| Email | info@sheikhgo.com |
| Web | sheikhgo.com |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

---

## 10. About this proposal

Introductory overview only — not a binding quotation or SLA. Roadmap items are not sold as live. Mobile-app Cloudflare/Blob migration is cancelled and excluded.

**SheikhGo Technologies** | Fleet & Transport Operations Platform  
**UAE & Saudi Arabia Market Proposal** — Ali / SheikhGo internal sales use only.

---

*Internal filename: `SheikhGo_B2B_Customer_Proposal_UAE_KSA_Premium.md`*
