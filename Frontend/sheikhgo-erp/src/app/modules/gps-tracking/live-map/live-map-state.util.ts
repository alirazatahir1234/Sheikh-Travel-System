import { VehicleLocation } from '../../../core/models/gps-tracking.model';

export function mergeVehicleLocations(
  existing: VehicleLocation[],
  incoming: VehicleLocation[]
): VehicleLocation[] {
  const byId = new Map(existing.map(location => [location.vehicleId, location]));

  incoming.forEach(location => {
    const previous = byId.get(location.vehicleId);
    byId.set(location.vehicleId, {
      ...(previous ?? {}),
      ...location
    });
  });

  return Array.from(byId.values());
}
