import {
  HistoryTimeline,
  HistoryTimelineDay,
  HistoryTimelineSegment,
  HistoryTimelineTotals,
  TripReplayPosition
} from '../../../core/models/gps-tracking.model';
import { DateInput, resolveZone, toDate, zonedParts } from '../../../core/utils/zoned-time.util';
import { looksLikeRoadSegment, sanitizeFleetAddress } from './gps-address.util';

/**
 * View helpers for the History workspace timeline.
 *
 * Every figure shown comes from the API's `timeline` (one calculation). These helpers only format,
 * filter and lay out what the server sent — they never add distances or durations up into a KPI.
 */

// ---- formatting -------------------------------------------------------------------------------

const formatters = new Map<string, Intl.DateTimeFormat>();

function namesFormatter(zone: string): Intl.DateTimeFormat {
  let f = formatters.get(zone);
  if (!f) {
    f = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      second: '2-digit',
      hour12: true
    });
    formatters.set(zone, f);
  }
  return f;
}

export interface ClockParts {
  weekday: string;
  month: string;
  day: string;
  year: string;
  hour: string;
  minute: string;
  second: string;
  dayPeriod: string;
  /** Local calendar day, yyyy-MM-dd. */
  dateKey: string;
}

/** Wall-clock pieces of an instant in the vehicle's zone, in the fixed "Fri 20 Nov · 7:48 AM" style. */
export function clockParts(value: DateInput, zone?: string | null): ClockParts | null {
  const date = toDate(value);
  const numeric = zonedParts(date, zone);
  if (!date || !numeric) return null;
  const parts = namesFormatter(resolveZone(zone)).formatToParts(date);
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === type)?.value ?? '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return {
    weekday: get('weekday'),
    month: get('month'),
    day: get('day'),
    year: get('year'),
    hour: get('hour'),
    minute: get('minute'),
    second: get('second'),
    dayPeriod: get('dayPeriod').toUpperCase(),
    dateKey: `${numeric.year}-${pad(numeric.month)}-${pad(numeric.day)}`
  };
}

/** "7:48 AM", or "10:42:16 AM" with seconds. */
export function formatClock(value: DateInput, zone?: string | null, withSeconds = false): string {
  const p = clockParts(value, zone);
  if (!p) return '—';
  return `${p.hour}:${p.minute}${withSeconds ? `:${p.second}` : ''} ${p.dayPeriod}`;
}

/** "Fri 20 Nov", or "Fri 20 Nov 2026". */
export function formatDay(value: DateInput, zone?: string | null, withYear = false): string {
  const p = clockParts(value, zone);
  if (!p) return '—';
  return `${p.weekday} ${p.day} ${p.month}${withYear ? ` ${p.year}` : ''}`;
}

/** Label for a local day key (yyyy-MM-dd) — a calendar date, never shifted by a timezone. */
export function formatDayKey(dateKey: string, withYear = false): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateKey.trim());
  if (!m) return dateKey;
  return formatDay(new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], 12)), 'UTC', withYear);
}

/** Abbreviations people use for zones whose Intl short name is only an offset ("GMT+5"). */
const ZONE_ABBREVIATIONS: Record<string, string> = {
  'Asia/Karachi': 'PKT',
  'Asia/Dubai': 'GST',
  'Asia/Riyadh': 'AST',
  'Asia/Qatar': 'AST',
  'Asia/Kolkata': 'IST',
  'Asia/Dhaka': 'BST',
  'Asia/Kabul': 'AFT'
};

/** Short name of a zone for the toolbar badge: "PKT", else what the browser calls it ("GMT+3"). */
export function zoneShortLabel(zone?: string | null, at: DateInput = new Date()): string {
  const resolved = resolveZone(zone);
  if (ZONE_ABBREVIATIONS[resolved]) return ZONE_ABBREVIATIONS[resolved];
  try {
    const part = new Intl.DateTimeFormat('en-US', { timeZone: resolved, timeZoneName: 'short' })
      .formatToParts(toDate(at) ?? new Date())
      .find(p => p.type === 'timeZoneName');
    return part?.value ?? resolved;
  } catch {
    return resolved;
  }
}

/** "19 Nov, 6:44 PM" — the toolbar's range button. */
export function formatRangeEdge(value: DateInput, zone?: string | null): string {
  const p = clockParts(value, zone);
  if (!p) return '—';
  return `${p.day} ${p.month}, ${p.hour}:${p.minute} ${p.dayPeriod}`;
}

/**
 * "7:48 – 8:31 AM" · "11:19 AM – 4:05 PM" · "6:23 PM – Sat 8:10 AM".
 * The end carries its weekday when it falls on another day, and its date after a week.
 */
export function formatTimeRange(start: DateInput, end: DateInput, zone?: string | null): string {
  const s = clockParts(start, zone);
  const e = clockParts(end, zone);
  if (!s || !e) return '—';
  const from = `${s.hour}:${s.minute}`;
  const to = `${e.hour}:${e.minute}`;
  if (s.dateKey === e.dateKey) {
    return s.dayPeriod === e.dayPeriod
      ? `${from} – ${to} ${e.dayPeriod}`
      : `${from} ${s.dayPeriod} – ${to} ${e.dayPeriod}`;
  }
  const startMs = toDate(start)?.getTime() ?? 0;
  const endMs = toDate(end)?.getTime() ?? 0;
  const endDay = endMs - startMs >= 6 * 24 * 60 * 60 * 1000 ? `${e.weekday} ${e.day} ${e.month}` : e.weekday;
  return `${from} ${s.dayPeriod} – ${endDay} ${to} ${e.dayPeriod}`;
}

export type DurationStyle = 'row' | 'compact' | 'hours';

/**
 * "43 min" · "1h 7m" · "4h" · "5d 22h 10m".
 * - compact: minutes as "52m", for the narrow column of a day row;
 * - hours: never rolls over into days ("25h 50m"), for driving time.
 */
export function formatDuration(totalMinutes: number, style: DurationStyle = 'row'): string {
  const minutes = Math.max(0, Math.round(Number(totalMinutes) || 0));
  if (minutes < 60) return style === 'compact' ? `${minutes}m` : `${minutes} min`;
  const days = style === 'hours' ? 0 : Math.floor(minutes / 1440);
  const hours = Math.floor((minutes - days * 1440) / 60);
  const mins = minutes % 60;
  const parts: string[] = [];
  if (days) parts.push(`${days}d`);
  if (hours) parts.push(`${hours}h`);
  if (mins) parts.push(`${mins}m`);
  return parts.join(' ');
}

/** Short name of a place for a trip's "From → To": the town after a street, else the first part. */
export function placeLabel(address?: string | null): string | null {
  const clean = sanitizeFleetAddress(address);
  if (!clean) return null;
  const parts = clean.split(',').map(p => p.trim()).filter(Boolean);
  if (!parts.length) return null;
  if (parts.length >= 2 && looksLikeRoadSegment(parts[0]) && !looksLikeRoadSegment(parts[1])) {
    return parts[1];
  }
  return parts[0];
}

/** Where a standstill is: the first two parts of the address ("Circular Road, Sialkot"). */
export function standstillPlace(address?: string | null): string | null {
  const clean = sanitizeFleetAddress(address);
  if (!clean) return null;
  const parts = clean.split(',').map(p => p.trim()).filter(Boolean);
  if (!parts.length) return null;
  return parts.slice(0, 2).join(', ');
}

function coordinateLabel(latitude: number, longitude: number): string {
  return `${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
}

export function tripTitle(segment: HistoryTimelineSegment): string {
  const from = placeLabel(segment.startAddress) ?? coordinateLabel(segment.startLatitude, segment.startLongitude);
  const to = placeLabel(segment.endAddress) ?? coordinateLabel(segment.endLatitude, segment.endLongitude);
  return `${from} → ${to}`;
}

/** "Stopped 52 min"; one still going on is "Stopped 52 min so far". */
export function standstillTitle(segment: HistoryTimelineSegment): string {
  const title = `${segment.kind === 'parking' ? 'Parked' : 'Stopped'} ${formatDuration(segment.durationMinutes)}`;
  return segment.ongoing ? `${title} so far` : title;
}

/**
 * A standstill still going on reads "– now" only while the data is this fresh: the ERP's Live
 * window (Live under 5 minutes, then Stale). After that it reads "since …" until a reload.
 */
export const ONGOING_FRESH_MS = 5 * 60 * 1000;

/**
 * When a standstill was: "1:27 – 2:20 PM". One still going on reads "1:27 PM – now" while the page
 * can vouch for it (fresh data, tracker live), else "since 1:27 PM".
 */
export function standstillWhen(segment: HistoryTimelineSegment, zone?: string | null, live = false): string {
  if (!segment.ongoing) return formatTimeRange(segment.startTime, segment.endTime, zone);
  const start = formatClock(segment.startTime, zone);
  return live ? `${start} – now` : `since ${start}`;
}

export function standstillWhere(segment: HistoryTimelineSegment): string {
  return standstillPlace(segment.startAddress) ?? coordinateLabel(segment.startLatitude, segment.startLongitude);
}

// ---- KPI strip ----------------------------------------------------------------------------------

export interface HistoryKpi {
  key: 'distance' | 'driving' | 'stopped' | 'stops' | 'parking' | 'maxSpeed' | 'avgSpeed';
  label: string;
  value: string;
  unit: string;
}

/** Share of the period spent driving, in whole percent. Stopped is the rest, so the two total 100. */
export function drivingPercent(totals: Pick<HistoryTimelineTotals, 'drivingMinutes' | 'windowMinutes'>): number | null {
  if (!totals.windowMinutes || totals.windowMinutes <= 0) return null;
  return Math.min(100, Math.max(0, Math.round((totals.drivingMinutes / totals.windowMinutes) * 100)));
}

/** The seven cells of the strip, straight from the server's totals. No totals → dashes. */
export function buildKpis(totals: HistoryTimelineTotals | null | undefined): HistoryKpi[] {
  if (!totals) {
    return [
      { key: 'distance', label: 'Distance', value: '—', unit: '' },
      { key: 'driving', label: 'Driving', value: '—', unit: '' },
      { key: 'stopped', label: 'Stopped', value: '—', unit: '' },
      { key: 'stops', label: 'Stops', value: '—', unit: '' },
      { key: 'parking', label: 'Parking', value: '—', unit: '' },
      { key: 'maxSpeed', label: 'Max speed', value: '—', unit: '' },
      { key: 'avgSpeed', label: 'Avg speed', value: '—', unit: '' }
    ];
  }

  const driving = drivingPercent(totals);
  const stopped = driving == null ? null : 100 - driving;
  return [
    { key: 'distance', label: 'Distance', value: Number(totals.distanceKm).toFixed(1), unit: 'km' },
    {
      key: 'driving',
      label: driving == null ? 'Driving' : `Driving · ${driving}%`,
      value: formatDuration(totals.drivingMinutes, 'hours'),
      unit: ''
    },
    {
      key: 'stopped',
      label: stopped == null ? 'Stopped' : `Stopped · ${stopped}%`,
      value: formatDuration(totals.stoppedMinutes),
      unit: ''
    },
    { key: 'stops', label: 'Stops', value: String(totals.stopCount), unit: '' },
    { key: 'parking', label: 'Parking', value: String(totals.parkingCount), unit: '' },
    { key: 'maxSpeed', label: 'Max speed', value: String(Math.round(Number(totals.maxSpeedKmh) || 0)), unit: 'km/h' },
    { key: 'avgSpeed', label: 'Avg speed', value: (Number(totals.avgSpeedKmh) || 0).toFixed(1), unit: 'km/h' }
  ];
}

// ---- rows ---------------------------------------------------------------------------------------

export type TimelineFilter = 'all' | 'trips' | 'stops';

/** Choices of "Hide stops shorter than". 0 lists everything, including pauses inside a trip. */
export const HIDE_STOP_OPTIONS: ReadonlyArray<{ minutes: number; label: string }> = [
  { minutes: 0, label: 'Show all' },
  { minutes: 5, label: '5 min' },
  { minutes: 10, label: '10 min' },
  { minutes: 15, label: '15 min' },
  { minutes: 30, label: '30 min' },
  { minutes: 60, label: '1 hour' },
  { minutes: 120, label: '2 hours' }
];

export const DEFAULT_HIDE_STOPS_MINUTES = 5;

export function hideStopsLabel(minutes: number): string {
  return HIDE_STOP_OPTIONS.find(o => o.minutes === minutes)?.label ?? `${minutes} min`;
}

export interface TimelineDayRow {
  type: 'day';
  key: string;
  date: string;
  label: string;
  km: string;
  duration: string;
  /** Distance as a share of the busiest day in the period. */
  barPercent: number;
  open: boolean;
  ariaLabel: string;
}

export interface TimelineSegmentRow {
  type: 'trip' | 'stop' | 'parking';
  key: string;
  id: string;
  date: string;
  title: string;
  subtitle: string;
  /** Standstills: the town, shown before the time range and shortened first. */
  where: string;
  /** Standstills: street and town, for the tooltip. */
  whereFull: string;
  /** The time range. */
  when: string;
  km: string;
  max: string;
  selected: boolean;
  /** Trips: minutes estimated because no fix arrived. */
  noSignalHint: string | null;
  /** Standstills: still going on, and the page can vouch for it now. Shows the "Ongoing" tag. */
  ongoing: boolean;
  ariaLabel: string;
}

/** A pause inside a trip, listed only with "Show all". Selecting it selects the trip at that time. */
export interface TimelineBriefRow {
  type: 'brief';
  key: string;
  tripId: string;
  atMs: number;
  title: string;
  subtitle: string;
}

export interface TimelineNoteRow {
  type: 'note';
  key: string;
  date: string;
  text: string;
}

export interface TimelineEmptyRow {
  type: 'empty';
  key: string;
  text: string;
}

export type TimelineRow =
  | TimelineDayRow
  | TimelineSegmentRow
  | TimelineBriefRow
  | TimelineNoteRow
  | TimelineEmptyRow;

export interface TimelineRowOptions {
  filter: TimelineFilter;
  hideStopsUnderMinutes: number;
  openDays: ReadonlySet<string>;
  selectedId: string | null;
  zone?: string | null;
  /** The period still reaches the present: fresh data and a live tracker. See {@link standstillWhen}. */
  ongoingLive?: boolean;
}

function isStandstill(segment: HistoryTimelineSegment): boolean {
  return segment.kind !== 'trip';
}

/** Standstills the list leaves out at this threshold, counting the pauses kept inside trips. */
export function hiddenStopCount(segments: HistoryTimelineSegment[], hideUnderMinutes: number): number {
  if (hideUnderMinutes <= 0) return 0;
  let hidden = 0;
  for (const s of segments) {
    if (isStandstill(s)) {
      if (s.durationMinutes < hideUnderMinutes) hidden++;
    } else {
      hidden += s.briefStops?.length ?? 0;
    }
  }
  return hidden;
}

/** The flat list the panel renders: a row per day and, under an open day, its segments. */
export function buildTimelineRows(timeline: HistoryTimeline, options: TimelineRowOptions): TimelineRow[] {
  const rows: TimelineRow[] = [];
  const busiest = Math.max(0, ...timeline.days.map(d => Number(d.distanceKm) || 0));
  const byDate = new Map<string, HistoryTimelineSegment[]>();
  for (const s of timeline.segments) {
    const list = byDate.get(s.date);
    if (list) list.push(s);
    else byDate.set(s.date, [s]);
  }

  const showTrips = options.filter !== 'stops';
  const showStops = options.filter !== 'trips';
  const threshold = Math.max(0, options.hideStopsUnderMinutes);

  for (const day of timeline.days) {
    const open = options.openDays.has(day.date);
    rows.push(dayRow(day, busiest, open));
    if (!open) continue;

    const segments = byDate.get(day.date) ?? [];
    let listed = 0;
    for (const s of segments) {
      if (s.kind === 'trip') {
        if (showTrips) {
          rows.push(tripRow(s, options));
          listed++;
        }
        if (showStops && threshold === 0) {
          for (const brief of s.briefStops ?? []) {
            rows.push(briefRow(s, brief, options.zone));
            listed++;
          }
        }
        continue;
      }
      if (showStops && s.durationMinutes >= threshold) {
        rows.push(standstillRow(s, options));
        listed++;
      }
    }

    const hidden = showStops ? hiddenStopCount(segments, threshold) : 0;
    if (hidden > 0) {
      rows.push({
        type: 'note',
        key: `note-${day.date}`,
        date: day.date,
        text: `${hidden} ${hidden === 1 ? 'stop' : 'stops'} under ${hideStopsLabel(threshold)} hidden`
      });
    } else if (listed === 0) {
      rows.push({
        type: 'empty',
        key: `empty-${day.date}`,
        text: segments.length ? 'Nothing to list with this filter' : 'No trips or new stops on this day'
      });
    }
  }

  return rows;
}

function dayRow(day: HistoryTimelineDay, busiest: number, open: boolean): TimelineDayRow {
  const km = Number(day.distanceKm) || 0;
  const label = formatDayKey(day.date);
  const duration = day.drivingMinutes > 0 ? formatDuration(day.drivingMinutes, 'compact') : '—';
  return {
    type: 'day',
    key: `day-${day.date}`,
    date: day.date,
    label,
    km: km.toFixed(1),
    duration,
    barPercent: busiest > 0 ? Math.round((km / busiest) * 100) : 0,
    open,
    ariaLabel: `${label}, ${km.toFixed(1)} km, ${day.drivingMinutes > 0 ? formatDuration(day.drivingMinutes) + ' driving' : 'no driving'}`
  };
}

function tripRow(s: HistoryTimelineSegment, options: TimelineRowOptions): TimelineSegmentRow {
  const title = tripTitle(s);
  const when = formatTimeRange(s.startTime, s.endTime, options.zone);
  const duration = formatDuration(s.durationMinutes);
  const km = (Number(s.distanceKm) || 0).toFixed(1);
  const max = String(Math.round(Number(s.maxSpeedKmh) || 0));
  const noSignalMs = (s.noSignal ?? []).reduce(
    (sum, n) => sum + Math.max(0, (toDate(n.endTime)?.getTime() ?? 0) - (toDate(n.startTime)?.getTime() ?? 0)),
    0
  );
  const noSignalHint = noSignalMs > 0
    ? `Includes ${formatDuration(Math.max(1, Math.round(noSignalMs / 60000)))} estimated without GPS signal`
    : null;
  return {
    type: 'trip',
    key: s.id,
    id: s.id,
    date: s.date,
    title,
    subtitle: `${when} · ${duration}`,
    where: '',
    whereFull: '',
    when,
    km,
    max,
    selected: options.selectedId === s.id,
    noSignalHint,
    ongoing: false,
    ariaLabel: `Trip ${title}, ${when}, ${duration}, ${km} km, max ${max} km/h`
  };
}

function standstillRow(s: HistoryTimelineSegment, options: TimelineRowOptions): TimelineSegmentRow {
  const title = standstillTitle(s);
  const whereFull = standstillWhere(s);
  const where = placeLabel(s.startAddress) ?? whereFull;
  const live = !!s.ongoing && !!options.ongoingLive;
  const when = standstillWhen(s, options.zone, live);
  const subtitle = `${where} · ${when}`;
  return {
    type: s.kind === 'parking' ? 'parking' : 'stop',
    key: s.id,
    id: s.id,
    date: s.date,
    title,
    subtitle,
    where,
    whereFull,
    when,
    km: '',
    max: '',
    selected: options.selectedId === s.id,
    noSignalHint: null,
    ongoing: live,
    ariaLabel: `${title}${live ? ', ongoing' : ''}, ${whereFull}, ${when}`
  };
}

function briefRow(
  trip: HistoryTimelineSegment,
  brief: { startTime: string; endTime: string; durationSeconds: number },
  zone?: string | null
): TimelineBriefRow {
  const atMs = toDate(brief.startTime)?.getTime() ?? 0;
  const minutes = Math.max(1, Math.round(brief.durationSeconds / 60));
  return {
    type: 'brief',
    key: `${trip.id}-brief-${atMs}`,
    tripId: trip.id,
    atMs,
    title: `Brief stop ${formatDuration(minutes)}`,
    subtitle: formatTimeRange(brief.startTime, brief.endTime, zone)
  };
}

/** The day to open first: the latest one with a trip, else the last day of the period. */
export function defaultOpenDay(timeline: HistoryTimeline): string | null {
  for (let i = timeline.days.length - 1; i >= 0; i--) {
    if (timeline.days[i].tripCount > 0) return timeline.days[i].date;
  }
  return timeline.days.length ? timeline.days[timeline.days.length - 1].date : null;
}

// ---- lookups ------------------------------------------------------------------------------------

export function segmentStartMs(segment: HistoryTimelineSegment): number {
  return toDate(segment.startTime)?.getTime() ?? 0;
}

export function segmentEndMs(segment: HistoryTimelineSegment): number {
  return toDate(segment.endTime)?.getTime() ?? 0;
}

/** The segment covering an instant; a boundary instant belongs to the segment that starts there. */
export function segmentAt(timeline: HistoryTimeline, ms: number): HistoryTimelineSegment | null {
  let found: HistoryTimelineSegment | null = null;
  for (const s of timeline.segments) {
    const start = segmentStartMs(s);
    if (start > ms) break;
    if (ms <= segmentEndMs(s)) found = s;
  }
  return found;
}

export function nextStandstill(timeline: HistoryTimeline, afterMs: number): HistoryTimelineSegment | null {
  return timeline.segments.find(s => isStandstill(s) && segmentStartMs(s) > afterMs) ?? null;
}

export function previousStandstill(timeline: HistoryTimeline, beforeMs: number): HistoryTimelineSegment | null {
  let found: HistoryTimelineSegment | null = null;
  for (const s of timeline.segments) {
    if (!isStandstill(s)) continue;
    if (segmentStartMs(s) >= beforeMs) break;
    found = s;
  }
  return found;
}

/** The trip to play from an instant: the one covering it, else the next one. */
export function tripAtOrAfter(timeline: HistoryTimeline, ms: number): HistoryTimelineSegment | null {
  return timeline.segments.find(s => s.kind === 'trip' && segmentEndMs(s) > ms) ?? null;
}

// ---- driven so far --------------------------------------------------------------------------------

function haversineKm(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const rad = Math.PI / 180;
  const dLat = (lat2 - lat1) * rad;
  const dLng = (lng2 - lng1) * rad;
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(lat1 * rad) * Math.cos(lat2 * rad) * Math.sin(dLng / 2) ** 2;
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

/**
 * Distance driven from the start of the period up to an instant: the server's distance of every
 * finished trip, plus the share of the trip in progress. The share follows the path already
 * covered (time when the path is too short to tell), so it reaches the trip's own figure at its end.
 */
export function drivenSoFarKm(
  timeline: HistoryTimeline,
  route: TripReplayPosition[],
  atMs: number
): number {
  let km = 0;
  for (const s of timeline.segments) {
    if (s.kind !== 'trip') continue;
    const start = segmentStartMs(s);
    const end = segmentEndMs(s);
    const distance = Number(s.distanceKm) || 0;
    if (atMs >= end) {
      km += distance;
      continue;
    }
    if (atMs <= start) break;
    km += distance * tripShare(route, start, end, atMs);
    break;
  }
  return km;
}

function tripShare(route: TripReplayPosition[], start: number, end: number, atMs: number): number {
  let covered = 0;
  let total = 0;
  let previous: TripReplayPosition | null = null;
  let previousMs = 0;
  for (const p of route) {
    const t = toDate(p.timestamp)?.getTime() ?? NaN;
    if (!(t >= start && t <= end)) continue;
    if (previous) {
      const step = haversineKm(previous.latitude, previous.longitude, p.latitude, p.longitude);
      total += step;
      if (t <= atMs) covered += step;
      else if (previousMs < atMs) covered += step * ((atMs - previousMs) / (t - previousMs));
    }
    previous = p;
    previousMs = t;
  }
  if (total > 0.02) return Math.min(1, Math.max(0, covered / total));
  return end > start ? Math.min(1, Math.max(0, (atMs - start) / (end - start))) : 0;
}
