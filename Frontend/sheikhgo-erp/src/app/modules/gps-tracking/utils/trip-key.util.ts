import { GpsTrip } from '../../../core/models/gps-tracking.model';

export function resolveTripKey(trip: GpsTrip): string {
  if (trip.tripKey?.trim()) return trip.tripKey;
  return `${trip.vehicleId}|${trip.startTime}|${trip.endTime}`;
}
