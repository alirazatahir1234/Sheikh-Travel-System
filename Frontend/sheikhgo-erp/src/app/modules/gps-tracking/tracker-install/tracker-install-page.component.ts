import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { TrackerDetail } from '../../../core/models/gps-tracking.model';
import { Vehicle, VehicleStatus } from '../../../core/models/vehicle.model';
import { DriverListItem } from '../../../core/models/driver.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import {
  buildRelayOutputOptions,
  RELAY_OUTPUT_HINT,
  relayOutputLabel,
  resolveDefaultRelayOutput,
} from '../utils/relay-immobilizer.util';
import { todayIsoDate } from '../tracker-register/tracker-register.validators';
import { isTrackerInstalled } from '../utils/tracker-status.util';

@Component({
  selector: 'app-tracker-install-page',
  templateUrl: './tracker-install-page.component.html',
  styleUrls: ['./tracker-install-page.component.scss']
})
export class TrackerInstallPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly gps = inject(GpsTrackingService);
  private readonly vehiclesSvc = inject(VehicleService);
  private readonly driversSvc = inject(DriverService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly relayOutputHint = RELAY_OUTPUT_HINT;
  readonly minDate = todayIsoDate();

  tracker: TrackerDetail | null = null;
  vehicles: Vehicle[] = [];
  drivers: DriverListItem[] = [];
  loading = false;
  saving = false;
  isReassign = false;
  trackerId = 0;

  form = this.fb.group({
    vehicleId: ['', Validators.required],
    driverId: [''],
    installationDate: [todayIsoDate(), Validators.required],
    installedBy: [''],
    installationNotes: [''],
    relayOutput: ['output1'],
  });

  ngOnInit(): void {
    this.trackerId = Number(this.route.snapshot.paramMap.get('id'));
    this.isReassign = this.route.snapshot.queryParamMap.get('reassign') === '1';

    if (!this.trackerId) {
      void this.router.navigate(['/gps-tracking/devices']);
      return;
    }

    this.loading = true;
    this.gps.getTracker(this.trackerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: tracker => {
        if (!this.isReassign && isTrackerInstalled(tracker)) {
          this.toast.warning('Tracker is already installed. Use Reassign to move it.');
          void this.router.navigate(['/gps-tracking/devices']);
          return;
        }

        this.tracker = tracker;
        this.form.patchValue({
          relayOutput: tracker.relayOutput ?? 'output1',
          installationNotes: tracker.installationNotes ?? '',
          installedBy: tracker.installedBy ?? '',
        });

        this.loadLookups();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Tracker not found');
        void this.router.navigate(['/gps-tracking/devices']);
      }
    });
  }

  get vehicleOptions(): UiSelectOption[] {
    return this.vehicles.map(v => ({
      value: String(v.id),
      label: `${v.name} - ${v.registrationNumber}${v.vehicleCode ? ` · ${v.vehicleCode}` : ''}`
    }));
  }

  get driverOptions(): UiSelectOption[] {
    return this.drivers.map(d => ({
      value: String(d.id),
      label: d.fullName
    }));
  }

  get showRelayOutput(): boolean {
    return !!this.tracker?.supportsEngineCutoff;
  }

  get relayOutputOptions(): UiSelectOption[] {
    const recommended = this.tracker?.relayOutput ?? resolveDefaultRelayOutput();
    return buildRelayOutputOptions(recommended);
  }

  get relayConfiguredLabel(): string {
    return relayOutputLabel(this.form.get('relayOutput')?.value as string);
  }

  get pageTitle(): string {
    return this.isReassign ? 'Reassign Tracker' : 'Install Tracker';
  }

  get trackerSummary(): string {
    if (!this.tracker) return '';
    const brand = this.tracker.trackerBrandName || this.tracker.vendor || '';
    const model = this.tracker.modelName || this.tracker.model || '';
    return [brand, model].filter(Boolean).join(' ');
  }

  cancel(): void {
    void this.router.navigate(['/gps-tracking/devices']);
  }

  submit(): void {
    if (this.form.invalid || this.saving || !this.tracker) return;

    this.saving = true;
    const v = this.form.getRawValue();

    this.gps.installTracker(this.trackerId, {
      vehicleId: Number(v.vehicleId),
      driverId: v.driverId ? Number(v.driverId) : undefined,
      installationDate: v.installationDate as string,
      installedBy: (v.installedBy as string) || undefined,
      installationNotes: (v.installationNotes as string) || undefined,
      relayOutput: this.showRelayOutput ? (v.relayOutput as string) : undefined,
    }).pipe(
      finalize(() => { this.saving = false; })
    ).subscribe({
      next: () => {
        this.toast.success(this.isReassign ? 'Tracker reassigned' : 'Tracker installed');
        void this.router.navigate(['/gps-tracking/devices']);
      },
      error: err => this.toast.error(err?.error?.message ?? 'Installation failed')
    });
  }

  private loadLookups(): void {
    this.vehiclesSvc.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.vehicles = res.items.filter(
          v => v.status !== VehicleStatus.Draft && v.name.trim().toLowerCase() !== 'draft vehicle'
        );
      }
    });

    this.driversSvc.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.drivers = res.items;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }
}
