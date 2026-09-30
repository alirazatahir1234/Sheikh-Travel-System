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

- [ ] Deploy API with new defaults (Railway env may omit new keys — class defaults apply).
- [ ] Live Map with GPS-online vehicle: address updates without Places Nearby spike.
- [ ] Manual Nearby panel search still returns fuel/POI results.
- [ ] Cloud Metrics: Geocoding + legacy Nearby Search down sharply within 24h.
