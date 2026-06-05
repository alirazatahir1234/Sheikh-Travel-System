/**
 * Vehicle-related DTOs that mirror SheikhTravelSystem.Application.Features.Vehicles.DTOs.
 * Naming, casing, and numeric enum values must match the backend exactly.
 */

export enum FuelType {
  Petrol = 1,
  Diesel = 2,
  CNG = 3
}

export enum VehicleStatus {
  Available = 1,
  OnTrip = 2,
  Maintenance = 3,
  Retired = 4
}

export interface Vehicle {
  id: number;
  name: string;
  registrationNumber: string;
  model?: string | null;
  year?: number | null;
  seatingCapacity: number;
  fuelAverage: number;
  fuelType: FuelType;
  currentMileage: number;
  insuranceExpiryDate?: string | null;
  status: VehicleStatus;
  createdAt: string;
}

/** Inner DTO — POST body sends `{ "vehicle": CreateVehicleDto }`. */
export interface CreateVehicleDto {
  name: string;
  registrationNumber: string;
  model?: string | null;
  year?: number | null;
  seatingCapacity: number;
  fuelAverage: number;
  fuelType: FuelType;
  currentMileage: number;
  insuranceExpiryDate?: string | null;
}

/** Inner DTO — PUT body sends `{ "id": number, "vehicle": UpdateVehicleDto }`. */
export interface UpdateVehicleDto extends CreateVehicleDto {
  status: VehicleStatus;
}

export interface CreateVehicleRequest {
  vehicle: CreateVehicleDto;
}

export interface UpdateVehicleRequest {
  id: number;
  vehicle: UpdateVehicleDto;
}

/** Convenience label map for the status chip. */
export const VehicleStatusLabels: Record<VehicleStatus, string> = {
  [VehicleStatus.Available]:   'Available',
  [VehicleStatus.OnTrip]:      'On Trip',
  [VehicleStatus.Maintenance]: 'Maintenance',
  [VehicleStatus.Retired]:     'Retired'
};

export const FuelTypeLabels: Record<FuelType, string> = {
  [FuelType.Petrol]: 'Petrol',
  [FuelType.Diesel]: 'Diesel',
  [FuelType.CNG]:    'CNG'
};
