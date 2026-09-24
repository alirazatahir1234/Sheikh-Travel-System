/** A typed address is not a location until both coordinates exist. */
export const PLACE_SELECTION_WARNING =
  'Please select a location from the address suggestions.';

/**
 * `Number(null)` is 0, which is a real coordinate. Null, blank, and non-numeric
 * values must stay missing so routing cannot run from Null Island.
 */
export function readCoordinate(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : null;
}

export function hasCoordinatePair(latitude: unknown, longitude: unknown): boolean {
  return readCoordinate(latitude) !== null && readCoordinate(longitude) !== null;
}

export function canCalculateRoute(
  pickupLatitude: unknown,
  pickupLongitude: unknown,
  destinationLatitude: unknown,
  destinationLongitude: unknown
): boolean {
  return (
    hasCoordinatePair(pickupLatitude, pickupLongitude) &&
    hasCoordinatePair(destinationLatitude, destinationLongitude)
  );
}

export function locationSelectionError(
  address: unknown,
  latitude: unknown,
  longitude: unknown
): string | null {
  const text = typeof address === 'string' ? address.trim() : '';
  if (!text) return null;
  return hasCoordinatePair(latitude, longitude) ? null : PLACE_SELECTION_WARNING;
}

export function routeMetricsForPayload(input: {
  pickupLatitude: unknown;
  pickupLongitude: unknown;
  destinationLatitude: unknown;
  destinationLongitude: unknown;
  plannedDistanceKm: number | null;
  estimatedDurationMinutes: number | null;
}): { plannedDistanceKm: number | null; estimatedDurationMinutes: number | null } {
  if (
    !canCalculateRoute(
      input.pickupLatitude,
      input.pickupLongitude,
      input.destinationLatitude,
      input.destinationLongitude
    )
  ) {
    return { plannedDistanceKm: null, estimatedDurationMinutes: null };
  }

  return {
    plannedDistanceKm: input.plannedDistanceKm,
    estimatedDurationMinutes: input.estimatedDurationMinutes
  };
}
