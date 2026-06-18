import { FuelType } from '../../../../core/models/vehicle.model';

export type WizardStepId = 'details' | 'technical' | 'gps' | 'documents' | 'review';

export interface WizardStep {
  id: WizardStepId;
  label: string;
  number: number;
}

export const WIZARD_STEPS: WizardStep[] = [
  { id: 'details', label: 'Details', number: 1 },
  { id: 'technical', label: 'Technical', number: 2 },
  { id: 'gps', label: 'GPS Tracker', number: 3 },
  { id: 'documents', label: 'Documents', number: 4 },
  { id: 'review', label: 'Review', number: 5 }
];

export type GpsWizardMode = 'new' | 'existing' | 'skip';

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
  progress?: number;
  error?: string;
}

export const WIZARD_DOCUMENT_SLOTS: Omit<DocumentSlotState, 'file' | 'fileUrl' | 'documentId'>[] = [
  { documentType: 'Registration', label: 'Registration Card', required: true },
  { documentType: 'Insurance', label: 'Insurance Policy', required: false }
];

export const TRACKER_MODELS = ['Teltonika FMB920', 'Queclink GV500', 'Concox GT06N', 'Other'];
export const TRACKER_VENDORS = ['Teltonika', 'Queclink', 'Concox', 'Traccar', 'Other'];

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
