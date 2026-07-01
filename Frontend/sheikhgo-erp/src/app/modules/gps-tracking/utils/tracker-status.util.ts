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
const IGNITION_INFER_THRESHOLD_KMH = 5;

export function resolveTrackerStatus(device: GpsDevice): TrackerStatusView {
  if (device.disabled || !device.isActive) {
    return status('disabled', 'Device Disabled', 'badge-gray', 'row-disabled');
  }

  if (!device.lastSeenAt) {
    return status('never_seen', 'Provisioned', 'badge-gray', 'row-never');
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
    return status('waiting_telemetry', 'Online', 'badge-teal', 'row-online');
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
  if (s === 'Available') return 'In Stock';
  if (s === 'Installed' && !device.vehicleId) return 'Unassigned';
  if (s === 'Maintenance') return 'Maintenance';
  return s;
}

export function assignmentTooltip(device: GpsDevice): string {
  if (device.vehicleName) return `Installed on ${device.vehicleName}`;
  const s = normalizeInventoryStatus(device.currentStatus);
  if (s === 'Available') return 'Available in inventory';
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
  // Normalize all-caps strings to title case
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
  // Infer ignition state from speed when protocol doesn't report it
  const speed = device.lastSpeed ?? 0;
  if (speed > IGNITION_INFER_THRESHOLD_KMH) {
    return { icon: 'trip_origin', label: 'ON (est.)', className: 'ignition--inferred' };
  }
  return { icon: 'help_outline', label: '—', className: 'ignition--unknown' };
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
  // Device is transmitting — it must have GSM connectivity, just no RSSI reported
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

export function batteryDisplayLabel(device: GpsDevice): string {
  if (device.lastBatteryLevel != null) {
    return `${Math.round(device.lastBatteryLevel)}%`;
  }
  if (!device.lastSeenAt) return '—';
  // Online with no battery data → likely a hardwired vehicle tracker
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
