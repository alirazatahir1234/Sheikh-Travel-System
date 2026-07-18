import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Letters, digits, spaces, and light punctuation; must include at least one letter (e.g. "Branch 01"). */
const BRANCH_NAME_PATTERN = /^[A-Za-z0-9]+(?:[\s'&-][A-Za-z0-9]+)*$/;

/** Physical address: letters, digits, spaces, and common address punctuation. */
const BRANCH_ADDRESS_PATTERN = /^[\p{L}\p{N}\s.,#/\-'&()]+$/u;

/**
 * City names: letters (any script), spaces, hyphens, apostrophes, periods
 * (e.g. "Dubai", "Al Ain", "Saint-Étienne", "St. Louis"). Rejects digits.
 */
const BRANCH_CITY_PATTERN = /^[\p{L}]+(?:[\s'.-][\p{L}]+)*$/u;

export const BRANCH_ADDRESS_MAX_LENGTH = 500;
export const BRANCH_ADDRESS_MIN_LENGTH = 5;
export const BRANCH_CITY_MAX_LENGTH = 100;
export const BRANCH_CITY_MIN_LENGTH = 2;

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

/**
 * Optional full address: when present, enforce min/max length and allowed characters.
 * Aligns with backend MaximumLength(500).
 */
export function branchAddressValidator(
  maxLength = BRANCH_ADDRESS_MAX_LENGTH,
  minLength = BRANCH_ADDRESS_MIN_LENGTH
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '');
    const v = raw.trim();
    if (!v) return null;

    if (raw.length > maxLength || v.length > maxLength) {
      return { maxlength: { requiredLength: maxLength, actualLength: raw.length } };
    }

    if (v.length < minLength) {
      return { minlength: { requiredLength: minLength, actualLength: v.length } };
    }

    // Pure numbers / no letters → not a usable street address
    if (/^\d+$/.test(v) || !/\p{L}/u.test(v)) {
      return { numericOnly: true };
    }

    if (!BRANCH_ADDRESS_PATTERN.test(v)) {
      return { invalidAddress: true };
    }

    return null;
  };
}

/**
 * Optional city: when present, must look like a real city name (letters + light separators).
 * Rejects numeric-only and any value containing digits.
 */
export function branchCityValidator(
  maxLength = BRANCH_CITY_MAX_LENGTH,
  minLength = BRANCH_CITY_MIN_LENGTH
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return null;

    if (v.length > maxLength) {
      return { maxlength: { requiredLength: maxLength, actualLength: v.length } };
    }

    if (v.length < minLength) {
      return { minlength: { requiredLength: minLength, actualLength: v.length } };
    }

    if (/\d/.test(v) || !/\p{L}/u.test(v)) {
      return { numericOnly: true };
    }

    return BRANCH_CITY_PATTERN.test(v) ? null : { invalidCity: true };
  };
}
