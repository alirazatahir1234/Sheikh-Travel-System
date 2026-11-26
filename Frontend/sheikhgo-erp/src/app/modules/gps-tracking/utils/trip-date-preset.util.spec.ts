import {
  HISTORY_DATE_PRESETS,
  TRIP_DATE_MAX_RANGE_MS,
  applyTripDatePreset,
  formatPeriodLabel,
  parseDatetimeLocalInput,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';
import { setActiveTimeZone } from '../../../core/utils/zoned-time.util';

/** Presets follow the company timezone, not the browser's. */
describe('applyTripDatePreset', () => {
  // 13 Jul 2026 09:30 UTC = 14:30 PKT = 13:30 GST
  const now = new Date(Date.UTC(2026, 6, 13, 9, 30, 0));

  afterEach(() => setActiveTimeZone('Asia/Karachi'));

  it('resolves today from local midnight to end of day (Asia/Karachi)', () => {
    setActiveTimeZone('Asia/Karachi');
    const { from, to } = applyTripDatePreset('today', now);
    expect(from.toISOString()).toBe('2026-07-12T19:00:00.000Z');
    expect(to.toISOString()).toBe('2026-07-13T18:59:59.999Z');
  });

  it('resolves today in Asia/Dubai for a UAE company', () => {
    setActiveTimeZone('Asia/Dubai');
    const { from } = applyTripDatePreset('today', now);
    expect(from.toISOString()).toBe('2026-07-12T20:00:00.000Z');
  });

  it('resolves last 7 days as fully rolling, ending now (never in the future)', () => {
    const { from, to } = applyTripDatePreset('last7Days', now);
    expect(to.getTime()).toBe(now.getTime());
    expect(to.getTime() - from.getTime()).toBe(7 * 24 * 60 * 60 * 1000);
  });

  it('resolves yesterday as the previous local calendar day', () => {
    setActiveTimeZone('Asia/Karachi');
    const { from, to } = applyTripDatePreset('yesterday', now);
    expect(from.toISOString()).toBe('2026-07-11T19:00:00.000Z');
    expect(to.toISOString()).toBe('2026-07-12T18:59:59.999Z');
  });

  it('starts this week on local Monday and this month on the local 1st', () => {
    setActiveTimeZone('Asia/Karachi');
    expect(applyTripDatePreset('thisWeek', now).from.toISOString()).toBe('2026-07-12T19:00:00.000Z'); // Mon 13 Jul PKT
    expect(applyTripDatePreset('thisMonth', now).from.toISOString()).toBe('2026-06-30T19:00:00.000Z');
  });

  it('clamps last30Days and thisYear to MaxRange (30 days)', () => {
    const month = applyTripDatePreset('last30Days', now);
    expect(month.to.getTime() - month.from.getTime()).toBeLessThanOrEqual(TRIP_DATE_MAX_RANGE_MS);

    const year = applyTripDatePreset('thisYear', now);
    expect(year.to.getTime() - year.from.getTime()).toBeLessThanOrEqual(TRIP_DATE_MAX_RANGE_MS);
  });

  it('round-trips picker values in the company timezone', () => {
    setActiveTimeZone('Asia/Karachi');
    const local = toDatetimeLocalInput(new Date(Date.UTC(2026, 10, 24, 2, 15)));
    expect(local).toBe('2026-11-24T07:15');
    expect(parseDatetimeLocalInput(local).toISOString()).toBe('2026-11-24T02:15:00.000Z');
    expect(parseDatetimeLocalInput('2026-11-24T02:15:00Z').toISOString()).toBe('2026-11-24T02:15:00.000Z');
  });

  it('formats period labels', () => {
    const from = toDatetimeLocalInput(new Date(Date.UTC(2026, 6, 1, 0, 0)));
    const to = toDatetimeLocalInput(new Date(Date.UTC(2026, 6, 13, 14, 0)));
    expect(formatPeriodLabel(from, to)).toContain('2026');
  });

  it('exposes history presets including custom', () => {
    expect(HISTORY_DATE_PRESETS.some(p => p.id === 'custom')).toBe(true);
    expect(HISTORY_DATE_PRESETS.some(p => p.id === 'thisYear')).toBe(true);
  });
});
