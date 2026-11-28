import {
  buildKpis,
  buildTimelineRows,
  defaultOpenDay,
  drivenSoFarKm,
  drivingPercent,
  formatClock,
  formatDay,
  formatDayKey,
  formatDuration,
  formatRangeEdge,
  formatTimeRange,
  hiddenStopCount,
  nextStandstill,
  placeLabel,
  previousStandstill,
  segmentAt,
  standstillPlace,
  standstillTitle,
  standstillWhen,
  TimelineDayRow,
  TimelineRow,
  TimelineRowOptions,
  TimelineSegmentRow,
  tripAtOrAfter,
  tripTitle,
  zoneShortLabel
} from './history-timeline.util';
import {
  FIXTURE_ZONE,
  fixtureIso,
  fixtureMs,
  fixtureRoute,
  fixtureTimeline
} from './history-timeline.fixture';

const ALL_DAYS = new Set(['2026-11-19', '2026-11-20', '2026-11-21']);

function rows(overrides: Partial<TimelineRowOptions> = {}): TimelineRow[] {
  return buildTimelineRows(fixtureTimeline(), {
    filter: 'all',
    hideStopsUnderMinutes: 5,
    openDays: ALL_DAYS,
    selectedId: null,
    zone: FIXTURE_ZONE,
    ...overrides
  });
}

const dayRows = (list: TimelineRow[]) => list.filter((r): r is TimelineDayRow => r.type === 'day');
const segmentRows = (list: TimelineRow[]) =>
  list.filter((r): r is TimelineSegmentRow => r.type === 'trip' || r.type === 'stop' || r.type === 'parking');
const tripRows = (list: TimelineRow[]) => segmentRows(list).filter(r => r.type === 'trip');

describe('history-timeline.util — formatting', () => {
  it('writes durations the way the design does', () => {
    expect(formatDuration(43)).toBe('43 min');
    expect(formatDuration(67)).toBe('1h 7m');
    expect(formatDuration(120)).toBe('2h');
    expect(formatDuration(286)).toBe('4h 46m');
    expect(formatDuration(827)).toBe('13h 47m');
    expect(formatDuration(1550)).toBe('1d 1h 50m');
    expect(formatDuration(8530)).toBe('5d 22h 10m');
    expect(formatDuration(0)).toBe('0 min');
  });

  it('keeps driving time in hours, never days', () => {
    expect(formatDuration(1550, 'hours')).toBe('25h 50m');
    expect(formatDuration(52, 'hours')).toBe('52 min');
  });

  it('uses the narrow minute unit in a day row', () => {
    expect(formatDuration(52, 'compact')).toBe('52m');
    expect(formatDuration(261, 'compact')).toBe('4h 21m');
  });

  it('writes a time range once per half of the day', () => {
    expect(formatTimeRange(fixtureIso(1, 468), fixtureIso(1, 511), FIXTURE_ZONE)).toBe('7:48 – 8:31 AM');
    expect(formatTimeRange(fixtureIso(1, 679), fixtureIso(1, 965), FIXTURE_ZONE)).toBe('11:19 AM – 4:05 PM');
  });

  it('names the weekday when a range ends on another day', () => {
    expect(formatTimeRange(fixtureIso(1, 1103), fixtureIso(2, 490), FIXTURE_ZONE)).toBe('6:23 PM – Sat 8:10 AM');
  });

  it('gives the end its date when a range runs a week or more', () => {
    expect(formatTimeRange(fixtureIso(0, 600), fixtureIso(8, 630), FIXTURE_ZONE)).toBe('10:00 AM – Fri 27 Nov 10:30 AM');
  });

  it('formats clock, day and range edge in the vehicle zone, not the browser zone', () => {
    // 05:42:16 UTC is 10:42:16 in Karachi.
    const instant = '2026-11-20T05:42:16Z';
    expect(formatClock(instant, FIXTURE_ZONE, true)).toBe('10:42:16 AM');
    expect(formatClock(instant, FIXTURE_ZONE)).toBe('10:42 AM');
    expect(formatDay(instant, FIXTURE_ZONE)).toBe('Fri 20 Nov');
    expect(formatDay(instant, FIXTURE_ZONE, true)).toBe('Fri 20 Nov 2026');
    expect(formatRangeEdge('2026-11-19T13:44:00Z', FIXTURE_ZONE)).toBe('19 Nov, 6:44 PM');
  });

  it('names the zone for the toolbar badge', () => {
    expect(zoneShortLabel('Asia/Karachi')).toBe('PKT');
    expect(zoneShortLabel('UTC')).toBe('UTC');
  });

  it('labels a local day key without shifting it', () => {
    expect(formatDayKey('2026-11-20')).toBe('Fri 20 Nov');
    expect(formatDayKey('2026-11-01')).toBe('Sun 1 Nov');
  });

  it('names a place by its town for a trip and by street and town for a standstill', () => {
    expect(placeLabel('Pasrur Road, Kalaswala, Sialkot')).toBe('Kalaswala');
    expect(placeLabel('Kotli Syedan')).toBe('Kotli Syedan');
    expect(placeLabel(null)).toBeNull();
    expect(standstillPlace('Circular Road, Pasrur, Sialkot')).toBe('Circular Road, Pasrur');
  });

  it('falls back to coordinates when a trip end has no address', () => {
    const t = { ...fixtureTimeline().segments[1], startAddress: null };
    expect(tripTitle(t)).toBe('32.2620, 74.6620 → Kalaswala');
  });
});

describe('history-timeline.util — KPI strip', () => {
  it('shows the server totals of the design mockup', () => {
    const kpis = buildKpis({
      distanceKm: 339.2,
      drivingMinutes: 1550,
      stoppedMinutes: 8530,
      windowMinutes: 10080,
      tripCount: 31,
      stopCount: 54,
      parkingCount: 16,
      maxSpeedKmh: 90,
      avgSpeedKmh: 13.1,
      noSignalMinutes: 0
    });
    expect(kpis.map(k => k.label)).toEqual([
      'Distance', 'Driving · 15%', 'Stopped · 85%', 'Stops', 'Parking', 'Max speed', 'Avg speed'
    ]);
    expect(kpis.map(k => k.value)).toEqual(['339.2', '25h 50m', '5d 22h 10m', '54', '16', '90', '13.1']);
    expect(kpis.map(k => k.unit)).toEqual(['km', '', '', '', '', 'km/h', 'km/h']);
  });

  it('makes the Driving and Stopped percentages total 100 for any split', () => {
    for (let driving = 0; driving <= 10080; driving += 37) {
      const kpis = buildKpis({
        distanceKm: 1, drivingMinutes: driving, stoppedMinutes: 10080 - driving, windowMinutes: 10080,
        tripCount: 0, stopCount: 0, parkingCount: 0, maxSpeedKmh: 0, avgSpeedKmh: 0, noSignalMinutes: 0
      });
      const percent = (label: string) => Number(/(\d+)%/.exec(label)![1]);
      expect(percent(kpis[1].label) + percent(kpis[2].label)).toBe(100);
    }
  });

  it('leaves the percentages out when the period has no length', () => {
    expect(drivingPercent({ drivingMinutes: 0, windowMinutes: 0 })).toBeNull();
    expect(buildKpis(null).every(k => k.value === '—')).toBeTrue();
  });
});

describe('history-timeline.util — rows', () => {
  it('lists a row per day and, under an open day, its segments in order', () => {
    const friday = rows({ openDays: new Set(['2026-11-20']) });
    expect(dayRows(friday).map(r => r.label)).toEqual(['Thu 19 Nov', 'Fri 20 Nov', 'Sat 21 Nov']);
    expect(segmentRows(friday).map(r => r.id)).toEqual([
      't1', 's2', 't2', 's3', 't3', 's4', 't4', 's5', 't5', 's6'
    ]);
    expect(segmentRows(friday).map(r => r.type)).toEqual([
      'trip', 'stop', 'trip', 'stop', 'trip', 'parking', 'trip', 'stop', 'trip', 'parking'
    ]);
  });

  it('writes a trip row and a standstill row as in the design', () => {
    const list = rows();
    const t1 = segmentRows(list).find(r => r.id === 't1')!;
    expect(t1.title).toBe('Pasrur → Kalaswala');
    expect(t1.subtitle).toBe('7:48 – 8:31 AM · 43 min');
    expect(t1.km).toBe('12.6');
    expect(t1.max).toBe('71');

    const s2 = segmentRows(list).find(r => r.id === 's2')!;
    expect(s2.title).toBe('Stopped 21 min');
    expect(s2.subtitle).toBe('Kalaswala · 8:31 – 8:52 AM');
    expect(s2.whereFull).toBe('Pasrur Road, Kalaswala');
    expect(s2.ariaLabel).toBe('Stopped 21 min, Pasrur Road, Kalaswala, 8:31 – 8:52 AM');

    const s6 = segmentRows(list).find(r => r.id === 's6')!;
    expect(s6.title).toBe('Parked 13h 47m');
    expect(s6.subtitle).toBe('Pasrur · 6:23 PM – Sat 8:10 AM');
  });

  it('shows the day figures the server sent, scaled against the busiest day', () => {
    const days = dayRows(rows());
    expect(days.map(d => d.km)).toEqual(['0.0', '58.3', '10.0']);
    expect(days.map(d => d.duration)).toEqual(['—', '4h 21m', '30m']);
    expect(days.map(d => d.barPercent)).toEqual([0, 100, 17]);
  });

  it('adds up on screen: trip rows to their day row, day rows to the strip', () => {
    const timeline = fixtureTimeline();
    const list = rows();
    const tenths = (text: string) => Math.round(Number(text) * 10);

    for (const day of dayRows(list)) {
      const trips = tripRows(list).filter(r => r.date === day.date);
      expect(trips.reduce((sum, r) => sum + tenths(r.km), 0)).toBe(tenths(day.km));
    }
    const strip = buildKpis(timeline.totals);
    expect(dayRows(list).reduce((sum, d) => sum + tenths(d.km), 0)).toBe(tenths(strip[0].value));

    // Driving time: the same check on the server's minutes behind the labels.
    for (const day of timeline.days) {
      const minutes = timeline.segments
        .filter(s => s.kind === 'trip' && s.date === day.date)
        .reduce((sum, s) => sum + s.durationMinutes, 0);
      expect(minutes).toBe(day.drivingMinutes);
    }
    expect(timeline.days.reduce((sum, d) => sum + d.drivingMinutes, 0)).toBe(timeline.totals.drivingMinutes);
    expect(timeline.totals.drivingMinutes + timeline.totals.stoppedMinutes).toBe(timeline.totals.windowMinutes);
  });

  it('lists as many standstills under "Stops" as the strip counts', () => {
    const timeline = fixtureTimeline();
    const stops = segmentRows(rows({ filter: 'stops', hideStopsUnderMinutes: 0 }));
    expect(stops.length).toBe(timeline.totals.stopCount);
    expect(stops.filter(r => r.type === 'parking').length).toBe(timeline.totals.parkingCount);
  });

  it('filters to trips or to standstills', () => {
    expect(segmentRows(rows({ filter: 'trips' })).every(r => r.type === 'trip')).toBeTrue();
    expect(segmentRows(rows({ filter: 'trips' })).length).toBe(6);
    expect(segmentRows(rows({ filter: 'stops' })).some(r => r.type === 'trip')).toBeFalse();
    expect(rows({ filter: 'trips' }).some(r => r.type === 'note')).toBeFalse();
  });

  it('hides stops under the threshold and says how many', () => {
    // Friday: stops of 21, 39 and 28 min, plus three pauses inside trips.
    const at5 = rows({ openDays: new Set(['2026-11-20']) });
    expect(segmentRows(at5).filter(r => r.type === 'stop').length).toBe(3);
    expect(at5.find(r => r.type === 'note')).toEqual(jasmine.objectContaining({ text: '3 stops under 5 min hidden' }));

    const at30 = rows({ openDays: new Set(['2026-11-20']), hideStopsUnderMinutes: 30 });
    expect(segmentRows(at30).filter(r => r.type === 'stop').map(r => r.id)).toEqual(['s3']);
    expect(at30.find(r => r.type === 'note')).toEqual(jasmine.objectContaining({ text: '5 stops under 30 min hidden' }));

    const at60 = rows({ openDays: new Set(['2026-11-20']), hideStopsUnderMinutes: 60 });
    expect(segmentRows(at60).filter(r => r.type !== 'trip').map(r => r.id)).toEqual(['s4', 's6']);
    expect(at60.find(r => r.type === 'note')).toEqual(jasmine.objectContaining({ text: '6 stops under 1 hour hidden' }));
  });

  it('lists the pauses inside trips with "Show all" and hides nothing', () => {
    const all = rows({ openDays: new Set(['2026-11-20']), hideStopsUnderMinutes: 0 });
    const brief = all.filter(r => r.type === 'brief');
    expect(brief.length).toBe(3);
    expect(brief[0]).toEqual(jasmine.objectContaining({ tripId: 't2', title: 'Brief stop 2 min', subtitle: '9:00 – 9:02 AM' }));
    expect(all.some(r => r.type === 'note')).toBeFalse();
    expect(hiddenStopCount(fixtureTimeline().segments, 0)).toBe(0);
  });

  it('never hides trips, whatever the threshold', () => {
    expect(tripRows(rows({ hideStopsUnderMinutes: 120 })).length).toBe(6);
  });

  it('shows only the day row for a closed day, and says so when a day has nothing to list', () => {
    const closed = rows({ openDays: new Set() });
    expect(closed.every(r => r.type === 'day')).toBeTrue();

    const thursdayTripsOnly = rows({ openDays: new Set(['2026-11-19']), filter: 'trips' });
    expect(thursdayTripsOnly.find(r => r.type === 'empty')).toEqual(
      jasmine.objectContaining({ text: 'Nothing to list with this filter' })
    );
  });

  it('marks the selected segment and flags a trip with a stretch without signal', () => {
    const list = rows({ selectedId: 't4' });
    const selected = segmentRows(list).filter(r => r.selected);
    expect(selected.map(r => r.id)).toEqual(['t4']);
    expect(selected[0].noSignalHint).toBe('Includes 6 min estimated without GPS signal');
    expect(segmentRows(list).find(r => r.id === 't1')!.noSignalHint).toBeNull();
  });

  it('opens the latest day that has a trip', () => {
    expect(defaultOpenDay(fixtureTimeline())).toBe('2026-11-21');
    const parked = fixtureTimeline();
    parked.days.forEach(d => (d.tripCount = 0));
    expect(defaultOpenDay(parked)).toBe('2026-11-21');
  });
});

describe('history-timeline.util — a standstill still going on', () => {
  /** The period ends Sat 12:00 PM while the vehicle has stood at Kalaswala since 8:40 AM. */
  function ongoingTimeline() {
    const timeline = fixtureTimeline();
    timeline.segments[timeline.segments.length - 1].ongoing = true;
    return timeline;
  }

  function s7Row(timeline = ongoingTimeline(), ongoingLive = true): TimelineSegmentRow {
    const list = buildTimelineRows(timeline, {
      filter: 'all',
      hideStopsUnderMinutes: 5,
      openDays: ALL_DAYS,
      selectedId: null,
      zone: FIXTURE_ZONE,
      ongoingLive
    });
    return segmentRows(list).find(r => r.id === 's7')!;
  }

  it('says "so far" in its title', () => {
    const s7 = ongoingTimeline().segments.find(s => s.id === 's7')!;
    expect(standstillTitle(s7)).toBe('Parked 3h 20m so far');
    expect(standstillTitle({ ...s7, kind: 'stop', durationMinutes: 52 })).toBe('Stopped 52 min so far');
  });

  it('writes when it was: a range when finished, else "– now" or "since"', () => {
    const s7 = ongoingTimeline().segments.find(s => s.id === 's7')!;
    expect(standstillWhen(s7, FIXTURE_ZONE, true)).toBe('8:40 AM – now');
    expect(standstillWhen(s7, FIXTURE_ZONE)).toBe('since 8:40 AM');
    expect(standstillWhen({ ...s7, ongoing: false }, FIXTURE_ZONE, true)).toBe('8:40 AM – 12:00 PM');
  });

  it('runs to "now" with the Ongoing tag while the page can vouch for it', () => {
    const row = s7Row();
    expect(row.title).toBe('Parked 3h 20m so far');
    expect(row.when).toBe('8:40 AM – now');
    expect(row.subtitle).toBe('Kalaswala · 8:40 AM – now');
    expect(row.ongoing).toBeTrue();
    expect(row.ariaLabel).toBe('Parked 3h 20m so far, ongoing, Pasrur Road, Kalaswala, 8:40 AM – now');
  });

  it('reads "since" without the tag when the data is old or the tracker is not live', () => {
    const row = s7Row(ongoingTimeline(), false);
    expect(row.title).toBe('Parked 3h 20m so far');
    expect(row.when).toBe('since 8:40 AM');
    expect(row.ongoing).toBeFalse();
    expect(row.ariaLabel).toBe('Parked 3h 20m so far, Pasrur Road, Kalaswala, since 8:40 AM');
  });

  it('leaves a finished standstill as it was', () => {
    const row = s7Row(fixtureTimeline(), true);
    expect(row.title).toBe('Parked 3h 20m');
    expect(row.when).toBe('8:40 AM – 12:00 PM');
    expect(row.ongoing).toBeFalse();
    expect(segmentRows(rows({ ongoingLive: true })).every(r => !r.ongoing)).toBeTrue();
  });
});

describe('history-timeline.util — lookups', () => {
  const timeline = fixtureTimeline();

  it('finds the segment at an instant; a boundary belongs to the one that starts there', () => {
    expect(segmentAt(timeline, fixtureMs(1, 480))!.id).toBe('t1');
    expect(segmentAt(timeline, fixtureMs(1, 511))!.id).toBe('s2');
    expect(segmentAt(timeline, fixtureMs(0, 600))!.id).toBe('s1');
    expect(segmentAt(timeline, fixtureMs(2, 721))).toBeNull();
  });

  it('steps to the next and the previous standstill', () => {
    expect(nextStandstill(timeline, fixtureMs(1, 480))!.id).toBe('s2');
    expect(nextStandstill(timeline, fixtureMs(1, 511))!.id).toBe('s3');
    expect(previousStandstill(timeline, fixtureMs(1, 600))!.id).toBe('s3');
    expect(previousStandstill(timeline, fixtureMs(1, 587))!.id).toBe('s2');
    expect(previousStandstill(timeline, fixtureMs(0, 0))).toBeNull();
    expect(nextStandstill(timeline, fixtureMs(2, 600))).toBeNull();
  });

  it('picks the trip to play: the one in progress, else the next', () => {
    expect(tripAtOrAfter(timeline, fixtureMs(1, 480))!.id).toBe('t1');
    expect(tripAtOrAfter(timeline, fixtureMs(1, 520))!.id).toBe('t2');
    expect(tripAtOrAfter(timeline, fixtureMs(2, 600))).toBeNull();
  });
});

describe('history-timeline.util — driven so far', () => {
  const timeline = fixtureTimeline();
  const route = fixtureRoute(timeline);

  it('is zero before the first trip and the period distance after the last', () => {
    expect(drivenSoFarKm(timeline, route, fixtureMs(0, 300))).toBe(0);
    expect(drivenSoFarKm(timeline, route, fixtureMs(2, 700))).toBeCloseTo(timeline.totals.distanceKm, 6);
  });

  it('reaches each finished trip exactly and stays there through the standstill after it', () => {
    expect(drivenSoFarKm(timeline, route, fixtureMs(1, 511))).toBeCloseTo(12.6, 6);
    expect(drivenSoFarKm(timeline, route, fixtureMs(1, 525))).toBeCloseTo(12.6, 6);
    expect(drivenSoFarKm(timeline, route, fixtureMs(1, 587))).toBeCloseTo(26.7, 6);
  });

  it('follows the path covered within the trip in progress', () => {
    // Halfway along the straight line of trip 1.
    expect(drivenSoFarKm(timeline, route, fixtureMs(1, 489.5))).toBeCloseTo(6.3, 1);
  });

  it('never goes backwards as time advances', () => {
    let previous = 0;
    for (let minute = 0; minute <= 3600; minute += 7) {
      const km = drivenSoFarKm(timeline, route, fixtureMs(0, minute));
      expect(km).toBeGreaterThanOrEqual(previous - 1e-9);
      previous = km;
    }
  });
});
