import { CreateMaintenanceSchedulePayload } from '../../../../../core/models/maintenance.model';

export type ScheduleFormState = CreateMaintenanceSchedulePayload & {
  serviceTypeId?: number | null;
};

function optionalNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

function optionalDate(value: unknown): string | null {
  if (value === null || value === undefined) return null;
  const trimmed = String(value).trim();
  return trimmed || null;
}

/** Builds an API-safe create payload; omits empty strings and interval-irrelevant fields. */
export function buildCreateMaintenanceSchedulePayload(
  form: ScheduleFormState
): CreateMaintenanceSchedulePayload | null {
  const vehicleId = Number(form.vehicleId);
  const serviceTypeName = form.serviceTypeName?.trim() ?? '';
  const intervalValue = Number(form.intervalValue);

  if (!vehicleId || !serviceTypeName || !Number.isFinite(intervalValue) || intervalValue < 1) {
    return null;
  }

  const payload: CreateMaintenanceSchedulePayload = {
    vehicleId,
    serviceTypeName,
    intervalType: form.intervalType,
    intervalValue,
    priority: form.priority?.trim() || 'Medium'
  };

  if (form.serviceTypeId != null && form.serviceTypeId > 0) {
    payload.serviceTypeId = form.serviceTypeId;
  }

  switch (form.intervalType) {
    case 'Mileage': {
      const mileage = optionalNumber(form.lastServiceMileage);
      if (mileage != null) payload.lastServiceMileage = mileage;
      break;
    }
    case 'EngineHours': {
      const hours = optionalNumber(form.lastServiceEngineHours);
      if (hours != null) payload.lastServiceEngineHours = hours;
      break;
    }
    case 'Months':
    case 'Days': {
      const date = optionalDate(form.lastServiceDate);
      if (date) payload.lastServiceDate = date;
      break;
    }
  }

  return payload;
}
