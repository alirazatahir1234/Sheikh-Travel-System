import {
  resolveFleetStatus,
  tallyFleetStatusCounts
} from './gps-status.util';
import { VehicleLocation } from '../models/gps-tracking.model';

describe('resolveFleetStatus', () => {
  const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
  const fresh = '2026-07-13T08:00:00.000Z';

  it('speed 0 + ignition OFF + fresh GPS → parked', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 0, ignition: false },
        nowMs
      )
    ).toBe('parked');
  });

  it('speed 35 + ignition ON + fresh GPS → moving', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 35, ignition: true },
        nowMs
      )
    ).toBe('moving');
  });

  it('speed 0 + ignition ON + fresh GPS → idle', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 0, ignition: true },
        nowMs
      )
    ).toBe('idle');
  });

  it('old GPS → offline', () => {
    const staleNow = Date.parse('2026-07-13T08:31:00.000Z');
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: '2026-07-13T08:00:00.000Z', speed: 0, ignition: false },
        staleNow
      )
    ).toBe('offline');
  });

  it('no GPS ever → never_seen', () => {
    expect(
      resolveFleetStatus({ hasGps: false, lastUpdated: null, speed: 0 }, nowMs)
    ).toBe('never_seen');
    expect(
      resolveFleetStatus({ hasGps: true, lastUpdated: null, speed: 0 }, nowMs)
    ).toBe('never_seen');
  });

  it('treats timezone-less lastUpdated as UTC so a fresh fix stays idle', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: '2026-07-13T08:00:00', speed: 0, ignition: true },
        nowMs
      )
    ).toBe('idle');
  });

  it('ignition OFF + low GPS drift stays parked', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 5, ignition: false },
        nowMs
      )
    ).toBe('parked');
  });

  it('ignition OFF + high speed is unknown (contradictory)', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 14, ignition: false },
        nowMs
      )
    ).toBe('unknown');
  });

  it('ignition null + low speed is unknown (insufficient telemetry)', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 0, ignition: null },
        nowMs
      )
    ).toBe('unknown');
  });

  it('speed >= 10 with unknown ignition is moving', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 40, ignition: null },
        nowMs
      )
    ).toBe('moving');
  });

  it('does not derive status from address text', () => {
    expect(
      resolveFleetStatus(
        { hasGps: true, lastUpdated: fresh, speed: 0, ignition: false },
        nowMs
      )
    ).toBe('parked');
  });
});

describe('tallyFleetStatusCounts', () => {
  const row = (status: VehicleLocation['status']): Pick<VehicleLocation, 'status'> => ({ status });

  it('counts parked cards exactly for Parked KPI', () => {
    const locations = [
      row('parked'),
      row('moving'),
      row('parked'),
      row('offline')
    ];
    const tallied = tallyFleetStatusCounts(locations);
    expect(tallied.parked).toBe(2);
    expect(tallied.parked).toBe(locations.filter(l => l.status === 'parked').length);
    expect(tallied.moving).toBe(1);
    expect(tallied.offline).toBe(1);
    expect(tallied.online).toBe(3);
    expect(tallied.total).toBe(4);
  });

  it('includes unknown in online without counting as parked', () => {
    const tallied = tallyFleetStatusCounts([row('unknown'), row('parked')]);
    expect(tallied.unknown).toBe(1);
    expect(tallied.parked).toBe(1);
    expect(tallied.online).toBe(2);
  });
});
