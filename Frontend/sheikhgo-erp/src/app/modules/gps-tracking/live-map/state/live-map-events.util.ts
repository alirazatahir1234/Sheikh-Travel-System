import { VehicleLocation } from '../../../../core/models/gps-tracking.model';

export interface LiveMapEvent {
  time: Date;
  message: string;
  type: 'info' | 'alert' | 'success' | 'warning';
  icon: string;
}

export function detectTelemetryEvents(prev: VehicleLocation, next: VehicleLocation): LiveMapEvent[] {
  const events: LiveMapEvent[] = [];
  const name = next.vehicleName;

  if (prev.status !== next.status) {
    events.push({
      time: new Date(),
      message: `${name} — ${formatStatus(prev.status)} → ${formatStatus(next.status)}`,
      type: next.status === 'sos' ? 'alert' : 'info',
      icon: next.status === 'sos' ? 'sos' : 'swap_horiz'
    });
  }

  if (prev.ignition !== next.ignition && next.ignition != null) {
    events.push({
      time: new Date(),
      message: `${name} — Ignition ${next.ignition ? 'ON' : 'OFF'}`,
      type: 'info',
      icon: next.ignition ? 'key' : 'key_off'
    });
  }

  if ((next.batteryLevel ?? 100) < 20 && (prev.batteryLevel ?? 100) >= 20) {
    events.push({
      time: new Date(),
      message: `${name} — Battery low (${next.batteryLevel}%)`,
      type: 'warning',
      icon: 'battery_alert'
    });
  }

  const prevSpeed = Number(prev.speed) || 0;
  const nextSpeed = Number(next.speed) || 0;
  if (nextSpeed > 100 && prevSpeed <= 100) {
    events.push({
      time: new Date(),
      message: `${name} — Overspeed (${Math.round(nextSpeed)} km/h)`,
      type: 'alert',
      icon: 'speed'
    });
  }

  if (next.alarmType && next.alarmType !== prev.alarmType) {
    events.push({
      time: new Date(),
      message: `${name} — Alarm: ${next.alarmType}`,
      type: 'alert',
      icon: 'warning'
    });
  }

  return events;
}

function formatStatus(status: string): string {
  return status.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}
