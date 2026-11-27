import { daySpan, occupancyBars, replayParams, visitKpis, zoneShortName } from './geofence-visits.component';
import { GeofenceVisitsSummary } from '../geofence.models';

const SUMMARY: GeofenceVisitsSummary = {
  visits: 38, vehicles: 12, totalMinutes: 3680, avgMinutes: 42, idleMinutes: 245, breaches: 5, overstays: 3,
  overspeeds: 1, afterHoursExits: 1, idleBreaches: 0, dwellTargetMinutes: 30,
  occupancyToday: [1, 1, 1, 1, 1, 1, 1, 2, 3, 3, 2, 2, 1, 2, 3, 3, 2, 2, 3, 2, 2, 1, 1, 0],
  dwellBreachHoursToday: Array.from({ length: 24 }, (_, h) => h === 13 || h === 14)
};

describe('geofence visits report helpers', () => {
  it('builds the five KPI cards, flagging an average over the dwell target', () => {
    const cards = visitKpis(SUMMARY);
    expect(cards.map(c => `${c.label}=${c.value} (${c.note})`)).toEqual([
      'Visits=38 (12 vehicles)',
      'Total time on site=61h 20m (open visits count up to now)',
      'Avg time on site=42m (target 30m)',
      'Idle inside=4h 05m (engine on, not moving)',
      'Rule breaches=5 (3 overstay · 1 overspeed · 1 after hours)'
    ]);
    expect(cards[2].tone).toBe('amber');
    expect(cards[4].tone).toBe('red');
    expect(visitKpis({ ...SUMMARY, avgMinutes: null, dwellTargetMinutes: null })[2].note).toBe('no visit ended in this range');
  });

  it('scales the hourly bars to the busiest hour and marks dwell breaches orange', () => {
    const bars = occupancyBars(SUMMARY, 64);
    expect(bars.length).toBe(24);
    expect(bars[8].heightPx).toBe(64);
    expect(bars[23].heightPx).toBe(2);
    expect(bars[13].breach && bars[14].breach).toBeTrue();
    expect(bars.filter(b => b.breach).length).toBe(2);
    expect(bars[14].label).toBe('2 PM: 3 vehicles inside, dwell rule breached');
    expect(occupancyBars(null).every(b => b.count === 0)).toBeTrue();
  });

  it('opens History five minutes either side of a visit, in company time', () => {
    const now = new Date('2026-11-27T12:00:00Z');
    const params = replayParams({ vehicleId: 42, enteredUtc: '2026-11-27T08:10:00', exitedUtc: '2026-11-27T09:00:00' }, 'Asia/Karachi', now);
    expect(params).toEqual({ vehicleId: '42', preset: 'custom', from: '2026-11-27T13:05', to: '2026-11-27T14:05' });

    const open = replayParams({ vehicleId: 42, enteredUtc: '2026-11-27T11:30:00Z', exitedUtc: null }, 'Asia/Karachi', now);
    expect(open['to']).toBe('2026-11-27T17:00');
  });

  it('measures the date range and names the zone', () => {
    expect(daySpan('2026-11-21', '2026-11-27')).toBe(6);
    expect(daySpan('2026-11-27', '2026-11-21')).toBe(-6);
    expect(Number.isNaN(daySpan('', '2026-11-27'))).toBeTrue();
    expect(zoneShortName('Asia/Karachi')).toMatch(/GMT\+5|PKT/);
    expect(zoneShortName('Not/AZone')).toBe('Not/AZone');
  });
});
