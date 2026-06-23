import {
  CreateMaintenanceRequestPayload,
  ISSUE_CATEGORIES,
  RequestPriority
} from '../../../../../core/models/maintenance.model';

export const REQUEST_TYPES = ['Corrective', 'Preventive', 'Breakdown'] as const;
export const REQUEST_PRIORITIES: RequestPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export const DESCRIPTION_MIN_LENGTH = 10;
export const DESCRIPTION_MAX_LENGTH = 2000;

export interface CreateRequestValidationResult {
  valid: boolean;
  errors: Partial<Record<keyof CreateMaintenanceRequestPayload | 'description', string>>;
}

export function defaultCreateMaintenanceRequestForm(): CreateMaintenanceRequestPayload {
  return {
    vehicleId: 0,
    requestType: '',
    priority: '',
    issueCategory: '',
    description: ''
  };
}

export function validateCreateMaintenanceRequest(
  form: CreateMaintenanceRequestPayload
): CreateRequestValidationResult {
  const errors: CreateRequestValidationResult['errors'] = {};

  if (!form.vehicleId || form.vehicleId <= 0) {
    errors.vehicleId = 'Please select a vehicle.';
  }

  const description = String(form.description ?? '').trim();
  if (!description) {
    errors.description = 'Description is required.';
  } else if (description.length < DESCRIPTION_MIN_LENGTH) {
    errors.description = `Description must be at least ${DESCRIPTION_MIN_LENGTH} characters.`;
  } else if (description.length > DESCRIPTION_MAX_LENGTH) {
    errors.description = `Description cannot exceed ${DESCRIPTION_MAX_LENGTH} characters.`;
  }

  if (!String(form.priority ?? '').trim()) {
    errors.priority = 'Priority is required.';
  } else if (!REQUEST_PRIORITIES.includes(form.priority as RequestPriority)) {
    errors.priority = 'Please select a valid priority.';
  }

  if (!String(form.requestType ?? '').trim()) {
    errors.requestType = 'Type is required.';
  } else if (!REQUEST_TYPES.includes(form.requestType as (typeof REQUEST_TYPES)[number])) {
    errors.requestType = 'Please select a valid request type.';
  }

  const category = String(form.issueCategory ?? '').trim();
  if (!category) {
    errors.issueCategory = 'Category is required.';
  } else if (!ISSUE_CATEGORIES.includes(category as (typeof ISSUE_CATEGORIES)[number])) {
    errors.issueCategory = 'Please select a valid category.';
  }

  return { valid: Object.keys(errors).length === 0, errors };
}

export function canSubmitCreateMaintenanceRequest(form: CreateMaintenanceRequestPayload): boolean {
  return validateCreateMaintenanceRequest(form).valid;
}
