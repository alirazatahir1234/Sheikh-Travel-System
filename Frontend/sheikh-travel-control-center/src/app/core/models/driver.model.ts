export enum DriverStatus {
  Available = 1,
  OnTrip = 2,
  OffDuty = 3,
  Suspended = 4
}

export const DriverStatusLabels: Record<DriverStatus, string> = {
  [DriverStatus.Available]: 'Available',
  [DriverStatus.OnTrip]: 'On Trip',
  [DriverStatus.OffDuty]: 'Off Duty',
  [DriverStatus.Suspended]: 'Suspended'
};

export interface Driver {
  id: number;
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  cnic?: string | null;
  address?: string | null;
  status: DriverStatus;
  isActive: boolean;
  createdAt: string;
}

export interface CreateDriverDto {
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  cnic?: string | null;
  address?: string | null;
}

export interface UpdateDriverDto extends CreateDriverDto {
  status: DriverStatus;
  isActive: boolean;
}

export interface CreateDriverRequest {
  driver: CreateDriverDto;
}

export interface UpdateDriverRequest {
  id: number;
  driver: UpdateDriverDto;
}
