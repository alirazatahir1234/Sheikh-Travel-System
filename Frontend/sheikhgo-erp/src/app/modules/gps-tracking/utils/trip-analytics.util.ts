import { GpsTrip, TripAnalyticsSummary, TripEvent, TripAnalyticsBundle } from '../../../core/models/gps-tracking.model';

type SummaryWire = Partial<TripAnalyticsSummary> & {
  TripCount?: number;
  DistanceKm?: number;
  DrivingMinutes?: number;
  IdleMinutes?: number;
  FuelLiters?: number | null;
  AvgSpeedKmh?: number;
  MaxSpeedKmh?: number;
  StopCount?: number;
  OverspeedCount?: number;
  HarshBrakeCount?: number;
  HarshAccelCount?: number;
  EngineHours?: number | null;
};

type BundleWire = Partial<TripAnalyticsBundle> & {
  Summary?: SummaryWire;
  Events?: TripEvent[];
  Stops?: unknown[];
};

type TripWire = Partial<GpsTrip> & Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : fallback;
}

/** Normalizes API summary objects that may use PascalCase property names. */
export function normalizeTripAnalyticsSummary(raw: SummaryWire | null | undefined): TripAnalyticsSummary | null {
  if (!raw || typeof raw !== 'object') return null;
  return {
    tripCount: num(raw.tripCount ?? raw.TripCount, 0),
    distanceKm: num(raw.distanceKm ?? raw.DistanceKm, 0),
    drivingMinutes: num(raw.drivingMinutes ?? raw.DrivingMinutes, 0),
    idleMinutes: num(raw.idleMinutes ?? raw.IdleMinutes, 0),
    fuelLiters: raw.fuelLiters ?? raw.FuelLiters ?? null,
    avgSpeedKmh: num(raw.avgSpeedKmh ?? raw.AvgSpeedKmh, 0),
    maxSpeedKmh: num(raw.maxSpeedKmh ?? raw.MaxSpeedKmh, 0),
    stopCount: num(raw.stopCount ?? raw.StopCount, 0),
    overspeedCount: num(raw.overspeedCount ?? raw.OverspeedCount, 0),
    harshBrakeCount: num(raw.harshBrakeCount ?? raw.HarshBrakeCount, 0),
    harshAccelCount: num(raw.harshAccelCount ?? raw.HarshAccelCount, 0),
    engineHours: raw.engineHours ?? raw.EngineHours ?? null
  };
}

export function normalizeTripAnalyticsBundle(raw: BundleWire | null | undefined): TripAnalyticsBundle | null {
  if (!raw || typeof raw !== 'object') return null;
  const summary = normalizeTripAnalyticsSummary(raw.summary ?? raw.Summary);
  if (!summary) return null;
  return {
    summary,
    events: (raw.events ?? raw.Events ?? []) as TripEvent[],
    stops: (raw.stops ?? raw.Stops ?? []) as TripAnalyticsBundle['stops']
  };
}

/** Normalizes a GPS trip row from API (camelCase or PascalCase). */
export function normalizeGpsTrip(raw: TripWire): GpsTrip {
  const w = raw as Record<string, unknown>;
  const pick = <T>(camel: string, pascal: string, fallback: T): T =>
    (w[camel] ?? w[pascal] ?? fallback) as T;

  return {
    vehicleId: num(pick('vehicleId', 'VehicleId', 0), 0),
    vehicleName: pick<string | undefined>('vehicleName', 'VehicleName', undefined),
    gpsDeviceId: pick<number | undefined>('gpsDeviceId', 'GpsDeviceId', undefined),
    deviceName: pick<string | undefined>('deviceName', 'DeviceName', undefined),
    startTime: String(pick('startTime', 'StartTime', '')),
    endTime: String(pick('endTime', 'EndTime', '')),
    distanceKm: num(pick('distanceKm', 'DistanceKm', 0), 0),
    avgSpeedKmh: num(pick('avgSpeedKmh', 'AvgSpeedKmh', 0), 0),
    maxSpeedKmh: num(pick('maxSpeedKmh', 'MaxSpeedKmh', 0), 0),
    durationMinutes: num(pick('durationMinutes', 'DurationMinutes', 0), 0),
    startAddress: pick<string | null>('startAddress', 'StartAddress', null),
    endAddress: pick<string | null>('endAddress', 'EndAddress', null),
    startLatitude: pick<number | null>('startLatitude', 'StartLatitude', null),
    startLongitude: pick<number | null>('startLongitude', 'StartLongitude', null),
    endLatitude: pick<number | null>('endLatitude', 'EndLatitude', null),
    endLongitude: pick<number | null>('endLongitude', 'EndLongitude', null),
    driverName: pick<string | null>('driverName', 'DriverName', null),
    fuelLiters: pick<number | null>('fuelLiters', 'FuelLiters', null),
    plateNumber: pick<string | null>('plateNumber', 'PlateNumber', null),
    tripKey: pick<string | null>('tripKey', 'TripKey', null),
    status: pick<string | null>('status', 'Status', null)
  };
}

const MOVING_MIN_DISTANCE_KM = 0.2;

function isMovingTrip(t: GpsTrip): boolean {
  return (t.distanceKm ?? 0) >= MOVING_MIN_DISTANCE_KM && t.status !== 'Stop';
}

function countOverspeedEvents(events: TripEvent[], fromMs?: number, toMs?: number): number {
  return events.filter(e => {
    const t = new Date(e.time).getTime();
    if (fromMs != null && t < fromMs) return false;
    if (toMs != null && t > toMs) return false;
    const type = (e.type ?? '').toLowerCase();
    return type.includes('overspeed') || type.includes('speed_exceeded') || type.includes('road_speed');
  }).length;
}

/** Build period KPIs from trip rows when analytics API is slow or unavailable. */
export function aggregateTripAnalyticsFromTrips(
  trips: GpsTrip[],
  totalCount: number,
  events: TripEvent[] = []
): TripAnalyticsSummary {
  const moving = trips.filter(isMovingTrip);
  const distanceKm = moving.reduce((s, t) => s + (Number(t.distanceKm) || 0), 0);
  const drivingMinutes = moving.reduce((s, t) => s + (Number(t.durationMinutes) || 0), 0);
  const maxSpeedKmh = moving.length ? Math.max(...moving.map(t => Number(t.maxSpeedKmh) || 0)) : 0;
  const avgSpeedKmh = moving.length
    ? moving.reduce((s, t) => s + (Number(t.avgSpeedKmh) || 0), 0) / moving.length
    : 0;
  const fuelLiters = trips.reduce((s, t) => s + (Number(t.fuelLiters) || 0), 0);

  return {
    tripCount: totalCount > 0 ? totalCount : trips.length,
    distanceKm: Math.round(distanceKm * 100) / 100,
    drivingMinutes,
    idleMinutes: 0,
    fuelLiters: fuelLiters > 0 ? Math.round(fuelLiters * 10) / 10 : null,
    avgSpeedKmh: Math.round(avgSpeedKmh * 10) / 10,
    maxSpeedKmh: Math.round(maxSpeedKmh * 10) / 10,
    stopCount: trips.filter(t => !isMovingTrip(t)).length,
    overspeedCount: countOverspeedEvents(events),
    harshBrakeCount: 0,
    harshAccelCount: 0,
    engineHours: drivingMinutes > 0 ? Math.round((drivingMinutes / 60) * 10) / 10 : null
  };
}

export function summaryForSelectedTrip(trip: GpsTrip, events: TripEvent[]): TripAnalyticsSummary {
  const moving = isMovingTrip(trip);
  const fromMs = new Date(trip.startTime).getTime();
  const toMs = new Date(trip.endTime).getTime();
  const periodEvents = events.filter(e => {
    const t = new Date(e.time).getTime();
    return t >= fromMs && t <= toMs;
  });

  return {
    tripCount: 1,
    distanceKm: Number(trip.distanceKm) || 0,
    drivingMinutes: Number(trip.durationMinutes) || 0,
    idleMinutes: 0,
    fuelLiters: trip.fuelLiters ?? null,
    avgSpeedKmh: Number(trip.avgSpeedKmh) || 0,
    maxSpeedKmh: Number(trip.maxSpeedKmh) || 0,
    stopCount: moving ? 0 : 1,
    overspeedCount: countOverspeedEvents(periodEvents),
    harshBrakeCount: 0,
    harshAccelCount: 0,
    engineHours:
      (Number(trip.durationMinutes) || 0) > 0
        ? Math.round((Number(trip.durationMinutes) / 60) * 10) / 10
        : null
  };
}

/** Prefer API summary; fill gaps from trip-list aggregation (same date range). */
export function mergeTripAnalyticsSummaries(
  primary: TripAnalyticsSummary | null,
  fallback: TripAnalyticsSummary | null
): TripAnalyticsSummary | null {
  if (!primary && !fallback) return null;
  if (!primary) return fallback;
  if (!fallback) return primary;

  const drivingMinutes = primary.drivingMinutes > 0 ? primary.drivingMinutes : fallback.drivingMinutes;
  const engineHours =
    primary.engineHours != null && primary.engineHours > 0
      ? primary.engineHours
      : fallback.engineHours ?? (drivingMinutes > 0 ? Math.round((drivingMinutes / 60) * 10) / 10 : null);

  return {
    tripCount: primary.tripCount > 0 ? primary.tripCount : fallback.tripCount,
    distanceKm: primary.distanceKm > 0 ? primary.distanceKm : fallback.distanceKm,
    drivingMinutes,
    idleMinutes: primary.idleMinutes > 0 ? primary.idleMinutes : fallback.idleMinutes,
    fuelLiters: primary.fuelLiters ?? fallback.fuelLiters,
    avgSpeedKmh: primary.avgSpeedKmh > 0 ? primary.avgSpeedKmh : fallback.avgSpeedKmh,
    maxSpeedKmh: primary.maxSpeedKmh > 0 ? primary.maxSpeedKmh : fallback.maxSpeedKmh,
    stopCount: primary.stopCount > 0 ? primary.stopCount : fallback.stopCount,
    overspeedCount: Math.max(primary.overspeedCount, fallback.overspeedCount),
    harshBrakeCount: Math.max(primary.harshBrakeCount, fallback.harshBrakeCount),
    harshAccelCount: Math.max(primary.harshAccelCount, fallback.harshAccelCount),
    engineHours
  };
}

export function formatTripDistanceCell(km: number | null | undefined): string {
  const n = Number(km);
  if (!Number.isFinite(n)) return '—';
  return n.toFixed(1);
}

export function formatTripTravelSpeedCell(km: number | null | undefined, trip?: GpsTrip): string {
  if (trip && !isMovingTrip(trip)) return '—';
  const n = Number(km);
  if (!Number.isFinite(n) || n <= 0) return '—';
  return `${Math.round(n)} km/h`;
}
