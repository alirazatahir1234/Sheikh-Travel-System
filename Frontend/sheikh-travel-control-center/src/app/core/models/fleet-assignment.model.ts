export interface FleetAssignment {
  id: number;
  assignmentNo: string;
  vehicleId: number;
  vehicleName: string;
  vehicleRegistration?: string | null;
  vehicleCode?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  driverCode?: string | null;
  assignmentType: string;
  status: string;
  startAt: string;
  endAt?: string | null;
  reason?: string | null;
  notes?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface FleetAssignmentStats {
  totalAssignments: number;
  activeAssignments: number;
  completedAssignments: number;
  cancelledAssignments: number;
  unassignedVehicles: number;
  availableDrivers: number;
  expiringLicenses: number;
}

export interface FleetAssignmentChangelog {
  id: number;
  actionType: string;
  oldVehicleId?: number | null;
  oldVehicleName?: string | null;
  newVehicleId?: number | null;
  newVehicleName?: string | null;
  oldDriverId?: number | null;
  oldDriverName?: string | null;
  newDriverId?: number | null;
  newDriverName?: string | null;
  reason?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface CreateAssignmentRequest {
  vehicleId: number;
  driverId: number;
  assignmentType: string;
  startDate: string;
  endDate?: string | null;
  reason?: string | null;
  notes?: string | null;
}

export interface TransferAssignmentRequest {
  newVehicleId: number;
  reason?: string | null;
  notes?: string | null;
}

export interface CompleteAssignmentRequest {
  reason?: string | null;
}

export interface CancelAssignmentRequest {
  reason?: string | null;
}

export interface FleetAssignmentFilters {
  search: string;
  status: string;
  assignmentType: string;
  vehicleId: string;
  driverId: string;
  dateFrom: string;
  dateTo: string;
}

export const EMPTY_ASSIGNMENT_FILTERS: FleetAssignmentFilters = {
  search: '',
  status: '',
  assignmentType: '',
  vehicleId: '',
  driverId: '',
  dateFrom: '',
  dateTo: ''
};

export const ASSIGNMENT_TYPES = ['Permanent', 'Temporary', 'Trip', 'Transfer'] as const;
export const ASSIGNMENT_STATUSES = ['Active', 'Completed', 'Cancelled'] as const;

export type AssignmentStatus = typeof ASSIGNMENT_STATUSES[number];
export type AssignmentType = typeof ASSIGNMENT_TYPES[number];
