export enum DriverStatus {
  Available = 1,
  OnTrip = 2,
  OffDuty = 3,
  Suspended = 4,
  OnLeave = 5
}

export const DriverStatusLabels: Record<DriverStatus, string> = {
  [DriverStatus.Available]: 'Available',
  [DriverStatus.OnTrip]: 'On Trip',
  [DriverStatus.OffDuty]: 'Off Duty',
  [DriverStatus.Suspended]: 'Suspended',
  [DriverStatus.OnLeave]: 'On Leave'
};

export interface DriverListItem {
  id: number;
  driverCode?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  licenseExpired: boolean;
  licenseExpiringSoon: boolean;
  nationality?: string | null;
  status: DriverStatus;
  isActive: boolean;
  verificationStatus: string;
  branchId?: number | null;
  branchName?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  hireDate?: string | null;
  assignedVehicleId?: number | null;
  assignedVehicleCode?: string | null;
  assignedVehicleRegistration?: string | null;
  createdAt: string;
}

export interface Driver {
  id: number;
  driverCode?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  licenseExpired: boolean;
  licenseExpiringSoon: boolean;
  cnic?: string | null;
  address?: string | null;
  nationality?: string | null;
  email?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  emergencyContactName?: string | null;
  emergencyContact?: string | null;
  hireDate?: string | null;
  photoUrl?: string | null;
  verificationStatus: string;
  branchId?: number | null;
  branchName?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  assignedVehicleId?: number | null;
  assignedVehicleCode?: string | null;
  assignedVehicleRegistration?: string | null;
  status: DriverStatus;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DriverStats {
  totalDrivers: number;
  active: number;
  onTrip: number;
  offDuty: number;
  available: number;
  onLeave: number;
  suspended: number;
  licensesExpiringSoon: number;
  licensesExpiringIn7Days: number;
  licensesExpired: number;
  verifiedDrivers: number;
  pendingVerification: number;
  assignedDrivers: number;
}

export interface DriverDocument {
  id: number;
  documentType: string;
  fileUrl?: string | null;
  expiryDate?: string | null;
  status: string;
  createdAt: string;
}

export interface DriverTimelineEvent {
  id: number;
  eventType: string;
  title: string;
  description?: string | null;
  occurredAt: string;
}

export interface DriverActiveDuty {
  recentTrips: { id: number; status: string; tripDate?: string | null; route?: string | null }[];
  fuelLogCount: number;
  hasGpsAssignment: boolean;
}

export interface CreateDriverDto {
  firstName: string;
  lastName: string;
  /** Computed client-side for API compatibility; backend derives FullName from first/last name. */
  fullName?: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  nationality?: string | null;
  email?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  emergencyContactName?: string | null;
  emergencyContact?: string | null;
  hireDate?: string | null;
  branchId?: number | null;
  departmentId?: number | null;
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

export interface AssignDriverVehicleRequest {
  vehicleId: number;
  bookingId?: number | null;
  assignmentType?: string | null;
}

export interface DriverAvailability {
  phoneAvailable: boolean;
  emailAvailable: boolean;
  licenseAvailable: boolean;
}

export interface CheckDriverAvailabilityParams {
  phone?: string;
  email?: string;
  licenseNumber?: string;
  excludeDriverId?: number;
}

export function driverDisplayName(d: Pick<DriverListItem | Driver, 'firstName' | 'lastName' | 'fullName'>): string {
  const first = d.firstName?.trim();
  const last = d.lastName?.trim();
  if (first || last) return [first, last].filter(Boolean).join(' ');
  return d.fullName?.trim() || '—';
}

export function normalizeDriverStatus(value: unknown, fallback = DriverStatus.Available): DriverStatus {
  const n = Number(value);
  return Object.values(DriverStatus).includes(n) ? (n as DriverStatus) : fallback;
}

export function normalizeDriverListItem(item: DriverListItem): DriverListItem {
  return { ...item, status: normalizeDriverStatus(item.status) };
}

export function normalizeDriver(driver: Driver): Driver {
  return { ...driver, status: normalizeDriverStatus(driver.status) };
}

export function buildDriverFullName(firstName: string, lastName: string): string {
  return [firstName?.trim(), lastName?.trim()].filter(Boolean).join(' ');
}

export function splitDriverFullName(fullName: string): { firstName: string; lastName: string } {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return { firstName: '', lastName: '' };
  if (parts.length === 1) return { firstName: parts[0], lastName: parts[0] };
  return { firstName: parts[0], lastName: parts.slice(1).join(' ') };
}

export function sanitizeCreateDriverDto(dto: CreateDriverDto): CreateDriverDto {
  const firstName = dto.firstName?.trim() || '';
  const lastName = dto.lastName?.trim() || '';
  return {
    ...dto,
    firstName,
    lastName,
    fullName: buildDriverFullName(firstName, lastName),
    phone: dto.phone?.trim() || '',
    licenseNumber: dto.licenseNumber?.trim() || '',
    nationality: dto.nationality?.trim() || null,
    email: dto.email?.trim() || null,
    gender: dto.gender?.trim() || null,
    emergencyContactName: dto.emergencyContactName?.trim() || null,
    emergencyContact: dto.emergencyContact?.trim() || null,
    cnic: dto.cnic?.trim() || null,
    address: dto.address?.trim() || null,
    branchId: dto.branchId ? Number(dto.branchId) : null,
    departmentId: dto.departmentId ? Number(dto.departmentId) : null
  };
}

export function sanitizeUpdateDriverDto(dto: UpdateDriverDto): UpdateDriverDto {
  return { ...sanitizeCreateDriverDto(dto), status: normalizeDriverStatus(dto.status), isActive: !!dto.isActive };
}

export const DRIVER_VERIFICATION_DOC_TYPES = [
  { type: 'DrivingLicense', label: 'Driving License' },
  { type: 'MedicalCertificate', label: 'Medical Certificate' },
  { type: 'BackgroundCheck', label: 'Background Check' }
] as const;
