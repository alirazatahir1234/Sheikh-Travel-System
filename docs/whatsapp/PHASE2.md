# WhatsApp Phase 2 — Trip Automations & Live Tracking

Phase 2 builds on the Phase 1 inbox/template pipeline. Trip lifecycle events enqueue durable `WhatsAppAutomationEvents`; a worker claims and sends approved Meta templates. Rules ship **disabled** for pilot rollout.

## What shipped

| Area | Detail |
|------|--------|
| Rules | `WhatsAppAutomationRules` — UQ `(TenantId, EventType)`; 10 seeded types, `IsEnabled=0` |
| Events | Outbox + dedupe; scheduler every ~15s recovers due/stuck rows |
| Consent | `WhatsAppContactConsents` — opt-in required; inbound chat alone ≠ opt-in; `STOP` opts out |
| Tracking | `TripTrackingLinks` — SHA-256 token lookup; public `GET /api/public/tracking/{token}` + `GET /t/{token}` |
| Ratings | `TripRatings` from `RATE:{tripId}:{5\|3\|1}` quick replies |
| GPS | After Traccar ingest evaluators: proximity → DriverArriving (800 m) / DriverArrived (100 m + &lt;5 km/h) |
| ERP | `/whatsapp/automations`, booking WhatsApp timeline + Resend, booking form opt-in checkbox, inbox **Auto** badge |

Tracking host: **`track.sheikhgo.com`** (same API host + static `/t` page; DNS points subdomain).

## Event → hook map

| Event | Raised from |
|-------|-------------|
| BookingConfirmed / Cancelled | `UpdateBookingStatusCommand` |
| BookingRescheduled + new PickupReminder | `UpdateBookingCommand` (pickup change after Confirmed) |
| PickupReminder | Scheduled at confirm/reschedule using rule `OffsetMinutes` |
| DriverAssigned | `AssignDriverCommand` (revokes prior tracking link) |
| DriverEnRoute | Driver Accept → `TripStatus.Started` |
| DriverArrived | Driver Arrived → `AtPickup` **or** GPS proximity |
| DriverArriving | GPS only |
| TripCompleted | Complete (+2 min DueAt); tracking expires +30 min |

## API (permissions reuse Phase 1)

- `GET/PUT /api/whatsapp/automation-rules[/{eventType}]` — `WhatsApp.Manage`; enable without approved `en` template → **409 TEMPLATE_NOT_APPROVED**
- `GET .../preview?bookingId=` — Manage
- `GET /api/bookings/{id}/whatsapp-timeline` — `WhatsApp.View`
- `POST /api/whatsapp/automation-events/{id}/resend` — `WhatsApp.Reply` (max 3)
- `POST/DELETE /api/whatsapp/consents` — `WhatsApp.Reply`
- `GET /api/public/tracking/{token}` — anonymous, rate-limited, `Cache-Control: no-store`
- `GET /t/{token}` — static HTML shell

## Rollout (pilot)

1. Deploy migration + API; leave all rules **off**.
2. Submit/approve Meta utility templates (en; ur optional).
3. SIT checklist (below) against a staging tenant with consent + GPS.
4. Enable **BookingConfirmed** + **DriverAssigned** first; then EnRoute / Arriving / Completed.
5. Point `track.sheikhgo.com` at the API host; restrict Maps key referrers if you add Maps JS later.

## SIT checklist

- [ ] Confirm booking with WhatsApp opt-in → Pending `BookingConfirmed` event (or Sent if rule on)
- [ ] Without consent → event Skipped `NoConsent`
- [ ] Assign driver → tracking link created; open `/t/{token}` shows Assigned / EnRoute
- [ ] Driver Accept → DriverEnRoute template (when enabled)
- [ ] GPS fix within 800 m → DriverArriving once (dedupe); within 100 m slow → DriverArrived once
- [ ] Re-assign driver → previous link revoked (identical 404 body)
- [ ] Complete trip → rating template after ~2 min; `RATE:…:5` writes `TripRatings` once
- [ ] Text `STOP` → OptOut; further automations Skipped `OptedOut`
- [ ] Restart API mid-Pending → scheduler recovers DueAt ≤ now
- [ ] Enable rule without Approved en template → 409 `TEMPLATE_NOT_APPROVED`
- [ ] Resend same event ≤3 times; 4th rejected

## Explicitly out of Phase 2

Custom rule builder, marketing templates, anonymous SignalR on track page, traffic ETA, SMS fallback, geofence editor, free-text feedback.
