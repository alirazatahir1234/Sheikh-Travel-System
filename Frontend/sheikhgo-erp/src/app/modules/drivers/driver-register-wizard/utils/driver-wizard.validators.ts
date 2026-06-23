import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { toDateInputValue } from '../../../../core/utils/date-input.util';

const MIN_DRIVER_AGE = 18;

export const PHONE_COUNTRY_CODES = [
  { code: '+971', flag: '🇦🇪', label: 'UAE (+971)', digits: 9, pattern: /^5\d{8}$/ },
  { code: '+92', flag: '🇵🇰', label: 'Pakistan (+92)', digits: 10, pattern: /^3\d{9}$/ },
  { code: '+91', flag: '🇮🇳', label: 'India (+91)', digits: 10, pattern: /^[6-9]\d{9}$/ },
  { code: '+966', flag: '🇸🇦', label: 'Saudi (+966)', digits: 9, pattern: /^5\d{8}$/ },
  { code: '+44', flag: '🇬🇧', label: 'UK (+44)', digits: 10, pattern: /^\d{10}$/ },
  { code: '+1', flag: '🇺🇸', label: 'US (+1)', digits: 10, pattern: /^\d{10}$/ }
] as const;

export function calcOrgFieldProgress(values: Record<string, unknown>): number {
  let score = 0;
  if (String(values['branchId'] ?? '').trim()) score += 70;
  if (String(values['departmentId'] ?? '').trim()) score += 30;
  return score;
}

export function maxDateOfBirthInputValue(minAge = MIN_DRIVER_AGE): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() - minAge);
  return toDateInputValue(d);
}

export function minDateOfBirthInputValue(maxAge = 80): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() - maxAge);
  return toDateInputValue(d);
}

export function dateOfBirthValidator(minAge = MIN_DRIVER_AGE): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '').trim();
    if (!raw) return { required: true };

    const dob = new Date(`${raw}T00:00:00`);
    if (isNaN(dob.getTime())) return { invalidDate: true };

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (dob >= today) return { futureDate: true };

    const minDob = new Date(today);
    minDob.setFullYear(minDob.getFullYear() - minAge);
    if (dob > minDob) return { minAge: { required: minAge } };

    return null;
  };
}

export function phoneLocalValidator(getCountryCode: () => string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const local = String(control.value ?? '').replace(/\D/g, '');
    if (!local) return { required: true };

    const rule = PHONE_COUNTRY_CODES.find(r => r.code === getCountryCode());
    if (!rule) {
      if (local.length < 7 || local.length > 15) return { phoneLength: true };
      return null;
    }

    if (local.length !== rule.digits) return { phoneLength: { expected: rule.digits } };
    if (rule.pattern && !rule.pattern.test(local)) return { phoneFormat: true };
    return null;
  };
}

export function buildFullName(firstName: string, lastName: string): string {
  return [firstName.trim(), lastName.trim()].filter(Boolean).join(' ');
}

export function emergencyContactPhoneValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return { required: true };
    const digits = v.replace(/\D/g, '');
    if (digits.length < 7 || digits.length > 15) return { phoneLength: true };
    return null;
  };
}

export function calcProfileCompletion(
  sections: { label: string; percent: number }[]
): { sections: { label: string; percent: number }[]; overall: number } {
  if (sections.length === 0) return { sections: [], overall: 0 };
  const overall = Math.round(sections.reduce((sum, s) => sum + s.percent, 0) / sections.length);
  return { sections, overall };
}

export function calcStepFieldProgress(
  fields: { name: string; required?: boolean }[],
  values: Record<string, unknown>,
  controls: Record<string, AbstractControl | null>
): number {
  const requiredFields = fields.filter(f => f.required !== false);
  if (requiredFields.length === 0) return 100;

  let filled = 0;
  for (const f of requiredFields) {
    const c = controls[f.name];
    const v = values[f.name];
    const str = String(v ?? '').trim();
    if (str && (!c || !c.invalid)) filled++;
  }
  return Math.round((filled / requiredFields.length) * 100);
}
