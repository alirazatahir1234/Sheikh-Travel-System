# Driver verification — NADRA & UAE KYC providers

SheikhGo Nexus calls **real HTTP APIs** when credentials are configured. There is **no mock / fake verification** path.

## NADRA (Pakistan) — CNIC

| Item | Detail |
|------|--------|
| Product | **Nishaan Pakistan** portal — Verisys demographic / CNIC checks ([NADRA announcement](https://www.nadra.gov.pk/media-release/nadra-unveils-nishan-pakistan-to-strengthen-secure-identity-verification-and-power-pakistans-digital-economy-4e319da8)) |
| Alternate channel | **e-Sahulat** franchise API (requires franchise contract — [e-sahulat.nadra.gov.pk](https://e-sahulat.nadra.gov.pk/)) |
| Trigger | Driver uploads **CNIC** document (ERP or driver app) or admin **Re-run provider check** |
| Stored | `DriverProviderVerificationChecks` row + compliance document remains admin-reviewed |

### Configuration keys (set via user-secrets or environment)

**Nishaan / Verisys (recommended for institutions)**

| Key | Env var |
|-----|---------|
| `DriverVerification:Nadra:Enabled` | `DriverVerification__Nadra__Enabled=true` |
| `DriverVerification:Nadra:Channel` | `Nishan` (default) or `Verisys` |
| `DriverVerification:Nadra:BaseUrl` | `DriverVerification__Nadra__BaseUrl` — **from NADRA after onboarding** |
| `DriverVerification:Nadra:VerisysPath` | Relative path from integration pack |
| `DriverVerification:Nadra:CompanyId` | Nishaan client / company id |
| `DriverVerification:Nadra:ApiKey` | `DriverVerification__Nadra__ApiKey` |

**e-Sahulat franchise**

| Key | Env var |
|-----|---------|
| `DriverVerification:Nadra:Channel` | `ESahulat` |
| `DriverVerification:Nadra:BaseUrl` | Franchise API base URL |
| `DriverVerification:Nadra:FranchiseId` | |
| `DriverVerification:Nadra:Username` | |
| `DriverVerification:Nadra:Password` | `DriverVerification__Nadra__Password` |
| `DriverVerification:Nadra:ESahulatVerifyPath` | From franchise integration doc |

### Currently missing in this repo / environment

- No `DriverVerification__Nadra__*` values in committed config (expected).
- **Action for Ali:** Complete Nishaan Pakistan onboarding (regulated entity) **or** e-Sahulat franchise agreement, then set `BaseUrl`, credentials, and paths from the official integration pack.

---

## UAE KYC / background screening (Dubai / UAE drivers)

| Item | Detail |
|------|--------|
| Default provider label | **Bayzat** (OAuth2 client credentials + partner screening endpoint) |
| Trigger | **BackgroundCheck** upload for drivers whose **nationality** indicates UAE / Emirati / Dubai |
| Note | Bayzat’s public docs emphasize HR/payroll APIs; **background screening URL must come from your Bayzat or AML partner contract** — set `ScreeningPath` accordingly. |

### Configuration keys

| Key | Env var |
|-----|---------|
| `DriverVerification:UaeKyc:Enabled` | `DriverVerification__UaeKyc__Enabled=true` |
| `DriverVerification:UaeKyc:Provider` | `Bayzat` |
| `DriverVerification:UaeKyc:BaseUrl` | Default `https://api.bayzat.com` |
| `DriverVerification:UaeKyc:ClientId` | `DriverVerification__UaeKyc__ClientId` |
| `DriverVerification:UaeKyc:ClientSecret` | `DriverVerification__UaeKyc__ClientSecret` |
| `DriverVerification:UaeKyc:ScreeningPath` | **Required** — from partner (not published in open Bayzat HR API catalog) |
| `DriverVerification:UaeKyc:CompanyId` | Bayzat company id if required by contract |

### Currently missing

- No `DriverVerification__UaeKyc__ClientId` / `ClientSecret` / `ScreeningPath` in repo.
- **Action for Ali:** Obtain Bayzat (or alternate UAE AML/KYC partner) API credentials and the **exact screening endpoint**; plug into config above.

---

## ERP API

- `GET /api/drivers/verification/config-status` — lists missing keys (no secrets).
- `GET /api/drivers/{id}/verification/provider-checks` — audit history.
- `POST /api/drivers/{id}/verification/provider-check` — manual re-verification (`{ "documentType": "CNIC" }` optional).

## Trip assignment

Drivers must be **`Verified`** (and not `Rejected` / `ExpiredDocs`) for trip and booking driver assignment.
