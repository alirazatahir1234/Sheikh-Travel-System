# Google Maps cost controls — Geocoding + Places

| Field | Value |
|-------|--------|
| Date | 2026-09-30 |
| Context | ~$133 Geocoding+Places (Sep 1–29) traced to reverse-geocode pairing + GPS backfill |

## Code defaults (after cost-cut)

| Setting | Default | Env override |
|---------|---------|--------------|
| Prefer Google on background GPS | **false** | `Geocoding__PreferGoogle` |
| Places Nearby on reverse geocode | **false** | `Geocoding__IncludeNearbyPlace` |
| Backfill cooldown per vehicle | **300 s** | `Geocoding__BackfillCooldownoldownSeconds` |

Background GPS address upgrades use **Nominatim + GpsAddressCache**. Explicit UI reverse-geocode may still call Google Geocoding (no Places Nearby unless you opt in).

## Ops — do this in Google Cloud (do not disable billing)

1. **APIs & Services → Geocoding API / Places API → Quotas**  
   Set daily request caps (start conservatively; raise if legitimate UI usage needs more).
2. **Billing → Budgets & alerts**  
   Alert at e.g. **$20** and **$50** per month for the Maps project.
3. **Billing → Reports → Group by SKU**  
   After deploy, confirm Geocoding + Places Nearby request volume drops vs prior week.
4. **APIs & Services → Metrics**  
   Watch requests/day for Geocoding and Places for 24–48h after release.

## Expected result

- Reverse geocode no longer bills Places Nearby by default (was 1 Places call per Google reverse).
- Live positions polling no longer runs up to 40 synchronous Google resolves.
- Coarse Urdu/Arabic addresses no longer force endless refresh loops.
- Nearby Places panel (ERP) still works when the operator expands it (Places API New, separate path, rounded cache key).

## Validation checklist

See full results: [`11-maps-cost-validation-results.md`](11-maps-cost-validation-results.md).

- [x] Deploy API with new defaults (Railway on `e6c0dc0a`; env omits PreferGoogle / IncludeNearbyPlace → class defaults).
- [x] Live Map API smoke: GPS-online vehicle 50 address from VCL; reverse `fromCache: true`.
- [x] Manual Nearby (`GET /api/gps/maps/nearby-places`) returns fuel POIs when called explicitly.
- [ ] Cloud Metrics: Geocoding + legacy Nearby Search down sharply within 24h–7d (fill table in doc 11).
- [ ] Console: set Geocoding v3 requests/day = 2000; Places Nearby legacy/day = 500; budgets $20 / $50.
