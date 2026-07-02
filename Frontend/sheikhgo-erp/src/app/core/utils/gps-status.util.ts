import { FleetTrackStatus } from '../models/gps-tracking.model';

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

/** Matches the backend's moving/stopped threshold (GpsPositionIngestionHelper.MovingSpeedKmh). */
const MOVING_THRESHOLD_KMH = 5;

export function isSosAlarm(alarmType?: string | null, sosValues: string[] = DEFAULT_SOS_ALARM_VALUES): boolean {
  if (!alarmType) return false;
  return sosValues.some(v => v.toLowerCase() === alarmType.toLowerCase());
}

/**
 * Single source of truth for deriving a live-map vehicle's status from telemetry.
 * Matches the backend's already-correct GetGpsFleetStatusQuery rule: ignition OFF -> parked
 * (regardless of speed), ignition ON -> moving/idle by speed. Unlike that endpoint, an
 * unknown/unreported ignition value falls back to a speed-based moving/idle guess rather than
 * being treated as "off" — trackers that don't wire ignition sensing shouldn't show a moving
 * vehicle as a stationary "Parked" marker.
 */
export function resolveFleetStatus(input: GpsStatusInput, nowMs: number = Date.now()): FleetTrackStatus {
  if (isSosAlarm(input.alarmType)) {
    return 'sos';
  }

  if (!input.hasGps || !input.lastUpdated) {
    return 'never_seen';
  }

  const ageMs = nowMs - new Date(input.lastUpdated).getTime();
  if (!Number.isFinite(ageMs) || ageMs > OFFLINE_STALE_MS) {
    return 'offline';
  }

  const speed = input.speed ?? 0;

  if (input.ignition === false) {
    return 'parked';
  }

  return speed > MOVING_THRESHOLD_KMH ? 'moving' : 'idle';
}
