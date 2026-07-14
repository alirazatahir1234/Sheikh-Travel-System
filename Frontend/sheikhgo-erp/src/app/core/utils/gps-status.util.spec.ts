import { resolveFleetStatus } from './gps-status.util';

describe('resolveFleetStatus timezone handling', () => {
  it('treats timezone-less lastUpdated as UTC so a fresh fix stays idle', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 0,
        ignition: true
      },
      nowMs
    );
    expect(status).toBe('idle');
  });

  it('marks offline only when age exceeds 30 minutes', () => {
    const nowMs = Date.parse('2026-07-13T08:31:00.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 0
      },
      nowMs
    );
    expect(status).toBe('offline');
  });
});
