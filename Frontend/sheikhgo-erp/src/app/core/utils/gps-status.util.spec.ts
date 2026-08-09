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

  it('treats ignition OFF as parked even when speed shows GPS drift', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 5,
        ignition: false
      },
      nowMs
    );
    expect(status).toBe('parked');
  });

  it('does not treat ignition OFF + high drift as moving', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 14,
        ignition: false
      },
      nowMs
    );
    expect(status).toBe('parked');
  });

  it('treats ignition OFF and near-zero speed as parked', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 0,
        ignition: false
      },
      nowMs
    );
    expect(status).toBe('parked');
  });

  it('treats speed >= 10 with ignition ON as moving', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 10,
        ignition: true
      },
      nowMs
    );
    expect(status).toBe('moving');
  });

  it('treats speed >= 10 with unknown ignition as moving (unwired ACC)', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 40,
        ignition: null
      },
      nowMs
    );
    expect(status).toBe('moving');
  });

  it('treats ignition ON and low speed as idle', () => {
    const nowMs = Date.parse('2026-07-13T08:00:30.000Z');
    const status = resolveFleetStatus(
      {
        hasGps: true,
        lastUpdated: '2026-07-13T08:00:00',
        speed: 3,
        ignition: true
      },
      nowMs
    );
    expect(status).toBe('idle');
  });
});
