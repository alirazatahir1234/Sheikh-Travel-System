import { computeFleetHealth } from './fleet-health.util';
import { VehicleLocation } from '../../../core/models/gps-tracking.model';

function createLocation(overrides: Partial<VehicleLocation> = {}): VehicleLocation {
  return {
    vehicleId: 1,
    vehicleName: 'Vehicle 1',
    registrationNumber: 'ABC-123',
    latitude: 31.5,
    longitude: 74.3,
    lastUpdated: new Date().toISOString(),
    speed: 0,
    status: 'idle',
    hasGps: true,
    ...overrides
  };
}

describe('computeFleetHealth', () => {
  it('returns all-zero buckets for an empty fleet', () => {
    expect(computeFleetHealth([])).toEqual({ optimal: 0, attention: 0, critical: 0, unknown: 0, total: 0 });
  });

  it('buckets never_seen and no-GPS vehicles as unknown', () => {
    const result = computeFleetHealth([
      createLocation({ status: 'never_seen' }),
      createLocation({ hasGps: false })
    ]);

    expect(result.unknown).toBe(2);
    expect(result.total).toBe(2);
  });

  it('buckets sos and offline vehicles as critical', () => {
    const result = computeFleetHealth([
      createLocation({ status: 'sos' }),
      createLocation({ status: 'offline' })
    ]);

    expect(result.critical).toBe(2);
  });

  it('buckets critically low battery as critical even when otherwise moving fine', () => {
    const result = computeFleetHealth([createLocation({ status: 'moving', batteryLevel: 5 })]);
    expect(result.critical).toBe(1);
  });

  it('buckets weak GSM signal or low-but-not-critical battery as attention', () => {
    const result = computeFleetHealth([
      createLocation({ status: 'idle', gsmSignal: 5 }),
      createLocation({ status: 'idle', batteryLevel: 20 })
    ]);

    expect(result.attention).toBe(2);
  });

  it('buckets a healthy, well-connected vehicle as optimal', () => {
    const result = computeFleetHealth([
      createLocation({ status: 'moving', batteryLevel: 90, gsmSignal: 28 })
    ]);

    expect(result.optimal).toBe(1);
  });
});
