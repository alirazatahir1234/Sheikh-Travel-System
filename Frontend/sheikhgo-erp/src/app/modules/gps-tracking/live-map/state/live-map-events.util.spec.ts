import { VehicleLocation } from '../../../../core/models/gps-tracking.model';
import { detectTelemetryEvents } from './live-map-events.util';

function baseLocation(overrides: Partial<VehicleLocation> = {}): VehicleLocation {
  return {
    vehicleId: 1,
    vehicleName: 'Bus 12',
    registrationNumber: 'ABC-123',
    latitude: 24.86,
    longitude: 67.0,
    speed: 0,
    status: 'parked',
    hasGps: true,
    lastUpdated: new Date().toISOString(),
    ...overrides
  } as VehicleLocation;
}

describe('detectTelemetryEvents', () => {
  it('emits status transition events', () => {
    const prev = baseLocation({ status: 'parked' });
    const next = baseLocation({ status: 'moving', speed: 40 });

    const events = detectTelemetryEvents(prev, next);

    expect(events.length).toBe(1);
    expect(events[0].message).toContain('Parked → Moving');
    expect(events[0].type).toBe('info');
  });

  it('emits SOS alert on status change to sos', () => {
    const prev = baseLocation({ status: 'moving' });
    const next = baseLocation({ status: 'sos' });

    const events = detectTelemetryEvents(prev, next);

    expect(events[0].type).toBe('alert');
    expect(events[0].icon).toBe('sos');
  });

  it('emits battery low warning when crossing threshold', () => {
    const prev = baseLocation({ batteryLevel: 25 });
    const next = baseLocation({ batteryLevel: 15 });

    const events = detectTelemetryEvents(prev, next);

    expect(events.some(e => e.message.includes('Battery low'))).toBe(true);
    expect(events.find(e => e.message.includes('Battery low'))?.type).toBe('warning');
  });

  it('emits overspeed alert above 100 km/h', () => {
    const prev = baseLocation({ speed: 90 });
    const next = baseLocation({ speed: 110, status: 'moving' });

    const events = detectTelemetryEvents(prev, next);

    expect(events.some(e => e.message.includes('Overspeed'))).toBe(true);
  });
});
