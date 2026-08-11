import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, OnInit, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatRadioModule } from '@angular/material/radio';
import { UiInputComponent } from '../../../../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../../../shared/components/ui/types/ui.types';
import { GpsDevice } from '../../../../../../core/models/gps-tracking.model';
import {
  AssignedTrackerInfo,
  resolveWizardTrackerModel,
  TRACKER_CATALOG_OPTIONS,
  TRACKER_VENDORS
} from '../../../models/vehicle-wizard.model';

@Component({
  selector: 'app-wizard-step-gps',
  standalone: true,
  imports: [ReactiveFormsModule, MatRadioModule, UiInputComponent, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6" [formGroup]="gpsForm()">
      <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
        <h2 class="mb-1 text-lg font-semibold text-fleet-text">GPS Tracker</h2>

        @if (showCurrentTrackerCard()) {
          <p class="mb-5 text-sm text-fleet-text-muted">
            This vehicle already has a GPS tracker. Keep it, or change to a different device.
          </p>

          <div class="rounded-lg border border-emerald-200 bg-emerald-50/60 p-4">
            <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div class="space-y-2">
                <p class="text-xs font-semibold uppercase tracking-wide text-emerald-800">Current Tracker</p>
                <p class="text-base font-semibold text-fleet-text">{{ assignedTrackerTitle() }}</p>
                <dl class="grid gap-1 text-sm text-fleet-text-muted">
                  @if (assignedTracker()?.uniqueId) {
                    <div class="flex gap-2">
                      <dt class="font-medium text-fleet-text">IMEI:</dt>
                      <dd class="font-mono">{{ assignedTracker()!.uniqueId }}</dd>
                    </div>
                  }
                  <div class="flex gap-2">
                    <dt class="font-medium text-fleet-text">Status:</dt>
                    <dd class="font-medium text-emerald-700">Assigned</dd>
                  </div>
                </dl>
              </div>
              <button
                type="button"
                class="inline-flex items-center justify-center rounded-md border border-fleet-border bg-white px-4 py-2 text-sm font-semibold text-fleet-text shadow-sm hover:bg-slate-50"
                (click)="changeTracker.emit()">
                Change Tracker
              </button>
            </div>
          </div>
        } @else {
          <p class="mb-5 text-sm text-fleet-text-muted">
            @if (assignedTracker() && changingTracker()) {
              Choose a replacement tracker, or keep the current one.
            } @else {
              Assign a new tracker or link an existing unassigned device. This step is optional.
            }
          </p>

          <mat-radio-group formControlName="mode" class="mb-6 flex flex-col gap-3 sm:flex-row sm:flex-wrap">
            <mat-radio-button value="new">Register new tracker</mat-radio-button>
            <mat-radio-button value="existing">Use existing device</mat-radio-button>
            <mat-radio-button value="skip">
              {{ assignedTracker() && changingTracker() ? 'Keep current tracker' : 'Skip for now' }}
            </mat-radio-button>
          </mat-radio-group>

          @if (assignedTracker() && changingTracker()) {
            <div class="mb-4 flex items-center justify-between gap-3 rounded-md bg-slate-50 px-3 py-2 text-sm text-fleet-text-muted">
              <span>Replacing: <strong class="text-fleet-text">{{ assignedTrackerTitle() }}</strong></span>
              <button
                type="button"
                class="font-semibold text-fleet-primary hover:underline"
                (click)="cancelChange.emit()">
                Cancel
              </button>
            </div>
          }

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
                hint="Exactly 15 digits"
                [required]="true"
                [error]="controlError('uniqueId')" />
              <ui-input
                formControlName="simNumber"
                label="SIM Number"
                type="tel"
                hint="Enter digits only"
                [error]="controlError('simNumber')" />
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
            @if (deviceOptions().length === 0) {
              <p class="mt-2 text-sm text-fleet-text-muted">
                No active unassigned devices available. Register a new tracker instead.
              </p>
            }
          }
        }
      </section>
    </div>
  `
})
export class WizardStepGpsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  readonly gpsForm = input.required<FormGroup>();
  readonly unassignedDevices = input<GpsDevice[]>([]);
  readonly assignedTracker = input<AssignedTrackerInfo | null>(null);
  readonly changingTracker = input(false);
  readonly changeTracker = output<void>();
  readonly cancelChange = output<void>();

  readonly modelOptions: UiSelectOption[] = TRACKER_CATALOG_OPTIONS.map(m => ({
    value: m.key,
    label: m.label
  }));
  readonly vendorOptions: UiSelectOption[] = TRACKER_VENDORS.map(v => ({ value: v, label: v }));

  ngOnInit(): void {
    const modelCtrl = this.gpsForm().get('model');
    const vendorCtrl = this.gpsForm().get('vendor');
    if (!modelCtrl || !vendorCtrl) return;

    // Normalize legacy label values patched from older sessions.
    const resolved = resolveWizardTrackerModel(String(modelCtrl.value ?? ''));
    if (modelCtrl.value !== resolved.key) {
      modelCtrl.setValue(resolved.key, { emitEvent: false });
    }
    if (!vendorCtrl.value) {
      vendorCtrl.setValue(resolved.vendor, { emitEvent: false });
    }

    modelCtrl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(key => {
      const m = resolveWizardTrackerModel(String(key ?? ''));
      vendorCtrl.setValue(m.vendor, { emitEvent: false });
    });
  }

  showCurrentTrackerCard(): boolean {
    return !!this.assignedTracker() && !this.changingTracker();
  }

  assignedTrackerTitle(): string {
    const t = this.assignedTracker();
    if (!t) return 'GPS Tracker';
    const brandModel = [t.brandName, t.modelName]
      .map(s => s?.trim())
      .filter((s): s is string => !!s)
      .join(' ');
    if (brandModel) return brandModel;
    const name = t.deviceName?.trim();
    if (name) return name;
    if (t.uniqueId?.trim()) return `IMEI ${t.uniqueId.trim()}`;
    return 'GPS Tracker';
  }

  deviceOptions(): UiSelectOption[] {
    return this.unassignedDevices().map(d => ({
      value: String(d.id),
      label: this.formatUnassignedDeviceLabel(d)
    }));
  }

  controlError(name: string): string | undefined {
    const control = this.gpsForm().get(name);
    if (!control || !this.shouldShow(control)) return undefined;
    if (!control.errors) return undefined;
    if (control.hasError('required')) return 'This field is required.';
    if (name === 'simNumber' && control.hasError('pattern')) return 'SIM Number must contain digits only.';
    if (name === 'uniqueId' && control.hasError('pattern')) return 'IMEI must be exactly 15 digits.';
    if (name === 'uniqueId' && control.hasError('minlength')) return 'IMEI / Unique ID is too short.';
    if (name === 'uniqueId' && control.hasError('maxlength')) return 'IMEI / Unique ID is too long.';
    return undefined;
  }

  private formatUnassignedDeviceLabel(d: GpsDevice): string {
    const brand = (d.trackerBrandName ?? '').trim();
    const model = (d.modelName ?? d.model ?? '').trim();
    const brandModel = [brand, model].filter(Boolean).join(' ');
    const title = brandModel || d.name?.trim() || 'Tracker';
    const imei = d.uniqueId?.trim();
    return imei ? `${title} — IMEI ${imei}` : title;
  }

  private shouldShow(control: AbstractControl): boolean {
    return control.touched || control.dirty;
  }
}
