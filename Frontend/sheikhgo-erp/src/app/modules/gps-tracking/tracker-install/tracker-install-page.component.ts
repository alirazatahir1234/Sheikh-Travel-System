import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { TrackerDetail, TrackerInstallVehicle } from '../../../core/models/gps-tracking.model';
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
  standalone: false,
  selector: 'app-tracker-install-page',
  templateUrl: './tracker-install-page.component.html',
  styleUrls: ['./tracker-install-page.component.scss']
})
export class TrackerInstallPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly gps = inject(GpsTrackingService);
  private readonly driversSvc = inject(DriverService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly relayOutputHint = RELAY_OUTPUT_HINT;
  readonly minDate = todayIsoDate();

  tracker: TrackerDetail | null = null;
  installVehicles: TrackerInstallVehicle[] = [];
  drivers: DriverListItem[] = [];
  loading = false;
  saving = false;
  isReassign = false;
  isTransfer = false;
  trackerId = 0;

  form = this.fb.group({
    vehicleId: ['', Validators.required],
    driverId: [''],
    installationDate: [todayIsoDate(), Validators.required],
    installedBy: [''],
    installationNotes: [''],
    relayOutput: ['output1'],
    reason: [''],
  });

  ngOnInit(): void {
    this.trackerId = Number(this.route.snapshot.paramMap.get('id'));
    this.isReassign = this.route.snapshot.queryParamMap.get('reassign') === '1';
    this.isTransfer = this.route.snapshot.url.some(s => s.path === 'transfer') || this.isReassign;

    if (!this.trackerId) {
      void this.router.navigate(['/gps-tracking/devices']);
      return;
    }

    this.loading = true;
    this.gps.getTracker(this.trackerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: tracker => {
        if (!this.isTransfer && isTrackerInstalled(tracker)) {
          this.toast.warning(
            `This tracker is already installed on ${tracker.vehicleName ?? 'a vehicle'}.`
          );
          void this.router.navigate(['/gps-tracking/devices', this.trackerId]);
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
    return this.installVehicles.map(v => ({
      value: String(v.vehicleId),
      label: this.installVehicleLabel(v),
      disabled: !v.isSelectable,
    }));
  }

  get selectableVehicleCount(): number {
    return this.installVehicles.filter(v => v.isSelectable).length;
  }

  get vehicleInstallHint(): string | null {
    if (this.selectableVehicleCount > 0) {
      return null;
    }
    if (!this.installVehicles.length) {
      return 'No vehicles found. Register and publish a vehicle in Fleet Management first.';
    }
    const draftBlocked = this.installVehicles.some(v =>
      (v.blockedReason ?? '').toLowerCase().includes('publish'));
    if (draftBlocked) {
      return 'Published vehicles without a tracker appear here. Draft vehicles must be published on the Review step before installation.';
    }
    return 'No vehicles are available for installation. Remove or transfer the active tracker from a vehicle first.';
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
    return this.isTransfer ? 'Transfer Tracker' : 'Install Tracker';
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

    if (this.isTransfer && !this.form.get('reason')?.value?.trim()) {
      this.form.get('reason')?.setErrors({ required: true });
      this.toast.error('Transfer reason is required');
      return;
    }

    const selectedVehicleId = Number(this.form.get('vehicleId')?.value);
    const selected = this.installVehicles.find(v => v.vehicleId === selectedVehicleId);
    if (!selected?.isSelectable) {
      this.toast.error(selected?.blockedReason ?? 'This vehicle already has an active tracker');
      return;
    }

    this.saving = true;
    const v = this.form.getRawValue();

    const payload = {
      vehicleId: Number(v.vehicleId),
      driverId: v.driverId ? Number(v.driverId) : undefined,
      installationDate: v.installationDate as string,
      installedBy: (v.installedBy as string) || undefined,
      installationNotes: (v.installationNotes as string) || undefined,
      relayOutput: this.showRelayOutput ? (v.relayOutput as string) : undefined,
    };

    const req$ = this.isTransfer
      ? this.gps.transferTracker(this.trackerId, { ...payload, reason: (v.reason as string).trim() })
      : this.gps.installTracker(this.trackerId, payload);

    req$.pipe(
      finalize(() => { this.saving = false; })
    ).subscribe({
      next: () => {
        this.toast.success(this.isTransfer ? 'Tracker transferred' : 'Tracker installed');
        void this.router.navigate(['/gps-tracking/devices', this.trackerId]);
      },
      error: err => this.toast.error(err?.error?.message ?? 'Installation failed')
    });
  }

  private loadLookups(): void {
    this.gps.getTrackerInstallVehicles(this.trackerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: vehicles => {
        this.installVehicles = vehicles;
      },
      error: () => {
        this.toast.error('Failed to load vehicles');
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

  private installVehicleLabel(v: TrackerInstallVehicle): string {
    const base = [v.name, v.plateNumber].filter(Boolean).join(' — ');
    const code = v.vehicleCode ? ` · ${v.vehicleCode}` : '';
    if (v.isSelectable) {
      return `${base}${code}`;
    }
    const reason = v.blockedReason ?? (v.assignedTrackerName
      ? `Already assigned to ${v.assignedTrackerName}`
      : 'Already has an active tracker');
    return `${base}${code} — ${reason}`;
  }
}
