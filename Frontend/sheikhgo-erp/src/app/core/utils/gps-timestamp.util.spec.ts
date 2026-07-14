import { parseGpsTimestamp } from './gps-timestamp.util';

describe('parseGpsTimestamp', () => {
  it('treats timezone-less ISO as UTC so age is not inflated by local offset', () => {
    const utcIso = '2026-07-13T08:00:00.000Z';
    const bare = '2026-07-13T08:00:00';
    expect(parseGpsTimestamp(bare)).toBe(parseGpsTimestamp(utcIso));
  });

  it('preserves explicit Z and offsets', () => {
    const withZ = parseGpsTimestamp('2026-07-13T08:00:00Z');
    const withOffset = parseGpsTimestamp('2026-07-13T13:00:00+05:00');
    expect(withZ).toBe(withOffset);
  });

  it('returns NaN for empty values', () => {
    expect(Number.isNaN(parseGpsTimestamp(''))).toBe(true);
    expect(Number.isNaN(parseGpsTimestamp(null))).toBe(true);
  });
});
