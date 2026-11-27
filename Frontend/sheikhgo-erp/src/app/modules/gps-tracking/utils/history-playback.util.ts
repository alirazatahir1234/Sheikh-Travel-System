import { HistoryTimeline, HistoryTimelineSegment, TripReplayPosition } from '../../../core/models/gps-tracking.model';
import { toDate, zonedAddDays, zonedStartOfDay } from '../../../core/utils/zoned-time.util';
import {
  clockParts,
  formatTimeRange,
  segmentAt,
  segmentEndMs,
  segmentStartMs,
  standstillWhere,
  tripTitle
} from './history-timeline.util';

/**
 * Time ↔ position maths for the History workspace playback dock and the vehicle marker.
 * Pure functions; the page owns the playhead.
 */

export interface TimeRange {
  startMs: number;
  endMs: number;
}

export type DockZoom = 'trip' | 'day' | 'week';

/** Multiples of real time: at 1× one second of history plays per second, at 60× one minute. */
export const PLAYBACK_RATES: readonly number[] = [1, 10, 30, 60, 120, 300];

export const DEFAULT_PLAYBACK_RATE = 60;

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;

/**
 * Where the playhead is after `elapsedMs` of real time at `rate`, never past `endMs`.
 * With `skipStops`, an instant inside a stop or parking moves on to its end, where the next trip
 * starts, so a range plays its driving without waiting through the standstills.
 */
export function advancePlayhead(
  ms: number,
  elapsedMs: number,
  rate: number,
  timeline: HistoryTimeline | null,
  skipStops: boolean,
  endMs: number
): { ms: number; finished: boolean } {
  let next = ms + Math.max(0, elapsedMs) * rate;
  if (skipStops && timeline) {
    for (let guard = 0; guard < timeline.segments.length && next < endMs; guard++) {
      const here = segmentAt(timeline, next);
      if (!here || here.kind === 'trip') break;
      const end = segmentEndMs(here);
      if (!(end > next)) break;
      next = end;
    }
  }
  return next >= endMs ? { ms: endMs, finished: true } : { ms: next, finished: false };
}

/** The first trip that starts after an instant; null after the last trip. */
export function tripAfter(timeline: HistoryTimeline, afterMs: number): HistoryTimelineSegment | null {
  return timeline.segments.find(s => s.kind === 'trip' && segmentStartMs(s) > afterMs) ?? null;
}

export function timesOf(points: TripReplayPosition[]): number[] {
  return points.map(p => toDate(p.timestamp)?.getTime() ?? NaN);
}

/** Index of the last time at or before `ms`; -1 when `ms` is before the first. Times ascend. */
export function indexAtOrBefore(times: number[], ms: number): number {
  let lo = 0;
  let hi = times.length - 1;
  let found = -1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (times[mid] <= ms) {
      found = mid;
      lo = mid + 1;
    } else {
      hi = mid - 1;
    }
  }
  return found;
}

export function bearingDegrees(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const rad = Math.PI / 180;
  const y = Math.sin((lng2 - lng1) * rad) * Math.cos(lat2 * rad);
  const x =
    Math.cos(lat1 * rad) * Math.sin(lat2 * rad) -
    Math.sin(lat1 * rad) * Math.cos(lat2 * rad) * Math.cos((lng2 - lng1) * rad);
  return (Math.atan2(y, x) / rad + 360) % 360;
}

export interface PlayheadPosition {
  latitude: number;
  longitude: number;
  /** Direction of travel; null when the vehicle is not moving between the two fixes. */
  heading: number | null;
  speedKmh: number;
  /** The fix at or before the playhead — the one the details panel describes. */
  fixIndex: number;
}

/**
 * Where the vehicle is at an instant: on the straight line between the two fixes around it.
 * Before the first fix it is at the first fix, after the last at the last.
 */
export function positionAt(
  points: TripReplayPosition[],
  times: number[],
  ms: number
): PlayheadPosition | null {
  if (!points.length) return null;
  const i = indexAtOrBefore(times, ms);
  if (i < 0) return fixPosition(points[0], 0);
  if (i >= points.length - 1) return fixPosition(points[points.length - 1], points.length - 1);

  const a = points[i];
  const b = points[i + 1];
  const span = times[i + 1] - times[i];
  const f = span > 0 ? Math.min(1, Math.max(0, (ms - times[i]) / span)) : 0;
  const moved = Math.abs(b.latitude - a.latitude) > 1e-6 || Math.abs(b.longitude - a.longitude) > 1e-6;
  const speedA = Number(a.speedKmh) || 0;
  const speedB = Number(b.speedKmh) || 0;
  return {
    latitude: a.latitude + (b.latitude - a.latitude) * f,
    longitude: a.longitude + (b.longitude - a.longitude) * f,
    heading: moved ? bearingDegrees(a.latitude, a.longitude, b.latitude, b.longitude) : a.heading ?? null,
    speedKmh: speedA + (speedB - speedA) * f,
    fixIndex: i
  };
}

function fixPosition(p: TripReplayPosition, index: number): PlayheadPosition {
  return {
    latitude: p.latitude,
    longitude: p.longitude,
    heading: p.heading ?? null,
    speedKmh: Number(p.speedKmh) || 0,
    fixIndex: index
  };
}

// ---- dock scale -----------------------------------------------------------------------------------

export function timeToPercent(ms: number, range: TimeRange): number {
  const span = range.endMs - range.startMs;
  if (!(span > 0)) return 0;
  return Math.min(100, Math.max(0, ((ms - range.startMs) / span) * 100));
}

export function percentToTime(percent: number, range: TimeRange): number {
  const p = Math.min(100, Math.max(0, percent));
  return range.startMs + ((range.endMs - range.startMs) * p) / 100;
}

/** The vehicle's local calendar day around an instant: midnight to midnight. */
export function localDayRange(ms: number, zone?: string | null): TimeRange {
  const start = zonedStartOfDay(ms, zone);
  return { startMs: start.getTime(), endMs: zonedAddDays(start, 1, zone).getTime() };
}

/** "Week" needs more than a day and a half of history to be worth a button. */
export function weekZoomAvailable(window: TimeRange): boolean {
  return window.endMs - window.startMs > 1.5 * DAY;
}

/**
 * What the dock shows.
 * - trip: the selected trip or standstill (the day when nothing is selected);
 * - day: the local day of the playhead;
 * - week: the whole period in whole local days, or the seven days around the playhead when the
 *   period is longer than a week.
 */
export function dockRange(
  zoom: DockZoom,
  playheadMs: number,
  window: TimeRange,
  selected: TimeRange | null,
  zone?: string | null
): TimeRange {
  const day = localDayRange(playheadMs, zone);
  if (zoom === 'trip') {
    return selected && selected.endMs > selected.startMs ? selected : day;
  }
  if (zoom === 'day' || !weekZoomAvailable(window)) return day;

  const first = localDayRange(window.startMs, zone).startMs;
  const last = localDayRange(Math.max(window.startMs, window.endMs - 1), zone).endMs;
  if (last - first <= 8 * DAY) return { startMs: first, endMs: last };

  let start = zonedAddDays(day.startMs, -3, zone).getTime();
  let end = zonedAddDays(start, 7, zone).getTime();
  if (start < first) {
    start = first;
    end = zonedAddDays(start, 7, zone).getTime();
  } else if (end > last) {
    end = last;
    start = zonedAddDays(end, -7, zone).getTime();
  }
  return { startMs: start, endMs: end };
}

export interface AxisTick {
  percent: number;
  label: string;
}

/** Evenly spaced labels: clock times within a day, days beyond it. */
export function axisTicks(range: TimeRange, zone?: string | null, count = 5): AxisTick[] {
  const span = range.endMs - range.startMs;
  if (!(span > 0) || count < 2) return [];
  const ticks: AxisTick[] = [];
  for (let i = 0; i < count; i++) {
    const percent = (i / (count - 1)) * 100;
    const p = clockParts(percentToTime(percent, range), zone);
    if (!p) continue;
    let label: string;
    if (span > 2 * DAY) label = `${p.weekday} ${p.day}`;
    else if (span > 6 * HOUR && p.minute === '00') label = `${p.hour} ${p.dayPeriod}`;
    else label = `${p.hour}:${p.minute} ${p.dayPeriod}`;
    ticks.push({ percent, label });
  }
  return ticks;
}

// ---- activity bar -----------------------------------------------------------------------------------

export interface ActivityBlock {
  id: string;
  kind: 'trip' | 'stop' | 'parking';
  leftPercent: number;
  widthPercent: number;
  label: string;
}

/** The segments that overlap the dock's range, clipped to it, as percentages of its width. */
export function activityBlocks(
  segments: HistoryTimelineSegment[],
  range: TimeRange,
  zone?: string | null
): ActivityBlock[] {
  const blocks: ActivityBlock[] = [];
  for (const s of segments) {
    const start = Math.max(segmentStartMs(s), range.startMs);
    const end = Math.min(segmentEndMs(s), range.endMs);
    if (!(end > start)) continue;
    const left = timeToPercent(start, range);
    const when = formatTimeRange(s.startTime, s.endTime, zone);
    blocks.push({
      id: s.id,
      kind: s.kind,
      leftPercent: left,
      widthPercent: timeToPercent(end, range) - left,
      label:
        s.kind === 'trip'
          ? `Trip ${tripTitle(s).replace(' → ', ' to ')}, ${when}`
          : `${s.kind === 'parking' ? 'Parked' : 'Stop'} at ${standstillWhere(s)}, ${when}`
    });
  }
  return blocks;
}

/** Parts of the dock's range that lie outside the loaded period (nothing is known there). */
export function outsideBlocks(range: TimeRange, window: TimeRange): { leftPercent: number; widthPercent: number }[] {
  const out: { leftPercent: number; widthPercent: number }[] = [];
  if (window.startMs > range.startMs) {
    out.push({ leftPercent: 0, widthPercent: timeToPercent(Math.min(window.startMs, range.endMs), range) });
  }
  if (window.endMs < range.endMs) {
    const left = timeToPercent(Math.max(window.endMs, range.startMs), range);
    out.push({ leftPercent: left, widthPercent: 100 - left });
  }
  return out.filter(b => b.widthPercent > 0);
}

// ---- speed chart ------------------------------------------------------------------------------------

export const SPEED_CHART_WIDTH = 1440;
export const SPEED_CHART_HEIGHT = 56;
const SPEED_BASELINE = 54;
const SPEED_RISE = 50;
const MAX_POINTS_PER_TRIP = 240;

export interface SpeedPaths {
  /** Outline of the speed curve. */
  line: string;
  /** The same shape closed down to the baseline, for the fill. */
  area: string;
}

/**
 * Speed over the dock's range as SVG paths in a 1440 × 56 box: one closed shape per trip, rising
 * from and returning to zero at its ends. Standstills stay flat on the baseline.
 */
export function speedPaths(
  points: TripReplayPosition[],
  times: number[],
  trips: HistoryTimelineSegment[],
  range: TimeRange,
  maxSpeedKmh: number
): SpeedPaths {
  const scale = maxSpeedKmh > 0 ? maxSpeedKmh : 1;
  const x = (ms: number) => ((timeToPercent(ms, range) / 100) * SPEED_CHART_WIDTH).toFixed(1);
  const y = (speed: number) => (SPEED_BASELINE - (Math.min(scale, Math.max(0, speed)) / scale) * SPEED_RISE).toFixed(1);

  let line = '';
  let area = '';
  for (const trip of trips) {
    if (trip.kind !== 'trip') continue;
    const start = Math.max(segmentStartMs(trip), range.startMs);
    const end = Math.min(segmentEndMs(trip), range.endMs);
    if (!(end > start)) continue;

    let from = indexAtOrBefore(times, start);
    if (from < 0 || times[from] < start) from++;
    const to = indexAtOrBefore(times, end);
    const stride = Math.max(1, Math.ceil((to - from + 1) / MAX_POINTS_PER_TRIP));

    let shape = `M${x(start)} ${SPEED_BASELINE}`;
    for (let i = from; i <= to; i += stride) {
      // Keep the peak of every stride so a thinned curve still shows the top speed.
      let peak = 0;
      for (let k = i; k < Math.min(i + stride, to + 1); k++) {
        peak = Math.max(peak, Number(points[k].speedKmh) || 0);
      }
      shape += `L${x(times[i])} ${y(peak)}`;
    }
    shape += `L${x(end)} ${SPEED_BASELINE}`;
    line += shape;
    area += `${shape}Z`;
  }
  return { line, area };
}
