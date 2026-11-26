import { VehicleListItem } from '../../../core/models/vehicle.model';
import { VehicleLocation } from '../../../core/models/gps-tracking.model';
import { MOVING_THRESHOLD_KMH } from './tracker-status.util';
import { formatInZone } from '../../../core/utils/zoned-time.util';

export const HISTORY_STATIONARY_FILTER_MINUTES = 60;
export const HISTORY_STATIONARY_ALERT_MINUTES = 24 * 60;
export const HISTORY_COMPARE_MAX_VEHICLES = 4;
export const HISTORY_PREFS_KEY = 'gps_history_page_prefs';
export const HISTORY_ALERT_NOTIFIED_KEY = 'gps_history_stationary_24h_notified';

export interface HistoryPagePrefs {
  stationaryOverOneHour: boolean;
  hideRecordsBefore: string | null;
  alertStationary24h: boolean;
}

export const DEFAULT_HISTORY_PREFS: HistoryPagePrefs = {
  stationaryOverOneHour: false,
  hideRecordsBefore: null,
  alertStationary24h: false
};

export function readHistoryPagePrefs(): HistoryPagePrefs {
  try {
    const raw = localStorage.getItem(HISTORY_PREFS_KEY);
    if (!raw) return { ...DEFAULT_HISTORY_PREFS };
    return { ...DEFAULT_HISTORY_PREFS, ...(JSON.parse(raw) as HistoryPagePrefs) };
  } catch {
    return { ...DEFAULT_HISTORY_PREFS };
  }
}

export function storeHistoryPagePrefs(prefs: HistoryPagePrefs): void {
  localStorage.setItem(HISTORY_PREFS_KEY, JSON.stringify(prefs));
}

/**
 * "Notify me when a vehicle is stationary > 24h" is a per-browser preference, set from
 * Alerts → Rules. GPS History reads it when it refreshes live positions.
 */
export function readStationary24hAlertPref(): boolean {
  return readHistoryPagePrefs().alertStationary24h;
}

export function storeStationary24hAlertPref(enabled: boolean): void {
  try {
    storeHistoryPagePrefs({ ...readHistoryPagePrefs(), alertStationary24h: enabled });
  } catch {
    /* storage unavailable (private window) — the preference just does not persist */
  }
}

export function resolveLastKnownIso(
  vehicle: VehicleListItem,
  live?: VehicleLocation | null
): string | null {
  return (
    live?.lastUpdated ??
    vehicle.locationLastUpdate ??
    vehicle.gpsLastSeenAt ??
    null
  );
}

export function resolveLastKnownSpeed(
  vehicle: VehicleListItem,
  live?: VehicleLocation | null
): number | null {
  if (live != null && Number.isFinite(live.speed)) return live.speed;
  return null;
}

export function isVehicleStationaryNow(
  vehicle: VehicleListItem,
  live: VehicleLocation | undefined,
  nowMs: number
): boolean {
  const speed = resolveLastKnownSpeed(vehicle, live);
  const status = live?.status;
  const lowSpeed = speed == null ? true : speed < MOVING_THRESHOLD_KMH;
  const parkedLike =
    status === 'parked' ||
    status === 'idle' ||
    status === 'offline';
  if (!lowSpeed && !parkedLike) return false;

  const iso = resolveLastKnownIso(vehicle, live);
  if (!iso) return false;
  const ageMin = (nowMs - new Date(iso).getTime()) / 60_000;
  return ageMin >= HISTORY_STATIONARY_FILTER_MINUTES;
}

export function stationaryMinutes(
  vehicle: VehicleListItem,
  live: VehicleLocation | undefined,
  nowMs: number
): number {
  const iso = resolveLastKnownIso(vehicle, live);
  if (!iso) return 0;
  const speed = resolveLastKnownSpeed(vehicle, live);
  const lowSpeed = speed == null ? true : speed < MOVING_THRESHOLD_KMH;
  if (!lowSpeed) return 0;
  return Math.max(0, Math.floor((nowMs - new Date(iso).getTime()) / 60_000));
}

export function formatStalenessLabel(iso: string | null, nowMs: number): string {
  if (!iso) return 'No location yet';
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return 'Unknown';
  const diffMin = Math.floor((nowMs - at.getTime()) / 60_000);
  const clock = formatInZone(at, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit'
  });
  if (diffMin < 1) return `${clock} · just now`;
  if (diffMin < 60) return `${clock} · ${diffMin}m ago`;
  const h = Math.floor(diffMin / 60);
  if (h < 48) return `${clock} · ${h}h ago`;
  const d = Math.floor(h / 24);
  return `${clock} · ${d}d ago`;
}

export function isOfflineOrCachedTracker(
  vehicle: VehicleListItem,
  live: VehicleLocation | undefined,
  hasDevice: boolean
): boolean {
  if (!hasDevice) return true;
  if (vehicle.gpsOnline === false) return true;
  if (live?.isLive === false) return true;
  if (live && live.hasGps === false) return true;
  return false;
}

export function googleMapsUrl(lat: number, lng: number): string {
  return `https://www.google.com/maps?q=${lat},${lng}`;
}

export const COMPARE_ROUTE_COLORS = ['#9333ea', '#ea580c', '#0891b2', '#be123c'];
