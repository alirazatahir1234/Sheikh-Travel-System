# SheikhGo — Germany Market Proposal (Premium)

**Market label:** **Germany Market Proposal**  
**Document:** Customer proposal (Germany / EU)  
**Version:** 1.0 — Premium edition  
**Date:** October 2026  
**Prepared by:** SheikhGo Technologies  
**Audience:** Prospective fleet operators in Germany evaluating fleet digitization  
**Internal use:** Ali / SheikhGo sales only — do not auto-send to clients  

**Contact**

| Channel | Details |
|--------|---------|
| Email | info@sheikhgo.com |
| Website | [sheikhgo.com](https://sheikhgo.com) |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

> **Status of this document:** Introductory commercial overview — **not** a binding quotation or SLA. Pricing is **EUR-oriented** for the German market, **indicative / subject to quote**. VAT, hosting region (EU), DPA / GDPR contractual terms, SLA, hardware, SIMs, and custom work are confirmed in discovery and SOW.

---

## 1. Positioning

### Smarter fleets. Better-controlled operations.

One connected platform for vehicles, drivers, trips, and fleet visibility — for German transport, logistics, rental, and corporate fleet operators who need live GPS, dispatch discipline, and office–field alignment.

**SheikhGo** unifies ERP workflows with on-road activity: vehicles, drivers, assignments, GPS, fuel, maintenance, bookings, and reports in one operational picture.

| Signal | Meaning |
|--------|---------|
| Live GPS | Vehicle visibility, trip history, playback |
| One platform | Fleet and transport workflows in one ERP |
| Field + office | Driver / fleet mobile apps connected to Nexus |

### Designed for fleet-driven businesses

- **Transport & logistics** — trips, driver accountability, fuel, operational reporting  
- **Car rental & leasing** — utilization, documents, maintenance, rental / booking workflows  
- **Staff / corporate shuttle** — routes, attendance evidence, GPS visibility  
- **Tour & transfer operators** — bookings connected with vehicles, drivers, and customers  

---

## 2. Phase 1 — Core GPS + Fleet

Live GPS (Traccar; Teltonika / Queclink / Jimi catalog), vehicle & driver management, fuel, maintenance & workshop, trips / bookings / routes, driver mobile app, reports & analytics, access control & audit.

Operating flow: **Plan → Execute → Control → Review**.

Includes live map, geofences, alerts, trip history, and **route / history playback**.

### GPS hardware note (Germany)

| Class | Examples (catalog-backed) | Notes |
|-------|---------------------------|--------|
| Entry / 2G-class | Jimi **VG03**, **GT06** family | Lower device cost where still viable |
| **4G / LTE (recommended)** | Teltonika **FMC920**, **FMC130**, **FMM130**, **FMC001** | **Higher device + SIM cost** vs VG03/GT06; preferred where 2G sunsets apply |

Hardware is quoted separately. Prefer **4G / LTE** for new German deployments unless the customer standardizes on existing devices (BYOD).

---

## 3. Phase 2 — Operations modules

| Module | What it delivers | Product status |
|--------|------------------|----------------|
| Settlements & payments | Invoice-linked payments, settlement workflows | In product |
| Pricing rules | Booking / trip pricing configuration | In product |
| Driver allowance / wallets | Allowance rules and wallet surfaces | In product (maturing) |
| Customer CRM + bookings | Customers, bookings, operational records | In product |
| Notifications center | In-app inbox, email; SMS / push config-gated | In product |
| Shareable live-tracking links | Token links (`/t/{token}`) | In product (MVP+) |
| Smart trip assignment | Suggest → confirm | Hardened MVP |
| Document / compliance tracker | Docs + reminder jobs | Partially implemented |
| Customer portal / hub | Self-serve surfaces | Partial — per SOW |
| Vehicle rental module | Rates, availability, rentals | In product (partial) |
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
- Separate “GoFleet AI” SKU (not in repo)  
- **Cancelled:** Mobile-app Cloudflare / Blob migration  

---

## 5. Technology & deployment

Angular Nexus ERP · ASP.NET Core / SignalR · Flutter Driver + Fleet · SQL Server · Traccar + supported devices · Docker / cloud / IIS.

**Options:** (1) SheikhGo Cloud (EU-region hosting preferred for German customers where agreed) · (2) Client Cloud · (3) On-premise.  
**Journey:** Discover → Configure → Data → Connect GPS → Pilot → Roll out. Typical cloud: **~3–5 weeks**.  
**Compliance note:** DPA / GDPR terms, data residency, and subprocessors are agreed in SOW — this overview does not replace legal review.

---

## 6. Commercial models (Germany · EUR)

1. **Monthly subscription** — per-vehicle or tiered SaaS with managed hosting (primary)  
2. **Monthly rental with GPS hardware** — software + device + SIM/data + support  
3. **Perpetual / on-premise** — quoted for enterprises with local hosting  

**Competitive anchors (DE research · Oct 2026):** mainstream telematics (Vimcar / Webfleet / YellowFox / Telekom) roughly **€12–28 / veh / mo** (exkl. MwSt); budget GPS **€6–15**; Samsara-class **~€30–60**. SheikhGo targets Webfleet/YellowFox mid — not budget trackers.

| Offer | Suggested list (EUR / veh / mo, exkl. MwSt) |
|-------|---------------------------------------------|
| **Software-only / BYOD** | **€14 – 22** |
| **Platform + 4G HW rental** | **€22 – 32** |
| Enterprise 100+ negotiated | **€12 – 18** SW · **€18 – 28** bundled |
| AI Assistant add-on | **+€2 – 5** / veh or flat **€49 – 149** / mo / tenant |

Indicative **fleet monthly bands** (software-led; **subject to quote**; MwSt extra):

| Plan | Indicative monthly (EUR) | Fleet band |
|------|--------------------------|------------|
| Starter | **€280 – 550** / mo | ~1–25 vehicles (~€14–22 / veh) |
| Pro | **€560 – 1,100** / mo | ~26–50 vehicles |
| Enterprise | **from €1,200+** / mo + scale | 51+ vehicles |

Pakistan PKR and UAE AED website/list bands do **not** apply to this document.

---

## 7. Phased pricing with ROI (indicative · subject to quote · EUR)

Assumptions: mid-size fleet **~40 vehicles**, cloud subscription, GPS hardware billed separately.

### Phase 1 — Core GPS + Fleet

| Item | Indicative |
|------|------------|
| Software (Pro-class, ~40 veh) | ~**€560 – 880 / month** (~**€14 – 22 / veh**) |
| With 4G HW rental (all-in) | ~**€880 – 1,280 / month** (~**€22 – 32 / veh**) |
| Implementation (one-time cloud) | ~**€2,500 – 8,000** (scope-dependent) |
| Hardware buy-out (4G) | Quoted separately — typically **higher** than 2G/VG03 class |
| Timeline | ~3–5 weeks typical |

**ROI (Phase 1):** fuel visibility (industry often targets 3–8% when GPS + logs enforced — results vary); theft/misuse reduction; dispatcher labor reclaim; fewer missed services. *Illustrative:* €40k/mo fuel × 4% ≈ €1,600/mo — often covers Phase 1 software on a mid fleet.

### Phase 2 — Ops modules

| Item | Indicative |
|------|------------|
| Ops uplift | ~**+15–35%** of Phase 1 software, or packaged in Pro/Enterprise |
| Rental + CMS | Module add-on quotes |
| WhatsApp fair-use | By message volume |

**ROI:** faster booking→invoice; fewer location chase-calls; fewer empty miles; fewer compliance fines.

### Phase 3 — Premium AI & intelligence

| Item | Indicative |
|------|------------|
| **AI Assistant (premium)** | ~**€49 – 149 / month** / tenant **or** ~**€2 – 5 / vehicle / month** + LLM pass-through |
| SmartOps + predictive (rules) + fatigue + driver score | ~**€80 – 250 / month** or Enterprise-included |
| Premium GPS alert add-ons | Per catalog |
| Accident risk / SOS / fuel fraud packs | Quoted; fraud may need OBD hardware |

**ROI:** faster managerial answers from live data; planned maintenance vs roadside failure; safety culture; fuel-fraud ROI only with supporting telemetry.

### Example 12-month TCO sketch (software only · 40 vehicles · illustrative)

| Phase | Year-1 software (indicative) |
|-------|------------------------------|
| Phase 1 (SW €14–22/veh) | ~€6.7k – 10.6k |
| Phase 2 uplift (+25%) | ~€1.7k – 2.6k |
| Phase 3 AI + intelligence | ~€1.6k – 4.8k |
| **Total software sketch** | **~€10k – 18k / year** |
| Alt. with HW rental (€22–32/veh) | **~€13k – 22k / year** platform+HW rental |

Hardware, SIMs, install, VAT, EU hosting premiums, and on-premise change TCO materially.

---

## 8. Why SheikhGo

- Live fleet visibility with playback and geofences  
- Accountability across drivers, assignments, and events  
- Fuel, maintenance, and trip cost in one picture  
- Premium AI Assistant when the fleet is ready  
- Clear EUR commercial model with honest product status labels  

---

## 9. Next step

Book a walkthrough. We will review fleet size, EU hosting / DPA needs, demo Premium AI Assistant if desired, and issue a formal EUR quote.

| | |
|--|--|
| Email | info@sheikhgo.com |
| Web | sheikhgo.com |
| UAE WhatsApp | **+971 55 770 1219** |
| Pakistan WhatsApp | **+92 317 736 8305** |

---

## 10. About this proposal

Introductory overview only — not a binding quotation or SLA. Roadmap items are not sold as live. Mobile-app Cloudflare/Blob migration is cancelled and excluded. GDPR / DPA details are SOW items.

**SheikhGo Technologies** | Fleet & Transport Operations Platform  
**Germany Market Proposal** — Ali / SheikhGo internal sales use only.

---

*Internal filename: `SheikhGo_B2B_Customer_Proposal_Germany_Premium.md`*
