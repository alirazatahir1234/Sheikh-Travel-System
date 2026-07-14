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

export function applyTripDatePreset(preset: TripDatePreset, now = new Date()): { from: Date; to: Date } {
  const today = startOfDay(now);
  switch (preset) {
    case 'today':
      return { from: today, to: endOfDay(now) };
    case 'yesterday': {
      const y = new Date(today);
      y.setDate(y.getDate() - 1);
      return { from: y, to: endOfDay(y) };
    }
    case 'last24Hours':
      return { from: new Date(now.getTime() - 24 * 60 * 60 * 1000), to: now };
    case 'thisWeek':
      return { from: startOfWeek(now), to: endOfDay(now) };
    case 'last7Days':
      return { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: endOfDay(now) };
    case 'last30Days':
      return { from: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000), to: endOfDay(now) };
    case 'lastWeek': {
      const end = new Date(startOfWeek(now));
      end.setDate(end.getDate() - 1);
      const start = startOfWeek(end);
      return { from: start, to: endOfDay(end) };
    }
    case 'thisMonth':
      return { from: new Date(now.getFullYear(), now.getMonth(), 1), to: endOfDay(now) };
    case 'previousMonth': {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      const end = new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59, 999);
      return { from: start, to: end };
    }
    case 'thisYear':
      return { from: new Date(now.getFullYear(), 0, 1), to: endOfDay(now) };
    default:
      return { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to: now };
  }
}
