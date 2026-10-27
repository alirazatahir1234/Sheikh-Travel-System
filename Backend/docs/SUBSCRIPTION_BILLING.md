# SheikhGo Nexus — subscription billing

## Plans (PKR / month)

| Plan | Price |
|------|-------|
| Starter | 79,000 |
| Pro | 119,000 |
| Enterprise | 165,000 base (51+ vehicles: +2,200 PKR per vehicle above 50) |

Public marketing copy lives on `/pricing` and the home `#subscription-plans` section (EN / AR / DE).

## Lifecycle (ERP)

- **Auto-renew:** `SubscriptionBillingHostedService` runs daily. Tenants with `AutoRenew = 1` receive a new row in `TenantInvoices` when `NextBillingDate` is due, then `NextBillingDate` advances one month.
- **Email:** Invoices dispatch through the shared notification email channel (`subscription_invoice` template key) to `TenantBilling.BillingEmail`, then tenant support email, then first admin user.
- **Payment status:** Invoices move `Pending` → `Overdue` after due date + 7-day grace. Platform admins mark paid via **Subscription Management** (records `TenantBillingHistory`).
- **Feature lock:** When any subscription invoice is overdue (or subscription is Suspended/Cancelled), `TenantSubscriptions.FeaturesLocked = 1` and module checks deny all modules except `DASHBOARD` and `ACCESS` (HTTP 402 on API module gates).

## Revenue dashboard

Platform admins: **Platform → Subscription revenue** (`/platform/subscription-revenue`) — active subscriptions, MRR sum of `MonthlyAmount`, upcoming renewals (14 days), overdue accounts, collections (30 days).

## Payment provider gaps

No Stripe/JazzCash/Easypaisa **subscription capture** is wired in this release:

- There are **no** live gateway credentials in `appsettings` for automated card/wallet settlement of Nexus plans.
- Clients pay via agreed **bank transfer**; finance marks invoices paid manually in ERP.
- Per-trip **customer invoices** (Finance module) reuse the same email infrastructure but are a separate AR flow.

To enable automated checkout later: add a `ISubscriptionPaymentProvider`, webhook handler for `invoice.paid`, and map provider customer IDs on `TenantBilling` without duplicating trip invoice tables.
