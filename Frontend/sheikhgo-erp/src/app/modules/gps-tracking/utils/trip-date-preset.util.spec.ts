import {
  HISTORY_DATE_PRESETS,
  TripDatePreset,
  applyTripDatePreset,
  formatPeriodLabel,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';

describe('applyTripDatePreset', () => {
  const now = new Date(2026, 6, 13, 14, 30, 0); // 13 Jul 2026 local

  it('resolves today from midnight to end of day', () => {
    const { from, to } = applyTripDatePreset('today', now);
    expect(from.getHours()).toBe(0);
    expect(from.getDate()).toBe(13);
    expect(to.getDate()).toBe(13);
    expect(to.getHours()).toBe(23);
  });

  it('resolves yesterday as the previous calendar day', () => {
    const { from, to } = applyTripDatePreset('yesterday', now);
    expect(from.getDate()).toBe(12);
    expect(to.getDate()).toBe(12);
  });

  it('resolves last 30 days and this year windows', () => {
    const month = applyTripDatePreset('last30Days', now);
    expect(month.to.getTime() - month.from.getTime()).toBeGreaterThanOrEqual(29 * 24 * 60 * 60 * 1000);

    const year = applyTripDatePreset('thisYear', now);
    expect(year.from.getMonth()).toBe(0);
    expect(year.from.getDate()).toBe(1);
    expect(year.from.getFullYear()).toBe(2026);
  });

  it('formats period labels', () => {
    const from = toDatetimeLocalInput(new Date(2026, 6, 1, 0, 0));
    const to = toDatetimeLocalInput(new Date(2026, 6, 13, 14, 0));
    expect(formatPeriodLabel(from, to)).toContain('2026');
  });

  it('exposes history presets including custom', () => {
    expect(HISTORY_DATE_PRESETS.some(p => p.id === 'custom')).toBe(true);
    expect(HISTORY_DATE_PRESETS.some(p => p.id === 'thisYear')).toBe(true);
  });
});
