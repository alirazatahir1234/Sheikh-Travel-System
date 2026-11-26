import {
  formatTimeInZone,
  fromZonedDateTimeLocal,
  isValidTimeZone,
  resolveZone,
  setActiveTimeZone,
  toDate,
  toZonedDateInput,
  toZonedDateTimeLocal,
  zoneOffsetMinutes,
  zoneOffsetString,
  zonedEndOfDay,
  zonedStartOfDay
} from './zoned-time.util';
import { DEFAULT_TIME_ZONE } from '../constants/time-zone.constants';

/**
 * BWY 217, 24 Nov 2026: a trip at 21:15 UTC must read 02:15 in Pakistan and 01:15 in Dubai,
 * whatever timezone the browser running the ERP is set to.
 */
describe('zoned-time.util', () => {
  const tripUtc = '2026-11-23T21:15:00Z';

  afterEach(() => setActiveTimeZone(DEFAULT_TIME_ZONE));

  it('defaults to Asia/Karachi', () => {
    expect(DEFAULT_TIME_ZONE).toBe('Asia/Karachi');
    expect(resolveZone(null)).toBe('Asia/Karachi');
  });

  it('formats an instant in Pakistan and UAE time', () => {
    const opts: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' };
    expect(formatTimeInZone(tripUtc, opts, 'Asia/Karachi', 'en-GB')).toBe('02:15');
    expect(formatTimeInZone(tripUtc, opts, 'Asia/Dubai', 'en-GB')).toBe('01:15');
  });

  it('uses the active company zone when none is passed', () => {
    setActiveTimeZone('Asia/Dubai');
    expect(toZonedDateTimeLocal(tripUtc)).toBe('2026-11-24T01:15');
    setActiveTimeZone('Asia/Karachi');
    expect(toZonedDateTimeLocal(tripUtc)).toBe('2026-11-24T02:15');
  });

  it('ignores invalid zones', () => {
    expect(isValidTimeZone('Mars/Olympus')).toBeFalse();
    setActiveTimeZone('Mars/Olympus');
    expect(resolveZone(undefined)).toBe('Asia/Karachi');
    expect(resolveZone('Mars/Olympus')).toBe('Asia/Karachi');
  });

  it('reports offsets as Angular timezone strings', () => {
    expect(zoneOffsetMinutes(tripUtc, 'Asia/Karachi')).toBe(300);
    expect(zoneOffsetString(tripUtc, 'Asia/Karachi')).toBe('+0500');
    expect(zoneOffsetString(tripUtc, 'Asia/Dubai')).toBe('+0400');
    expect(zoneOffsetString(tripUtc, 'UTC')).toBe('+0000');
  });

  it('turns picker values into instants in the zone (round trip)', () => {
    expect(fromZonedDateTimeLocal('2026-11-24T02:15', 'Asia/Karachi')!.toISOString()).toBe('2026-11-23T21:15:00.000Z');
    expect(fromZonedDateTimeLocal('2026-11-24T01:15', 'Asia/Dubai')!.toISOString()).toBe('2026-11-23T21:15:00.000Z');
    expect(fromZonedDateTimeLocal('2026-11-24', 'Asia/Karachi')!.toISOString()).toBe('2026-11-23T19:00:00.000Z');
    expect(fromZonedDateTimeLocal('not a date', 'Asia/Karachi')).toBeNull();
  });

  it('finds local day boundaries', () => {
    expect(zonedStartOfDay(tripUtc, 'Asia/Karachi').toISOString()).toBe('2026-11-23T19:00:00.000Z');
    expect(zonedEndOfDay(tripUtc, 'Asia/Karachi').toISOString()).toBe('2026-11-24T18:59:59.999Z');
    expect(zonedStartOfDay(tripUtc, 'Asia/Dubai').toISOString()).toBe('2026-11-23T20:00:00.000Z');
  });

  it('handles DST zones (Europe/London spring forward)', () => {
    // 29 Mar 2026: clocks jump 01:00 → 02:00; 03:00 local is 02:00 UTC.
    expect(fromZonedDateTimeLocal('2026-03-29T03:00', 'Europe/London')!.toISOString()).toBe('2026-03-29T02:00:00.000Z');
    expect(fromZonedDateTimeLocal('2026-03-28T12:00', 'Europe/London')!.toISOString()).toBe('2026-03-28T12:00:00.000Z');
  });

  it('treats timezone-less API timestamps as UTC and date-only values as calendar dates', () => {
    expect(toDate('2026-11-23T21:15:00')!.toISOString()).toBe('2026-11-23T21:15:00.000Z');
    expect(toZonedDateInput('2026-11-24', 'Asia/Karachi')).toBe('2026-11-24');
    expect(toZonedDateInput('2026-11-24', 'America/New_York')).toBe('2026-11-24');
  });
});
