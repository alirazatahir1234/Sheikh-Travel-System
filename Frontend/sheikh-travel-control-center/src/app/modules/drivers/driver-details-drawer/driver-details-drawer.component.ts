import { ChangeDetectionStrategy, Component, effect, inject, input, model, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  DRIVER_VERIFICATION_DOC_TYPES,
  Driver,
  DriverActiveDuty,
  DriverDocument,
  DriverStatus,
  DriverStatusLabels,
  DriverTimelineEvent,
  driverDisplayName
} from '../../../core/models/driver.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { vehicleUploadSizeError, UPLOAD_MAX_SIZE_LABEL } from '../../../core/utils/upload-url.util';
import { resolveUploadUrl, resolveDriverPhotoUrl } from '../../../core/utils/upload-url.util';
import { UiDrawerComponent } from '../../../shared/components/ui/drawer/ui-drawer.component';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiStatusBadgeComponent } from '../../../shared/components/ui/status-badge/ui-status-badge.component';
import { licenseExpiryLabel, licenseExpiryState } from '../utils/driver-status.util';

type DrawerTab = 'overview' | 'license' | 'vehicle' | 'verification' | 'timeline' | 'duty';

@Component({
  selector: 'driver-details-drawer',
  standalone: true,
  imports: [DatePipe, RouterModule, MatIconModule, UiDrawerComponent, UiButtonComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-details-drawer.component.html',
  styleUrls: ['./driver-details-drawer.component.scss']
})
export class DriverDetailsDrawerComponent {
  private readonly driverService = inject(DriverService);
  private readonly vehicleService = inject(VehicleService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  readonly open = model(false);
  readonly driverId = input<number | null>(null);
  readonly changed = output<void>();

  readonly tab = signal<DrawerTab>('overview');
  readonly driver = signal<Driver | null>(null);
  readonly documents = signal<DriverDocument[]>([]);
  readonly timeline = signal<DriverTimelineEvent[]>([]);
  readonly activeDuty = signal<DriverActiveDuty | null>(null);
  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly loading = signal(false);
  readonly assignVehicleId = signal<number | ''>('');
  readonly uploadSizeError = signal<string | null>(null);
  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;

  readonly docTypes = DRIVER_VERIFICATION_DOC_TYPES;

  constructor() {
    effect(() => {
      const id = this.driverId();
      const isOpen = this.open();
      if (isOpen && id) this.load(id);
    }, { allowSignalWrites: true });
  }

  load(id: number): void {
    this.loading.set(true);
    forkJoin({
      driver: this.driverService.getById(id),
      documents: this.driverService.getDocuments(id),
      timeline: this.driverService.getTimeline(id),
      activeDuty: this.driverService.getActiveDuty(id),
      vehicles: this.vehicleService.getAll(1, 200)
    }).subscribe({
      next: data => {
        this.driver.set(data.driver);
        this.documents.set(data.documents);
        this.timeline.set(data.timeline);
        this.activeDuty.set(data.activeDuty);
        this.vehicles.set(data.vehicles.items);
        this.assignVehicleId.set(data.driver.assignedVehicleId ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load driver details', 'Close', { duration: 3000 });
      }
    });
  }

  name(d: Driver): string {
    return driverDisplayName(d);
  }

  emergencyContactDisplay(d: Driver): string {
    const name = d.emergencyContactName?.trim();
    const phone = d.emergencyContact?.trim();
    if (name && phone) return `${name} (${phone})`;
    return name || phone || '—';
  }

  photoUrl(d: Driver): string | null {
    return resolveDriverPhotoUrl(d.photoUrl);
  }

  licenseBadge(d: Driver): { label: string; variant: 'success' | 'warning' | 'error' } {
    const state = licenseExpiryState(d.licenseExpired, d.licenseExpiringSoon);
    return {
      label: licenseExpiryLabel(state),
      variant: state === 'expired' ? 'error' : state === 'expiring' ? 'warning' : 'success'
    };
  }

  onDrawerClosed(): void {
    this.open.set(false);
  }

  editDriver(): void {
    const id = this.driverId();
    if (!id) return;
    this.open.set(false);
    void this.router.navigate(['/drivers', id, 'edit']);
  }

  docFor(type: string): DriverDocument | undefined {
    return this.documents().find(d => d.documentType === type);
  }

  onUploadDoc(type: string, event: Event): void {
    const id = this.driverId();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!id || !file) return;

    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.uploadSizeError.set(sizeError);
      input.value = '';
      return;
    }

    this.uploadSizeError.set(null);
    this.driverService.uploadDocument(id, type, file).subscribe({
      next: () => {
        this.snackBar.open('Document uploaded', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
        input.value = '';
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Upload failed'), 'Close', { duration: 3000 })
    });
  }

  markVerified(): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.updateVerification(id, 'Verified').subscribe({
      next: () => {
        this.snackBar.open('Driver marked as verified', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Update failed'), 'Close', { duration: 3000 })
    });
  }

  assignVehicle(): void {
    const id = this.driverId();
    const vehicleId = Number(this.assignVehicleId());
    if (!id || !vehicleId) return;
    this.driverService.assignVehicle(id, { vehicleId }).subscribe({
      next: () => {
        this.snackBar.open('Vehicle assigned', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Assignment failed'), 'Close', { duration: 3000 })
    });
  }

  setStatus(status: DriverStatus): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.changeStatus(id, status).subscribe({
      next: () => {
        this.snackBar.open('Status updated', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Status update failed'), 'Close', { duration: 3000 })
    });
  }

  statusLabels = DriverStatusLabels;
  driverStatus = DriverStatus;
  resolveUploadUrl = resolveUploadUrl;
}
