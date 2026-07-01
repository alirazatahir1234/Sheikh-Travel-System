import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { toDateInputValue } from '../../../../core/utils/date-input.util';

const MIN_DRIVER_AGE = 18;

export const PHONE_COUNTRY_CODES = [
  { code: '+971', flag: '🇦🇪', label: 'UAE (+971)', digits: 9, pattern: /^5\d{8}$/ },
  { code: '+92', flag: '🇵🇰', label: 'Pakistan (+92)', digits: 10, pattern: /^3\d{9}$/ },
  { code: '+91', flag: '🇮🇳', label: 'India (+91)', digits: 10, pattern: /^[6-9]\d{9}$/ },
  { code: '+880', flag: '🇧🇩', label: 'Bangladesh (+880)', digits: 10, pattern: /^1\d{9}$/ },
  { code: '+63', flag: '🇵🇭', label: 'Philippines (+63)', digits: 10, pattern: /^9\d{9}$/ },
  { code: '+20', flag: '🇪🇬', label: 'Egypt (+20)', digits: 10, pattern: /^\d{10}$/ },
  { code: '+962', flag: '🇯🇴', label: 'Jordan (+962)', digits: 9, pattern: /^7\d{8}$/ },
  { code: '+961', flag: '🇱🇧', label: 'Lebanon (+961)', digits: 8, pattern: /^\d{7,8}$/ },
  { code: '+963', flag: '🇸🇾', label: 'Syria (+963)', digits: 9, pattern: /^9\d{8}$/ },
  { code: '+249', flag: '🇸🇩', label: 'Sudan (+249)', digits: 9, pattern: /^9\d{8}$/ },
  { code: '+977', flag: '🇳🇵', label: 'Nepal (+977)', digits: 10, pattern: /^9\d{9}$/ },
  { code: '+94', flag: '🇱🇰', label: 'Sri Lanka (+94)', digits: 9, pattern: /^7\d{8}$/ },
  { code: '+966', flag: '🇸🇦', label: 'Saudi (+966)', digits: 9, pattern: /^5\d{8}$/ },
  { code: '+44', flag: '🇬🇧', label: 'UK (+44)', digits: 10, pattern: /^\d{10}$/ },
  { code: '+1', flag: '🇺🇸', label: 'US (+1)', digits: 10, pattern: /^\d{10}$/ }
] as const;

/** Maps driver nationality options to their default international dialing code. */
export const NATIONALITY_PHONE_CODE_MAP: Record<string, string> = {
  'United Arab Emirates': '+971',
  Pakistan: '+92',
  India: '+91',
  Bangladesh: '+880',
  Philippines: '+63',
  Egypt: '+20',
  Jordan: '+962',
  Lebanon: '+961',
  Syria: '+963',
  Sudan: '+249',
  Nepal: '+977',
  'Sri Lanka': '+94',
  'United Kingdom': '+44',
  'United States': '+1'
};

export function phoneCodeForNationality(nationality: string): string | null {
  const code = NATIONALITY_PHONE_CODE_MAP[nationality.trim()];
  if (!code) return null;
  return PHONE_COUNTRY_CODES.some(c => c.code === code) ? code : null;
}

export { digitsOnlyPhoneInput } from '../../../../core/utils/phone-input.util';

export const PERSON_NAME_MIN_LENGTH = 3;
export const PERSON_NAME_MAX_LENGTH = 15;

/** Latin letters; optional hyphen/apostrophe between letter groups (e.g. Al-Mansoor). */
const PERSON_NAME_PATTERN = /^[A-Za-z]+(?:['-][A-Za-z]+)*$/;

export function sanitizePersonNameInput(value: string): string {
  return value.replace(/[^A-Za-z'-]/g, '').slice(0, PERSON_NAME_MAX_LENGTH);
}

export function blockNonPersonNameKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  if (event.key.length === 1 && !/[A-Za-z'-]/.test(event.key)) {
    event.preventDefault();
  }
}

export function personNameValidator(
  minLength = PERSON_NAME_MIN_LENGTH,
  maxLength = PERSON_NAME_MAX_LENGTH
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return { required: true };
    if (v.length < minLength) {
      return { minlength: { requiredLength: minLength, actualLength: v.length } };
    }
    if (v.length > maxLength) {
      return { maxlength: { requiredLength: maxLength, actualLength: v.length } };
    }
    if (/\d/.test(v) || !PERSON_NAME_PATTERN.test(v)) {
      return { lettersOnly: true };
    }
    return null;
  };
}

export function personNameErrorMessage(
  control: AbstractControl | null | undefined,
  label: string
): string | null {
  if (!control?.invalid) return null;
  if (control.hasError('required')) return `${label} is required`;
  if (control.hasError('lettersOnly')) return `${label} must contain letters only (no numbers)`;
  if (control.hasError('minlength')) {
    return `${label} must be at least ${PERSON_NAME_MIN_LENGTH} characters`;
  }
  if (control.hasError('maxlength')) {
    return `${label} cannot exceed ${PERSON_NAME_MAX_LENGTH} characters`;
  }
  return `${label} is invalid`;
}

export const CONTACT_NAME_MIN_LENGTH = 3;
export const CONTACT_NAME_MAX_LENGTH = 12;

/** Latin letters with optional spaces, hyphens, or apostrophes between words. */
const CONTACT_NAME_PATTERN = /^[A-Za-z]+(?:[\s'-][A-Za-z]+)*$/;

export function sanitizeContactNameInput(value: string): string {
  return value.replace(/[^A-Za-z\s'-]/g, '').slice(0, CONTACT_NAME_MAX_LENGTH);
}

export function blockNonContactNameKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  if (event.key.length === 1 && !/[A-Za-z\s'-]/.test(event.key)) {
    event.preventDefault();
  }
}

export function contactNameValidator(
  minLength = CONTACT_NAME_MIN_LENGTH,
  maxLength = CONTACT_NAME_MAX_LENGTH
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return { required: true };
    if (v.length < minLength) {
      return { minlength: { requiredLength: minLength, actualLength: v.length } };
    }
    if (v.length > maxLength) {
      return { maxlength: { requiredLength: maxLength, actualLength: v.length } };
    }
    if (/\d/.test(v) || !CONTACT_NAME_PATTERN.test(v)) {
      return { lettersOnly: true };
    }
    return null;
  };
}

export function contactNameErrorMessage(
  control: AbstractControl | null | undefined,
  label: string
): string | null {
  if (!control?.invalid) return null;
  if (control.hasError('required')) return `${label} is required`;
  if (control.hasError('lettersOnly')) return `${label} must contain letters only (no numbers)`;
  if (control.hasError('minlength')) {
    return `${label} must be at least ${CONTACT_NAME_MIN_LENGTH} characters`;
  }
  if (control.hasError('maxlength')) {
    return `${label} cannot exceed ${CONTACT_NAME_MAX_LENGTH} characters`;
  }
  return `${label} is invalid`;
}

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

export const EMERGENCY_CONTACT_PHONE_MIN_DIGITS = 7;
export const EMERGENCY_CONTACT_PHONE_MAX_DIGITS = 15;

export function emergencyContactPhoneValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return { required: true };
    const digits = v.replace(/\D/g, '');
    if (digits.length < EMERGENCY_CONTACT_PHONE_MIN_DIGITS || digits.length > EMERGENCY_CONTACT_PHONE_MAX_DIGITS) {
      return { phoneLength: true };
    }
    return null;
  };
}

export function sanitizeEmergencyContactPhoneInput(value: string): string {
  return value.replace(/\D/g, '').slice(0, EMERGENCY_CONTACT_PHONE_MAX_DIGITS);
}

export function emergencyContactPhoneErrorMessage(
  control: AbstractControl | null | undefined
): string | null {
  if (!control?.invalid) return null;
  if (control.hasError('required')) return 'Emergency contact phone is required';
  if (control.hasError('phoneLength')) {
    return `Enter a valid phone number (${EMERGENCY_CONTACT_PHONE_MIN_DIGITS}–${EMERGENCY_CONTACT_PHONE_MAX_DIGITS} digits)`;
  }
  return 'Emergency contact phone is invalid';
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
