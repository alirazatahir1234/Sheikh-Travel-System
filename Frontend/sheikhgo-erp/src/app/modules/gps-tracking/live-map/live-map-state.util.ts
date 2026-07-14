import { VehicleLocation } from '../../../core/models/gps-tracking.model';
import { compareGpsTimestamps } from '../../../core/utils/gps-timestamp.util';

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
      ...location
    });
  });

  return Array.from(byId.values());
}
