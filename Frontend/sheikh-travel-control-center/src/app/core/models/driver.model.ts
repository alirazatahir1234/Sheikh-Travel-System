import { dateInputToIso } from '../utils/date-input.util';

// ── Verification status pipeline ────────────────────────────────────────────
export type DriverVerificationStatus =
  | 'Pending'
  | 'UnderReview'
  | 'Verified'
  | 'Rejected'
  | 'ExpiredDocs';

export const DRIVER_VERIFICATION_STATUSES: DriverVerificationStatus[] = [
  'Pending', 'UnderReview', 'Verified', 'Rejected', 'ExpiredDocs'
];

export const DRIVER_VERIFICATION_STATUS_LABELS: Record<DriverVerificationStatus, string> = {
  Pending: 'Pending',
  UnderReview: 'Under Review',
  Verified: 'Verified',
  Rejected: 'Rejected',
  ExpiredDocs: 'Docs Expired'
};

// ── Per-document status ──────────────────────────────────────────────────────
export type DocumentStatus = 'Missing' | 'Uploaded' | 'Approved' | 'Rejected' | 'Expired';

export interface DriverDocumentDetailed {
  id: number;
  documentType: string;
  fileUrl?: string | null;
  expiryDate?: string | null;
  /** Fine-grained per-document status set by reviewer. */
  status: DocumentStatus;
  rejectionReason?: string | null;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
}

export interface VerificationReviewNote {
  id: number;
  note: string;
  /** Optional: scoped to a specific document type (e.g. 'DrivingLicense'). */
  documentType?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface VerificationSummary {
  overallStatus: DriverVerificationStatus;
  documents: DriverDocumentDetailed[];
  reviewNotes: VerificationReviewNote[];
  checklist: {
    drivingLicenseUploaded: boolean;
    drivingLicenseApproved: boolean;
    medicalCertUploaded: boolean;
    medicalCertApproved: boolean;
    backgroundCheckUploaded: boolean;
    backgroundCheckApproved: boolean;
    licenseNotExpired: boolean;
    cnicVerified: boolean;
  };
  completionPct: number;
  lastReviewedBy?: string | null;
  lastReviewedAt?: string | null;
}

export interface UpdateDocumentStatusRequest {
  status: 'Approved' | 'Rejected';
  rejectionReason?: string | null;
}

export interface AddReviewNoteRequest {
  note: string;
  documentType?: string | null;
}

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
  rating?: number | null;
  gpsOnline?: boolean;
  availabilityBucket?: string | null;
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
  rating?: number | null;
  yearsExperience?: number | null;
  gpsOnline?: boolean;
  availabilityBucket?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DriverStats {
  totalDrivers: number;
  active: number;
  inactive?: number;
  onTrip: number;
  offDuty: number;
  available: number;
  busy?: number;
  onLeave: number;
  suspended: number;
  gpsOnline?: number;
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

export type DriverAvailabilityBucket = 'Available' | 'Busy' | 'OnTrip' | 'Unavailable';

export interface DriversAvailabilitySummary {
  available: number;
  busy: number;
  onTrip: number;
  unavailable: number;
}

export interface DriverAssignment {
  id: number;
  vehicleId: number;
  vehicleRegistration?: string | null;
  vehicleCode?: string | null;
  assignmentType: string;
  status: string;
  startAt: string;
  endAt?: string | null;
  bookingId?: number | null;
}

export interface DriverPerformanceSummary {
  driverId: number;
  driverName: string;
  rating?: number | null;
  yearsExperience?: number | null;
  totalTrips: number;
  completedTrips: number;
  totalRevenue: number;
  completionRate: number;
  violationCount: number;
  attendancePresentCount: number;
}

export interface DriverViolation {
  id: number;
  violationType: string;
  severity: string;
  occurredAt: string;
  description?: string | null;
  bookingId?: number | null;
  status: string;
  createdAt: string;
}

export interface DriverAttendance {
  id: number;
  attendanceDate: string;
  status: string;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface DriverLocation {
  latitude?: number | null;
  longitude?: number | null;
  speed?: number | null;
  ignition?: boolean | null;
  lastSeen?: string | null;
  vehicleId?: number | null;
  vehicleRegistration?: string | null;
  gpsOnline: boolean;
}

export interface TransferDriverVehicleRequest {
  newVehicleId: number;
  bookingId?: number | null;
  assignmentType?: string | null;
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

function pickField<T>(raw: Record<string, unknown>, camel: string, pascal: string): T | undefined {
  const v = raw[camel] ?? raw[pascal];
  return v as T | undefined;
}

function pickString(raw: Record<string, unknown>, camel: string, pascal: string): string | null {
  const v = pickField<unknown>(raw, camel, pascal);
  if (v == null || v === '') return null;
  return String(v);
}

function normalizeAddress(value: string | null | undefined): string | null {
  if (!value) return null;
  const lines = value.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
  const unique = [...new Set(lines)];
  return unique.length ? unique.join('\n') : null;
}

export function normalizeDriverListItem(item: DriverListItem): DriverListItem {
  return { ...item, status: normalizeDriverStatus(item.status) };
}

export function normalizeDriver(driver: Driver): Driver {
  const r = driver as Driver & Record<string, unknown>;
  const dateOfBirthRaw = pickField<string | null>(r, 'dateOfBirth', 'DateOfBirth');
  const licenseExpiryRaw = pickField<string | null>(r, 'licenseExpiryDate', 'LicenseExpiryDate');
  const hireDateRaw = pickField<string | null>(r, 'hireDate', 'HireDate');

  return {
    ...driver,
    firstName: pickString(r, 'firstName', 'FirstName') ?? driver.firstName,
    lastName: pickString(r, 'lastName', 'LastName') ?? driver.lastName,
    email: pickString(r, 'email', 'Email') ?? driver.email,
    gender: pickString(r, 'gender', 'Gender') ?? driver.gender,
    nationality: pickString(r, 'nationality', 'Nationality') ?? driver.nationality,
    cnic: pickString(r, 'cnic', 'CNIC') ?? driver.cnic,
    address: normalizeAddress(pickString(r, 'address', 'Address') ?? driver.address),
    emergencyContactName: pickString(r, 'emergencyContactName', 'EmergencyContactName') ?? driver.emergencyContactName,
    emergencyContact: pickString(r, 'emergencyContact', 'EmergencyContact') ?? driver.emergencyContact,
    photoUrl: pickString(r, 'photoUrl', 'PhotoUrl') ?? driver.photoUrl,
    status: normalizeDriverStatus(driver.status),
    dateOfBirth: dateOfBirthRaw ?? null,
    licenseExpiryDate: licenseExpiryRaw ?? driver.licenseExpiryDate,
    hireDate: hireDateRaw ?? driver.hireDate,
    updatedAt: pickString(r, 'updatedAt', 'UpdatedAt') ?? driver.updatedAt,
    createdAt: pickString(r, 'createdAt', 'CreatedAt') ?? driver.createdAt
  };
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
  const dobIso = dto.dateOfBirth ? dateInputToIso(dto.dateOfBirth) : null;
  const licenseIso = dto.licenseExpiryDate ? dateInputToIso(dto.licenseExpiryDate) : null;
  return {
    ...dto,
    firstName,
    lastName,
    fullName: buildDriverFullName(firstName, lastName),
    phone: dto.phone?.trim() || '',
    licenseNumber: dto.licenseNumber?.trim() || '',
    licenseExpiryDate: licenseIso ?? dto.licenseExpiryDate,
    dateOfBirth: dobIso,
    nationality: dto.nationality?.trim() || null,
    email: dto.email?.trim() || null,
    gender: dto.gender?.trim() || null,
    emergencyContactName: dto.emergencyContactName?.trim() || null,
    emergencyContact: dto.emergencyContact?.trim() || null,
    cnic: dto.cnic?.trim() || null,
    address: normalizeAddress(dto.address),
    branchId: dto.branchId ? Number(dto.branchId) : null,
    departmentId: dto.departmentId ? Number(dto.departmentId) : null,
    hireDate: dto.hireDate ? dateInputToIso(dto.hireDate) : null
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
