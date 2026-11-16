# SheikhGo — Pakistan Market Proposal (v2 · Premium)

**Market label:** **Pakistan Market Proposal**  
**Document:** Customer proposal (expanded)  
**Version:** 2.0 — Premium edition  
**Date:** October 2026  
**Prepared by:** SheikhGo Technologies  
**Audience:** Prospective fleet operators in Pakistan evaluating fleet digitization  
**Internal use:** Ali / SheikhGo sales only — do not auto-send to clients  

**Contact**

| Channel | Details |
|--------|---------|
| Email | info@sheikhgo.com |
| Website | [sheikhgo.com](https://sheikhgo.com) |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

> **Status of this document:** Introductory commercial overview — **not** a binding quotation or SLA. Module availability, integrations, hardware compatibility, deployment architecture, timeline, pricing, taxes, support scope, and contractual terms are confirmed through discovery and a mutually agreed statement of work (SOW). Pricing below is **indicative / subject to quote** and **PKR-oriented** for the Pakistan market.

---

## 1. Positioning

### Smarter fleets. Better-controlled operations.

One connected platform for vehicles, drivers, trips, and fleet visibility.

**SheikhGo** brings office operations and on-road activity into one platform. Manage vehicles, drivers, assignments, GPS activity, fuel records, maintenance, bookings, and reports with a shared operational picture — so teams spend less time coordinating across spreadsheets, WhatsApp threads, and disconnected tools.

| Signal | Meaning |
|--------|---------|
| Live GPS | Vehicle visibility, trip history, playback |
| One platform | Fleet and transport workflows in one ERP |
| Field + office | Driver / fleet mobile apps connected to Nexus |

### Designed for fleet-driven businesses

- **Transport & logistics** — trips, driver accountability, fuel, operational reporting  
- **Car rental & leasing** — utilization, documents, maintenance, rental / booking workflows  
- **Staff & school transport** — routes, attendance evidence, GPS visibility, driver operations  
- **Travel & tour operators** — bookings connected with vehicles, drivers, and customer records  

---

## 2. Phase 1 — Core GPS + Fleet (included in core packages)

Bring day-to-day fleet activity into a single operational workspace.

### Live GPS tracking

Live fleet map, vehicle location, geofences, event alerts, trip history, and **route / history playback**. Tracking is integrated through **Traccar** with supported **Teltonika**, **Queclink**, and **Jimi** IoT devices (catalog-backed).

### Vehicle & driver management

Centralized vehicle and driver records, assignments, document tracking, licence and registration expiry visibility, and incident history.

### Fuel management

Capture fuel entries with quantity, cost, and odometer details. Review consumption and cost trends by vehicle, driver, and reporting period.

### Maintenance & workshop

Service requests, work orders, maintenance scheduling, service history, spare-parts records, and workshop / vendor information.

### Trips, bookings & routes

Manage bookings, planned and active trips, routes, vehicle / driver assignments, pricing rules, and operational trip records.

### Driver mobile app

Flutter-based Android and iOS app for assigned trips, navigation, attendance, fuel capture, inspection records, service requests, and notifications. Offline queuing is supported for selected workflows.

### Reports & analytics

Operational and fleet reports, driver performance views, scheduled reporting, and export to Excel, PDF, and CSV.

### Access control & audit

Role- and permission-based access, tenant-level configuration, and an audit trail of recorded changes.

### Practical operating flow

1. **Plan** — bookings, routes, assignments  
2. **Execute** — driver app, trips, GPS activity  
3. **Control** — fuel, maintenance, alerts  
4. **Review** — reports, costs, audit history  

---

## 3. Phase 2 — Operations modules (ops depth)

Typically layered onto Phase 1 for fleets that need fuller day-to-day control:

| Module | What it delivers | Product status |
|--------|------------------|----------------|
| Settlements & payments | Invoice-linked payments, settlement workflows | In product |
| Pricing rules | Booking / trip pricing configuration | In product |
| Driver allowance / wallets | Allowance rules and wallet surfaces | In product (maturing) |
| Customer CRM + bookings | Customers, bookings, operational records | In product |
| Notifications center | In-app inbox, email; SMS / push config-gated | In product (channels vary by deployment) |
| Shareable live-tracking links | Token links for customers (`/t/{token}`) | In product (MVP+) |
| Smart trip assignment | Constraint + score suggest → confirm | Hardened MVP |
| Document / compliance tracker | Compliance docs + reminder jobs | Partially implemented |
| Customer portal / hub | Self-serve booking / payment surfaces | Partial — scope per SOW |
| Vehicle rental module | Availability, rates, rental list / detail in ERP | In product (partial depth) |
| Website CMS | Public site features / pages managed from Nexus | In product |

> **Deploy note (internal):** Website Cloudflare / public media Blob delivery continues. **Mobile-app Cloudflare/Blob migration is cancelled** — not part of this proposal or any plan.

---

## 4. Premium section

**Premium** capabilities are sold as add-ons or Phase 3 packs. They are **not** implied in a basic GPS-only or Starter core licence unless explicitly quoted.

**Honesty rule used in this proposal:** items below exist as code / modules / UI in SheikhGo **or** are clearly labelled **Roadmap**. Speculative marketing claims are avoided.

### 4.1 Premium flagship — SheikhGo AI Assistant

| | |
|--|--|
| **Commercial label** | **Premium service** |
| **What it is** | In-ERP AI Assistant (chat / tools) for operations questions — fleet, GPS, trips, alerts, maintenance helpers — backed by allowlisted tools and optional LLM provider (e.g. DeepInfra) when configured |
| **Status** | **In product** as a premium / enableable module (`AI` — “SheikhGo AI Assistant”). Depth depends on tenant enablement, provider keys, and tool allowlists |
| **Not claimed** | Fully autonomous fleet decisions without human oversight; unlimited free LLM usage |

### 4.2 Premium & advanced modules (exist or clearly scaffolded)

| Capability | Customer value | Status |
|------------|----------------|--------|
| **GPS history playback & signed exports** | Replay routes; secure coordinate exports (private blob + SAS) | **In product** |
| **SmartOps / Smart Alerts** | Rule alerts: maintenance due, document expiry, unassigned trips, overdue invoices, fuel anomaly; trip-delay risk kinds | **In product** (rule-based) |
| **Predictive maintenance (interval / odometer)** | Oil / tire / engine-life style dues, ERP predictive view, driver notify job | **In product — rule-based**, **not** ML failure prediction |
| **Fatigue detection** | Fatigue-related alerting / escalation path | **In product** (module + hosted escalation) |
| **Accident risk scoring** | Rule-based risk score (0–100) + scanner | **Partially implemented** |
| **Driver performance scoring** | Safety / fuel / on-time heuristic scores | **Partially implemented** (rule-based MVP) |
| **Fuel fraud detection** | Pump vs telemetry signals | **Partial** — needs fuel-level / OBD-capable hardware |
| **SOS auto-dispatch** | Nearest verified team / recovery partners | **Partially implemented** |
| **Premium GPS alert add-ons** | e.g. fixed overspeed, road-speed limits, GPS lost, low fuel (catalog: premium, default off) | **In product** as subscription add-ons |
| **WhatsApp inbox + automation** | Accounts, templates, inbox, automation rules, SignalR hub | **In product**; production Meta credentials / fair-use bundles per deployment |
| **Website CMS** | Manage public website features / pages from the platform | **In product** |
| **Rental module** | Rental rates, availability, rental records in ERP | **In product** (partial module health) |
| **SheikhGo Fleet (mobile)** | Flutter fleet / field operations app alongside driver app | **In product** (`sheikhGo-Fleet`) |
| **EV fleet support** | EV dashboard / charging POI / range-aware hooks | **Partially implemented** |
| **Anomaly detection** | Night / speed-drop / off-route style rules on ingest | **Partially implemented** |
| **OCR document assist** | Document extraction where configured | **Partial** |
| **AI recommendations / predictions APIs** | Dashboard / AI surfaces | **Partial** — not a substitute for the Assistant chat product |

### 4.3 Roadmap / do not sell as live without SOW confirmation

| Item | Note |
|------|------|
| Full **ML** predictive maintenance (vibration / RUL / OBD DTC models) | Roadmap — current product is interval/odometer rules |
| Fully production-credentialed WhatsApp Cloud at every tenant without setup | Requires Meta / Cloud API onboarding per customer |
| Customer portal as complete white-label marketplace | Partial today |
| Direct Jimi adapter independent of Traccar | Documented as remaining / roadmap in implementation status |
| Hangfire-class job platform for all workers | Current workers are hosted services; deepen per SOW |
| “GoFleet AI” as a separately branded product SKU | **Not** a distinct SKU in repo — position as **SheikhGo Fleet app + Premium AI Assistant** unless Ali defines a separate brand |
| Mobile-app Cloudflare / Blob migration | **Cancelled / dropped** — do not include in proposals or plans. Website CF/Blob continues separately. |

---

## 5. Technology & deployment

| Layer | Stack |
|-------|--------|
| Web ERP (Nexus) | Angular |
| Backend & APIs | ASP.NET Core, REST, SignalR |
| Mobile | Flutter (Driver + Fleet) — Android & iOS |
| Database | Microsoft SQL Server |
| GPS | Traccar + supported tracker devices |
| Packaging | Docker; cloud and IIS configurations |

### Deployment options

1. **SheikhGo Cloud** — managed hosting, multi-tenant setup, backups, updates, monitoring  
2. **Client Cloud** — customer Azure / AWS / GCP, subject to agreed infrastructure  
3. **On-premise** — customer servers, subject to security and integration requirements  

### Typical implementation journey

1. Discover — fleet size, workflows, requirements  
2. Configure — tenant, users, modules, settings  
3. Prepare data — import and verify agreed records  
4. Connect GPS — install / bind devices and validate tracking  
5. Train & pilot — controlled pilot  
6. Roll out — expand and hand over  

**Planning reference:** a typical cloud deployment is estimated at approximately **3–5 weeks**. Actual timing depends on data readiness, GPS fitting, integrations, configuration, and customer-side dependencies.

---

## 6. Commercial models (Pakistan · PKR)

Flexible approaches — selected after fleet size, modules, hosting, hardware, and support expectations are clear:

1. **Perpetual licence** — one-time software licence for agreed scope (often with on-premise)  
2. **Monthly subscription** — per-vehicle / tiered software with managed hosting and updates  
3. **Monthly rental with GPS hardware** — software + device rental + SIM / data + support (subject to plan)  

Published website reference bands (Pakistan cloud SaaS — for context only):

| Plan | Indicative monthly (PKR) | Fleet band |
|------|--------------------------|------------|
| Starter | 79,000 | ~1–25 vehicles |
| Pro | 119,000 | ~26–50 vehicles |
| Enterprise | from 165,000 + scale | 51+ vehicles |

Single-tracker annual platform (Pakistan, software only, BYOD / partner install): Essential **8,000** / Plus **9,000** / Premium **10,000** PKR per year.

---

## 7. Phased pricing with ROI (indicative · subject to quote · PKR)

**All figures below are placeholders for discovery conversations.** They are aligned to public website bands and internal research — **not** a formal price list commitment. Taxes, hardware, SIMs, installation, custom integrations, premium LLM usage, and WhatsApp message volume are **extra** unless quoted.

Assumptions for examples: mid-size fleet **~40 vehicles**, cloud subscription, customer-supplied or partner GPS hardware billed separately.

### Phase 1 — Core GPS + Fleet

| Item | Indicative |
|------|------------|
| Software (Pro-class band) | ~**PKR 119,000 / month** (~**USD 430**/mo at rough 1 USD ≈ 275 PKR — **illustrative FX only**) |
| Or per-vehicle software guide | ~**PKR 2,000–3,500 / vehicle / month** depending on tier and volume |
| Implementation (one-time cloud) | ~**PKR 150,000–400,000** (scope-dependent) |
| Timeline | ~3–5 weeks typical |

**ROI narrative (Phase 1)**

- **Fuel visibility** — stop unexplained fill-ups; 3–8% fuel spend improvement is a common industry target when logging + GPS are enforced (customer results vary).  
- **Theft / misuse** — geofence + ignition + offline alerts reduce unauthorized use and recovery time.  
- **Labor** — fewer hours spent chasing locations in WhatsApp/Excel (dispatcher time often 30–60 min/day reclaimable on 20+ vehicle fleets).  
- **Downtime** — basic maintenance schedules reduce missed services.

*Illustrative only:* if a 40-vehicle fleet spends PKR 2.5M/month on fuel, a **4%** reduction ≈ **PKR 100,000/month** — often enough to cover Phase 1 software alone. Real ROI requires the customer’s own fuel and utilization numbers.

### Phase 2 — Ops modules

| Item | Indicative |
|------|------------|
| Ops uplift on subscription | ~**+15–35%** of Phase 1 software, **or** packaged into Pro / Enterprise |
| Optional rental + CMS enablement | Quoted as module add-ons |
| WhatsApp fair-use bundle | Quoted by message volume |

**ROI narrative (Phase 2)**

- Faster booking → assignment → invoice cycle; fewer missed settlements.  
- Shareable tracking links cut “where is my car?” calls.  
- Smart assignment reduces empty miles and double-booking errors.  
- Compliance reminders reduce fines and denied trips from expired documents.

### Phase 3 — Premium AI & intelligence

| Item | Indicative |
|------|------------|
| **AI Assistant (premium)** | ~**PKR 25,000–75,000 / month** per tenant **or** ~**PKR 500–1,500 / vehicle / month**, plus LLM/provider pass-through if applicable |
| SmartOps + predictive maintenance (rules) + fatigue + driver score pack | ~**PKR 15,000–50,000 / month** or included in Enterprise |
| Premium GPS alert add-ons (road speed, etc.) | Per catalog / subscription add-on quote |
| Accident risk / SOS / fuel fraud packs | Quoted; fraud may require OBD / fuel-sensor hardware |

**ROI narrative (Phase 3)**

- **AI Assistant** — managers get answers from live ops data without pulling multiple reports (labor + decision speed).  
- **Predictive / scheduled maintenance** — fewer roadside failures; planned workshop slots vs emergency downtime.  
- **Fatigue & accident risk** — safety culture + potential insurance / liability mitigation (not a guaranteed underwriting outcome).  
- **Fuel fraud / anomaly** — high ROI only when telemetry hardware supports it; otherwise keep expectations on process controls.

### Example 12-month TCO sketch (software only · 40 vehicles · illustrative)

| Phase | Year-1 software (indicative) |
|-------|------------------------------|
| Phase 1 | ~PKR 1.43M (119k × 12) |
| Phase 2 uplift (+25%) | ~PKR 0.36M |
| Phase 3 AI + intelligence pack | ~PKR 0.60–1.20M |
| **Total software sketch** | **~PKR 2.4–3.0M / year** |
| Rough USD sketch | **~USD 8,700–11,000 / year** (FX illustrative) |

Hardware, SIMs, install, and on-premise options change TCO materially.

---

## 8. Why SheikhGo (decision-maker outcomes)

- Improve fleet visibility — locations, trip history, geofence events, playback  
- Strengthen accountability — drivers, assignments, recorded events  
- Understand operating costs — fuel, maintenance, trip cost together  
- Reduce manual coordination — connected digital processes instead of scattered records  
- Grow into Premium AI and intelligence when the fleet is ready — without replacing the core platform  

---

## 9. Next step

**Let’s put your fleet in view.**

Book a personalized walkthrough. We’ll review your fleet, demonstrate the relevant workflows (including Premium AI Assistant if desired), and prepare a formal quote based on your requirements.

| | |
|--|--|
| Email | info@sheikhgo.com |
| Web | sheikhgo.com |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

---

## 10. About this proposal

This document is an introductory overview, not a binding quotation or service-level agreement. Final module availability, integrations, hardware compatibility, deployment architecture, implementation timeline, pricing, taxes, support scope, and contractual terms will be confirmed through discovery and a mutually agreed statement of work. Some customer portal and other capabilities are partial or subject to configuration; **roadmap items are not represented as currently available**.

**SheikhGo Technologies** | Fleet & Transport Operations Platform  
**Pakistan Market Proposal** — Prepared for Ali / SheikhGo internal sales use only.

---

*Internal filename: `SheikhGo_B2B_Customer_Proposal_v2_Premium.md` · Pakistan Market Proposal · Premium section, phased ROI pricing, PKR-oriented.*
