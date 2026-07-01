import { GpsDevice } from '../../../core/models/gps-tracking.model';

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

const MOVING_THRESHOLD_KMH = 0;

export function resolveTrackerStatus(device: GpsDevice): TrackerStatusView {
  if (device.disabled || !device.isActive) {
    return status('disabled', 'Device Disabled', 'badge-gray', 'row-disabled');
  }

  if (!device.lastSeenAt) {
    return status('never_seen', 'Never Seen', 'badge-gray', 'row-never');
  }

  if (!device.isOnline) {
    return status('offline', 'Offline', 'badge-red', 'row-offline');
  }

  const speed = device.lastSpeed ?? 0;
  const ignition = device.lastIgnition;

  if (ignition == null) {
    if (speed > MOVING_THRESHOLD_KMH) {
      return status('moving', 'Moving', 'badge-blue', 'row-moving');
    }
    return status('waiting_telemetry', 'Waiting for telemetry', 'badge-gray', 'row-unknown');
  }

  if (ignition) {
    if (speed > MOVING_THRESHOLD_KMH) {
      return status('moving', 'Moving', 'badge-blue', 'row-moving');
    }
    return status('idle', 'Idle', 'badge-amber', 'row-idle');
  }

  if (speed <= MOVING_THRESHOLD_KMH) {
    return status('parked', 'Parked', 'badge-green', 'row-parked');
  }

  return status('stopped', 'Stopped', 'badge-gray', 'row-stopped');
}

export function isTrackerMoving(device: GpsDevice): boolean {
  return resolveTrackerStatus(device).key === 'moving';
}

export function isTrackerIdle(device: GpsDevice): boolean {
  return resolveTrackerStatus(device).key === 'idle';
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
  const status = normalizeInventoryStatus(device.currentStatus);
  return !device.vehicleId && (status === 'Available' || status === 'InStock');
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
  if (device.vehicleName) {
    return `Installed on ${device.vehicleName}`;
  }
  const status = normalizeInventoryStatus(device.currentStatus);
  if (status === 'Available') return 'Available';
  if (status === 'Installed' && !device.vehicleId) return 'Not assigned';
  return status;
}

export function assignmentBadgeClass(device: GpsDevice): string {
  if (device.vehicleName) return 'badge-green';
  if (isTrackerInInventory(device)) return 'badge-gray';
  return 'badge-amber';
}

export function vehicleDisplayLabel(device: GpsDevice): string {
  if (device.vehicleName) return device.vehicleName;
  if (device.vehicleId) return 'Vehicle not found';
  return 'No vehicle assigned';
}

export function traccarLinkHint(device: GpsDevice): string | null {
  if (device.isTraccarLinked) return null;
  return 'Synchronization required';
}

export function trackerBrandLabel(device: GpsDevice): string {
  return device.trackerBrandName || device.vendor || '—';
}

export function trackerModelLabel(device: GpsDevice): string {
  return device.modelName || device.model || '—';
}

export function ignitionDisplay(device: GpsDevice): { icon: string; label: string; className: string } {
  if (!device.lastSeenAt) {
    return { icon: 'remove', label: 'No data', className: 'ignition--none' };
  }
  if (device.lastIgnition === true) {
    return { icon: 'trip_origin', label: 'ON', className: 'ignition--on' };
  }
  if (device.lastIgnition === false) {
    return { icon: 'circle', label: 'OFF', className: 'ignition--off' };
  }
  return { icon: 'help_outline', label: 'Unknown', className: 'ignition--unknown' };
}

export function gsmSignalLabel(device: GpsDevice): string {
  if (device.lastRssi == null) {
    if (!device.lastSeenAt) return 'No data';
    if (!device.isOnline) return 'No signal';
    return 'No data';
  }
  if (device.lastRssi >= -70) return 'Strong';
  if (device.lastRssi >= -85) return 'Good';
  if (device.lastRssi >= -100) return 'Weak';
  return 'Poor';
}

export function batteryDisplayLabel(device: GpsDevice): string {
  if (device.lastBatteryLevel != null) {
    return `${Math.round(device.lastBatteryLevel)}%`;
  }
  if (!device.lastSeenAt) return 'No data';
  return 'No data';
}

export function formatLastSeenLabel(lastSeenAt: string | undefined, nowMs: number): string {
  if (!lastSeenAt) return 'Never';

  const when = new Date(lastSeenAt);
  const diff = nowMs - when.getTime();
  const mins = Math.floor(diff / 60_000);

  if (mins < 1) return 'Just now';
  if (mins < 60) return `${mins}m ago`;

  const today = new Date(nowMs);
  const isToday = when.toDateString() === today.toDateString();
  const time = when.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  if (isToday) return `Today ${time}`;

  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;

  const days = Math.floor(hrs / 24);
  if (days < 7) return `${days}d ago`;

  return when.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}

export function formatLastSeenTooltip(lastSeenAt: string | undefined): string {
  if (!lastSeenAt) return 'No telemetry received yet';
  const when = new Date(lastSeenAt);
  return when.toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit'
  });
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
