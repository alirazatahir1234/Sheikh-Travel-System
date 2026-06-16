import { FuelType, VehicleListItem, VehicleStatus } from '../../../core/models/vehicle.model';
import { DriverFilterValue, GpsFilterStatus, TrackerFilterValue, VehicleFilters } from '../models/vehicle-inventory.model';

export function normalizeFuelTypeFilter(value: unknown): FuelType | 'ALL' {
  if (value === 'ALL' || value == null || value === '') return 'ALL';
  const n = Number(value);
  if (n === FuelType.Petrol || n === FuelType.Diesel || n === FuelType.CNG) return n;
  return 'ALL';
}

export function normalizeDriverIdFilter(value: unknown): DriverFilterValue {
  if (value === 'ALL' || value == null || value === '') return 'ALL';
  if (value === 'UNASSIGNED') return 'UNASSIGNED';
  const n = Number(value);
  return Number.isFinite(n) ? n : 'ALL';
}

export function normalizeGpsStatusFilter(value: unknown): GpsFilterStatus {
  if (value === 'ONLINE' || value === 'OFFLINE' || value === 'UNASSIGNED') return value;
  return 'ALL';
}

export function normalizeTrackerFilter(value: unknown): TrackerFilterValue {
  if (value === 'ASSIGNED' || value === 'UNASSIGNED') return value;
  return 'ALL';
}

export function normalizeVehicleFilters(filters: VehicleFilters): VehicleFilters {
  return {
    ...filters,
    fuelType: normalizeFuelTypeFilter(filters.fuelType),
    driverId: normalizeDriverIdFilter(filters.driverId),
    gpsStatus: normalizeGpsStatusFilter(filters.gpsStatus),
    trackerAssigned: normalizeTrackerFilter(filters.trackerAssigned),
    maintenanceDue: !!filters.maintenanceDue,
    insuranceExpiring: !!filters.insuranceExpiring
  };
}

export function vehicleHasTracker(row: VehicleListItem): boolean {
  return !!(row.hasGpsDevice || row.gpsImei?.trim());
}

export function isGpsOnline(row: VehicleListItem): boolean {
  return row.gpsOnline === true;
}

export function isMaintenanceDue(row: VehicleListItem): boolean {
  if (row.status === VehicleStatus.Maintenance) return true;
  if (row.serviceAlert?.trim()) return true;
  if (row.nextDueMileage != null && row.currentMileage >= row.nextDueMileage) return true;
  if (row.nextServiceDue && new Date(row.nextServiceDue).getTime() < Date.now()) return true;
  return false;
}

export function isInsuranceExpiringSoon(row: VehicleListItem, withinDays = 30): boolean {
  if (!row.insuranceExpiryDate) return false;
  const exp = new Date(row.insuranceExpiryDate).getTime();
  if (!Number.isFinite(exp)) return false;
  const now = Date.now();
  const windowMs = withinDays * 24 * 60 * 60 * 1000;
  return exp <= now + windowMs;
}

export function matchesVehicleFilters(
  row: VehicleListItem,
  raw: VehicleFilters,
  driverNameById?: ReadonlyMap<number, string>
): boolean {
  const f = normalizeVehicleFilters(raw);

  if (f.vehicleType !== 'ALL' && row.vehicleType !== f.vehicleType) return false;
  if (f.status !== 'ALL' && row.status !== f.status) return false;
  if (f.branchId != null && row.branchId !== f.branchId) return false;

  if (f.fuelType !== 'ALL' && Number(row.fuelType) !== f.fuelType) return false;

  const hasTracker = vehicleHasTracker(row);
  const online = isGpsOnline(row);

  if (f.gpsStatus === 'ONLINE' && !online) return false;
  if (f.gpsStatus === 'OFFLINE' && (!hasTracker || online)) return false;
  if (f.gpsStatus === 'UNASSIGNED' && hasTracker) return false;

  if (f.trackerAssigned === 'ASSIGNED' && !hasTracker) return false;
  if (f.trackerAssigned === 'UNASSIGNED' && hasTracker) return false;

  if (f.driverId === 'UNASSIGNED') {
    if (row.driverName?.trim() || row.driverId != null) return false;
  } else if (typeof f.driverId === 'number') {
    const wantId = f.driverId;
    if (row.driverId != null) {
      if (Number(row.driverId) !== wantId) return false;
    } else if (row.driverName && driverNameById?.has(wantId)) {
      if (row.driverName.trim() !== driverNameById.get(wantId)) return false;
    } else {
      return false;
    }
  }

  if (f.maintenanceDue && !isMaintenanceDue(row)) return false;
  if (f.insuranceExpiring && !isInsuranceExpiringSoon(row)) return false;

  const term = f.search.trim().toLowerCase();
  if (term) {
    const hay = [
      row.name, row.registrationNumber, row.vehicleCode, row.make, row.model,
      row.driverName, row.gpsImei, row.gpsSim, row.vin
    ].filter(Boolean).join(' ').toLowerCase();
    if (!hay.includes(term)) return false;
  }

  return true;
}
