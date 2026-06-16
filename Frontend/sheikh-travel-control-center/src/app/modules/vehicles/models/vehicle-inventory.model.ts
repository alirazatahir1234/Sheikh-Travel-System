import { FuelType, VehicleStatus } from '../../../core/models/vehicle.model';

export type GpsFilterStatus = 'ALL' | 'ONLINE' | 'OFFLINE' | 'UNASSIGNED';
export type DriverFilterValue = 'ALL' | 'UNASSIGNED' | number;
export type TrackerFilterValue = 'ALL' | 'ASSIGNED' | 'UNASSIGNED';

export interface VehicleFilters {
  search: string;
  vehicleType: string;
  status: VehicleStatus | 'ALL';
  branchId: number | null;
  fuelType: FuelType | 'ALL';
  gpsStatus: GpsFilterStatus;
  driverId: DriverFilterValue;
  trackerAssigned: TrackerFilterValue;
  maintenanceDue: boolean;
  insuranceExpiring: boolean;
}

export const EMPTY_VEHICLE_FILTERS: VehicleFilters = {
  search: '',
  vehicleType: 'ALL',
  status: 'ALL',
  branchId: null,
  fuelType: 'ALL',
  gpsStatus: 'ALL',
  driverId: 'ALL',
  trackerAssigned: 'ALL',
  maintenanceDue: false,
  insuranceExpiring: false
};

export interface VehiclePagination {
  page: number;
  pageSize: number;
  total: number;
}
