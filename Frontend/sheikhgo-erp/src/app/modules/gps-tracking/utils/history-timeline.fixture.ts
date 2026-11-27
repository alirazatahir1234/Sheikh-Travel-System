import {
  HistoryTimeline,
  HistoryTimelineSegment,
  TripReplayPosition
} from '../../../core/models/gps-tracking.model';

/**
 * Sample timeline for specs, shaped like an API response: Thu 19 – Sat 21 Nov 2026 in Karachi.
 * Friday follows the design mockup (five trips, 58.3 km, 4h 21m). Days add up to the totals and
 * trips add up to their day, as the API guarantees.
 */

export const FIXTURE_ZONE = 'Asia/Karachi';

/** Thu 19 Nov 2026, 00:00 in Karachi. */
const BASE_MS = Date.UTC(2026, 10, 18, 19, 0, 0);

/** Instant for a local day (0 = Thu) and minute of that day. */
export function fixtureMs(day: number, minute: number): number {
  return BASE_MS + (day * 1440 + minute) * 60_000;
}

export function fixtureIso(day: number, minute: number): string {
  return new Date(fixtureMs(day, minute)).toISOString();
}

const DATES = ['2026-11-19', '2026-11-20', '2026-11-21'];

const PLACES: Record<string, { lat: number; lng: number; address: string }> = {
  pasrur: { lat: 32.262, lng: 74.662, address: 'Circular Road, Pasrur, Sialkot' },
  kalaswala: { lat: 32.19, lng: 74.648, address: 'Pasrur Road, Kalaswala, Sialkot' },
  kotli: { lat: 32.321, lng: 74.755, address: 'Kotli Syedan' },
  talwandi: { lat: 32.219, lng: 74.675, address: 'Talwandi Inayat Khan, Sialkot' }
};

function trip(
  id: string,
  day: number,
  start: number,
  end: number,
  from: keyof typeof PLACES,
  to: keyof typeof PLACES,
  km: number,
  max: number,
  extra: Partial<HistoryTimelineSegment> = {}
): HistoryTimelineSegment {
  return {
    id,
    kind: 'trip',
    date: DATES[day],
    startTime: fixtureIso(day, start),
    endTime: fixtureIso(day, end),
    durationMinutes: end - start,
    distanceKm: km,
    maxSpeedKmh: max,
    startLatitude: PLACES[from].lat,
    startLongitude: PLACES[from].lng,
    startAddress: PLACES[from].address,
    endLatitude: PLACES[to].lat,
    endLongitude: PLACES[to].lng,
    endAddress: PLACES[to].address,
    ...extra
  };
}

function standstill(
  id: string,
  day: number,
  start: number,
  end: number,
  at: keyof typeof PLACES
): HistoryTimelineSegment {
  const minutes = end - start;
  return {
    id,
    kind: minutes >= 120 ? 'parking' : 'stop',
    date: DATES[day],
    startTime: fixtureIso(day, start),
    endTime: fixtureIso(day, end),
    durationMinutes: minutes,
    distanceKm: null,
    maxSpeedKmh: null,
    startLatitude: PLACES[at].lat,
    startLongitude: PLACES[at].lng,
    startAddress: PLACES[at].address,
    endLatitude: PLACES[at].lat,
    endLongitude: PLACES[at].lng,
    endAddress: PLACES[at].address
  };
}

export function fixtureTimeline(): HistoryTimeline {
  const segments: HistoryTimelineSegment[] = [
    // Thu: parked all day, until Friday 7:48 AM.
    standstill('s1', 0, 0, 1440 + 468, 'pasrur'),
    // Fri: the mockup's day.
    trip('t1', 1, 468, 511, 'pasrur', 'kalaswala', 12.6, 71),
    standstill('s2', 1, 511, 532, 'kalaswala'),
    trip('t2', 1, 532, 587, 'kalaswala', 'kotli', 14.1, 64, {
      briefStops: [
        { startTime: fixtureIso(1, 540), endTime: fixtureIso(1, 542), durationSeconds: 120, latitude: 32.2, longitude: 74.66 },
        { startTime: fixtureIso(1, 560), endTime: fixtureIso(1, 563), durationSeconds: 180, latitude: 32.25, longitude: 74.7 }
      ]
    }),
    standstill('s3', 1, 587, 626, 'kotli'),
    trip('t3', 1, 626, 679, 'kotli', 'pasrur', 13.2, 68),
    standstill('s4', 1, 679, 965, 'pasrur'),
    trip('t4', 1, 965, 1032, 'pasrur', 'talwandi', 11.3, 90, {
      briefStops: [
        { startTime: fixtureIso(1, 990), endTime: fixtureIso(1, 994), durationSeconds: 240, latitude: 32.24, longitude: 74.67 }
      ],
      noSignal: [
        {
          startTime: fixtureIso(1, 965),
          endTime: fixtureIso(1, 971),
          startLatitude: PLACES['pasrur'].lat,
          startLongitude: PLACES['pasrur'].lng,
          endLatitude: 32.25,
          endLongitude: 74.668,
          distanceKm: 1.4
        }
      ]
    }),
    standstill('s5', 1, 1032, 1060, 'talwandi'),
    trip('t5', 1, 1060, 1103, 'talwandi', 'pasrur', 7.1, 52),
    // Parked from Fri 6:23 PM to Sat 8:10 AM: listed under Friday.
    standstill('s6', 1, 1103, 1440 + 490, 'pasrur'),
    // Sat.
    trip('t6', 2, 490, 520, 'pasrur', 'kalaswala', 10, 60),
    standstill('s7', 2, 520, 720, 'kalaswala')
  ];

  return {
    totals: {
      distanceKm: 68.3,
      drivingMinutes: 291,
      stoppedMinutes: 3309,
      windowMinutes: 3600,
      tripCount: 6,
      stopCount: 7,
      parkingCount: 4,
      maxSpeedKmh: 90,
      avgSpeedKmh: 14.1,
      noSignalMinutes: 6
    },
    days: [
      {
        date: DATES[0],
        startTime: fixtureIso(0, 0),
        endTime: fixtureIso(1, 0),
        distanceKm: 0,
        drivingMinutes: 0,
        tripCount: 0,
        stopCount: 1,
        parkingCount: 1
      },
      {
        date: DATES[1],
        startTime: fixtureIso(1, 0),
        endTime: fixtureIso(2, 0),
        distanceKm: 58.3,
        drivingMinutes: 261,
        tripCount: 5,
        stopCount: 5,
        parkingCount: 2
      },
      {
        date: DATES[2],
        startTime: fixtureIso(2, 0),
        endTime: fixtureIso(2, 720),
        distanceKm: 10,
        drivingMinutes: 30,
        tripCount: 1,
        stopCount: 1,
        parkingCount: 1
      }
    ],
    segments,
    minStopMinutes: 5,
    parkingMinutes: 120
  };
}

/**
 * A thinned route for the fixture: every trip is a straight line between its two places with a fix
 * every few minutes, speed rising to the trip's maximum in the middle; standstills hold one fix.
 */
export function fixtureRoute(timeline: HistoryTimeline = fixtureTimeline()): TripReplayPosition[] {
  const route: TripReplayPosition[] = [];
  for (const s of timeline.segments) {
    const start = new Date(s.startTime).getTime();
    const end = new Date(s.endTime).getTime();
    if (s.kind !== 'trip') {
      route.push(fix(start + 60_000, s.startLatitude, s.startLongitude, 0));
      continue;
    }
    const steps = 6;
    for (let i = 0; i <= steps; i++) {
      const f = i / steps;
      const speed = i === 0 || i === steps ? 0 : (Number(s.maxSpeedKmh) || 0) * (i === steps / 2 ? 1 : 0.7);
      route.push(fix(
        start + (end - start) * f,
        s.startLatitude + (s.endLatitude - s.startLatitude) * f,
        s.startLongitude + (s.endLongitude - s.startLongitude) * f,
        speed
      ));
    }
  }
  return route.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
}

function fix(ms: number, latitude: number, longitude: number, speedKmh: number): TripReplayPosition {
  return { timestamp: new Date(ms).toISOString(), latitude, longitude, speedKmh };
}
