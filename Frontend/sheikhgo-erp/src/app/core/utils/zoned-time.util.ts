import { DEFAULT_TIME_ZONE } from '../constants/time-zone.constants';
import { parseGpsTimestamp } from './gps-timestamp.util';

/**
 * Timezone-aware date helpers built on Intl (no extra library).
 * Timestamps from the API are UTC instants; these helpers display them — and turn date pickers back
 * into instants — in a given IANA zone (company / branch), never the browser's own timezone.
 *
 * The active zone is set once from the company branding (TenantTimeService) so plain utility
 * functions can use it without dependency injection.
 */

export type DateInput = Date | string | number | null | undefined;

let activeZone = DEFAULT_TIME_ZONE;

export function isValidTimeZone(zone: string | null | undefined): boolean {
  const z = zone?.trim();
  if (!z) return false;
  try {
    new Intl.DateTimeFormat('en-US', { timeZone: z });
    return true;
  } catch {
    return false;
  }
}

/** Set the company zone used when callers do not pass one. Invalid/blank values are ignored. */
export function setActiveTimeZone(zone: string | null | undefined): void {
  if (isValidTimeZone(zone)) activeZone = zone!.trim();
}

export function activeTimeZone(): string {
  return activeZone;
}

/** Explicit zone when valid (e.g. a vehicle's branch), else the active company zone. */
export function resolveZone(zone?: string | null): string {
  return isValidTimeZone(zone) ? zone!.trim() : activeZone;
}

export function isDateOnly(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value.trim());
}

/** Parse API values; timezone-less ISO strings are UTC (see parseGpsTimestamp). */
export function toDate(value: DateInput): Date | null {
  if (value === null || value === undefined || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value;
  if (typeof value === 'number') return isNaN(value) ? null : new Date(value);
  // Date-only ('YYYY-MM-DD') is a calendar date, not an instant: noon UTC keeps the same date in UTC−11..+11.
  if (isDateOnly(value)) return new Date(`${value.trim()}T12:00:00Z`);
  const t = parseGpsTimestamp(value);
  return isNaN(t) ? null : new Date(t);
}

export interface ZonedParts {
  year: number;
  month: number; // 1-12
  day: number;
  hours: number;
  minutes: number;
  seconds: number;
}

const partFormatters = new Map<string, Intl.DateTimeFormat>();

function partsFormatter(zone: string): Intl.DateTimeFormat {
  let f = partFormatters.get(zone);
  if (!f) {
    f = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
    partFormatters.set(zone, f);
  }
  return f;
}

/** Wall-clock fields of an instant in a zone. */
export function zonedParts(value: DateInput, zone?: string | null): ZonedParts | null {
  const d = toDate(value);
  if (!d) return null;
  const parts = partsFormatter(resolveZone(zone)).formatToParts(d);
  const get = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find(p => p.type === type)?.value ?? 0);
  return {
    year: get('year'),
    month: get('month'),
    day: get('day'),
    hours: get('hour') % 24,
    minutes: get('minute'),
    seconds: get('second')
  };
}

/** Minutes the zone is ahead of UTC at that instant (Asia/Karachi → 300). */
export function zoneOffsetMinutes(value: DateInput, zone?: string | null): number {
  const d = toDate(value);
  const p = zonedParts(d, zone);
  if (!d || !p) return 0;
  const wallAsUtc = Date.UTC(p.year, p.month - 1, p.day, p.hours, p.minutes, p.seconds);
  return Math.round((wallAsUtc - Math.floor(d.getTime() / 1000) * 1000) / 60000);
}

/** '+0500' style offset — the timezone format Angular's formatDate/DatePipe accept. */
export function zoneOffsetString(value: DateInput, zone?: string | null): string {
  const m = zoneOffsetMinutes(value, zone);
  const sign = m < 0 ? '-' : '+';
  const abs = Math.abs(m);
  return `${sign}${String(Math.floor(abs / 60)).padStart(2, '0')}${String(abs % 60).padStart(2, '0')}`;
}

/** Instant for a wall-clock time in a zone (DST-safe: re-checks the offset at the result). */
export function zonedWallTimeToUtc(
  year: number, month: number, day: number,
  hours = 0, minutes = 0, seconds = 0, ms = 0,
  zone?: string | null
): Date {
  const guess = Date.UTC(year, month - 1, day, hours, minutes, seconds, ms);
  const first = guess - zoneOffsetMinutes(guess, zone) * 60000;
  const second = guess - zoneOffsetMinutes(first, zone) * 60000;
  return new Date(second);
}

/** Intl formatting in a zone — drop-in for toLocaleString / toLocaleTimeString / toLocaleDateString. */
export function formatInZone(
  value: DateInput,
  options: Intl.DateTimeFormatOptions = {},
  zone?: string | null,
  locale?: string | string[]
): string {
  const d = toDate(value);
  if (!d) return '';
  return d.toLocaleString(locale, { ...options, timeZone: resolveZone(zone) });
}

export function formatTimeInZone(
  value: DateInput,
  options: Intl.DateTimeFormatOptions = {},
  zone?: string | null,
  locale?: string | string[]
): string {
  const d = toDate(value);
  if (!d) return '';
  return d.toLocaleTimeString(locale, { ...options, timeZone: resolveZone(zone) });
}

export function formatDateInZone(
  value: DateInput,
  options: Intl.DateTimeFormatOptions = {},
  zone?: string | null,
  locale?: string | string[]
): string {
  const d = toDate(value);
  if (!d) return '';
  return d.toLocaleDateString(locale, { ...options, timeZone: resolveZone(zone) });
}

const pad = (n: number) => String(n).padStart(2, '0');

/** `<input type="datetime-local">` value ('YYYY-MM-DDTHH:mm') for an instant, in the zone. */
export function toZonedDateTimeLocal(value: DateInput, zone?: string | null): string {
  const p = zonedParts(value, zone);
  if (!p) return '';
  return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hours)}:${pad(p.minutes)}`;
}

/** `<input type="date">` value ('YYYY-MM-DD') for an instant, in the zone. */
export function toZonedDateInput(value: DateInput, zone?: string | null): string {
  const p = zonedParts(value, zone);
  return p ? `${p.year}-${pad(p.month)}-${pad(p.day)}` : '';
}

/** Instant for a 'YYYY-MM-DD' or 'YYYY-MM-DDTHH:mm[:ss]' picker value interpreted in the zone. */
export function fromZonedDateTimeLocal(local: string | null | undefined, zone?: string | null): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?$/.exec((local ?? '').trim());
  if (!m) return null;
  return zonedWallTimeToUtc(+m[1], +m[2], +m[3], +(m[4] ?? 0), +(m[5] ?? 0), +(m[6] ?? 0), 0, zone);
}

/** Start of the zone's calendar day containing `value` (local midnight, as an instant). */
export function zonedStartOfDay(value: DateInput, zone?: string | null): Date {
  const p = zonedParts(value ?? new Date(), zone)!;
  return zonedWallTimeToUtc(p.year, p.month, p.day, 0, 0, 0, 0, zone);
}

/** Last millisecond of the zone's calendar day containing `value`. */
export function zonedEndOfDay(value: DateInput, zone?: string | null): Date {
  const p = zonedParts(value ?? new Date(), zone)!;
  return new Date(zonedWallTimeToUtc(p.year, p.month, p.day + 1, 0, 0, 0, 0, zone).getTime() - 1);
}

/** Same wall-clock time `days` later in the zone. */
export function zonedAddDays(value: DateInput, days: number, zone?: string | null): Date {
  const p = zonedParts(value, zone)!;
  const d = toDate(value)!;
  return zonedWallTimeToUtc(p.year, p.month, p.day + days, p.hours, p.minutes, p.seconds, d.getUTCMilliseconds(), zone);
}
