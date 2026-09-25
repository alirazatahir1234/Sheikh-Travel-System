import { FleetTrackStatus, VehicleLocation } from '../models/gps-tracking.model';
import { parseGpsTimestamp } from './gps-timestamp.util';

export interface GpsStatusInput {
  speed?: number | null;
  ignition?: boolean | null;
  /** ISO timestamp of the most recent telemetry ever received for this vehicle, if any. */
  lastUpdated?: string | null;
  /** False when the vehicle has no GPS-capable tracker assigned at all. */
  hasGps?: boolean;
  alarmType?: string | null;
}

/** Mirrors the backend default in TraccarOptions.SosAlarmValues — keep in sync if that changes. */
export const DEFAULT_SOS_ALARM_VALUES = ['sos', 'panic'];

/** Matches the backend's IsOnline window (GetGpsDevicesQuery: LastSeenAt > now - 30min). */
export const OFFLINE_STALE_MS = 30 * 60 * 1000;

/**
 * Moving threshold (km/h). Aligned with TraccarOptions.MovingSpeedKmh.
 * Values below this with ignition OFF are treated as GPS drift → Parked.
 */
export const MOVING_THRESHOLD_KMH = 10;

export interface FleetStatusCounts {
  total: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  unknown: number;
  neverSeen: number;
  sos: number;
  delayed: number;
  scheduled: number;
}

export function isSosAlarm(alarmType?: string | null, sosValues: string[] = DEFAULT_SOS_ALARM_VALUES): boolean {
  if (!alarmType) return false;
  return sosValues.some(v => v.toLowerCase() === alarmType.toLowerCase());
}

/**
 * Single source of truth for deriving a live-map vehicle's status from telemetry.
 *
 * Connectivity first, then operational (when online):
 * 1. never_seen — no GPS / no last update
 * 2. offline — last fix older than offline threshold
 * 3. sos — SOS/panic while otherwise online
 * 4. parked — ignition OFF + speed &lt; moving threshold (covers speed 0 + low GPS drift)
 * 5. unknown — ignition OFF + speed ≥ threshold (contradictory), or ignition null + low speed
 * 6. moving — speed ≥ moving threshold
 * 7. idle — ignition ON + speed &lt; threshold
 *
 * Do not derive status from reverse-geocoded address text.
 */
export function resolveFleetStatus(input: GpsStatusInput, nowMs: number = Date.now()): FleetTrackStatus {
  if (!input.hasGps || !input.lastUpdated) {
    return 'never_seen';
  }

  const ageMs = nowMs - parseGpsTimestamp(input.lastUpdated);
  if (!Number.isFinite(ageMs) || ageMs > OFFLINE_STALE_MS) {
    return 'offline';
  }

  if (isSosAlarm(input.alarmType)) {
    return 'sos';
  }

  const speed = Number(input.speed) || 0;

  // Explicit ACC OFF + below moving threshold → Parked (incl. low GPS drift at rest).
  if (input.ignition === false && speed < MOVING_THRESHOLD_KMH) {
    return 'parked';
  }

  // Ignition OFF but reporting highway-like speed → contradictory telemetry.
  if (input.ignition === false && speed >= MOVING_THRESHOLD_KMH) {
    return 'unknown';
  }

  if (speed >= MOVING_THRESHOLD_KMH) {
    return 'moving';
  }

  if (input.ignition === true) {
    return 'idle';
  }

  // Ignition unwired / null with low speed — insufficient telemetry.
  return 'unknown';
}

/**
 * KPI / roster tallies from the same `status` field used by cards, markers, and the detail panel.
 * Counts every row — no `hasGps` gate (parked implies online telemetry already).
 */
export function tallyFleetStatusCounts(
  locations: ReadonlyArray<Pick<VehicleLocation, 'status'>>
): FleetStatusCounts {
  const counts: FleetStatusCounts = {
    total: locations.length,
    online: 0,
    offline: 0,
    moving: 0,
    idle: 0,
    parked: 0,
    unknown: 0,
    neverSeen: 0,
    sos: 0,
    delayed: 0,
    scheduled: 0
  };

  for (const loc of locations) {
    switch (loc.status) {
      case 'moving':
        counts.moving++;
        counts.online++;
        break;
      case 'idle':
        counts.idle++;
        counts.online++;
        break;
      case 'parked':
        counts.parked++;
        counts.online++;
        break;
      case 'unknown':
        counts.unknown++;
        counts.online++;
        break;
      case 'sos':
        counts.sos++;
        counts.online++;
        break;
      case 'offline':
        counts.offline++;
        break;
      case 'never_seen':
        counts.neverSeen++;
        break;
      case 'delayed':
        counts.delayed++;
        break;
      case 'scheduled':
        counts.scheduled++;
        break;
      default:
        break;
    }
  }

  return counts;
}
