# SheikhGo Driver Production Readiness Checklist

## Sprint 1 Stabilization

- [ ] Accept / Arrived / Onboard / Complete / Reject verified on iOS
- [ ] Accept / Arrived / Onboard / Complete / Reject verified on Android
- [ ] Continue Trip opens destination in Google / Apple / Waze
- [ ] Booking-only trip converts to operational trip on first action
- [ ] Offline trip actions replay after reconnect
- [ ] GPS background queue drains to `/driver-app/location/batch`

## Sprint 2 Payments

- [ ] `GET /driver-app/trips/{id}/payment-summary` returns due/pending state
- [ ] `POST /driver-app/trips/{id}/collect-payment` creates payment in ERP
- [ ] Collect screen validates non-cash reference number
- [ ] Skip leaves pending balance for finance follow-up
- [ ] Offline payment queue sync works after reconnect

## Sprint 3 Operations

- [ ] Timeline shows payment collected events
- [ ] Timeline shows trip lifecycle milestones
- [ ] Driver can set status: Online/Busy/Break/Unavailable
- [ ] Dashboard reflects status immediately after update

## Sprint 5/6 Hardening & Release

- [ ] Device integrity checks pass in Security screen
- [ ] Maps keys restricted by package/bundle id
- [ ] Background GPS battery test passed (2-hour run)
- [ ] Push notifications verified foreground/background
- [ ] Android internal + closed tracks passed
- [ ] iOS TestFlight + App Review artifacts ready
