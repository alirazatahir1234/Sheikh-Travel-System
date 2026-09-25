import { ChartData } from 'chart.js';
import { FleetHealthSummary, FleetVehicleHealth } from '../../../core/models/gps-tracking.model';

export interface FleetHealthBreakdown {
  optimal: number;
  healthy: number;
  attention: number;
  critical: number;
  unknown: number;
  total: number;
  /** Vehicles with enough data to score (excludes Unknown). */
  assessed: number;
  /** Average score among assessed vehicles; null when nothing assessed. */
  assessedPercent: number | null;
}

export interface FleetHealthReasonCount {
  reason: string;
  count: number;
}

const EMPTY_BREAKDOWN: FleetHealthBreakdown = {
  optimal: 0,
  healthy: 0,
  attention: 0,
  critical: 0,
  unknown: 0,
  total: 0,
  assessed: 0,
  assessedPercent: null
};

/** Map API summary → UI breakdown (no invented client scoring). */
export function breakdownFromSummary(summary: FleetHealthSummary | null | undefined): FleetHealthBreakdown {
  if (!summary) return { ...EMPTY_BREAKDOWN };
  return {
    optimal: summary.optimal ?? 0,
    healthy: summary.healthy ?? 0,
    attention: summary.attention ?? 0,
    critical: summary.critical ?? 0,
    unknown: summary.unknown ?? 0,
    total: summary.total ?? 0,
    assessed: summary.assessed ?? 0,
    assessedPercent: summary.assessedPercent ?? null
  };
}

/** Doughnut chart for Live Map Fleet Health card. */
export function fleetHealthChartData(breakdown: FleetHealthBreakdown): ChartData {
  return {
    labels: ['Optimal', 'Healthy', 'Attention', 'Critical', 'Unknown'],
    datasets: [{
      data: [
        breakdown.optimal,
        breakdown.healthy,
        breakdown.attention,
        breakdown.critical,
        breakdown.unknown
      ],
      backgroundColor: ['#10B981', '#34D399', '#F59E0B', '#EF4444', '#94A3B8'],
      borderWidth: 0,
      hoverOffset: 6
    }]
  };
}

/**
 * Aggregate human-readable reasons across Attention/Critical vehicles
 * so the Live Map can show why the fleet needs attention.
 */
export function aggregateHealthReasons(
  vehicles: FleetVehicleHealth[] | null | undefined,
  limit = 5
): FleetHealthReasonCount[] {
  if (!vehicles?.length) return [];
  const counts = new Map<string, number>();
  for (const v of vehicles) {
    const band = (v.band ?? '').toLowerCase();
    if (band !== 'attention' && band !== 'critical') continue;
    for (const reason of v.reasons ?? []) {
      const key = reason.trim();
      if (!key) continue;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
  }
  return [...counts.entries()]
    .map(([reason, count]) => ({ reason, count }))
    .sort((a, b) => b.count - a.count || a.reason.localeCompare(b.reason))
    .slice(0, limit);
}

export function vehicleHealthById(
  vehicles: FleetVehicleHealth[] | null | undefined
): Map<number, FleetVehicleHealth> {
  const map = new Map<number, FleetVehicleHealth>();
  for (const v of vehicles ?? []) {
    map.set(v.vehicleId, v);
  }
  return map;
}
