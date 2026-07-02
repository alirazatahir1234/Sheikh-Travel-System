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
});
