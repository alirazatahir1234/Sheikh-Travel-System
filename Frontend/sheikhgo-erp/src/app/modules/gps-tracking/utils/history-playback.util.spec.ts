import { TripReplayPosition } from '../../../core/models/gps-tracking.model';
import {
  activityBlocks,
  advancePlayhead,
  axisTicks,
  bearingDegrees,
  DEFAULT_PLAYBACK_RATE,
  dockRange,
  eventKind,
  eventTicks,
  MAX_EVENT_TICKS,
  indexAtOrBefore,
  localDayRange,
  outsideBlocks,
  PLAYBACK_RATES,
  percentToTime,
  positionAt,
  speedPaths,
  timesOf,
  timeToPercent,
  tripAfter,
  weekZoomAvailable
} from './history-playback.util';
import { FIXTURE_ZONE, fixtureMs, fixtureRoute, fixtureTimeline } from './history-timeline.fixture';

const friday = { startMs: fixtureMs(1, 0), endMs: fixtureMs(2, 0) };
const period = { startMs: fixtureMs(0, 0), endMs: fixtureMs(2, 720) };

describe('history-playback.util — playhead', () => {
  it('offers true multiples of real time, 60× by default', () => {
    expect(PLAYBACK_RATES).toEqual([1, 10, 30, 60, 120, 300]);
    expect(DEFAULT_PLAYBACK_RATE).toBe(60);
  });

  it('plays one second per second at 1× and one minute per second at 60×', () => {
    const t0 = fixtureMs(1, 480);
    const end = fixtureMs(2, 720);
    expect(advancePlayhead(t0, 1000, 1, null, false, end)).toEqual({ ms: t0 + 1000, finished: false });
    expect(advancePlayhead(t0, 1000, 60, null, false, end)).toEqual({ ms: t0 + 60_000, finished: false });
  });

  it('skips the overnight parking to the next trip only when asked', () => {
    const timeline = fixtureTimeline();
    const end = fixtureMs(2, 720);
    // Trip 5 ends Fri 18:23; ten minutes on lands in the parking that lasts until Sat 08:10.
    const from = fixtureMs(1, 1100);
    const tenMinutes = 10 * 60_000;
    expect(advancePlayhead(from, tenMinutes, 1, timeline, true, end)).toEqual({ ms: fixtureMs(2, 490), finished: false });
    expect(advancePlayhead(from, tenMinutes, 1, timeline, false, end)).toEqual({ ms: fixtureMs(1, 1110), finished: false });
  });

  it('starts from a standstill on the next trip when stops are skipped', () => {
    const timeline = fixtureTimeline();
    expect(advancePlayhead(fixtureMs(1, 520), 0, 60, timeline, true, fixtureMs(2, 720)).ms).toBe(fixtureMs(1, 532));
  });

  it('stops at the end, including when only parking is left', () => {
    const timeline = fixtureTimeline();
    expect(advancePlayhead(fixtureMs(2, 519), 120_000, 1, timeline, false, fixtureMs(2, 520)))
      .toEqual({ ms: fixtureMs(2, 520), finished: true });
    // Saturday ends parked until 12:00, the end of the period.
    expect(advancePlayhead(fixtureMs(2, 519), 120_000, 1, timeline, true, fixtureMs(2, 720)))
      .toEqual({ ms: fixtureMs(2, 720), finished: true });
  });

  it('finds the trip after an instant for "Next trip"', () => {
    const timeline = fixtureTimeline();
    expect(tripAfter(timeline, fixtureMs(1, 470))?.id).toBe('t2');
    expect(tripAfter(timeline, fixtureMs(0, 0))?.id).toBe('t1');
    expect(tripAfter(timeline, fixtureMs(2, 490))).toBeNull();
  });

  it('finds the fix at or before an instant', () => {
    const times = [10, 20, 30];
    expect(indexAtOrBefore(times, 5)).toBe(-1);
    expect(indexAtOrBefore(times, 10)).toBe(0);
    expect(indexAtOrBefore(times, 25)).toBe(1);
    expect(indexAtOrBefore(times, 30)).toBe(2);
    expect(indexAtOrBefore(times, 99)).toBe(2);
    expect(indexAtOrBefore([], 5)).toBe(-1);
  });

  it('puts the vehicle on the line between the two fixes around the playhead', () => {
    const t0 = fixtureMs(1, 468);
    const points: TripReplayPosition[] = [
      { timestamp: new Date(t0).toISOString(), latitude: 32, longitude: 74, speedKmh: 10 },
      { timestamp: new Date(t0 + 60_000).toISOString(), latitude: 32, longitude: 74.01, speedKmh: 30 }
    ];
    const times = timesOf(points);

    const mid = positionAt(points, times, t0 + 30_000)!;
    expect(mid.latitude).toBeCloseTo(32, 6);
    expect(mid.longitude).toBeCloseTo(74.005, 6);
    expect(mid.speedKmh).toBeCloseTo(20, 6);
    expect(mid.heading!).toBeCloseTo(90, 0);
    expect(mid.fixIndex).toBe(0);

    expect(positionAt(points, times, t0 - 5000)!.longitude).toBe(74);
    expect(positionAt(points, times, t0 + 600_000)!).toEqual(
      jasmine.objectContaining({ longitude: 74.01, fixIndex: 1 })
    );
    expect(positionAt([], [], t0)).toBeNull();
  });

  it('measures bearings clockwise from north', () => {
    expect(bearingDegrees(32, 74, 33, 74)).toBeCloseTo(0, 0);
    expect(bearingDegrees(32, 74, 32, 75)).toBeCloseTo(90, 0);
    expect(bearingDegrees(32, 74, 31, 74)).toBeCloseTo(180, 0);
    expect(bearingDegrees(32, 74, 32, 73)).toBeCloseTo(270, 0);
  });
});

describe('history-playback.util — dock scale', () => {
  it('maps time to a position in the dock and back, clamped to its ends', () => {
    expect(timeToPercent(fixtureMs(1, 468), friday)).toBeCloseTo(32.5, 6);
    expect(timeToPercent(fixtureMs(1, 1440), friday)).toBe(100);
    expect(timeToPercent(fixtureMs(0, 600), friday)).toBe(0);
    expect(timeToPercent(fixtureMs(3, 0), friday)).toBe(100);
    expect(percentToTime(32.5, friday)).toBeCloseTo(fixtureMs(1, 468), 3);
    expect(percentToTime(-5, friday)).toBe(friday.startMs);
    expect(timeToPercent(5, { startMs: 10, endMs: 10 })).toBe(0);
  });

  it('takes the local day of the vehicle, not the UTC day', () => {
    // 05:42 UTC on 20 Nov is 10:42 on Friday in Karachi; that day starts 19:00 UTC on the 19th.
    const day = localDayRange(Date.parse('2026-11-20T05:42:16Z'), FIXTURE_ZONE);
    expect(new Date(day.startMs).toISOString()).toBe('2026-11-19T19:00:00.000Z');
    expect(new Date(day.endMs).toISOString()).toBe('2026-11-20T19:00:00.000Z');
  });

  it('zooms to the day of the playhead', () => {
    expect(dockRange('day', fixtureMs(1, 642), period, null, FIXTURE_ZONE)).toEqual(friday);
  });

  it('zooms to the selected trip, or the day when nothing is selected', () => {
    const trip = { startMs: fixtureMs(1, 468), endMs: fixtureMs(1, 511) };
    expect(dockRange('trip', fixtureMs(1, 480), period, trip, FIXTURE_ZONE)).toEqual(trip);
    expect(dockRange('trip', fixtureMs(1, 480), period, null, FIXTURE_ZONE)).toEqual(friday);
  });

  it('zooms "Week" to the whole period in whole local days when it fits', () => {
    expect(dockRange('week', fixtureMs(1, 642), period, null, FIXTURE_ZONE)).toEqual({
      startMs: fixtureMs(0, 0),
      endMs: fixtureMs(3, 0)
    });
  });

  it('zooms "Week" to seven days around the playhead in a longer period, kept inside it', () => {
    const month = { startMs: fixtureMs(0, 0), endMs: fixtureMs(30, 0) };
    expect(dockRange('week', fixtureMs(15, 600), month, null, FIXTURE_ZONE)).toEqual({
      startMs: fixtureMs(12, 0), endMs: fixtureMs(19, 0)
    });
    expect(dockRange('week', fixtureMs(1, 600), month, null, FIXTURE_ZONE)).toEqual({
      startMs: fixtureMs(0, 0), endMs: fixtureMs(7, 0)
    });
    expect(dockRange('week', fixtureMs(29, 600), month, null, FIXTURE_ZONE)).toEqual({
      startMs: fixtureMs(23, 0), endMs: fixtureMs(30, 0)
    });
  });

  it('has no "Week" for a period of a day', () => {
    const oneDay = { startMs: fixtureMs(1, 0), endMs: fixtureMs(2, 0) };
    expect(weekZoomAvailable(oneDay)).toBeFalse();
    expect(weekZoomAvailable(period)).toBeTrue();
    expect(dockRange('week', fixtureMs(1, 642), oneDay, null, FIXTURE_ZONE)).toEqual(friday);
  });

  it('labels a day 12 AM · 6 AM · 12 PM · 6 PM · 12 AM', () => {
    const ticks = axisTicks(friday, FIXTURE_ZONE);
    expect(ticks.map(t => t.label)).toEqual(['12 AM', '6 AM', '12 PM', '6 PM', '12 AM']);
    expect(ticks.map(t => t.percent)).toEqual([0, 25, 50, 75, 100]);
  });

  it('labels a trip with clock times and a week with days', () => {
    const trip = { startMs: fixtureMs(1, 468), endMs: fixtureMs(1, 511) };
    expect(axisTicks(trip, FIXTURE_ZONE).map(t => t.label)).toEqual([
      '7:48 AM', '7:58 AM', '8:09 AM', '8:20 AM', '8:31 AM'
    ]);
    const threeDays = { startMs: fixtureMs(0, 0), endMs: fixtureMs(3, 0) };
    expect(axisTicks(threeDays, FIXTURE_ZONE).map(t => t.label)).toEqual([
      'Thu 19', 'Thu 19', 'Fri 20', 'Sat 21', 'Sun 22'
    ]);
  });
});

describe('history-playback.util — activity bar', () => {
  const timeline = fixtureTimeline();

  it('places each segment of the day by its share of the day', () => {
    const blocks = activityBlocks(timeline.segments, friday, FIXTURE_ZONE);
    expect(blocks.map(b => b.id)).toEqual(['s1', 't1', 's2', 't2', 's3', 't3', 's4', 't4', 's5', 't5', 's6']);

    const t1 = blocks.find(b => b.id === 't1')!;
    expect(t1.leftPercent).toBeCloseTo(32.5, 6);
    expect(t1.widthPercent).toBeCloseTo((43 / 1440) * 100, 6);
    expect(t1.label).toBe('Trip Pasrur to Kalaswala, 7:48 – 8:31 AM');

    expect(blocks.find(b => b.id === 's2')!.label).toBe('Stop at Pasrur Road, Kalaswala, 8:31 – 8:52 AM');
    expect(blocks.find(b => b.id === 's4')!.label).toBe('Parked at Circular Road, Pasrur, 11:19 AM – 4:05 PM');
  });

  it('clips a segment that runs over the edge of the dock', () => {
    const blocks = activityBlocks(timeline.segments, friday, FIXTURE_ZONE);
    const fromThursday = blocks.find(b => b.id === 's1')!;
    expect(fromThursday.leftPercent).toBe(0);
    expect(fromThursday.widthPercent).toBeCloseTo(32.5, 6);

    const intoSaturday = blocks.find(b => b.id === 's6')!;
    expect(intoSaturday.leftPercent + intoSaturday.widthPercent).toBeCloseTo(100, 6);
  });

  it('covers the day without gaps or overlaps when the timeline does', () => {
    const blocks = activityBlocks(timeline.segments, friday, FIXTURE_ZONE);
    for (let i = 1; i < blocks.length; i++) {
      expect(blocks[i].leftPercent).toBeCloseTo(blocks[i - 1].leftPercent + blocks[i - 1].widthPercent, 6);
    }
    expect(blocks.reduce((sum, b) => sum + b.widthPercent, 0)).toBeCloseTo(100, 6);
  });

  it('marks the part of the dock that lies outside the loaded period', () => {
    const thursday = { startMs: fixtureMs(0, 0), endMs: fixtureMs(1, 0) };
    const fromEvening = { startMs: fixtureMs(0, 1124), endMs: fixtureMs(2, 720) };
    const before = outsideBlocks(thursday, fromEvening);
    expect(before.length).toBe(1);
    expect(before[0].leftPercent).toBe(0);
    expect(before[0].widthPercent).toBeCloseTo((1124 / 1440) * 100, 6);

    const saturday = { startMs: fixtureMs(2, 0), endMs: fixtureMs(3, 0) };
    const after = outsideBlocks(saturday, period);
    expect(after.length).toBe(1);
    expect(after[0].leftPercent).toBeCloseTo(50, 6);
    expect(after[0].widthPercent).toBeCloseTo(50, 6);

    expect(outsideBlocks(friday, period)).toEqual([]);
  });

  it('labels a standstill still going on "– now" while the page can vouch for it, else "since"', () => {
    const ongoing = fixtureTimeline();
    ongoing.segments[ongoing.segments.length - 1].ongoing = true;
    const saturday = { startMs: fixtureMs(2, 0), endMs: fixtureMs(3, 0) };

    const live = activityBlocks(ongoing.segments, saturday, FIXTURE_ZONE, true);
    expect(live.find(b => b.id === 's7')!.label).toBe('Parked at Pasrur Road, Kalaswala, 8:40 AM – now');

    const old = activityBlocks(ongoing.segments, saturday, FIXTURE_ZONE, false);
    expect(old.find(b => b.id === 's7')!.label).toBe('Parked at Pasrur Road, Kalaswala, since 8:40 AM');
    expect(old.find(b => b.id === 't6')!.label).toBe('Trip Pasrur to Kalaswala, 8:10 – 8:40 AM');
  });
});

describe('history-playback.util — events', () => {
  const event = (minute: number, type: string, label: string | null = null) =>
    ({ time: new Date(fixtureMs(1, minute)).toISOString(), type, label, latitude: 32.2, longitude: 74.6 });

  it('draws geofence, overspeed and every other event as an alarm', () => {
    expect(eventKind('geofenceEnter')).toBe('geofence');
    expect(eventKind('geofenceExit')).toBe('geofence');
    expect(eventKind('deviceOverspeed')).toBe('overspeed');
    expect(eventKind('alarm')).toBe('alarm');
    expect(eventKind('hardBraking')).toBe('alarm');
    expect(eventKind(null)).toBe('alarm');
  });

  it('keeps the events of the dock, in time order, placed by their share of it', () => {
    const ticks = eventTicks(
      [event(720, 'alarm', 'SOS'), event(360, 'deviceOverspeed', 'Overspeed'), event(-30, 'alarm', 'Day before')],
      friday,
      FIXTURE_ZONE
    );
    expect(ticks.map(t => t.label)).toEqual(['Overspeed at 6:00:00 AM', 'SOS at 12:00:00 PM']);
    expect(ticks.map(t => t.percent)).toEqual([25, 50]);
    expect(ticks.map(t => t.kind)).toEqual(['overspeed', 'alarm']);
  });

  it('leaves out events without a time, and names an unlabelled one by its type', () => {
    const ticks = eventTicks([{ time: '', type: 'alarm' }, event(60, 'alarm')], friday, FIXTURE_ZONE);
    expect(ticks.map(t => t.label)).toEqual(['alarm at 1:00:00 AM']);
  });

  it('shows at most the cap', () => {
    const many = Array.from({ length: MAX_EVENT_TICKS + 50 }, (_, i) => event(i % 1440, 'alarm', 'Alarm'));
    expect(eventTicks(many, friday, FIXTURE_ZONE).length).toBe(MAX_EVENT_TICKS);
  });
});

describe('history-playback.util — speed chart', () => {
  const timeline = fixtureTimeline();
  const route = fixtureRoute(timeline);
  const times = timesOf(route);
  const trips = timeline.segments.filter(s => s.kind === 'trip');

  it('draws one closed shape per trip of the day, from zero up and back to zero', () => {
    const { line, area } = speedPaths(route, times, trips, friday, 90);
    expect((area.match(/Z/g) ?? []).length).toBe(5);
    expect(line.startsWith('M468.0 54')).toBeTrue();
    expect(line).toContain('L511.0 54');
    expect(line).not.toContain('Z');
  });

  it('reaches the top of the chart at the period maximum and scales the rest to it', () => {
    const { line } = speedPaths(route, times, trips, friday, 90);
    // Trip 4 peaks at 90 km/h in its middle; trip 1 at 71 km/h.
    expect(line).toContain('L998.5 4.0');
    expect(line).toContain('L489.5 14.6');
  });

  it('draws nothing where there is no trip', () => {
    const thursday = { startMs: fixtureMs(0, 0), endMs: fixtureMs(1, 0) };
    expect(speedPaths(route, times, trips, thursday, 90)).toEqual({ line: '', area: '' });
  });

  it('keeps the peak when a long trip is thinned', () => {
    const start = fixtureMs(1, 468);
    const dense: TripReplayPosition[] = [];
    for (let i = 0; i <= 2000; i++) {
      dense.push({
        timestamp: new Date(start + i * 1000).toISOString(),
        latitude: 32,
        longitude: 74 + i * 1e-5,
        speedKmh: i === 1337 ? 88 : 40
      });
    }
    const trip = { ...trips[0], startTime: dense[0].timestamp, endTime: dense[2000].timestamp };
    const { line } = speedPaths(dense, timesOf(dense), [trip], friday, 88);
    expect(line).toContain(' 4.0');
    expect((line.match(/L/g) ?? []).length).toBeLessThan(260);
  });
});
