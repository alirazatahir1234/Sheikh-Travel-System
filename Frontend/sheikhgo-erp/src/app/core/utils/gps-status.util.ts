import { FleetTrackStatus } from '../models/gps-tracking.model';
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
const OFFLINE_STALE_MS = 30 * 60 * 1000;

/**
 * Moving threshold (km/h). Aligned with TraccarOptions.MovingSpeedKmh.
 * Values at or below this with ignition OFF are treated as GPS drift → Parked.
 */
export const MOVING_THRESHOLD_KMH = 10;

export function isSosAlarm(alarmType?: string | null, sosValues: string[] = DEFAULT_SOS_ALARM_VALUES): boolean {
  if (!alarmType) return false;
  return sosValues.some(v => v.toLowerCase() === alarmType.toLowerCase());
}

/**
 * Single source of truth for deriving a live-map vehicle's status from telemetry.
 *
 * Priority (when online):
 * 1. SOS alarm
 * 2. Ignition explicitly OFF → Parked (GPS drift 1–9 km/h must not become Moving)
 * 3. Speed >= 10 km/h → Moving (ignition ON or unknown / unwired ACC)
 * 4. Ignition ON → Idle
 * 5. Otherwise → Idle (unknown ignition, low speed)
 */
export function resolveFleetStatus(input: GpsStatusInput, nowMs: number = Date.now()): FleetTrackStatus {
  if (isSosAlarm(input.alarmType)) {
    return 'sos';
  }

  if (!input.hasGps || !input.lastUpdated) {
    return 'never_seen';
  }

  const ageMs = nowMs - parseGpsTimestamp(input.lastUpdated);
  if (!Number.isFinite(ageMs) || ageMs > OFFLINE_STALE_MS) {
    return 'offline';
  }

  const speed = Number(input.speed) || 0;

  // Explicit ACC OFF: always Parked — Jimi VG03 / Traccar often report 1–5 km/h drift at rest.
  if (input.ignition === false) {
    return 'parked';
  }

  if (speed >= MOVING_THRESHOLD_KMH) {
    return 'moving';
  }

  if (input.ignition === true) {
    return 'idle';
  }

  return 'idle';
}
