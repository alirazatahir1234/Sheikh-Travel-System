import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { DriverService } from '../../../../core/services/driver.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { UiModalComponent } from '../../../../shared/components/ui/modal/ui-modal.component';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiInputComponent } from '../../../../shared/components/ui/input/ui-input.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';

@Component({
  selector: 'vehicle-assign-dialog',
  standalone: true,
  imports: [FormsModule, UiModalComponent, UiButtonComponent, UiSelectComponent, UiInputComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Assign driver" size="sm">
      <ui-select label="Driver" [options]="driverOptions()" [(ngModel)]="selectedDriverId" [required]="true"></ui-select>
      <ui-input class="mt-4 block" label="Booking ID (optional)" type="number" [(ngModel)]="bookingId"></ui-input>
      <div modal-footer class="flex justify-end gap-2">
        <ui-button variant="ghost" (clicked)="open.set(false)">Cancel</ui-button>
        <ui-button variant="primary" [loading]="submitting()" [disabled]="!selectedDriverId || submitting()" (clicked)="submit()">Assign</ui-button>
      </div>
    </ui-modal>
  `
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

  selectedDriverId: string | null = null;
  bookingId: number | null = null;

  constructor() {
    // Load drivers when dialog opens
    let prevOpen = false;
    const check = () => {
      const isOpen = this.open();
      if (isOpen && !prevOpen) {
        this.selectedDriverId = null;
        this.bookingId = null;
        this.driverService.getAll(1, 500).pipe(
          catchError(() => of({ items: [] }))
        ).subscribe(r => {
          this.driverOptions.set(r.items.map(d => ({
            value: String(d.id),
            label: `${d.fullName} (${d.phone})`
          })));
        });
      }
      prevOpen = isOpen;
    };
    // effect alternative: called from parent via open()
  }

  show(vehicleId: number): void {
    this.vehicleId.set(vehicleId);
    this.selectedDriverId = null;
    this.bookingId = null;
    this.driverService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))).subscribe(r => {
      this.driverOptions.set(r.items.map(d => ({
        value: String(d.id),
        label: `${d.fullName} (${d.phone})`
      })));
    });
    this.open.set(true);
  }

  submit(): void {
    const id = this.vehicleId();
    if (!id || !this.selectedDriverId) return;

    this.submitting.set(true);
    this.vehicleService.assignDriver(id, {
      driverId: Number(this.selectedDriverId),
      bookingId: this.bookingId || null
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.open.set(false);
        this.toast.success('Driver assigned');
        this.assigned.emit();
      },
      error: () => {
        this.submitting.set(false);
        this.toast.error('Assign failed');
      }
    });
  }
}
