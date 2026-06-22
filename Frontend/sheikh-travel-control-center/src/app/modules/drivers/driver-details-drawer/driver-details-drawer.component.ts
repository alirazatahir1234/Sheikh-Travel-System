import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, of, catchError } from 'rxjs';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { UiConfirmService } from '../../../shared/components/ui/confirm-dialog/ui-confirm.service';
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
import { UiSelectComponent } from '../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { licenseExpiryLabel, licenseExpiryState, availabilityBucketLabel } from '../utils/driver-status.util';

type DrawerTab = 'overview' | 'license' | 'vehicle' | 'verification' | 'performance' | 'tracking' | 'timeline' | 'duty';

interface VehicleOption {
  id: number;
  registrationNumber: string;
  vehicleCode?: string | null;
  name: string;
  make?: string | null;
  model?: string | null;
  color?: string | null;
}

export interface AssignmentTimelineEntry {
  date: string;
  action: string;
  detail: string;
  assignment: DriverAssignment;
  isActive: boolean;
}

@Component({
  selector: 'driver-details-drawer',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    MatIconModule,
    UiDrawerComponent,
    UiButtonComponent,
    UiStatusBadgeComponent,
    UiSelectComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-details-drawer.component.html',
  styleUrls: ['./driver-details-drawer.component.scss']
})
export class DriverDetailsDrawerComponent {
  private readonly driverService = inject(DriverService);
  private readonly vehicleService = inject(VehicleService);
  private readonly confirm = inject(UiConfirmService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

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
  readonly vehicles = signal<VehicleOption[]>([]);
  readonly loading = signal(false);
  readonly assignVehicleRemarks = signal('');
  readonly assignEffectiveFrom = signal('');
  readonly assignEffectiveTo = signal('');
  readonly uploadSizeError = signal<string | null>(null);
  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;
  readonly newRating = signal('');
  readonly violationForm = signal({ type: 'Overspeed', severity: 'Medium', description: '' });
  readonly attendanceForm = signal({ status: 'Present', notes: '' });

  selectedVehicleId: string | null = null;

  readonly vehicleSelectOptions = computed<UiSelectOption[]>(() => {
    const currentId = this.driver()?.assignedVehicleId;
    return this.vehicles().map(v => ({
      value: String(v.id),
      label: this.vehicleSelectLabel(v),
      disabled: v.id === currentId
    }));
  });

  readonly assignmentTimeline = computed<AssignmentTimelineEntry[]>(() => {
    const items = [...this.assignments()].sort(
      (a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime()
    );
    return items.map((a, index) => {
      const prev = index > 0 ? items[index - 1] : null;
      const vehicleLabel = this.assignmentVehicleLabel(a);
      let action = 'Assigned';
      if (a.assignmentType?.toLowerCase() === 'transfer') {
        action = prev ? `Transferred to ${vehicleLabel}` : 'Transferred';
      } else if (a.status.toLowerCase() === 'completed' && a.endAt && !prev) {
        action = 'Assignment removed';
      } else if (prev && a.vehicleId !== prev.vehicleId) {
        action = `Transferred to ${vehicleLabel}`;
      }
      const detailParts = [
        vehicleLabel,
        a.vehicleMake || a.vehicleName ? [a.vehicleMake, a.vehicleModel].filter(Boolean).join(' ') || a.vehicleName : null,
        a.vehicleColor,
        a.vehicleRegistration && a.vehicleRegistration !== vehicleLabel ? a.vehicleRegistration : null
      ].filter(Boolean);
      return {
        date: a.startAt,
        action,
        detail: detailParts.join(' · '),
        assignment: a,
        isActive: a.status.toLowerCase() === 'active'
      };
    }).reverse();
  });

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
    this.resetVehicleForm();
    forkJoin({
      driver: this.driverService.getById(id),
      documents: this.driverService.getDocuments(id).pipe(catchError(() => of([]))),
      timeline: this.driverService.getTimeline(id).pipe(catchError(() => of([]))),
      activeDuty: this.driverService.getActiveDuty(id).pipe(catchError(() => of(null))),
      assignments: this.driverService.getAssignments(id, 1, 20).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }))),
      performance: this.driverService.getPerformanceSummary(id).pipe(catchError(() => of(null))),
      violations: this.driverService.getViolations(id, 1, 10).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 }))),
      attendance: this.driverService.getAttendance(id).pipe(catchError(() => of([]))),
      location: this.driverService.getLocation(id).pipe(catchError(() => of(null))),
      vehicles: this.vehicleService.getAll(1, 200).pipe(catchError(() => of({ items: [] })))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
          name: v.name,
          make: v.make,
          model: v.model,
          color: (v as { color?: string | null }).color
        })));
        this.newRating.set(data.driver.rating != null ? String(data.driver.rating) : '');
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load driver details');
      }
    });
  }

  private resetVehicleForm(): void {
    this.selectedVehicleId = null;
    this.assignVehicleRemarks.set('');
    this.assignEffectiveFrom.set(new Date().toISOString().slice(0, 10));
    this.assignEffectiveTo.set('');
  }

  name(d: Driver): string {
    return driverDisplayName(d);
  }

  availabilityLabel(d: Driver): string {
    return availabilityBucketLabel(d.availabilityBucket);
  }

  driverStatusBadge(d: Driver): { label: string; variant: 'success' | 'warning' | 'error' | 'info' } {
    if (!d.isActive || d.status === DriverStatus.Suspended) {
      return { label: 'Suspended', variant: 'error' };
    }
    if (d.status === DriverStatus.OnTrip) {
      return { label: 'On Duty', variant: 'info' };
    }
    if (d.assignedVehicleId) {
      return { label: 'Assigned', variant: 'success' };
    }
    if (d.status === DriverStatus.Available) {
      return { label: 'Available', variant: 'success' };
    }
    return { label: DriverStatusLabels[d.status], variant: 'warning' };
  }

  currentVehiclePrimary(d: Driver): string {
    return d.assignedVehicleCode || d.assignedVehicleRegistration || '—';
  }

  currentVehicleModelLine(d: Driver): string | null {
    const makeModel = [d.assignedVehicleMake, d.assignedVehicleModel].filter(Boolean).join(' ');
    return makeModel || d.assignedVehicleName || null;
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

  canSubmitVehicleChange(): boolean {
    const d = this.driver();
    const vehicleId = Number(this.selectedVehicleId);
    return !!d && !!vehicleId && vehicleId !== d.assignedVehicleId;
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
    this.driverService.uploadDocument(id, type, file).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Document uploaded');
        this.load(id);
        this.changed.emit();
        input.value = '';
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Upload failed'))
    });
  }

  markVerified(): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.updateVerification(id, 'Verified').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Driver marked as verified');
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Update failed'))
    });
  }

  private assignmentPayload() {
    const remarks = this.assignVehicleRemarks().trim() || null;
    const effectiveFrom = this.assignEffectiveFrom() || null;
    const effectiveTo = this.assignEffectiveTo() || null;
    return { remarks, effectiveFrom, effectiveTo };
  }

  submitVehicleChange(): void {
    const id = this.driverId();
    const d = this.driver();
    const vehicleId = Number(this.selectedVehicleId);
    if (!id || !d || !vehicleId || vehicleId === d.assignedVehicleId) return;

    const payload = this.assignmentPayload();
    const request$ = d.assignedVehicleId
      ? this.driverService.transferVehicle(id, { newVehicleId: vehicleId, ...payload, assignmentType: 'Transfer' })
      : this.driverService.assignVehicle(id, { vehicleId, ...payload, assignmentType: 'Manual' });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success(d.assignedVehicleId ? 'Vehicle changed' : 'Vehicle assigned');
        this.resetVehicleForm();
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Assignment failed'))
    });
  }

  async removeAssignment(): Promise<void> {
    const id = this.driverId();
    const d = this.driver();
    if (!id || !d?.assignedVehicleId) return;

    const vehicleLabel = this.currentVehiclePrimary(d);
    const modelLine = this.currentVehicleModelLine(d);
    const message = [
      'Are you sure you want to remove this vehicle assignment?',
      '',
      `Current Vehicle: ${vehicleLabel}${modelLine ? ` (${modelLine})` : ''}`
    ].join('\n');

    const ok = await this.confirm.open({
      variant: 'delete',
      title: 'Remove vehicle assignment',
      message,
      confirmText: 'Remove',
      cancelText: 'Cancel'
    });
    if (!ok) return;

    this.driverService.unassignVehicle(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Vehicle assignment removed');
        this.resetVehicleForm();
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Removal failed'))
    });
  }

  vehicleSelectLabel(v: VehicleOption): string {
    const code = v.vehicleCode || v.registrationNumber;
    const desc = [v.make, v.model].filter(Boolean).join(' ') || v.name;
    return desc ? `${code} | ${desc}` : code;
  }

  assignmentVehicleLabel(a: DriverAssignment): string {
    return a.vehicleCode || a.vehicleRegistration || a.vehicleName || '—';
  }

  assignmentStatusLabel(a: DriverAssignment): string {
    if (a.status.toLowerCase() === 'active') return 'Active';
    if (a.endAt) return 'Ended';
    return a.status;
  }

  setStatus(status: DriverStatus): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.changeStatus(id, status).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Status updated');
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Status update failed'))
    });
  }

  toggleActive(): void {
    const id = this.driverId();
    if (!id) return;
    this.driverService.toggleActive(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Account status updated');
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Update failed'))
    });
  }

  saveRating(): void {
    const id = this.driverId();
    const rating = Number(this.newRating());
    if (!id || Number.isNaN(rating) || rating < 0 || rating > 5) {
      this.toast.warning('Enter a rating between 0 and 5');
      return;
    }
    this.driverService.updateRating(id, rating).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Rating saved');
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Save failed'))
    });
  }

  logViolation(): void {
    const id = this.driverId();
    const form = this.violationForm();
    if (!id || !form.description.trim()) {
      this.toast.warning('Enter a violation description');
      return;
    }
    this.driverService.createViolation(id, {
      violationType: form.type,
      severity: form.severity,
      occurredAt: new Date().toISOString(),
      description: form.description.trim()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Violation logged');
        this.violationForm.set({ ...form, description: '' });
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to log violation'))
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
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Attendance recorded');
        this.load(id);
        this.changed.emit();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to record attendance'))
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
