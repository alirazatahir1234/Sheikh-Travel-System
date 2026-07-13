import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Letters, digits, spaces, and light punctuation; must include at least one letter (e.g. "Branch 01"). */
const BRANCH_NAME_PATTERN = /^[A-Za-z0-9]+(?:[\s'&-][A-Za-z0-9]+)*$/;

export function branchNameValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return null;
    if (/^\d+$/.test(v) || !/[A-Za-z]/.test(v)) {
      return { numericOnly: true };
    }
    return BRANCH_NAME_PATTERN.test(v) ? null : { invalidBranchName: true };
  };
}

/** Optional phone: when present, digit count must not exceed maxDigits (E.164 = 15). */
export function phoneMaxDigitsValidator(maxDigits = 15): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '').trim();
    if (!raw) return null;
    const digits = raw.replace(/\D/g, '');
    if (digits.length > maxDigits) {
      return { phoneMaxDigits: { max: maxDigits, actual: digits.length } };
    }
    return null;
  };
}
