import { CreateWorkOrderPayload } from '../../../../../core/models/maintenance.model';

export const WO_MAINTENANCE_TYPES = ['Preventive', 'Corrective', 'Emergency'] as const;
export const WO_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;
export const WO_SERVICE_CHIPS = [
  'Oil Change',
  'Brake Service',
  'Tire Rotation',
  'Battery Replacement',
  'Engine Repair'
] as const;

export const NOTES_MAX_LENGTH = 2000;
export const MAX_COST = 9_999_999.99;

export type WoCreateFormField = keyof WoCreateFormValue | 'serviceItems';

export const WO_CREATE_VALIDATION_ORDER: WoCreateFormField[] = [
  'vehicleId',
  'serviceItems',
  'priority',
  'maintenanceType',
  'startDate',
  'estimatedCompletionDate',
  'partsCost',
  'laborCost',
  'notes'
];

export interface WoCreateFormValue {
  vehicleId: number;
  workshopId: number;
  priority: string;
  maintenanceType: string;
  serviceItems: string[];
  startDate: string;
  estimatedCompletionDate: string;
  laborCost: number;
  partsCost: number;
  notes: string;
}

export interface WoCreateValidationResult {
  valid: boolean;
  errors: Partial<Record<WoCreateFormField, string>>;
}

export function defaultWoCreateForm(): WoCreateFormValue {
  return {
    vehicleId: 0,
    workshopId: 0,
    priority: 'Medium',
    maintenanceType: 'Preventive',
    serviceItems: [],
    startDate: '',
    estimatedCompletionDate: '',
    laborCost: 0,
    partsCost: 0,
    notes: ''
  };
}

export function validateWoCreateForm(form: WoCreateFormValue): WoCreateValidationResult {
  const errors: WoCreateValidationResult['errors'] = {};

  if (!form.vehicleId || form.vehicleId <= 0) {
    errors.vehicleId = 'Please select a vehicle.';
  }

  if (!form.serviceItems?.length) {
    errors.serviceItems = 'Select at least one service item.';
  }

  const priority = String(form.priority ?? '').trim();
  if (!priority) {
    errors.priority = 'Priority is required.';
  } else if (!WO_PRIORITIES.includes(priority as (typeof WO_PRIORITIES)[number])) {
    errors.priority = 'Please select a valid priority.';
  }

  const maintenanceType = String(form.maintenanceType ?? '').trim() || 'Preventive';
  if (!WO_MAINTENANCE_TYPES.includes(maintenanceType as (typeof WO_MAINTENANCE_TYPES)[number])) {
    errors.maintenanceType = 'Please select a valid maintenance type.';
  }

  const startDate = String(form.startDate ?? '').trim();
  const completionDate = String(form.estimatedCompletionDate ?? '').trim();

  if (maintenanceType === 'Emergency' && !startDate) {
    errors.startDate = 'Start date is required for emergency work orders.';
  }

  if (startDate && completionDate && completionDate < startDate) {
    errors.estimatedCompletionDate = 'Estimated completion must be on or after the start date.';
  }

  const laborCost = Number(form.laborCost);
  if (!Number.isFinite(laborCost) || laborCost < 0) {
    errors.laborCost = 'Labor cost cannot be negative.';
  } else if (laborCost > MAX_COST) {
    errors.laborCost = `Labor cost cannot exceed ${MAX_COST.toLocaleString()}.`;
  }

  const partsCost = Number(form.partsCost);
  if (!Number.isFinite(partsCost) || partsCost < 0) {
    errors.partsCost = 'Parts cost cannot be negative.';
  } else if (partsCost > MAX_COST) {
    errors.partsCost = `Parts cost cannot exceed ${MAX_COST.toLocaleString()}.`;
  }

  const notes = String(form.notes ?? '');
  if (notes.length > NOTES_MAX_LENGTH) {
    errors.notes = `Notes cannot exceed ${NOTES_MAX_LENGTH} characters.`;
  }

  return { valid: Object.keys(errors).length === 0, errors };
}

export function firstWoCreateFormError(
  errors: Partial<Record<WoCreateFormField, string>>
): { field: WoCreateFormField; message: string } | null {
  for (const field of WO_CREATE_VALIDATION_ORDER) {
    const message = errors[field];
    if (message) return { field, message };
  }
  return null;
}

export function woCreateFormErrorMessages(errors: Partial<Record<WoCreateFormField, string>>): string[] {
  return WO_CREATE_VALIDATION_ORDER
    .map(field => errors[field])
    .filter((message): message is string => !!message);
}

export function toCreateWorkOrderPayload(form: WoCreateFormValue): CreateWorkOrderPayload {
  const notes = String(form.notes ?? '').trim();
  const workshopId = Number(form.workshopId) || 0;
  const maintenanceType = String(form.maintenanceType ?? '').trim() || 'Preventive';

  return {
    vehicleId: Number(form.vehicleId) || 0,
    priority: form.priority || 'Medium',
    maintenanceType,
    serviceTypeName: form.serviceItems.join(', '),
    startDate: form.startDate || null,
    estimatedCompletionDate: form.estimatedCompletionDate || null,
    laborCost: roundMoney(form.laborCost),
    partsCost: roundMoney(form.partsCost),
    notes: notes || null,
    ...(workshopId > 0 ? { workshopId } : {})
  };
}

function roundMoney(value: number): number {
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount < 0) return 0;
  return Math.round(amount * 100) / 100;
}
