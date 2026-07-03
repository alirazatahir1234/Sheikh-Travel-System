import { VehicleLocation } from '../../../core/models/gps-tracking.model';

export interface FleetHealthBreakdown {
  optimal: number;
  attention: number;
  critical: number;
  unknown: number;
  total: number;
}

const CRITICAL_BATTERY_PERCENT = 10;
const ATTENTION_BATTERY_PERCENT = 25;
const ATTENTION_GSM_SIGNAL = 10;

/**
 * First-draft fleet-health categorization derived from telemetry already on VehicleLocation — no
 * backend health-scoring concept exists (the Maintenance module's vehicle health is a different,
 * service-scheduling-scoped concern). Thresholds are a starting point, subject to tuning once real
 * fleet telemetry is observed at scale.
 */
export function computeFleetHealth(locations: VehicleLocation[]): FleetHealthBreakdown {
  const result: FleetHealthBreakdown = { optimal: 0, attention: 0, critical: 0, unknown: 0, total: locations.length };

  for (const loc of locations) {
    if (loc.status === 'never_seen' || !loc.hasGps) {
      result.unknown++;
      continue;
    }

    if (loc.status === 'sos' || loc.status === 'offline' || (loc.batteryLevel != null && loc.batteryLevel < CRITICAL_BATTERY_PERCENT)) {
      result.critical++;
      continue;
    }

    const weakSignal = loc.gsmSignal != null && loc.gsmSignal < ATTENTION_GSM_SIGNAL;
    const lowBattery = loc.batteryLevel != null && loc.batteryLevel < ATTENTION_BATTERY_PERCENT;
    if (weakSignal || lowBattery) {
      result.attention++;
      continue;
    }

    result.optimal++;
  }

  return result;
}
