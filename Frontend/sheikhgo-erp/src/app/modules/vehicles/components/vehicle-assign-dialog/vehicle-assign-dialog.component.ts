import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { DriverService } from '../../../../core/services/driver.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiModalComponent } from '../../../../shared/components/ui/modal/ui-modal.component';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiInputComponent } from '../../../../shared/components/ui/input/ui-input.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { buildDriverAssignOptions, parseOptionalBookingId } from '../../utils/vehicle-assign.util';

@Component({
  selector: 'vehicle-assign-dialog',
  standalone: true,
  imports: [FormsModule, UiModalComponent, UiButtonComponent, UiSelectComponent, UiInputComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" [title]="dialogTitle()" size="sm">
      @if (assigneeWarning(); as warn) {
        <div class="assignee-warn">{{ warn }}</div>
      }
      <ui-select
        label="Driver"
        placeholder="Select driver"
        [options]="driverOptions()"
        [(ngModel)]="selectedDriverId"
        [searchable]="true"
        [required]="true" />
      <ui-input
        class="mt-4 block"
        label="Booking ID (Optional)"
        type="number"
        placeholder="Enter booking ID"
        [(ngModel)]="bookingId" />
      <div modal-footer class="flex justify-end gap-2">
        <ui-button variant="ghost" (clicked)="open.set(false)">Cancel</ui-button>
        <ui-button
          variant="primary"
          [loading]="submitting()"
          [disabled]="!selectedDriverId || submitting()"
          (clicked)="submit()">
          Assign Driver
        </ui-button>
      </div>
    </ui-modal>
  `,
  styles: [`
    .assignee-warn {
      margin-bottom: 0.75rem;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      font-size: 0.8125rem;
      background: #fffbeb;
      color: #b45309;
      border: 1px solid #fde68a;
    }
  `]
})
export class VehicleAssignDialogComponent {
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly toast = inject(UiToastService);

  readonly open = model(false);
  readonly vehicleId = model<number | null>(null);
  readonly assigned = output<void>();

  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly submitting = signal(false);
  readonly vehicleIds = signal<number[]>([]);
  /** vehicleId → current driver display name */
  readonly currentAssignees = signal<Record<number, string>>({});
  readonly assigneeWarning = signal<string | null>(null);

  selectedDriverId: string | null = null;
  bookingId: string | number | null = null;

  dialogTitle(): string {
    const n = this.vehicleIds().length;
    return n > 1 ? `Assign Driver (${n} vehicles)` : 'Assign Driver';
  }

  show(
    vehicleId: number | number[],
    assignees?: Record<number, string | null | undefined> | string | null
  ): void {
    const ids = (Array.isArray(vehicleId) ? vehicleId : [vehicleId]).filter(id => Number.isFinite(id) && id > 0);
    this.vehicleIds.set(ids);
    this.vehicleId.set(ids[0] ?? null);
    this.selectedDriverId = null;
    this.bookingId = null;

    const map: Record<number, string> = {};
    if (typeof assignees === 'string' && assignees.trim()) {
      if (ids[0] != null) map[ids[0]] = assignees.trim();
    } else if (assignees && typeof assignees === 'object') {
      for (const [k, v] of Object.entries(assignees)) {
        const name = (v ?? '').toString().trim();
        if (name) map[Number(k)] = name;
      }
    }
    this.currentAssignees.set(map);

    const occupied = ids
      .map(id => map[id])
      .filter((n): n is string => !!n);
    this.assigneeWarning.set(
      occupied.length === 0
        ? null
        : occupied.length === 1
          ? `Currently assigned to ${occupied[0]}. Reassigning will transfer this vehicle.`
          : `${occupied.length} of these vehicles already have drivers. Reassigning will transfer them.`
    );

    this.driverService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))).subscribe(r => {
      this.driverOptions.set(buildDriverAssignOptions(r.items));
    });
    this.open.set(true);
  }

  async submit(): Promise<void> {
    const ids = this.vehicleIds();
    if (!ids.length || !this.selectedDriverId) return;

    const bookingId = parseOptionalBookingId(this.bookingId);
    if (this.bookingId !== null && this.bookingId !== undefined && String(this.bookingId).trim() !== '' && bookingId === null) {
      this.toast.error('Enter a valid booking ID.');
      return;
    }

    const assignees = this.currentAssignees();
    const occupiedNames = ids.map(id => assignees[id]).filter(Boolean);
    if (occupiedNames.length > 0) {
      const proceed = window.confirm(
        occupiedNames.length === 1
          ? `This vehicle is assigned to ${occupiedNames[0]}. Continue and transfer?`
          : `${occupiedNames.length} vehicles already have drivers. Continue and transfer?`
      );
      if (!proceed) return;
    }

    this.submitting.set(true);
    const driverId = Number(this.selectedDriverId);
    let failed = 0;
    const errorMessages: string[] = [];

    for (const id of ids) {
      const result = await new Promise<{ ok: true } | { ok: false; message: string }>(resolve => {
        this.vehicleService.assignDriver(id, { driverId, bookingId }).subscribe({
          next: () => resolve({ ok: true }),
          error: (err) => resolve({
            ok: false,
            message: apiErrorMessage(err, 'Driver assignment failed')
          })
        });
      });
      if (!result.ok) {
        failed++;
        if (!errorMessages.includes(result.message)) {
          errorMessages.push(result.message);
        }
      }
    }

    this.submitting.set(false);

    if (failed === 0) {
      this.open.set(false);
      this.toast.success(ids.length > 1 ? `Driver assigned to ${ids.length} vehicles` : 'Driver assigned successfully');
      this.assigned.emit();
      return;
    }

    const detail = errorMessages.join(' · ');
    if (failed === ids.length) {
      this.toast.error(detail || 'Driver assignment failed');
      return;
    }

    this.open.set(false);
    this.toast.error(`Assigned with ${failed} failure(s): ${detail}`);
    this.assigned.emit();
  }
}
