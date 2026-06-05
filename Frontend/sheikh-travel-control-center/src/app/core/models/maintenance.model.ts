export enum MaintenanceStatus {
  Scheduled   = 1,
  InProgress  = 2,
  Completed   = 3
}

export const MaintenanceStatusLabels: Record<MaintenanceStatus, string> = {
  [MaintenanceStatus.Scheduled]:  'Scheduled',
  [MaintenanceStatus.InProgress]: 'In Progress',
  [MaintenanceStatus.Completed]:  'Completed'
};

export interface Maintenance {
  id: number;
  vehicleId: number;
  description: string;
  cost: number;
  maintenanceDate: string;
  nextDueDate?: string | null;
  status: MaintenanceStatus;
  serviceProvider?: string | null;
  createdAt: string;
  
  // Joined fields
  vehicleName?: string;
  vehicleRegistration?: string;
}

export interface CreateMaintenanceDto {
  vehicleId: number;
  description: string;
  cost: number;
  maintenanceDate: string;
  nextDueDate?: string | null;
  serviceProvider?: string | null;
}

export interface CreateMaintenanceRequest {
  maintenance: CreateMaintenanceDto;
}

export interface UpdateMaintenanceStatusRequest {
  id: number;
  status: MaintenanceStatus;
}
