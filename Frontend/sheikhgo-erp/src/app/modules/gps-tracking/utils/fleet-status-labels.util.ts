import { FleetTrackStatus } from '../../../core/models/gps-tracking.model';

export type FleetConnectivity = 'online' | 'offline' | 'never_seen';

/** Connectivity axis — separate from operational motion state. */
export function connectivityFromStatus(status: FleetTrackStatus): FleetConnectivity {
  if (status === 'never_seen') return 'never_seen';
  if (status === 'offline') return 'offline';
  return 'online';
}

export function connectivityLabel(status: FleetTrackStatus): string {
  switch (connectivityFromStatus(status)) {
    case 'never_seen':
      return 'Never Seen';
    case 'offline':
      return 'Offline';
    default:
      return 'Online';
  }
}

/** Operational motion / alarm state for UI badges (telemetry-derived). */
export function operationalLabel(status: FleetTrackStatus): string {
  switch (status) {
    case 'moving':
      return 'Moving';
    case 'idle':
      return 'Idle';
    case 'parked':
      return 'Parked';
    case 'unknown':
      return 'Unknown';
    case 'sos':
      return 'SOS';
    case 'scheduled':
      return 'Scheduled';
    case 'delayed':
      return 'Delayed';
    case 'offline':
      return 'Unknown';
    case 'never_seen':
      return 'Unknown';
    default:
      return 'Unknown';
  }
}

/** Combined card line: "Online • Parked". */
export function dualStatusLine(status: FleetTrackStatus): string {
  const conn = connectivityFromStatus(status);
  if (conn === 'never_seen') return 'Never Seen';
  if (conn === 'offline') return 'Offline';
  if (status === 'sos') return 'Online • SOS';
  if (status === 'unknown') return 'Online • Unknown';
  return `Online • ${operationalLabel(status)}`;
}
