import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { getTrackerCountry } from './tracker-country.config';

export const IMEI_PATTERN = /^\d{15}$/;

export function todayIsoDate(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function notPastDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '').trim();
    if (!raw) return null;
    const value = new Date(`${raw}T00:00:00`);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return value < today ? { pastDate: true } : null;
  };
}

export function phoneLocalValidator(getCountryCode: () => string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const local = String(control.value ?? '').replace(/\D/g, '');
    if (!local) return null;
    const country = getTrackerCountry(getCountryCode());
    if (!country) return { phoneFormat: true };
    return country.localPattern.test(local) ? null : { phoneFormat: true };
  };
}

export function buildInternationalPhone(countryCode: string, local: string): string | undefined {
  const digits = local.replace(/\D/g, '');
  if (!digits) return undefined;
  return `${countryCode}${digits}`;
}
