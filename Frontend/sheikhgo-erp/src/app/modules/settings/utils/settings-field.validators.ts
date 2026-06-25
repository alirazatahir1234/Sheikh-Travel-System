import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { SettingFieldSchema } from '../models/settings.model';

/** Skips validation when the control value is empty (for optional fields). */
function optionalWhenEmpty(validator: ValidatorFn): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = control.value;
    if (v === null || v === undefined || String(v).trim() === '') {
      return null;
    }
    return validator(control);
  };
}

const HEX_COLOR_PATTERN = /^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$/;

export function optionalUrlValidator(control: AbstractControl): ValidationErrors | null {
  const raw = String(control.value ?? '').trim();
  if (!raw) {
    return null;
  }

  const candidate = /^https?:\/\//i.test(raw) ? raw : `https://${raw}`;
  try {
    const url = new URL(candidate);
    if (url.protocol === 'http:' || url.protocol === 'https:') {
      return null;
    }
  } catch {
    // fall through
  }

  return { url: true };
}

export function buildSettingFieldValidators(field: SettingFieldSchema): ValidatorFn[] {
  if (field.type === 'readonly') {
    return [];
  }

  const validators: ValidatorFn[] = [];

  if (field.required) {
    validators.push(Validators.required);
  }

  if (field.minLength != null) {
    validators.push(Validators.minLength(field.minLength));
  }

  if (field.maxLength != null) {
    validators.push(Validators.maxLength(field.maxLength));
  }

  if (field.min != null && field.type === 'number') {
    validators.push(Validators.min(field.min));
  }

  if (field.max != null && field.type === 'number') {
    validators.push(Validators.max(field.max));
  }

  if (field.pattern) {
    const patternValidator = Validators.pattern(new RegExp(field.pattern));
    validators.push(field.required ? patternValidator : optionalWhenEmpty(patternValidator));
  }

  if (field.type === 'email') {
    validators.push(optionalWhenEmpty(Validators.email));
  }

  if (field.type === 'url') {
    validators.push(optionalUrlValidator);
  }

  if (field.type === 'color') {
    validators.push(optionalWhenEmpty(Validators.pattern(HEX_COLOR_PATTERN)));
  }

  return validators;
}
