import { VehicleListItem, VehicleStatus, normalizeVehicleStatus } from '../../../core/models/vehicle.model';
import { UiStatusVariant } from '../../../shared/components/ui/types/ui.types';

export interface OperationalStatus {
  label: string;
  variant: UiStatusVariant;
}

/** Derives fleet operational status from vehicle status + GPS telemetry. */
export function deriveOperationalStatus(row: VehicleListItem): OperationalStatus {
  const status = normalizeVehicleStatus(row.status);
  if (status === VehicleStatus.Maintenance) {
    return { label: 'Maintenance', variant: 'warning' };
  }
  if (status === VehicleStatus.OnTrip) {
    return { label: 'On Route', variant: 'info' };
  }
  if (status === VehicleStatus.Retired) {
    return { label: 'Offline', variant: 'inactive' };
  }

  const hasTracker = row.hasGpsDevice || !!row.gpsImei;
  if (!hasTracker) {
    return { label: 'Unknown', variant: 'inactive' };
  }

  if (!row.gpsOnline) {
    return { label: 'Offline', variant: 'error' };
  }

  if (row.engineIgnition) {
    return { label: 'Active', variant: 'success' };
  }

  return { label: 'Idle', variant: 'warning' };
}
