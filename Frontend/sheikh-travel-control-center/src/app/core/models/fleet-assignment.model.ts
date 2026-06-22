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
  displayStatus: string;
  startAt: string;
  endAt?: string | null;
  purpose?: string | null;
  pickupLocation?: string | null;
  dropLocation?: string | null;
  durationDays?: number | null;
  odometerStart?: number | null;
  odometerEnd?: number | null;
  reason?: string | null;
  notes?: string | null;
  createdBy?: string | null;
  createdAt: string;
  modifiedBy?: string | null;
  modifiedAt?: string | null;
  gpsOnline: boolean;
  gpsSpeed?: number | null;
  gpsLastSeen?: string | null;
  ignition?: boolean | null;
  driverLicenseExpiringSoon: boolean;
  vehicleMaintenanceDue: boolean;
}

export interface FleetAssignmentStats {
  totalAssignments: number;
  activeAssignments: number;
  completedAssignments: number;
  cancelledAssignments: number;
  unassignedVehicles: number;
  availableVehicles: number;
  availableDrivers: number;
  expiringLicenses: number;
  ongoingTrips: number;
  upcomingAssignments: number;
  overdueReturns: number;
  expiredDocuments: number;
  driversOnLeave: number;
  vehiclesUnderMaintenance: number;
  assignmentUtilizationPct: number;
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

export interface AssignmentValidationIssue {
  code: string;
  message: string;
  severity: 'Error' | 'Warning';
}

export interface AssignmentValidationResult {
  canProceed: boolean;
  issues: AssignmentValidationIssue[];
}

export interface CreateAssignmentRequest {
  vehicleId: number;
  driverId: number;
  assignmentType: string;
  startDate: string;
  endDate?: string | null;
  purpose?: string | null;
  pickupLocation?: string | null;
  dropLocation?: string | null;
  odometerStart?: number | null;
  reason?: string | null;
  notes?: string | null;
  bookingId?: number | null;
}

export interface TransferAssignmentRequest {
  transferType: string;
  newVehicleId?: number | null;
  newDriverId?: number | null;
  reason?: string | null;
  notes?: string | null;
}

export interface CompleteAssignmentRequest {
  reason?: string | null;
  odometerEnd?: number | null;
}

export interface CancelAssignmentRequest {
  reason?: string | null;
}

export interface BulkAssignmentIdsRequest {
  assignmentIds: number[];
  reason?: string | null;
}

export interface BulkAssignmentResult {
  succeeded: number;
  failed: number;
  errors: string[];
}

export interface AssignmentCalendarItem {
  id: number;
  assignmentNo: string;
  vehicleId: number;
  vehicleName: string;
  driverId?: number | null;
  driverName?: string | null;
  status: string;
  startAt: string;
  endAt?: string | null;
  assignmentType: string;
}

export interface AssignmentUtilizationReport {
  totalVehicles: number;
  assignedVehicles: number;
  utilizationPct: number;
  totalDrivers: number;
  assignedDrivers: number;
  driverUtilizationPct: number;
  activeAssignments: number;
  completedThisMonth: number;
}

export interface ValidateAssignmentRequest {
  vehicleId: number;
  driverId: number;
  startDate?: string | null;
  assignmentType?: string | null;
  skipSoftWarnings?: boolean;
}

export interface FleetAssignmentFilters {
  search: string;
  status: string;
  assignmentType: string;
  vehicleId: string;
  driverId: string;
  branchId: string;
  departmentId: string;
  dateFrom: string;
  dateTo: string;
}

export const EMPTY_ASSIGNMENT_FILTERS: FleetAssignmentFilters = {
  search: '',
  status: '',
  assignmentType: '',
  vehicleId: '',
  driverId: '',
  branchId: '',
  departmentId: '',
  dateFrom: '',
  dateTo: ''
};

export const ASSIGNMENT_TYPES = ['Permanent', 'Temporary', 'Trip', 'Transfer', 'Rental'] as const;
export const ASSIGNMENT_PURPOSES = ['Trip', 'Rental', 'Staff Transport', 'Permanent', 'Temporary'] as const;
export const ASSIGNMENT_STATUSES = [
  'Active', 'Scheduled', 'PendingApproval', 'Assigned', 'Overdue',
  'Completed', 'Cancelled', 'Expired'
] as const;
export const TRANSFER_TYPES = ['Vehicle', 'Driver', 'Emergency', 'Temporary'] as const;

export type AssignmentStatus = typeof ASSIGNMENT_STATUSES[number];
export type AssignmentType = typeof ASSIGNMENT_TYPES[number];

export function assignmentEffectiveStatus(row: FleetAssignment): string {
  return row.displayStatus || row.status;
}

export function statusBadgeClass(status: string): string {
  const s = status.toLowerCase();
  if (['active', 'assigned'].includes(s)) return 'success';
  if (['scheduled', 'pendingapproval'].includes(s)) return 'warning';
  if (['overdue', 'cancelled', 'expired'].includes(s)) return 'error';
  if (s === 'completed') return 'info';
  return 'slate';
}
