import { Component, Input, OnChanges, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { SettingsService } from '../../services/settings.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { SettingFieldSchema, SettingsValues } from '../../models/settings.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

interface SettingsSection {
  title: string;
  fields: SettingFieldSchema[];
}

@Component({
  selector: 'app-dynamic-settings-form',
  templateUrl: './dynamic-settings-form.component.html',
  styleUrls: ['./dynamic-settings-form.component.scss']
})
export class DynamicSettingsFormComponent implements OnChanges {
  @Input() category = '';
  @Input() schema: SettingFieldSchema[] = [];
  @Input() values: SettingsValues = {};

  private readonly fb = inject(FormBuilder);
  private readonly settings = inject(SettingsService);
  private readonly toast = inject(UiToastService);

  form: FormGroup = this.fb.group({});
  sections: SettingsSection[] = [];
  saving = false;

  ngOnChanges(): void {
    this.sections = this.buildSections();
    this.form = this.fb.group(this.buildControls());
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
      controls[field.key] = field.type === 'readonly' ? [{ value, disabled: true }] : value;
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

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving = true;
    const payload = this.serialize();
    this.settings.update(this.category, payload).subscribe({
      next: () => {
        this.saving = false;
        this.values = payload;
        this.form.markAsPristine();
        this.toast.success('Settings saved.');
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save settings.'));
      }
    });
  }

  discard(): void {
    this.form.reset(this.buildControls());
    this.form.markAsPristine();
  }

  private serialize(): SettingsValues {
    const result: SettingsValues = {};
    for (const field of this.schema) {
      const value = this.form.get(field.key)?.value;
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
