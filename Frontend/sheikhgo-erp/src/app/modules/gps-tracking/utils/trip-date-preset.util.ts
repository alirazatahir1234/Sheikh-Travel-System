import {
  formatInZone,
  fromZonedDateTimeLocal,
  toDate,
  toZonedDateTimeLocal,
  zonedAddDays,
  zonedEndOfDay,
  zonedParts,
  zonedStartOfDay,
  zonedWallTimeToUtc
} from '../../../core/utils/zoned-time.util';

export type TripDatePreset =
  | 'today'
  | 'yesterday'
  | 'last24Hours'
  | 'thisWeek'
  | 'last7Days'
  | 'last30Days'
  | 'lastWeek'
  | 'thisMonth'
  | 'previousMonth'
  | 'thisYear'
  | 'custom';

/** Matches backend TripVehicleQueryValidation.MaxRange (30 days inclusive). */
export const TRIP_DATE_MAX_RANGE_MS = 30 * 24 * 60 * 60 * 1000;

/** Presets for Trip Analytics and Live Map history playback. */
export const TRIP_DATE_PRESETS: { id: TripDatePreset; label: string }[] = [
  { id: 'today', label: 'Today' },
  { id: 'yesterday', label: 'Yesterday' },
  { id: 'last24Hours', label: 'Last 24h' },
  { id: 'thisWeek', label: 'This Week' },
  { id: 'last7Days', label: 'Last 7 Days' },
  { id: 'last30Days', label: 'Last 30 Days' },
  { id: 'lastWeek', label: 'Last Week' },
  { id: 'thisMonth', label: 'This Month' },
  { id: 'previousMonth', label: 'Last Month' },
  { id: 'thisYear', label: 'This Year' },
  { id: 'custom', label: 'Custom' }
];

/** Compact chip set for the Live Map history toolbar (avoids overcrowding). */
export const HISTORY_DATE_PRESETS: { id: TripDatePreset; label: string }[] = [
  { id: 'today', label: 'Today' },
  { id: 'yesterday', label: 'Yesterday' },
  { id: 'last24Hours', label: 'Last 24h' },
  { id: 'last7Days', label: 'Last 7 Days' },
  { id: 'last30Days', label: 'Last 30 Days' },
  { id: 'thisMonth', label: 'This Month' },
  { id: 'previousMonth', label: 'Last Month' },
  { id: 'thisYear', label: 'This Year' },
  { id: 'custom', label: 'Custom' }
];

/**
 * Management-focused presets for Fleet Analytics.
 * Omits noisy short windows and "This Year" (silently clamped to 30 days and misleading).
 */
export const ANALYTICS_DATE_PRESETS: { id: TripDatePreset; label: string }[] = [
  { id: 'today', label: 'Today' },
  { id: 'yesterday', label: 'Yesterday' },
  { id: 'last7Days', label: 'Last 7 Days' },
  { id: 'last30Days', label: 'Last 30 Days' },
  { id: 'thisMonth', label: 'This Month' },
  { id: 'previousMonth', label: 'Last Month' },
  { id: 'custom', label: 'Custom' }
];

/**
 * Picker strings ('YYYY-MM-DDTHH:mm') are wall-clock time in the company timezone, not the browser's.
 * ISO instants (with Z / offset) pass through unchanged.
 */
export function parseDatetimeLocalInput(value: string): Date {
  return fromZonedDateTimeLocal(value) ?? toDate(value) ?? new Date(NaN);
}

/** Validate a custom analytics date range against the trip-backed 30-day API limit. */
export function validateAnalyticsDateRange(from: string, to: string): string | null {
  if (!from || !to) return 'Select both From and To dates.';
  const f = parseDatetimeLocalInput(from);
  const t = parseDatetimeLocalInput(to);
  if (Number.isNaN(f.getTime()) || Number.isNaN(t.getTime())) return 'Invalid date values.';
  if (t.getTime() < f.getTime()) return 'End date must be on or after the start date.';
  if (t.getTime() - f.getTime() > TRIP_DATE_MAX_RANGE_MS) {
    return 'Date range cannot exceed 30 days for trip-based analytics.';
  }
  return null;
}

/** `datetime-local` value for an instant, in the company timezone. */
export function toDatetimeLocalInput(d: Date): string {
  return toZonedDateTimeLocal(d);
}

export function formatPeriodLabel(from: string, to: string): string {
  if (!from || !to) return '—';
  const fmt = (raw: string) =>
    formatInZone(parseDatetimeLocalInput(raw), { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  return `${fmt(from)} – ${fmt(to)}`;
}

// Day / week / month boundaries in the company timezone (see zoned-time.util).
const startOfDay = (d: Date): Date => zonedStartOfDay(d);
const endOfDay = (d: Date): Date => zonedEndOfDay(d);

function startOfWeek(d: Date): Date {
  const p = zonedParts(d)!;
  const day = new Date(Date.UTC(p.year, p.month - 1, p.day)).getUTCDay();
  const diff = day === 0 ? -6 : 1 - day;
  return startOfDay(zonedAddDays(startOfDay(d), diff));
}

/** Clamp so (to - from) never exceeds the backend 30-day MaxRange. */
export function clampTripDateRange(from: Date, to: Date, maxMs = TRIP_DATE_MAX_RANGE_MS): { from: Date; to: Date } {
  if (to.getTime() < from.getTime()) {
    return { from: to, to: from };
  }
  if (to.getTime() - from.getTime() <= maxMs) {
    return { from, to };
  }
  return { from: new Date(to.getTime() - maxMs), to };
}

export function applyTripDatePreset(preset: TripDatePreset, now = new Date()): { from: Date; to: Date } {
  const today = startOfDay(now);
  const { year, month } = zonedParts(now)!;
  let range: { from: Date; to: Date };
  switch (preset) {
    case 'today':
      range = { from: today, to: endOfDay(now) };
      break;
    case 'yesterday': {
      const y = zonedAddDays(today, -1);
      range = { from: y, to: endOfDay(y) };
      break;
    }
    case 'last24Hours':
      range = { from: new Date(now.getTime() - 24 * 60 * 60 * 1000), to: now };
      break;
    case 'thisWeek':
      range = { from: startOfWeek(now), to: endOfDay(now) };
      break;
    case 'last7Days':
      // Fully rolling: a rolling start with an end-of-day end would run into the future.
      range = { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: now };
      break;
    case 'last30Days':
      // Inclusive 30-day window ending now — avoid now−30d → endOfDay(now) which exceeds MaxRange.
      range = { from: new Date(now.getTime() - TRIP_DATE_MAX_RANGE_MS), to: now };
      break;
    case 'lastWeek': {
      const end = zonedAddDays(startOfWeek(now), -1);
      const start = startOfWeek(end);
      range = { from: start, to: endOfDay(end) };
      break;
    }
    case 'thisMonth':
      range = { from: zonedWallTimeToUtc(year, month, 1), to: endOfDay(now) };
      break;
    case 'previousMonth': {
      const start = zonedWallTimeToUtc(year, month - 1, 1);
      const end = new Date(zonedWallTimeToUtc(year, month, 1).getTime() - 1);
      range = { from: start, to: end };
      break;
    }
    case 'thisYear':
      range = { from: zonedWallTimeToUtc(year, 1, 1), to: endOfDay(now) };
      break;
    default:
      range = { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: now };
      break;
  }
  return clampTripDateRange(range.from, range.to);
}
