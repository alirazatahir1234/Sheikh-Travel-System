import {
  canCalculateRoute,
  hasCoordinatePair,
  locationSelectionError,
  PLACE_SELECTION_WARNING,
  readCoordinate,
  routeMetricsForPayload
} from './trip-location.rules';

describe('trip location rules', () => {
  it('does not treat null as coordinate 0', () => {
    expect(readCoordinate(null)).toBeNull();
    expect(readCoordinate('')).toBeNull();
    expect(readCoordinate(undefined)).toBeNull();
    expect(hasCoordinatePair(null, null)).toBeFalse();
    expect(hasCoordinatePair(0, 74.3)).toBeTrue();
  });

  it('refuses routing unless pickup and destination coordinates both exist', () => {
    expect(canCalculateRoute(null, null, null, null)).toBeFalse();
    expect(canCalculateRoute(31.5, 74.3, null, null)).toBeFalse();
    expect(canCalculateRoute('Lahore', 'Lahore', 24.8, 67)).toBeFalse();
    expect(canCalculateRoute(31.5, 74.3, 24.8, 67)).toBeTrue();
  });

  it('requires a Google place selection when address text has no coordinates', () => {
    expect(locationSelectionError('Lahore', null, null)).toBe(PLACE_SELECTION_WARNING);
    expect(locationSelectionError('  ', null, null)).toBeNull();
    expect(locationSelectionError('Lahore, Punjab, Pakistan', 31.52, 74.35)).toBeNull();
  });

  it('drops distance and duration when coordinates are incomplete', () => {
    expect(
      routeMetricsForPayload({
        pickupLatitude: null,
        pickupLongitude: null,
        destinationLatitude: 24.86,
        destinationLongitude: 67.0,
        plannedDistanceKm: 340,
        estimatedDurationMinutes: 240
      })
    ).toEqual({ plannedDistanceKm: null, estimatedDurationMinutes: null });
  });

  it('keeps distance and duration only when both coordinate pairs exist', () => {
    expect(
      routeMetricsForPayload({
        pickupLatitude: 31.52,
        pickupLongitude: 74.35,
        destinationLatitude: 30.15,
        destinationLongitude: 71.52,
        plannedDistanceKm: 340,
        estimatedDurationMinutes: 240
      })
    ).toEqual({ plannedDistanceKm: 340, estimatedDurationMinutes: 240 });
  });
});
