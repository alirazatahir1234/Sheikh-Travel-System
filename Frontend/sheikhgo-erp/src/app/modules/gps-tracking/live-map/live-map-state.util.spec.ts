import { mergeVehicleLocations } from './live-map-state.util';
import { VehicleLocation } from '../../../core/models/gps-tracking.model';

describe('mergeVehicleLocations', () => {
  const createVehicle = (id: number, overrides: Partial<VehicleLocation> = {}): VehicleLocation => ({
    vehicleId: id,
    vehicleName: `Vehicle ${id}`,
    registrationNumber: '',
    latitude: 0,
    longitude: 0,
    lastUpdated: '',
    speed: 0,
    status: 'offline',
    hasGps: false,
    isLive: false,
    routeHint: '',
    ...overrides
  } as VehicleLocation);

  it('updates existing vehicles and preserves vehicles not returned in the refresh payload', () => {
    const existing = [
      createVehicle(1, { speed: 0, status: 'offline' }),
      createVehicle(2, { speed: 0, status: 'offline' })
    ];

    const incoming = [
      createVehicle(1, { speed: 42, status: 'moving', latitude: 31.5, longitude: 74.3 })
    ];

    const merged = mergeVehicleLocations(existing, incoming);

    expect(merged).toHaveSize(2);
    expect(merged.find(vehicle => vehicle.vehicleId === 1)?.speed).toBe(42);
    expect(merged.find(vehicle => vehicle.vehicleId === 1)?.status).toBe('moving');
    expect(merged.find(vehicle => vehicle.vehicleId === 2)?.vehicleId).toBe(2);
  });

  it('keeps the fresher SignalR row when poll returns an older lastUpdated', () => {
    const existing = [
      createVehicle(1, {
        speed: 0,
        status: 'idle',
        lastUpdated: '2026-07-13T08:30:00.000Z',
        latitude: 32.1,
        longitude: 74.1,
        isLive: true,
        hasGps: true
      })
    ];

    const incoming = [
      createVehicle(1, {
        speed: 0,
        status: 'offline',
        lastUpdated: '2026-07-13T03:30:00.000Z',
        latitude: 32.0,
        longitude: 74.0,
        isLive: false,
        hasGps: true
      })
    ];

    const merged = mergeVehicleLocations(existing, incoming);
    const row = merged.find(v => v.vehicleId === 1)!;

    expect(row.lastUpdated).toBe('2026-07-13T08:30:00.000Z');
    expect(row.status).toBe('idle');
    expect(row.latitude).toBe(32.1);
    expect(row.isLive).toBe(true);
  });

  it('applies incoming when it is newer than existing', () => {
    const existing = [
      createVehicle(1, {
        lastUpdated: '2026-07-13T08:00:00.000Z',
        status: 'idle',
        speed: 0
      })
    ];
    const incoming = [
      createVehicle(1, {
        lastUpdated: '2026-07-13T08:01:00.000Z',
        status: 'moving',
        speed: 40
      })
    ];

    const merged = mergeVehicleLocations(existing, incoming);
    expect(merged[0].status).toBe('moving');
    expect(merged[0].speed).toBe(40);
    expect(merged[0].lastUpdated).toBe('2026-07-13T08:01:00.000Z');
  });
});
