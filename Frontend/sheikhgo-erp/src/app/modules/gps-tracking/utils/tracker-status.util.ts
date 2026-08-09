import { GpsDevice } from '../../../core/models/gps-tracking.model';
import { MOVING_THRESHOLD_KMH, resolveFleetStatus } from '../../../core/utils/gps-status.util';

export type TrackerFleetStatus =
  | 'never_seen'
  | 'offline'
  | 'disabled'
  | 'moving'
  | 'idle'
  | 'parked'
  | 'stopped'
  | 'waiting_telemetry';

export interface TrackerStatusView {
  key: TrackerFleetStatus;
  label: string;
  badgeClass: string;
  rowClass: string;
}

/** Re-export so device screens stay aligned with live-map threshold. */
export { MOVING_THRESHOLD_KMH };

/** Telemetry must be this fresh to classify as currently moving. */
export const TELEMETRY_MOVING_MAX_AGE_MS = 2 * 60 * 1000;

/** When Last Seen exceeds this, status/ignition are shown as last-known, not live. */
export const TELEMETRY_STALE_MS = 15 * 60 * 1000;

const IGNITION_INFER_THRESHOLD_KMH = MOVING_THRESHOLD_KMH;

export function isTraccarReachable(connected: boolean | undefined | null): boolean {
  return connected === true;
}

export function telemetryAgeMs(device: GpsDevice, nowMs: number): number | null {
  if (!device.lastSeenAt) return null;
  const age = nowMs - new Date(device.lastSeenAt).getTime();
  return Number.isFinite(age) ? Math.max(0, age) : null;
}

/** True when telemetry cannot be treated as live (source offline or Last Seen too old). */
export function isTelemetryStale(
  device: GpsDevice,
  nowMs: number,
  traccarReachable: boolean
): boolean {
  if (!traccarReachable) return !!device.lastSeenAt;
  const age = telemetryAgeMs(device, nowMs);
  if (age == null) return false;
  return age > TELEMETRY_STALE_MS;
}

export function resolveDisplayTrackerStatus(
  device: GpsDevice,
  nowMs: number,
  traccarReachable: boolean
): TrackerStatusView {
  const base = resolveTrackerStatus(device, nowMs);
  if (!isTelemetryStale(device, nowMs, traccarReachable)) {
    return base;
  }

  if (!device.lastSeenAt) {
    return status('offline', 'Unknown', 'badge-gray', 'row-stale');
  }

  if (base.key === 'never_seen' || base.key === 'offline' || base.key === 'disabled') {
    return status(base.key, 'Unknown', 'badge-gray', 'row-stale');
  }

  return {
    ...base,
    label: `Last known: ${base.label}`,
    badgeClass: 'badge-gray',
    rowClass: 'row-stale',
  };
}

export function isFreshForMovement(device: GpsDevice, nowMs: number): boolean {
  const age = telemetryAgeMs(device, nowMs);
  if (age == null) return false;
  return age <= TELEMETRY_MOVING_MAX_AGE_MS;
}

export function isSpeedMoving(speed: number): boolean {
  return speed >= MOVING_THRESHOLD_KMH;
}

/**
 * Device-grid status — Moving/Idle/Parked aligned with {@link resolveFleetStatus}.
 */
export function resolveTrackerStatus(device: GpsDevice, nowMs?: number): TrackerStatusView {
  if (device.disabled || !device.isActive) {
    return status('disabled', 'Device Disabled', 'badge-gray', 'row-disabled');
  }

  if (!device.lastSeenAt) {
    return status('never_seen', 'Provisioned', 'badge-gray', 'row-never');
  }

  if (!device.isOnline) {
    return status('offline', 'Offline', 'badge-red', 'row-offline');
  }

  const clock = nowMs ?? Date.now();
  const fresh = nowMs == null || isFreshForMovement(device, clock);
  const fleetKey = resolveFleetStatus({
    speed: device.lastSpeed ?? 0,
    ignition: device.lastIgnition,
    lastUpdated: device.lastSeenAt,
    hasGps: true
  }, clock);

  // Stale high-speed samples: don't claim live Moving.
  if (fleetKey === 'moving' && !fresh) {
    return device.lastIgnition === false
      ? status('parked', 'Parked', 'badge-green', 'row-parked')
      : status('idle', 'Online', 'badge-teal', 'row-online');
  }

  switch (fleetKey) {
    case 'moving':
      return status('moving', 'Moving', 'badge-blue', 'row-moving');
    case 'parked':
      return status('parked', 'Parked', 'badge-green', 'row-parked');
    case 'idle':
      return status('idle', 'Idle', 'badge-amber', 'row-idle');
    case 'sos':
      return status('moving', 'SOS', 'badge-red', 'row-moving');
    default:
      return status('waiting_telemetry', 'Online', 'badge-teal', 'row-online');
  }
}

export function isTrackerMoving(device: GpsDevice, nowMs: number = Date.now()): boolean {
  return resolveTrackerStatus(device, nowMs).key === 'moving';
}

export function isTrackerIdle(device: GpsDevice, nowMs: number = Date.now()): boolean {
  return resolveTrackerStatus(device, nowMs).key === 'idle';
}

export function isTrackerOffline(device: GpsDevice): boolean {
  return resolveTrackerStatus(device).key === 'offline';
}

export function isTrackerNeverSeen(device: GpsDevice): boolean {
  return resolveTrackerStatus(device).key === 'never_seen';
}

export function isTrackerUnassigned(device: GpsDevice): boolean {
  return !device.vehicleId || !device.vehicleName;
}

export function isTrackerInInventory(device: GpsDevice): boolean {
  const s = normalizeInventoryStatus(device.currentStatus);
  return !device.vehicleId && (s === 'Available' || s === 'InStock');
}

export function isTrackerInstalled(device: GpsDevice): boolean {
  return !!device.vehicleId || normalizeInventoryStatus(device.currentStatus) === 'Installed';
}

export function normalizeInventoryStatus(status?: string): string {
  if (!status) return 'Available';
  if (status.toLowerCase() === 'instock') return 'Available';
  return status;
}

export function assignmentLabel(device: GpsDevice): string {
  if (device.vehicleName) return 'Installed';
  const s = normalizeInventoryStatus(device.currentStatus);
  if (s === 'Available') return 'Available';
  if (s === 'Installed' && !device.vehicleId) return 'Unassigned';
  if (s === 'Maintenance') return 'Maintenance';
  if (s === 'Removed') return 'Removed';
  return s;
}

export function assignmentTooltip(device: GpsDevice): string {
  if (device.vehicleName) return `Installed on ${device.vehicleName}`;
  const s = normalizeInventoryStatus(device.currentStatus);
  if (s === 'Available') return 'Available — ready to install';
  return assignmentLabel(device);
}

export function assignmentBadgeClass(device: GpsDevice): string {
  if (device.vehicleName) return 'badge-green';
  const s = normalizeInventoryStatus(device.currentStatus);
  if (s === 'Maintenance') return 'badge-amber';
  if (s === 'Installed' && !device.vehicleId) return 'badge-amber';
  return 'badge-gray';
}

export function vehicleDisplayLabel(device: GpsDevice): string {
  if (device.vehicleName) return device.vehicleName;
  if (device.vehicleId) return 'Vehicle not found';
  return '—';
}

export function traccarLinkHint(device: GpsDevice): string | null {
  if (device.isTraccarLinked) return null;
  return 'Synchronization required';
}

export function trackerBrandLabel(device: GpsDevice): string {
  const raw = device.trackerBrandName || device.vendor || '';
  if (!raw) return '—';
  if (raw === raw.toUpperCase() && raw.length > 2) {
    return raw.replace(/\w+/g, w => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase());
  }
  return raw;
}

export function trackerModelLabel(device: GpsDevice): string {
  return device.modelName || device.model || '—';
}

export function ignitionDisplay(device: GpsDevice): { icon: string; label: string; className: string } {
  if (!device.lastSeenAt) {
    return { icon: 'remove', label: '—', className: 'ignition--none' };
  }
  if (device.lastIgnition === true) {
    return { icon: 'trip_origin', label: 'ON', className: 'ignition--on' };
  }
  if (device.lastIgnition === false) {
    return { icon: 'circle', label: 'OFF', className: 'ignition--off' };
  }
  const speed = device.lastSpeed ?? 0;
  if (speed >= IGNITION_INFER_THRESHOLD_KMH) {
    return { icon: 'trip_origin', label: 'ON (est.)', className: 'ignition--inferred' };
  }
  return { icon: 'help_outline', label: '—', className: 'ignition--unknown' };
}

export function ignitionDisplayForView(
  device: GpsDevice,
  nowMs: number,
  traccarReachable: boolean
): { icon: string; label: string; className: string } {
  const base = ignitionDisplay(device);
  if (!isTelemetryStale(device, nowMs, traccarReachable)) {
    return base;
  }
  if (base.className === 'ignition--none') {
    return base;
  }
  return {
    icon: 'history',
    label: base.label === '—' ? 'Unknown' : `${base.label} (cached)`,
    className: 'ignition--stale',
  };
}

export function gsmSignalLabel(device: GpsDevice): string {
  if (device.lastRssi != null) {
    if (device.lastRssi >= -70) return 'Excellent';
    if (device.lastRssi >= -85) return 'Good';
    if (device.lastRssi >= -100) return 'Weak';
    return 'Poor';
  }
  if (!device.lastSeenAt) return '—';
  if (!device.isOnline) return 'No signal';
  return 'Active';
}

export function gsmSignalClass(device: GpsDevice): string {
  if (device.lastRssi != null) {
    if (device.lastRssi >= -70) return 'signal-excellent';
    if (device.lastRssi >= -85) return 'signal-good';
    if (device.lastRssi >= -100) return 'signal-weak';
    return 'signal-poor';
  }
  if (device.isOnline) return 'signal-active';
  return 'signal-none';
}

export function gsmSignalLabelForView(
  device: GpsDevice,
  nowMs: number,
  traccarReachable: boolean
): string {
  if (isTelemetryStale(device, nowMs, traccarReachable) && device.lastSeenAt) {
    return 'Cached';
  }
  return gsmSignalLabel(device);
}

export function gsmSignalClassForView(
  device: GpsDevice,
  nowMs: number,
  traccarReachable: boolean
): string {
  if (isTelemetryStale(device, nowMs, traccarReachable) && device.lastSeenAt) {
    return 'signal-cached';
  }
  return gsmSignalClass(device);
}

export function batteryDisplayLabel(device: GpsDevice): string {
  if (device.lastBatteryLevel != null) {
    return `${Math.round(device.lastBatteryLevel)}%`;
  }
  if (!device.lastSeenAt) return '—';
  if (device.isOnline) return 'Ext. Power';
  return '—';
}

export function formatLastSeenLabel(lastSeenAt: string | undefined, nowMs: number): string {
  if (!lastSeenAt) return 'Never';

  const when = new Date(lastSeenAt);
  const diff = nowMs - when.getTime();
  const mins = Math.floor(diff / 60_000);

  if (mins < 1)  return 'Just now';
  if (mins < 60) return `${mins}m ago`;

  const hrs = Math.floor(mins / 60);
  if (hrs < 24)  return `${hrs}h ago`;

  const days = Math.floor(hrs / 24);
  if (days === 1) return 'Yesterday';
  if (days < 7)   return `${days}d ago`;

  return when.toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

export function formatLastSeenTooltip(lastSeenAt: string | undefined): string {
  if (!lastSeenAt) return 'No telemetry received yet';
  const when = new Date(lastSeenAt);
  const date = when.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  const time = when.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  return `${date}  ${time}`;
}

export function deviceMatchesSearch(device: GpsDevice, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;

  const haystack = [
    device.vehicleName,
    device.plateNumber,
    device.driverName,
    device.model,
    device.modelName,
    device.trackerBrandName,
    device.vendor,
    device.uniqueId,
    device.name,
    device.serialNumber,
    device.simNumber
  ];

  return haystack.some(v => v?.toLowerCase().includes(q));
}

function status(
  key: TrackerFleetStatus,
  label: string,
  badgeClass: string,
  rowClass: string
): TrackerStatusView {
  return { key, label, badgeClass, rowClass };
}
