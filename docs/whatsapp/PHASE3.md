# WhatsApp Phase 3 — Self-Service Channel

Phase 3 turns WhatsApp into a customer self-service channel beside the existing DEMO lead bot and Phase 2 trip automations.

## Menu (interactive list)

| Id | Title |
|----|--------|
| `ss_book` | Book a Vehicle |
| `ss_track` | Track My Trip |
| `ss_invoices` | My Invoices |
| `ss_agent` | Talk to an Agent |

Entry: Idle / `HI` / `HELP` / `MENU` / `HELLO` / `START`. Type `DEMO` still starts the lead-qualification FSM.

## States (`CurrentBotState`)

`MainMenu` · `AwaitBookingPickup` · `AwaitBookingDestination` · `AwaitBookingDate` · `AwaitBookingTime` · `AwaitBookingVehicleType` · `AwaitBookingPassengers` · `AwaitBookingNotes` · `AwaitTripRef` · `Invoices` · `HumanHandoff`

Session draft stored in `WhatsAppConversations.BotSessionJson`.

## Ownership

Trip / invoice lookups always filter by WhatsApp phone → `Customers.Phone` → booking/trip. Reference alone is never enough.

## Booking (3.2)

Multi-step chat collects pickup, destination, date, time, vehicle type, passengers, notes → `CreateBookingCommand` (same as ERP).

Config:

- `WhatsApp:SelfServiceDefaultRouteId` (required for create; else first tenant route)
- `WhatsApp:SelfServiceDefaultAmount` (placeholder fare until ops confirms)

Meta Flow `nfm_reply` is parsed; payload maps to the same finalizer. Idempotency via `WhatsAppFlowSubmissions` (`TenantId` + Meta `wamid`).

**Ops:** publish a Meta Flow with fields `pickup`, `destination`, `date`, `time`, `vehicle_type`, `passengers`, `notes` if you want Flows instead of chat steps.

## Payments (3.3)

Unpaid booking rows → `Pay Now` → `CreatePortalPaymentCheckoutCommand` (Stripe / JazzCash / EasyPaisa per `PortalPaymentGateway:Provider`). Only the customer-facing `checkoutUrl` is sent.

On gateway webhook success, `PaymentGatewayPaymentRecorder` notifies WhatsApp with a paid confirmation (does **not** mark paid on link click).

## SIT checklist

### 3.1 Menu
- [ ] New chat → main menu list
- [ ] Book → pickup prompt
- [ ] Track → deny unknown/other customer’s ref; show authorized summary + tracking URL
- [ ] Invoices → list for this phone
- [ ] Agent → bot off, HumanHandoff, inbox agent handling
- [ ] `DEMO` still works

### 3.2 Booking
- [ ] Full chat booking → Pending booking in ERP
- [ ] Past date rejected
- [ ] Duplicate Flow wamid does not double-book

### 3.3 Pay
- [ ] Pay Now with gateway enabled → checkout URL
- [ ] Gateway callback → WhatsApp “Payment received”
- [ ] Unauthorized `ss_pay_{id}` denied

## Explicit non-goals

ERP inbox redesign, duplicate Finance/Booking systems, SMS fallback, marketing blasts.
