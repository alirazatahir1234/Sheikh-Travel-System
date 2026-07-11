import { TripEvent } from '../../../core/models/gps-tracking.model';

export interface TripEventView {
  time: string;
  type: string;
  label: string;
  address?: string | null;
  speedKmh?: number | null;
  icon: string;
  tone: 'green' | 'red' | 'blue' | 'amber' | 'gray';
}

export function mapTripEvent(evt: TripEvent): TripEventView {
  const type = evt.type.toLowerCase();
  if (type.includes('ignitionon') || type.includes('ignition on')) {
    return { ...evt, label: 'Trip started', icon: 'play_circle', tone: 'green' };
  }
  if (type.includes('ignitionoff') || type.includes('ignition off')) {
    return { ...evt, label: 'Trip ended', icon: 'stop_circle', tone: 'gray' };
  }
  if (type.includes('overspeed') || type.includes('speed')) {
    return {
      ...evt,
      label: evt.speedKmh != null ? `Overspeeding — ${Math.round(evt.speedKmh)} km/h` : 'Overspeeding alert',
      icon: 'speed',
      tone: 'red'
    };
  }
  if (type.includes('geofenceenter')) {
    return { ...evt, label: 'Geofence entry', icon: 'fence', tone: 'blue' };
  }
  if (type.includes('geofenceexit')) {
    return { ...evt, label: 'Geofence exit', icon: 'logout', tone: 'blue' };
  }
  if (type.includes('brak')) {
    return { ...evt, label: 'Harsh braking', icon: 'warning', tone: 'amber' };
  }
  if (type.includes('accel')) {
    return { ...evt, label: 'Harsh acceleration', icon: 'bolt', tone: 'amber' };
  }
  if (type.includes('stop') || type.includes('idle')) {
    return { ...evt, label: 'Idle stop', icon: 'pause_circle', tone: 'amber' };
  }
  return { ...evt, label: evt.type, icon: 'info', tone: 'gray' };
}

export function computeSafetyScore(
  overspeed: number,
  harshBrake: number,
  harshAccel: number
): number {
  const score = 100 - overspeed * 8 - harshBrake * 5 - harshAccel * 5;
  return Math.max(0, Math.min(100, Math.round(score)));
}

export function safetyMessage(score: number): string {
  if (score >= 85) return 'Excellent safety behavior overall. Minor events may still appear on highways.';
  if (score >= 70) return 'Good driving with some overspeed or harsh events detected.';
  if (score >= 50) return 'Several safety events detected. Review driver coaching.';
  return 'Multiple safety alerts detected during this period.';
}

export function formatTripEventType(type: string): string {
  return mapTripEvent({ time: '', type, latitude: null, longitude: null, address: null, speedKmh: null }).label;
}

export function eventIconsForTrip(events: TripEvent[], start: string, end: string): string[] {
  const startMs = new Date(start).getTime();
  const endMs = new Date(end).getTime();
  return events
    .filter(e => {
      const t = new Date(e.time).getTime();
      return t >= startMs && t <= endMs;
    })
    .slice(0, 4)
    .map(e => mapTripEvent(e).icon);
}
