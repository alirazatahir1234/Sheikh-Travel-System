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
  Retired = 4,
  Draft = 5
}

export enum MaintenanceStatus {
  Scheduled = 1,
  InProgress = 2,
  Completed = 3
}

export interface VehicleListItem {
  id: number;
  name: string;
  registrationNumber: string;
  vehicleCode?: string | null;
  make?: string | null;
  model?: string | null;
  year?: number | null;
  vehicleType?: string | null;
  seatingCapacity: number;
  fuelAverage: number;
  fuelType: FuelType;
  currentMileage: number;
  insuranceExpiryDate?: string | null;
  status: VehicleStatus;
  branchId?: number | null;
  createdAt: string;
  driverName?: string | null;
  driverId?: number | null;
  gpsImei?: string | null;
  gpsSim?: string | null;
  engineIgnition?: boolean | null;
  locationLatitude?: number | null;
  locationLongitude?: number | null;
  locationLastUpdate?: string | null;
  gpsLastSeenAt?: string | null;
  gpsOnline?: boolean;
  hasGpsDevice?: boolean;
  nextServiceDue?: string | null;
  nextDueMileage?: number | null;
  serviceAlert?: string | null;
  vin?: string | null;
  imageUrl?: string | null;
}

/** Full vehicle detail (drawer / edit). */
export interface Vehicle {
  id: number;
  name: string;
  registrationNumber: string;
  vehicleCode?: string | null;
  vin?: string | null;
  make?: string | null;
  model?: string | null;
  year?: number | null;
  color?: string | null;
  vehicleType?: string | null;
  seatingCapacity: number;
  fuelAverage: number;
  fuelType: FuelType;
  engineNo?: string | null;
  chassisNo?: string | null;
  currentMileage: number;
  insuranceExpiryDate?: string | null;
  gpsDeviceId?: number | null;
  purchaseDate?: string | null;
  purchasePrice?: number | null;
  branchId?: number | null;
  departmentId?: number | null;
  status: VehicleStatus;
  isActive?: boolean;
  createdAt: string;
  updatedAt?: string | null;
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
  vehicleCode?: string | null;
  vin?: string | null;
  make?: string | null;
  color?: string | null;
  vehicleType?: string | null;
  engineNo?: string | null;
  chassisNo?: string | null;
  purchaseDate?: string | null;
  purchasePrice?: number | null;
  branchId?: number | null;
  departmentId?: number | null;
}

/** Inner DTO — PUT body sends `{ "id": number, "vehicle": UpdateVehicleDto }`. */
export interface UpdateVehicleDto extends CreateVehicleDto {
  status: VehicleStatus;
}

export interface CreateVehicleRequest {
  vehicle: CreateVehicleDto;
  saveAsDraft?: boolean;
}

export interface UpdateVehicleRequest {
  id: number;
  vehicle: UpdateVehicleDto;
}

export interface VehicleDocument {
  id: number;
  vehicleId: number;
  documentType: string;
  fileUrl?: string;
  expiryDate?: string;
  notes?: string;
}

export interface VehicleMaintenance {
  id: number;
  vehicleId: number;
  description: string;
  cost: number;
  maintenanceDate: string;
  nextDueDate?: string | null;
  status: MaintenanceStatus;
  serviceProvider?: string | null;
  createdAt: string;
}

export interface VehicleFuelLog {
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
}

export interface VehicleFuelSummary {
  items: VehicleFuelLog[];
  totalLiters: number;
  totalCost: number;
  totalCount: number;
}

export interface VehicleGps {
  gpsDeviceId?: number | null;
  deviceName?: string | null;
  uniqueId?: string | null;
  isActive?: boolean | null;
  lastSeenAt?: string | null;
  lastIgnition?: boolean | null;
  latitude?: number | null;
  longitude?: number | null;
  speed?: number | null;
  lastUpdate?: string | null;
}

export interface ChangeVehicleStatusRequest {
  status: VehicleStatus;
  reason?: string | null;
}

export interface AssignVehicleDriverRequest {
  driverId: number;
  bookingId?: number | null;
  assignmentType?: string | null;
}

export interface AssignVehicleGpsRequest {
  gpsDeviceId: number;
}

export interface UploadVehicleDocumentResult {
  documentId: number;
  fileUrl: string;
  documentType: string;
}
export const VehicleStatusLabels: Record<VehicleStatus, string> = {
  [VehicleStatus.Available]:   'Available',
  [VehicleStatus.OnTrip]:      'On Trip',
  [VehicleStatus.Maintenance]: 'Maintenance',
  [VehicleStatus.Retired]:     'Retired',
  [VehicleStatus.Draft]:       'Draft'
};

export const FuelTypeLabels: Record<FuelType, string> = {
  [FuelType.Petrol]: 'Petrol',
  [FuelType.Diesel]: 'Diesel',
  [FuelType.CNG]:    'CNG'
};

export function normalizeFuelType(value: unknown, fallback: FuelType = FuelType.Petrol): FuelType {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value as FuelType;
  }
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) return fallback;
    const asNumber = Number(trimmed);
    if (Number.isFinite(asNumber)) return asNumber as FuelType;
    const byLabel = (Object.entries(FuelTypeLabels) as [string, string][])
      .find(([, label]) => label.toLowerCase() === trimmed.toLowerCase());
    if (byLabel) return Number(byLabel[0]) as FuelType;
    if (trimmed in FuelType) return FuelType[trimmed as keyof typeof FuelType];
  }
  return fallback;
}

export function normalizeVehicleStatus(
  value: unknown,
  fallback: VehicleStatus = VehicleStatus.Available
): VehicleStatus {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value as VehicleStatus;
  }
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) return fallback;
    const asNumber = Number(trimmed);
    if (Number.isFinite(asNumber)) return asNumber as VehicleStatus;
    const byLabel = (Object.entries(VehicleStatusLabels) as [string, string][])
      .find(([, label]) => label.toLowerCase() === trimmed.toLowerCase());
    if (byLabel) return Number(byLabel[0]) as VehicleStatus;
    const normalized = trimmed.replace(/\s+/g, '');
    if (normalized in VehicleStatus) return VehicleStatus[normalized as keyof typeof VehicleStatus];
  }
  return fallback;
}

function emptyToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function toOptionalInt(value: unknown): number | null {
  const n = Number(value);
  return Number.isFinite(n) ? Math.trunc(n) : null;
}

function toOptionalNumber(value: unknown): number | null {
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

function toOptionalIsoDate(value: string | null | undefined): string | null {
  if (value == null) return null;
  if (typeof value === 'string' && value.trim() === '') return null;
  const parsed = new Date(value.includes('T') ? value : `${value}T00:00:00`);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

export function sanitizeCreateVehicleDto(dto: CreateVehicleDto): CreateVehicleDto {
  return {
    name: dto.name?.trim() || 'Draft Vehicle',
    registrationNumber: dto.registrationNumber?.trim() || '',
    vehicleCode: emptyToNull(dto.vehicleCode ?? undefined),
    vin: emptyToNull(dto.vin ?? undefined),
    make: emptyToNull(dto.make ?? undefined),
    model: emptyToNull(dto.model ?? undefined),
    year: toOptionalInt(dto.year),
    color: emptyToNull(dto.color ?? undefined),
    vehicleType: emptyToNull(dto.vehicleType ?? undefined),
    seatingCapacity: Math.max(1, Number(dto.seatingCapacity) || 1),
    fuelAverage: Math.max(0.1, Number(dto.fuelAverage) || 1),
    fuelType: normalizeFuelType(dto.fuelType),
    engineNo: emptyToNull(dto.engineNo ?? undefined),
    chassisNo: emptyToNull(dto.chassisNo ?? undefined),
    currentMileage: Math.max(0, Number(dto.currentMileage) || 0),
    insuranceExpiryDate: toOptionalIsoDate(dto.insuranceExpiryDate ?? undefined),
    purchaseDate: toOptionalIsoDate(dto.purchaseDate ?? undefined),
    purchasePrice: toOptionalNumber(dto.purchasePrice),
    branchId: toOptionalInt(dto.branchId),
    departmentId: toOptionalInt(dto.departmentId)
  };
}

export function sanitizeUpdateVehicleDto(dto: UpdateVehicleDto): UpdateVehicleDto {
  return {
    ...sanitizeCreateVehicleDto(dto),
    status: normalizeVehicleStatus(dto.status, VehicleStatus.Draft)
  };
}

export function normalizeVehicle(vehicle: Vehicle): Vehicle {
  return {
    ...vehicle,
    fuelType: normalizeFuelType(vehicle.fuelType),
    status: normalizeVehicleStatus(vehicle.status)
  };
}

export function normalizeVehicleListItem(item: VehicleListItem): VehicleListItem {
  return {
    ...item,
    fuelType: normalizeFuelType(item.fuelType),
    status: normalizeVehicleStatus(item.status)
  };
}

export const MaintenanceStatusLabels: Record<MaintenanceStatus, string> = {
  [MaintenanceStatus.Scheduled]:  'Scheduled',
  [MaintenanceStatus.InProgress]: 'In Progress',
  [MaintenanceStatus.Completed]:  'Completed'
};
