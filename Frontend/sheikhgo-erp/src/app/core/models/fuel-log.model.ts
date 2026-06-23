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
