import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, forkJoin, of } from 'rxjs';
import { DriverService } from '../../../../core/services/driver.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { UiModalComponent } from '../../../../shared/components/ui/modal/ui-modal.component';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';

@Component({
  selector: 'driver-bulk-assign-dialog',
  standalone: true,
  imports: [FormsModule, UiModalComponent, UiButtonComponent, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Bulk assign vehicle" size="md">
      <p class="hint">Assign one vehicle to multiple unassigned drivers. Drivers with an active assignment are skipped.</p>
      <ui-select class="block" label="Vehicle" [options]="vehicleOptions()" [(ngModel)]="selectedVehicleId" [required]="true" />
      <div class="driver-list">
        <p class="list-label">Drivers</p>
        @if (driverOptions().length === 0) {
          <p class="empty">No unassigned drivers found.</p>
        } @else {
          @for (opt of driverOptions(); track opt.value) {
            <label class="driver-row">
              <input type="checkbox" [value]="opt.value" [checked]="isSelected(opt.value)" (change)="toggleDriver(opt.value, $any($event.target).checked)" />
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
          [disabled]="!selectedVehicleId || selectedDriverIds().length === 0 || submitting()"
          (clicked)="submit()">
          Assign {{ selectedDriverIds().length || '' }} driver(s)
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
  private readonly snackBar = inject(MatSnackBar);

  readonly open = model(false);
  readonly assigned = output<void>();

  readonly vehicleOptions = signal<UiSelectOption[]>([]);
  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly selectedDriverIds = signal<string[]>([]);
  readonly submitting = signal(false);

  selectedVehicleId: string | null = null;

  show(): void {
    this.selectedVehicleId = null;
    this.selectedDriverIds.set([]);
    forkJoin({
      drivers: this.driverService.getAll({ page: 1, pageSize: 500 }).pipe(catchError(() => of({ items: [] }))),
      vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).subscribe(({ drivers, vehicles }) => {
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

  isSelected(id: string): boolean {
    return this.selectedDriverIds().includes(id);
  }

  toggleDriver(id: string, checked: boolean): void {
    this.selectedDriverIds.update(ids => checked ? [...ids, id] : ids.filter(x => x !== id));
  }

  submit(): void {
    const vehicleId = Number(this.selectedVehicleId);
    const driverIds = this.selectedDriverIds().map(Number).filter(Number.isFinite);
    if (!vehicleId || driverIds.length === 0) return;

    this.submitting.set(true);
    forkJoin(
      driverIds.map(id =>
        this.driverService.assignVehicle(id, { vehicleId }).pipe(catchError(err => of({ error: err, id })))
      )
    ).subscribe(results => {
      this.submitting.set(false);
      const failed = results.filter((r: unknown) => r && typeof r === 'object' && 'error' in (r as object)).length;
      const ok = driverIds.length - failed;
      if (ok > 0) {
        this.snackBar.open(`Assigned ${ok} driver(s) to vehicle`, 'Close', { duration: 3000 });
        this.assigned.emit();
        this.open.set(false);
      } else {
        this.snackBar.open('Assignment failed for all selected drivers', 'Close', { duration: 3500 });
      }
    });
  }
}
