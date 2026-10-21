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

export function toDatetimeLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function formatPeriodLabel(from: string, to: string): string {
  if (!from || !to) return '—';
  const f = new Date(from);
  const t = new Date(to);
  const fmt = (d: Date) =>
    d.toLocaleString(undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  return `${fmt(f)} – ${fmt(t)}`;
}

function startOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
}

function endOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
}

function startOfWeek(d: Date): Date {
  const day = d.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  const monday = new Date(d);
  monday.setDate(d.getDate() + diff);
  return startOfDay(monday);
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
  let range: { from: Date; to: Date };
  switch (preset) {
    case 'today':
      range = { from: today, to: endOfDay(now) };
      break;
    case 'yesterday': {
      const y = new Date(today);
      y.setDate(y.getDate() - 1);
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
      range = { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: endOfDay(now) };
      break;
    case 'last30Days':
      // Inclusive 30-day window ending now — avoid now−30d → endOfDay(now) which exceeds MaxRange.
      range = { from: new Date(now.getTime() - TRIP_DATE_MAX_RANGE_MS), to: now };
      break;
    case 'lastWeek': {
      const end = new Date(startOfWeek(now));
      end.setDate(end.getDate() - 1);
      const start = startOfWeek(end);
      range = { from: start, to: endOfDay(end) };
      break;
    }
    case 'thisMonth':
      range = { from: new Date(now.getFullYear(), now.getMonth(), 1), to: endOfDay(now) };
      break;
    case 'previousMonth': {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      const end = new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59, 999);
      range = { from: start, to: end };
      break;
    }
    case 'thisYear':
      range = { from: new Date(now.getFullYear(), 0, 1), to: endOfDay(now) };
      break;
    default:
      range = { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: now };
      break;
  }
  return clampTripDateRange(range.from, range.to);
}
