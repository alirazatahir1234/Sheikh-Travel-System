import { GpsAlertEvent } from '../../../core/models/gps-tracking.model';

export const GPS_ALERT_EVENT_TYPES = [
  'sos',
  'speed_exceeded',
  'road_speed_exceeded',
  'geofence_enter',
  'geofence_exit',
  'offline',
  'online',
  'ignition_on',
  'ignition_off',
  'low_battery',
  'power_cut',
  'fuel_station_stop',
  'fuel_fraud'
] as const;

/** Collapse stored synonyms so filter chips / badges match API EventType. */
export function normalizeGpsAlertEventType(raw: string | null | undefined): string {
  const k = (raw ?? '').trim().toLowerCase().replace(/[\s-]+/g, '_');
  switch (k) {
    case 'device_offline':
    case 'gps_offline':
    case 'vehicle_offline':
    case 'deviceoffline':
      return 'offline';
    case 'device_online':
    case 'vehicle_online':
    case 'gps_online':
    case 'deviceonline':
      return 'online';
    case 'ignitionon':
      return 'ignition_on';
    case 'ignitionoff':
      return 'ignition_off';
    case 'geofenceenter':
      return 'geofence_enter';
    case 'geofenceexit':
      return 'geofence_exit';
    case 'overspeed':
      return 'speed_exceeded';
    default:
      return k;
  }
}

export type GpsAlertLifecycleTab = 'open' | 'acknowledged' | 'resolved';

export interface GpsAlertDisplayGroup {
  key: string;
  representative: GpsAlertEvent;
  events: GpsAlertEvent[];
  count: number;
}

export interface GpsAlertQuietHours {
  enabled: boolean;
  start: string;
  end: string;
}

const SNOOZE_STORAGE = 'sheikhgo.gps-alert-snooze.v1';
const MUTE_TYPES_STORAGE = 'sheikhgo.gps-alert-muted-types.v1';
const QUIET_HOURS_STORAGE = 'sheikhgo.gps-alert-quiet-hours.v1';

export function alertRowKey(event: GpsAlertEvent): string {
  return `${event.tenantId ?? 0}:${event.id}`;
}

export function severityRank(severity?: string | null): number {
  switch ((severity ?? 'medium').toLowerCase()) {
    case 'critical': return 0;
    case 'high': return 1;
    case 'medium': return 2;
    case 'low': return 3;
    default: return 4;
  }
}

/** Newest first by time, then higher severity. Keeps WHEN column chronological. */
export function sortAlertsBySeverity(events: GpsAlertEvent[]): GpsAlertEvent[] {
  return [...events].sort((a, b) => {
    const timeDiff = new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime();
    if (timeDiff !== 0) return timeDiff;
    return severityRank(a.severity) - severityRank(b.severity);
  });
}

export function groupSimilarAlerts(events: GpsAlertEvent[]): GpsAlertDisplayGroup[] {
  const buckets = new Map<string, GpsAlertEvent[]>();
  for (const e of events) {
    const hour = new Date(e.timestamp);
    hour.setMinutes(0, 0, 0);
    const key = `${e.vehicleId}|${e.eventType}|${hour.toISOString()}`;
    const list = buckets.get(key) ?? [];
    list.push(e);
    buckets.set(key, list);
  }
  const groups: GpsAlertDisplayGroup[] = [];
  for (const [key, list] of buckets) {
    const sorted = sortAlertsBySeverity(list);
    groups.push({
      key,
      representative: sorted[0],
      events: sorted,
      count: sorted.length
    });
  }
  return groups.sort(
    (a, b) =>
      new Date(b.representative.timestamp).getTime() - new Date(a.representative.timestamp).getTime()
      || severityRank(a.representative.severity) - severityRank(b.representative.severity)
  );
}

export function readSnoozeMap(): Record<string, string> {
  try {
    const raw = localStorage.getItem(SNOOZE_STORAGE);
    return raw ? JSON.parse(raw) as Record<string, string> : {};
  } catch {
    return {};
  }
}

export function writeSnoozeMap(map: Record<string, string>): void {
  localStorage.setItem(SNOOZE_STORAGE, JSON.stringify(map));
}

export function snoozeAlert(event: GpsAlertEvent, hours: number): void {
  const map = readSnoozeMap();
  map[alertRowKey(event)] = new Date(Date.now() + hours * 3600_000).toISOString();
  writeSnoozeMap(map);
}

export function isSnoozed(event: GpsAlertEvent, map: Record<string, string>): boolean {
  const until = map[alertRowKey(event)];
  if (!until) return false;
  return new Date(until).getTime() > Date.now();
}

export function readMutedEventTypes(): Set<string> {
  try {
    const raw = localStorage.getItem(MUTE_TYPES_STORAGE);
    const arr = raw ? JSON.parse(raw) as string[] : [];
    return new Set(arr.map(t => t.toLowerCase()));
  } catch {
    return new Set();
  }
}

export function writeMutedEventTypes(types: Set<string>): void {
  localStorage.setItem(MUTE_TYPES_STORAGE, JSON.stringify([...types]));
}

export function readQuietHours(): GpsAlertQuietHours {
  try {
    const raw = localStorage.getItem(QUIET_HOURS_STORAGE);
    if (raw) return JSON.parse(raw) as GpsAlertQuietHours;
  } catch { /* ignore */ }
  return { enabled: false, start: '22:00', end: '07:00' };
}

export function writeQuietHours(prefs: GpsAlertQuietHours): void {
  localStorage.setItem(QUIET_HOURS_STORAGE, JSON.stringify(prefs));
}

export function isQuietHoursNow(prefs: GpsAlertQuietHours, now = new Date()): boolean {
  if (!prefs.enabled) return false;
  const [sh, sm] = prefs.start.split(':').map(Number);
  const [eh, em] = prefs.end.split(':').map(Number);
  const mins = now.getHours() * 60 + now.getMinutes();
  const start = sh * 60 + (sm || 0);
  const end = eh * 60 + (em || 0);
  if (start <= end) return mins >= start && mins < end;
  return mins >= start || mins < end;
}

export function isSosEvent(event: GpsAlertEvent): boolean {
  return (event.eventType ?? '').toLowerCase().includes('sos');
}

export function needsEscalation(event: GpsAlertEvent, thresholdMinutes = 15): boolean {
  if ((event.severity ?? '').toLowerCase() !== 'critical') return false;
  if (event.status === 'resolved' || event.status === 'archived') return false;
  if (event.isAcknowledged || event.acknowledgedAt) return false;
  const ageMs = Date.now() - new Date(event.timestamp).getTime();
  return ageMs >= thresholdMinutes * 60_000;
}

export function alertsToCsv(events: GpsAlertEvent[]): string {
  const header = [
    'Id', 'Company', 'Type', 'Severity', 'Status', 'Plate', 'Vehicle', 'Driver',
    'SpeedKmh', 'Message', 'Timestamp', 'Lat', 'Lng'
  ];
  const rows = events.map(e => [
    e.id,
    e.tenantName ?? e.tenantId ?? '',
    e.eventType,
    e.severity ?? '',
    e.status ?? '',
    e.registrationNumber ?? '',
    e.vehicleName ?? e.vehicleId,
    e.driverName ?? '',
    e.speed,
    (e.message ?? '').replace(/"/g, '""'),
    e.timestamp,
    e.latitude,
    e.longitude
  ].map(v => `"${String(v ?? '')}"`).join(','));
  return [header.join(','), ...rows].join('\n');
}

export function downloadTextFile(filename: string, content: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
