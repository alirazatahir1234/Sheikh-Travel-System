import { FuelType } from '../../../../core/models/vehicle.model';

export type WizardStepId = 'documents' | 'details' | 'technical' | 'gps' | 'review';

export interface WizardStep {
  id: WizardStepId;
  label: string;
  number: number;
}

export const WIZARD_STEPS: WizardStep[] = [
  { id: 'documents', label: 'Documents', number: 1 },
  { id: 'details', label: 'Details', number: 2 },
  { id: 'technical', label: 'Technical', number: 3 },
  { id: 'gps', label: 'GPS Tracker', number: 4 },
  { id: 'review', label: 'Review', number: 5 }
];

export type GpsWizardMode = 'new' | 'existing' | 'skip';

/** Snapshot of the tracker already linked to the vehicle being edited. */
export interface AssignedTrackerInfo {
  gpsDeviceId: number;
  deviceName?: string | null;
  brandName?: string | null;
  modelName?: string | null;
  uniqueId?: string | null;
  gpsOnline?: boolean;
}

export type VehicleImageAngle = 'Front' | 'Side' | 'Back';

export interface VehicleImageSlotState {
  angle: VehicleImageAngle;
  label: string;
  file?: File;
  fileUrl?: string;
  documentId?: number;
  isPrimary?: boolean;
  uploading?: boolean;
  error?: string;
}

export const VEHICLE_IMAGE_ANGLES: { angle: VehicleImageAngle; label: string }[] = [
  { angle: 'Front', label: 'Front View' },
  { angle: 'Side', label: 'Side View' },
  { angle: 'Back', label: 'Back View' }
];

export function parseVehicleImageAngle(notes?: string | null): VehicleImageAngle | null {
  if (!notes?.trim()) return null;
  const raw = notes.split('|')[0]?.trim() ?? '';
  if (!raw || raw.toLowerCase() === 'primary') return null;
  const match = VEHICLE_IMAGE_ANGLES.find(a => a.angle.toLowerCase() === raw.toLowerCase());
  return match?.angle ?? null;
}

export function isPrimaryVehicleImage(notes?: string | null): boolean {
  if (!notes?.trim()) return false;
  const value = notes.trim().toLowerCase();
  return value === 'primary' || value.includes('|primary');
}

export interface DocumentSlotState {
  documentType: string;
  label: string;
  required: boolean;
  file?: File;
  fileUrl?: string;
  documentId?: number;
  uploading?: boolean;
  error?: string;
}

export const WIZARD_DOCUMENT_SLOTS: Omit<DocumentSlotState, 'file' | 'fileUrl' | 'documentId'>[] = [
  { documentType: 'Registration', label: 'Registration Card', required: true },
  { documentType: 'Insurance', label: 'Insurance Policy', required: false }
];

/** UI-facing document state for badges and messaging. */
export type DocumentDisplayStatus = 'Empty' | 'Processing' | 'Verified' | 'Failed';

export function resolveDocumentDisplayStatus(slot: DocumentSlotState): DocumentDisplayStatus {
  if (slot.uploading) return 'Processing';
  if (slot.error && !slot.fileUrl) return 'Failed';
  if (slot.fileUrl) return 'Verified';
  return 'Empty';
}

export function documentContinueMessage(slot: DocumentSlotState): string | null {
  const display = resolveDocumentDisplayStatus(slot);
  switch (display) {
    case 'Failed':
      return slot.error || 'Upload failed. Try again.';
    case 'Empty':
      return slot.required ? `${slot.label} is required.` : null;
    default:
      return null;
  }
}

export const TRACKER_CATALOG_OPTIONS: { key: string; label: string; vendor: string }[] = [
  { key: 'teltonika_fmb920', label: 'Teltonika FMB920', vendor: 'Teltonika' },
  { key: 'teltonika_fmb140', label: 'Teltonika FMB140', vendor: 'Teltonika' },
  { key: 'teltonika_fmb001', label: 'Teltonika FMB001', vendor: 'Teltonika' },
  { key: 'teltonika_fmc001', label: 'Teltonika FMC001', vendor: 'Teltonika' },
  { key: 'concox_gt06n', label: 'Concox GT06N', vendor: 'Concox' },
  { key: 'queclink_gv75', label: 'Queclink GV75', vendor: 'Queclink' }
];

/** @deprecated Use TRACKER_CATALOG_OPTIONS — kept for any residual string maps. */
export const TRACKER_MODELS = TRACKER_CATALOG_OPTIONS.map(m => m.label);
export const TRACKER_VENDORS = [...new Set(TRACKER_CATALOG_OPTIONS.map(m => m.vendor))];

export function resolveWizardTrackerModel(keyOrLabel: string | null | undefined) {
  const raw = keyOrLabel?.trim() ?? '';
  if (!raw) return TRACKER_CATALOG_OPTIONS[0];
  return (
    TRACKER_CATALOG_OPTIONS.find(m => m.key === raw)
    ?? TRACKER_CATALOG_OPTIONS.find(m => m.label.toLowerCase() === raw.toLowerCase())
    ?? TRACKER_CATALOG_OPTIONS[0]
  );
}

export type VinValidationState = 'empty' | 'incomplete' | 'valid' | 'invalid';

export function validateVin(vin: string | null | undefined): boolean {
  if (!vin) return false;
  const v = vin.trim().toUpperCase();
  if (v.length !== 17 || /[IOQ]/.test(v)) return false;
  return /^[A-HJ-NPR-Z0-9]{17}$/.test(v);
}

export function getVinValidationState(vin: string | null | undefined): VinValidationState {
  const v = vin?.trim() ?? '';
  if (!v) return 'empty';
  if (v.length < 17) return 'incomplete';
  return validateVin(v) ? 'valid' : 'invalid';
}

export function generateVehicleCode(): string {
  const year = new Date().getFullYear();
  const seq = Math.floor(Math.random() * 9000) + 1000;
  return `ST-FLT-${year}-${seq}`;
}

export function fuelTypeLabel(ft: FuelType | string | number): string {
  const n = Number(ft);
  if (n === FuelType.Diesel) return 'Diesel';
  if (n === FuelType.CNG) return 'CNG';
  return 'Petrol';
}
