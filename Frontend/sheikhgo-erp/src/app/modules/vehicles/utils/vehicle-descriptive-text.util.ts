import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function isDescriptiveVehicleText(value: string | null | undefined): boolean {
  const text = (value ?? '').trim();
  if (!text) return false;

  if (/^\d+$/.test(text)) return false;

  const letters = (text.match(/[A-Za-z]/g) ?? []).length;
  if (letters === 0) return false;

  const digits = (text.match(/\d/g) ?? []).length;
  if (digits > letters * 2) return false;
  if (digits > 0 && letters < 2 && text.length > 4) return false;

  return true;
}

export function descriptiveVehicleTextValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = control.value;
    if (raw == null || (typeof raw === 'string' && !raw.trim())) {
      return null;
    }
    return isDescriptiveVehicleText(String(raw)) ? null : { descriptiveText: true };
  };
}

export function descriptiveVehicleTextError(field: 'name' | 'make' | 'model' | 'color'): string {
  switch (field) {
    case 'name':
      return 'Vehicle name must be descriptive text (e.g. Toyota Hiace 2024), not numbers only.';
    case 'make':
      return 'Make must be a valid manufacturer name (e.g. Toyota, Nissan, Ford).';
    case 'model':
      return 'Model must be a valid model name (e.g. Hiace, Corolla, Civic).';
    case 'color':
      return 'Color must be a valid color name (e.g. White, Black, Silver).';
  }
}
