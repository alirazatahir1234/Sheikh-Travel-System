import { ChangeDetectionStrategy, Component, DestroyRef, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { DriverService } from '../../../../core/services/driver.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { UiModalComponent } from '../../../../shared/components/ui/modal/ui-modal.component';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  selector: 'driver-bulk-assign-dialog',
  standalone: true,
  imports: [FormsModule, UiModalComponent, UiButtonComponent, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Bulk assign vehicle" size="md">
      <p class="hint">Assign the selected vehicle to one driver at a time. Each driver must be unassigned and verified.</p>
      <ui-select class="block" label="Vehicle" [options]="vehicleOptions()" [(ngModel)]="selectedVehicleId" [required]="true" />
      <div class="driver-list">
        <p class="list-label">Driver</p>
        @if (driverOptions().length === 0) {
          <p class="empty">No unassigned drivers found.</p>
        } @else {
          @for (opt of driverOptions(); track opt.value) {
            <label class="driver-row">
              <input type="radio" name="bulkAssignDriver" [value]="opt.value" [checked]="selectedDriverId() === opt.value" (change)="selectedDriverId.set(opt.value)" />
              <span>{{ opt.label }}</span>
            </label>
          }
        }
      </div>
      <div modal-footer class="flex justify-end gap-2">
        <ui-button variant="ghost" (clicked)="open.set(false)">Cancel</ui-button>
        <ui-button
          variant="primary"
          [loading]="submitting()"
          [disabled]="!selectedVehicleId || !selectedDriverId() || submitting()"
          (clicked)="submit()">
          Assign driver
        </ui-button>
      </div>
    </ui-modal>
  `,
  styles: [`
    .hint { font-size: 0.8125rem; color: #64748b; margin-bottom: 1rem; }
    .driver-list { margin-top: 1rem; max-height: 240px; overflow-y: auto; border: 1px solid #e2e8f0; border-radius: 0.5rem; padding: 0.5rem; }
    .list-label { font-size: 0.75rem; font-weight: 600; text-transform: uppercase; color: #64748b; margin: 0 0 0.5rem; }
    .driver-row { display: flex; align-items: center; gap: 0.5rem; padding: 0.35rem 0.25rem; font-size: 0.875rem; cursor: pointer; }
    .empty { font-size: 0.875rem; color: #94a3b8; padding: 0.5rem; }
  `]
})
export class DriverBulkAssignDialogComponent {
  private readonly driverService = inject(DriverService);
  private readonly vehicleService = inject(VehicleService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  readonly assigned = output<void>();

  readonly vehicleOptions = signal<UiSelectOption[]>([]);
  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly selectedDriverId = signal<string | null>(null);
  readonly submitting = signal(false);

  selectedVehicleId: string | null = null;

  show(): void {
    this.selectedVehicleId = null;
    this.selectedDriverId.set(null);
    forkJoin({
      drivers: this.driverService.getAll({ page: 1, pageSize: 500 }).pipe(catchError(() => of({ items: [] }))),
      vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(({ drivers, vehicles }) => {
      this.driverOptions.set(
        drivers.items
          .filter(d => !d.assignedVehicleId && d.isActive !== false)
          .map(d => ({ value: String(d.id), label: `${d.fullName} (${d.driverCode || d.phone})` }))
      );
      this.vehicleOptions.set(
        vehicles.items.map(v => ({
          value: String(v.id),
          label: `${v.registrationNumber || v.vehicleCode} — ${v.make || ''} ${v.model || ''}`.trim()
        }))
      );
    });
    this.open.set(true);
  }

  submit(): void {
    const vehicleId = Number(this.selectedVehicleId);
    const driverId = Number(this.selectedDriverId());
    if (!vehicleId || !Number.isFinite(driverId)) return;

    this.submitting.set(true);
    this.driverService.assignVehicle(driverId, { vehicleId }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toast.success('Vehicle assigned to driver');
        this.assigned.emit();
        this.open.set(false);
      },
      error: () => {
        this.submitting.set(false);
        this.toast.error('Assignment failed');
      }
    });
  }
}
