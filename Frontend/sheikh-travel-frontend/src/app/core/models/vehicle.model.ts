export interface Vehicle {
  id: number;
  name: string;
  registrationNumber: string;
  seatingCapacity: number;
  fuelAverage: number;
  isActive: boolean;
  createdAt: string;
}

export interface CreateVehicleRequest {
  name: string;
  registrationNumber: string;
  seatingCapacity: number;
  fuelAverage: number;
}

export interface UpdateVehicleRequest extends CreateVehicleRequest {
  id: number;
}
