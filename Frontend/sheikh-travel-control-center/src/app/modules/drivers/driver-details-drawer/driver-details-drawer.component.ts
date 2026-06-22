import { ChangeDetectionStrategy, Component, effect, inject, input, model, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of, catchError } from 'rxjs';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  DRIVER_VERIFICATION_DOC_TYPES,
  Driver,
  DriverActiveDuty,
  DriverAssignment,
  DriverAttendance,
  DriverDocument,
  DriverLocation,
  DriverPerformanceSummary,
  DriverStatus,
  DriverStatusLabels,
  DriverTimelineEvent,
  DriverViolation,
  driverDisplayName
} from '../../../core/models/driver.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { vehicleUploadSizeError, UPLOAD_MAX_SIZE_LABEL } from '../../../core/utils/upload-url.util';
import { resolveUploadUrl, resolveDriverPhotoUrl } from '../../../core/utils/upload-url.util';
import { UiDrawerComponent } from '../../../shared/components/ui/drawer/ui-drawer.component';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiStatusBadgeComponent } from '../../../shared/components/ui/status-badge/ui-status-badge.component';
import { licenseExpiryLabel, licenseExpiryState, availabilityBucketLabel } from '../utils/driver-status.util';

type DrawerTab = 'overview' | 'license' | 'vehicle' | 'verification' | 'performance' | 'tracking' | 'timeline' | 'duty';

@Component({
  selector: 'driver-details-drawer',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterModule, MatIconModule, UiDrawerComponent, UiButtonComponent, UiStatusBadgeComponent],
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
  readonly assignments = signal<DriverAssignment[]>([]);
  readonly performance = signal<DriverPerformanceSummary | null>(null);
  readonly violations = signal<DriverViolation[]>([]);
  readonly attendance = signal<DriverAttendance[]>([]);
  readonly location = signal<DriverLocation | null>(null);
  readonly vehicles = signal<{ id: number; registrationNumber: string; vehicleCode?: string | null; name: string }[]>([]);
  readonly loading = signal(false);
  readonly assignVehicleId = signal<number | ''>('');
  readonly transferVehicleId = signal<number | ''>('');
  readonly uploadSizeError = signal<string | null>(null);
  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;
  readonly newRating = signal('');
  readonly violationForm = signal({ type: 'Overspeed', severity: 'Medium', description: '' });
  readonly attendanceForm = signal({ status: 'Present', notes: '' });

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
      documents: this.driverService.getDocuments(id).pipe(catchError(() => of([]))),
      timeline: this.driverService.getTimeline(id).pipe(catchError(() => of([]))),
      activeDuty: this.driverService.getActiveDuty(id).pipe(catchError(() => of(null))),
      assignments: this.driverService.getAssignments(id, 1, 10).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 }))),
      performance: this.driverService.getPerformanceSummary(id).pipe(catchError(() => of(null))),
      violations: this.driverService.getViolations(id, 1, 10).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 }))),
      attendance: this.driverService.getAttendance(id).pipe(catchError(() => of([]))),
      location: this.driverService.getLocation(id).pipe(catchError(() => of(null))),
      vehicles: this.vehicleService.getAll(1, 200).pipe(catchError(() => of({ items: [] })))
    }).subscribe({
      next: data => {
        this.driver.set(data.driver);
        this.documents.set(data.documents);
        this.timeline.set(data.timeline);
        this.activeDuty.set(data.activeDuty);
        this.assignments.set(data.assignments.items);
        this.performance.set(data.performance);
        this.violations.set(data.violations.items);
        this.attendance.set(data.attendance);
        this.location.set(data.location);
        this.vehicles.set(data.vehicles.items.map(v => ({
          id: v.id,
          registrationNumber: v.registrationNumber,
          vehicleCode: v.vehicleCode,
          name: v.name
        })));
        this.assignVehicleId.set(data.driver.assignedVehicleId ?? '');
        this.newRating.set(data.driver.rating != null ? String(data.driver.rating) : '');
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

  availabilityLabel(d: Driver): string {
    return availabilityBucketLabel(d.availabilityBucket);
  }

  emergencyContactDisplay(d: Driver): string {
    const n = d.emergencyContactName?.trim();
    const phone = d.emergencyContact?.trim();
    if (n && phone) return `${n} (${phone})`;
    return n || phone || '—';
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

  openLiveMap(): void {
    const id = this.driverId();
    if (!id) return;
    void this.router.navigate(['/gps-tracking/live'], { queryParams: { driverId: id } });
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

  unassignVehicle(): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.unassignVehicle(id).subscribe({
      next: () => {
        this.snackBar.open('Vehicle unassigned', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Unassign failed'), 'Close', { duration: 3000 })
    });
  }

  transferVehicle(): void {
    const id = this.driverId();
    const newVehicleId = Number(this.transferVehicleId());
    if (!id || !newVehicleId) return;
    this.driverService.transferVehicle(id, { newVehicleId }).subscribe({
      next: () => {
        this.snackBar.open('Vehicle transferred', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Transfer failed'), 'Close', { duration: 3000 })
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

  toggleActive(): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.toggleActive(id).subscribe({
      next: () => {
        this.snackBar.open('Account status updated', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Update failed'), 'Close', { duration: 3000 })
    });
  }

  saveRating(): void {
    const id = this.driverId();
    const rating = Number(this.newRating());
    if (!id || Number.isNaN(rating) || rating < 0 || rating > 5) {
      this.snackBar.open('Enter a rating between 0 and 5', 'Close', { duration: 2500 });
      return;
    }
    this.driverService.updateRating(id, rating).subscribe({
      next: () => {
        this.snackBar.open('Rating saved', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Save failed'), 'Close', { duration: 3000 })
    });
  }

  logViolation(): void {
    const id = this.driverId();
    const form = this.violationForm();
    if (!id || !form.description.trim()) {
      this.snackBar.open('Enter a violation description', 'Close', { duration: 2500 });
      return;
    }
    this.driverService.createViolation(id, {
      violationType: form.type,
      severity: form.severity,
      occurredAt: new Date().toISOString(),
      description: form.description.trim()
    }).subscribe({
      next: () => {
        this.snackBar.open('Violation logged', 'Close', { duration: 2000 });
        this.violationForm.set({ ...form, description: '' });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Failed to log violation'), 'Close', { duration: 3000 })
    });
  }

  logAttendance(): void {
    const id = this.driverId();
    const form = this.attendanceForm();
    if (!id) return;
    this.driverService.createAttendance(id, {
      attendanceDate: new Date().toISOString().slice(0, 10),
      status: form.status,
      notes: form.notes.trim() || null
    }).subscribe({
      next: () => {
        this.snackBar.open('Attendance recorded', 'Close', { duration: 2000 });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Failed to record attendance'), 'Close', { duration: 3000 })
    });
  }

  updateViolationField(field: 'type' | 'severity' | 'description', value: string): void {
    const current = this.violationForm();
    if (field === 'type') this.violationForm.set({ ...current, type: value });
    else if (field === 'severity') this.violationForm.set({ ...current, severity: value });
    else this.violationForm.set({ ...current, description: value });
  }

  updateAttendanceField(field: 'status' | 'notes', value: string): void {
    const current = this.attendanceForm();
    if (field === 'status') this.attendanceForm.set({ ...current, status: value });
    else this.attendanceForm.set({ ...current, notes: value });
  }

  statusLabels = DriverStatusLabels;
  driverStatus = DriverStatus;
  resolveUploadUrl = resolveUploadUrl;
}
