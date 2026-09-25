import { VehicleLocation } from '../../../core/models/gps-tracking.model';
import { compareGpsTimestamps } from '../../../core/utils/gps-timestamp.util';
import { isCoarseAddress } from '../utils/gps-address.util';

/** Keep the more specific address when a poll/realtime payload sends a coarse locality. */
export function preferRicherAddress(
  current?: string | null,
  incoming?: string | null
): string | undefined {
  const cur = current?.trim() || '';
  const next = incoming?.trim() || '';
  if (!next) return cur || undefined;
  if (!cur) return next;
  if (isCoarseAddress(next) && !isCoarseAddress(cur)) return cur;
  if (!isCoarseAddress(next) && isCoarseAddress(cur)) return next;
  if (next.length > cur.length + 8) return next;
  return cur;
}

export function mergeVehicleLocations(
  existing: VehicleLocation[],
  incoming: VehicleLocation[]
): VehicleLocation[] {
  const byId = new Map(existing.map(location => [location.vehicleId, location]));

  incoming.forEach(location => {
    const previous = byId.get(location.vehicleId);
    // Keep the fresher lastUpdated so a poll cannot overwrite a newer SignalR fix
    // with an older-looking (or timezone-misparsed) DB timestamp.
    if (previous && compareGpsTimestamps(previous.lastUpdated, location.lastUpdated) > 0) {
      byId.set(location.vehicleId, {
        ...location,
        ...previous,
        // Prefer newer telemetry fields from previous, but still allow incoming metadata
        // (name/plate) when previous lacked them.
        vehicleName: previous.vehicleName || location.vehicleName,
        registrationNumber: previous.registrationNumber || location.registrationNumber
      });
      return;
    }
    byId.set(location.vehicleId, {
      ...(previous ?? {}),
      ...location,
      // Don't wipe a resolved street address with a coarse poll payload.
      address: preferRicherAddress(previous?.address, location.address),
      placeName: location.placeName?.trim() || previous?.placeName,
      placeType: location.placeType?.trim() || previous?.placeType,
      addressLocality: location.addressLocality?.trim() || previous?.addressLocality
    });
  });

  return Array.from(byId.values());
}

/**
 * Like {@link mergeVehicleLocations}, but keeps the same array reference when
 * vehicle membership (set of vehicleIds) is unchanged — patches slots in place.
 */
export function mergeVehicleLocationsPreservingIdentity(
  existing: VehicleLocation[],
  incoming: VehicleLocation[]
): { locations: VehicleLocation[]; membershipChanged: boolean } {
  const merged = mergeVehicleLocations(existing, incoming);
  if (existing.length === 0) {
    return { locations: merged, membershipChanged: merged.length > 0 };
  }

  const existingIds = new Set(existing.map(l => l.vehicleId));
  const mergedIds = new Set(merged.map(l => l.vehicleId));
  let membershipChanged = existingIds.size !== mergedIds.size;
  if (!membershipChanged) {
    for (const id of existingIds) {
      if (!mergedIds.has(id)) {
        membershipChanged = true;
        break;
      }
    }
  }

  if (membershipChanged) {
    return { locations: merged, membershipChanged: true };
  }

  const byId = new Map(merged.map(l => [l.vehicleId, l]));
  for (let i = 0; i < existing.length; i++) {
    const next = byId.get(existing[i].vehicleId);
    if (next && existing[i] !== next) {
      existing[i] = next;
    }
  }
  return { locations: existing, membershipChanged: false };
}
