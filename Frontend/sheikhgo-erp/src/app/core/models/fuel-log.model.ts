import { FuelType, FuelTypeLabels } from './vehicle.model';

export { FuelType, FuelTypeLabels };

export interface FuelLog {
  id: number;
  vehicleId: number;
  driverId?: number | null;
  liters: number;
  pricePerLiter: number;
  totalCost: number;
  odometerReading: number;
  fuelType: FuelType;
  fuelDate: string;
  station?: string | null;
  createdAt: string;
  
  // Joined fields (from list with vehicle/driver names)
  vehicleName?: string;
  vehicleRegistration?: string;
  driverName?: string;
}

export interface CreateFuelLogDto {
  vehicleId: number;
  driverId?: number | null;
  liters: number;
  pricePerLiter: number;
  odometerReading: number;
  fuelType: FuelType;
  fuelDate: string;
  station?: string | null;
}

export interface CreateFuelLogRequest {
  fuelLog: CreateFuelLogDto;
}

export interface VehicleFuelTelemetry {
  vehicleId: number;
  vehicleName?: string | null;
  lastUpdate?: string | null;
  fuelLevelPercent?: number | null;
  fuelConsumptionRateLph?: number | null;
  obdFuelUsedLiters?: number | null;
  obdOdometerKm?: number | null;
  integrationNote?: string | null;
}

export interface FuelEfficiencyRow {
  groupKey: string;
  label: string;
  distanceKm?: number | null;
  liters: number;
  kmPerLiter?: number | null;
  basisNote: string;
}

export interface FuelEfficiencyReport {
  from: string;
  to: string;
  byVehicle: FuelEfficiencyRow[];
  byDriver: FuelEfficiencyRow[];
  byRoute: FuelEfficiencyRow[];
}
