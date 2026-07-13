import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Letters with optional spaces, hyphens, apostrophes, or ampersands between words (e.g. "Head Office", "R&D"). */
const ORG_NAME_PATTERN = /^[A-Za-z]+(?:[\s'&-][A-Za-z]+)*$/;

export function sanitizeOrgNameInput(value: string): string {
  return value.replace(/[^A-Za-z\s'&-]/g, '');
}

export function blockNonOrgNameKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  if (event.key.length === 1 && !/[A-Za-z\s'&-]/.test(event.key)) {
    event.preventDefault();
  }
}

export function orgNameValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    if (!v) return null;
    return ORG_NAME_PATTERN.test(v) ? null : { lettersOnly: true };
  };
}

export function orgNameListValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '').trim();
    if (!raw) return null;
    const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
    const invalid = parts.some(p => !ORG_NAME_PATTERN.test(p));
    return invalid ? { lettersOnly: true } : null;
  };
}

export function orgNameErrorMessage(label: string): string {
  return `${label} must contain letters only (no numbers).`;
}
