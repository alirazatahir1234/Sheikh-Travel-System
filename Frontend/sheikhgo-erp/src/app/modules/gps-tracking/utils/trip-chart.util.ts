import { ChartData } from 'chart.js';
import { GpsTrip, TripAnalyticsSummary } from '../../../core/models/gps-tracking.model';

const EMPTY_CHART: ChartData = { labels: [], datasets: [] };

function dayKey(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function dayLabel(key: string): string {
  const [y, m, d] = key.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
}

/** Buckets trips by calendar day (local time), sorted chronologically. */
function bucketTripsByDay(trips: GpsTrip[]): Map<string, GpsTrip[]> {
  const map = new Map<string, GpsTrip[]>();
  for (const t of trips) {
    const key = dayKey(t.startTime);
    const list = map.get(key);
    if (list) list.push(t);
    else map.set(key, [t]);
  }
  return new Map([...map.entries()].sort(([a], [b]) => a.localeCompare(b)));
}

export function distanceTrendData(trips: GpsTrip[]): ChartData {
  if (!trips.length) return EMPTY_CHART;
  const buckets = bucketTripsByDay(trips);
  return {
    labels: [...buckets.keys()].map(dayLabel),
    datasets: [{
      label: 'Distance (km)',
      data: [...buckets.values()].map(list => Math.round(list.reduce((sum, t) => sum + (Number(t.distanceKm) || 0), 0) * 10) / 10),
      borderColor: '#0f766e',
      backgroundColor: '#0f766e33',
      tension: 0.3,
      fill: true
    }]
  };
}

export function speedTrendData(trips: GpsTrip[]): ChartData {
  if (!trips.length) return EMPTY_CHART;
  const buckets = bucketTripsByDay(trips);
  return {
    labels: [...buckets.keys()].map(dayLabel),
    datasets: [{
      label: 'Avg speed (km/h)',
      data: [...buckets.values()].map(list => {
        const speeds = list.map(t => Number(t.avgSpeedKmh) || 0);
        return Math.round(speeds.reduce((a, b) => a + b, 0) / speeds.length);
      }),
      borderColor: '#2563eb',
      backgroundColor: '#2563eb33',
      tension: 0.3,
      fill: true
    }]
  };
}

export function tripsPerDayData(trips: GpsTrip[]): ChartData {
  if (!trips.length) return EMPTY_CHART;
  const buckets = bucketTripsByDay(trips);
  return {
    labels: [...buckets.keys()].map(dayLabel),
    datasets: [{
      label: 'Trips',
      data: [...buckets.values()].map(list => list.length),
      backgroundColor: '#7c3aed'
    }]
  };
}

export function drivingVsIdleData(summary: TripAnalyticsSummary | null): ChartData {
  if (!summary || (!summary.drivingMinutes && !summary.idleMinutes)) return EMPTY_CHART;
  return {
    labels: ['Driving', 'Idle'],
    datasets: [{
      data: [summary.drivingMinutes, summary.idleMinutes],
      backgroundColor: ['#0f766e', '#f59e0b']
    }]
  };
}

/**
 * Fuel Consumption chart — grouped per-vehicle (fleet-wide mode, where comparing vehicles is more
 * useful) or per-day (single-vehicle mode). Trips with no fuel figure are excluded rather than
 * counted as zero.
 */
export function fuelConsumptionData(trips: GpsTrip[], groupByVehicle: boolean): ChartData {
  const withFuel = trips.filter(t => t.fuelLiters != null);
  if (!withFuel.length) return EMPTY_CHART;

  if (groupByVehicle) {
    const byVehicle = new Map<string, number>();
    for (const t of withFuel) {
      const label = t.vehicleName ?? `Vehicle #${t.vehicleId}`;
      byVehicle.set(label, (byVehicle.get(label) ?? 0) + (Number(t.fuelLiters) || 0));
    }
    const entries = [...byVehicle.entries()].sort(([, a], [, b]) => b - a).slice(0, 12);
    return {
      labels: entries.map(([label]) => label),
      datasets: [{ label: 'Fuel (L)', data: entries.map(([, v]) => Math.round(v * 10) / 10), backgroundColor: '#059669' }]
    };
  }

  const buckets = bucketTripsByDay(withFuel);
  return {
    labels: [...buckets.keys()].map(dayLabel),
    datasets: [{
      label: 'Fuel (L)',
      data: [...buckets.values()].map(list => Math.round(list.reduce((sum, t) => sum + (Number(t.fuelLiters) || 0), 0) * 10) / 10),
      backgroundColor: '#059669'
    }]
  };
}
