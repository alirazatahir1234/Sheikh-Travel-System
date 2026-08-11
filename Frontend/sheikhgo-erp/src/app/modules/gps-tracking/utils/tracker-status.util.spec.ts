import {
  isTrackerMoving,
  isFreshForMovement,
  isSpeedMoving,
  resolveTrackerStatus,
  TELEMETRY_MOVING_MAX_AGE_MS,
} from './tracker-status.util';
import { GpsDevice } from '../../../core/models/gps-tracking.model';

function device(overrides: Partial<GpsDevice> = {}): GpsDevice {
  return {
    id: 1,
    uniqueId: '123456789012345',
    name: 'Test',
    supportsEngineCutoff: false,
    isActive: true,
    ...overrides,
  };
}

describe('tracker-status.util', () => {
  const now = Date.parse('2026-07-17T17:00:00.000Z');

  it('does not mark stale speed as moving', () => {
    const d = device({
      isOnline: true,
      lastSeenAt: new Date(now - TELEMETRY_MOVING_MAX_AGE_MS - 60_000).toISOString(),
      lastSpeed: 39,
      lastIgnition: undefined,
    });

    expect(isTrackerMoving(d, now)).toBe(false);
    expect(resolveTrackerStatus(d, now).label).toBe('Online');
  });

  it('marks fresh speed above threshold as moving', () => {
    const d = device({
      isOnline: true,
      lastSeenAt: new Date(now - 30_000).toISOString(),
      lastSpeed: 39,
      lastIgnition: undefined,
    });

    expect(isFreshForMovement(d, now)).toBe(true);
    expect(isSpeedMoving(39)).toBe(true);
    expect(isTrackerMoving(d, now)).toBe(true);
  });

  it('does not mark low fresh speed as moving', () => {
    const d = device({
      isOnline: true,
      lastSeenAt: new Date(now - 30_000).toISOString(),
      lastSpeed: 3,
      lastIgnition: undefined,
    });

    expect(isTrackerMoving(d, now)).toBe(false);
    expect(resolveTrackerStatus(d, now).label).toBe('Idle');
  });

  it('marks ignition OFF with drift speed as parked', () => {
    const d = device({
      isOnline: true,
      lastSeenAt: new Date(now - 30_000).toISOString(),
      lastSpeed: 5,
      lastIgnition: false,
    });

    expect(isTrackerMoving(d, now)).toBe(false);
    expect(resolveTrackerStatus(d, now).key).toBe('parked');
  });
});
