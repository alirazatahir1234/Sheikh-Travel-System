import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { SettingsService } from '../../services/settings.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { SettingFieldSchema, SettingsValues } from '../../models/settings.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { buildSettingFieldValidators } from '../../utils/settings-field.validators';

interface SettingsSection {
  title: string;
  fields: SettingFieldSchema[];
}

export interface SettingsFormStatus {
  invalid: boolean;
  pristine: boolean;
  saving: boolean;
}

@Component({
  selector: 'app-dynamic-settings-form',
  templateUrl: './dynamic-settings-form.component.html',
  styleUrls: ['./dynamic-settings-form.component.scss']
})
export class DynamicSettingsFormComponent implements OnChanges, OnDestroy {
  @Input() category = '';
  @Input() schema: SettingFieldSchema[] = [];
  @Input() values: SettingsValues = {};
  @Output() formStatusChange = new EventEmitter<SettingsFormStatus>();

  private readonly fb = inject(FormBuilder);
  private readonly settings = inject(SettingsService);
  private readonly toast = inject(UiToastService);
  private readonly destroy$ = new Subject<void>();

  form: FormGroup = this.fb.group({});
  sections: SettingsSection[] = [];
  saving = false;

  ngOnChanges(): void {
    this.destroy$.next();
    this.sections = this.buildSections();
    this.form = this.fb.group(this.buildControls());
    this.watchFormStatus();
    this.emitFormStatus();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  control(field: SettingFieldSchema) {
    return this.form.get(field.key);
  }

  showFieldError(field: SettingFieldSchema): boolean {
    const c = this.control(field);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  getFieldError(field: SettingFieldSchema): string | null {
    const c = this.control(field);
    if (!c?.errors || !(c.touched || c.dirty)) {
      return null;
    }

    const errors = c.errors;
    if (errors['required']) {
      return `${field.label} is required.`;
    }
    if (errors['email']) {
      return 'Enter a valid email address.';
    }
    if (errors['url']) {
      return 'Enter a valid URL (e.g. https://example.com).';
    }
    if (errors['minlength']) {
      return `Must be at least ${errors['minlength'].requiredLength} characters.`;
    }
    if (errors['maxlength']) {
      return `Must be at most ${errors['maxlength'].requiredLength} characters.`;
    }
    if (errors['min']) {
      return `Must be at least ${errors['min'].min}.`;
    }
    if (errors['max']) {
      return `Must be at most ${errors['max'].max}.`;
    }
    if (errors['pattern']) {
      return field.patternMessage ?? 'Invalid format.';
    }

    return 'Invalid value.';
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Please fix the highlighted errors before saving.');
      return;
    }

    this.saving = true;
    this.emitFormStatus();
    const payload = this.serialize();
    this.settings.update(this.category, payload).subscribe({
      next: () => {
        this.saving = false;
        this.values = payload;
        this.form.markAsPristine();
        this.emitFormStatus();
        this.toast.success('Settings saved.');
      },
      error: (err) => {
        this.saving = false;
        this.emitFormStatus();
        this.toast.error(apiErrorMessage(err, 'Failed to save settings.'));
      }
    });
  }

  discard(): void {
    const resetValue: Record<string, unknown> = {};
    for (const field of this.schema) {
      resetValue[field.key] = this.toFormValue(field);
    }
    this.form.reset(resetValue);
    this.form.markAsPristine();
    this.emitFormStatus();
  }

  private watchFormStatus(): void {
    this.form.statusChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.emitFormStatus());
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.emitFormStatus());
  }

  private emitFormStatus(): void {
    this.formStatusChange.emit({
      invalid: this.form.invalid,
      pristine: this.form.pristine,
      saving: this.saving
    });
  }

  private buildSections(): SettingsSection[] {
    const order: string[] = [];
    const grouped = new Map<string, SettingFieldSchema[]>();
    for (const field of this.schema) {
      const title = field.section ?? 'General';
      if (!grouped.has(title)) {
        grouped.set(title, []);
        order.push(title);
      }
      grouped.get(title)!.push(field);
    }
    return order.map(title => ({ title, fields: grouped.get(title)! }));
  }

  private buildControls(): Record<string, unknown> {
    const controls: Record<string, unknown> = {};
    for (const field of this.schema) {
      const value = this.toFormValue(field);
      const validators = buildSettingFieldValidators(field);
      if (field.type === 'readonly') {
        controls[field.key] = [{ value, disabled: true }];
      } else {
        controls[field.key] = [value, validators];
      }
    }
    return controls;
  }

  private toFormValue(field: SettingFieldSchema): boolean | number | string {
    const raw = this.values[field.key] ?? '';
    if (field.type === 'toggle') {
      return raw === 'true';
    }
    if (field.type === 'number') {
      return raw === '' ? '' : Number(raw);
    }
    return raw;
  }

  private serialize(): SettingsValues {
    const raw = this.form.getRawValue() as Record<string, unknown>;
    const result: SettingsValues = {};
    for (const field of this.schema) {
      const value = raw[field.key];
      if (field.type === 'toggle') {
        result[field.key] = value ? 'true' : 'false';
      } else if (value === null || value === undefined || value === '') {
        result[field.key] = null;
      } else {
        result[field.key] = String(value);
      }
    }
    return result;
  }
}
