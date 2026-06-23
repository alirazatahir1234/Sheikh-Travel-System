import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AbstractControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatRadioModule } from '@angular/material/radio';
import { UiInputComponent } from '../../../../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../../../shared/components/ui/types/ui.types';
import { GpsDevice } from '../../../../../../core/models/gps-tracking.model';
import { TRACKER_MODELS, TRACKER_VENDORS } from '../../../models/vehicle-wizard.model';

@Component({
  selector: 'app-wizard-step-gps',
  standalone: true,
  imports: [ReactiveFormsModule, MatRadioModule, UiInputComponent, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6" [formGroup]="gpsForm()">
      <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
        <h2 class="mb-1 text-lg font-semibold text-fleet-text">GPS Tracker</h2>
        <p class="mb-5 text-sm text-fleet-text-muted">Assign a new tracker or link an existing unassigned device. This step is optional.</p>

        <mat-radio-group formControlName="mode" class="mb-6 flex flex-col gap-3 sm:flex-row">
          <mat-radio-button value="new">Register new tracker</mat-radio-button>
          <mat-radio-button value="existing">Use existing device</mat-radio-button>
          <mat-radio-button value="skip">Skip for now</mat-radio-button>
        </mat-radio-group>

        @if (gpsForm().get('mode')?.value === 'new') {
          <div class="grid gap-4 md:grid-cols-2">
            <ui-select
              formControlName="model"
              label="Tracker Model"
              [options]="modelOptions"
              [required]="true"
              [error]="controlError('model')" />
            <ui-input
              formControlName="uniqueId"
              label="IMEI / Unique ID"
              [required]="true"
              [error]="controlError('uniqueId')" />
            <ui-input formControlName="simNumber" label="SIM Number" />
            <ui-select
              formControlName="vendor"
              label="Provider"
              [options]="vendorOptions"
              [required]="true"
              [error]="controlError('vendor')" />
            <ui-input formControlName="deviceName" label="Device Name (optional)" class="md:col-span-2" />
          </div>
        }

        @if (gpsForm().get('mode')?.value === 'existing') {
          <ui-select
            formControlName="existingDeviceId"
            label="Unassigned Device"
            [options]="deviceOptions()"
            [searchable]="true"
            [required]="true"
            [error]="controlError('existingDeviceId')"
            searchPlaceholder="Search devices…" />
        }

        @if (gpsAssigned()) {
          <p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            A GPS tracker is already assigned to this vehicle.
          </p>
        }
      </section>
    </div>
  `
})
export class WizardStepGpsComponent {
  readonly gpsForm = input.required<FormGroup>();
  readonly unassignedDevices = input<GpsDevice[]>([]);
  readonly gpsAssigned = input(false);

  readonly modelOptions: UiSelectOption[] = TRACKER_MODELS.map(m => ({ value: m, label: m }));
  readonly vendorOptions: UiSelectOption[] = TRACKER_VENDORS.map(v => ({ value: v, label: v }));

  deviceOptions(): UiSelectOption[] {
    return this.unassignedDevices().map(d => ({
      value: String(d.id),
      label: `${d.name} (${d.uniqueId})`
    }));
  }

  controlError(name: string): string | undefined {
    const control = this.gpsForm().get(name);
    if (!control || !this.shouldShow(control)) return undefined;
    if (!control.errors) return undefined;
    if (control.hasError('required')) return 'This field is required.';
    if (name === 'uniqueId' && control.hasError('minlength')) return 'IMEI / Unique ID is too short.';
    if (name === 'uniqueId' && control.hasError('maxlength')) return 'IMEI / Unique ID is too long.';
    return undefined;
  }

  private shouldShow(control: AbstractControl): boolean {
    return control.touched || control.dirty;
  }
}
