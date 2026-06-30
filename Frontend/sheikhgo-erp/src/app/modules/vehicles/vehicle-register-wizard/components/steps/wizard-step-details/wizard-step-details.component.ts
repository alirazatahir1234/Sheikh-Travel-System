import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AbstractControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { UiInputComponent } from '../../../../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../../../shared/components/ui/types/ui.types';
import { UiStatusBadgeComponent } from '../../../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { VehicleCodeFieldComponent } from '../../vehicle-code-field/vehicle-code-field.component';
import { VinValidationState } from '../../../models/vehicle-wizard.model';

@Component({
  selector: 'app-wizard-step-details',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    UiInputComponent,
    UiSelectComponent,
    UiStatusBadgeComponent,
    VehicleCodeFieldComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6" [formGroup]="form()">
      <section class="overflow-visible rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
        <h2 class="mb-1 text-lg font-semibold text-fleet-text">Vehicle Details</h2>
        <p class="mb-5 text-sm text-fleet-text-muted">Basic identification and registration information.</p>

        <div class="grid gap-4 overflow-visible md:grid-cols-2">
          <ui-select
            formControlName="name"
            label="Vehicle Name"
            placeholder="Select vehicle name"
            [searchable]="true"
            [required]="true"
            [options]="vehicleNameOptions()"
            [error]="controlError('name')" />
          <app-vehicle-code-field
            formControlName="vehicleCode"
            (regenerate)="regenerateCode.emit()" />
          <ui-input
            formControlName="registrationNumber"
            label="License Plate"
            placeholder="ABC-1234"
            [required]="true"
            [error]="controlError('registrationNumber')" />
          <div>
            <ui-input
              formControlName="vin"
              label="VIN"
              placeholder="17-character VIN"
              [error]="controlError('vin')" />
            @switch (vinStatus()) {
              @case ('valid') {
                <div class="mt-1">
                  <ui-status-badge status="valid" label="VALID" />
                </div>
              }
              @case ('invalid') {
                <div class="mt-1">
                  <ui-status-badge status="expired" label="INVALID" />
                </div>
              }
              @case ('incomplete') {
                <p class="mt-1 text-xs text-fleet-text-muted">
                  {{ vinLength() }}/17 characters
                </p>
              }
            }
          </div>
          <ui-select formControlName="year" label="Year" [options]="yearOptions()" />
          <ui-select
            formControlName="make"
            label="Make"
            placeholder="Select make"
            [searchable]="true"
            [required]="true"
            [options]="makeOptions()"
            [error]="controlError('make')" />
          <ui-select
            formControlName="model"
            label="Model"
            placeholder="Select model"
            [searchable]="true"
            [required]="true"
            [options]="modelOptions()"
            [error]="controlError('model')" />
          <ui-select
            formControlName="color"
            label="Color"
            placeholder="Select color"
            [searchable]="true"
            [required]="true"
            [options]="colorOptions()"
            [error]="controlError('color')" />
        </div>
      </section>

      <div class="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">
        <strong>Pro tip:</strong> Enter the VIN to auto-validate format. A valid VIN helps with insurance and compliance tracking.
      </div>
    </div>
  `
})
export class WizardStepDetailsComponent {
  readonly form = input.required<FormGroup>();
  readonly yearOptions = input.required<UiSelectOption[]>();
  readonly vehicleNameOptions = input<UiSelectOption[]>([]);
  readonly makeOptions = input<UiSelectOption[]>([]);
  readonly modelOptions = input<UiSelectOption[]>([]);
  readonly colorOptions = input<UiSelectOption[]>([]);
  readonly vinStatus = input<VinValidationState>('empty');
  readonly regenerateCode = output<void>();

  vinLength(): number {
    const value = this.form().get('vin')?.value as string | undefined;
    return value?.trim().length ?? 0;
  }

  controlError(name: string): string | undefined {
    const control = this.form().get(name);
    if (!control || !this.shouldShow(control)) return undefined;
    if (!control.errors) return undefined;

    if (control.hasError('required')) return 'This field is required.';
    if (name === 'registrationNumber' && control.hasError('conflict')) return 'This license plate is already in use.';
    if (control.hasError('maxlength')) return 'Maximum length exceeded.';
    if (name === 'vin' && control.hasError('pattern')) return 'VIN must be alphanumeric and cannot include I, O, or Q.';
    if (name === 'vin' && control.hasError('vinIncomplete')) return 'VIN must be exactly 17 characters.';
    if (name === 'vin' && control.hasError('vinInvalid')) return 'VIN format is invalid.';
    return undefined;
  }

  private shouldShow(control: AbstractControl): boolean {
    return control.touched || control.dirty;
  }
}
