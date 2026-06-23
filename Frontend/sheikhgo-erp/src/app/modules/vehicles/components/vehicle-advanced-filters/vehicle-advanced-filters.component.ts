import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FuelType, FuelTypeLabels } from '../../../../core/models/vehicle.model';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { VehicleFilters, EMPTY_VEHICLE_FILTERS } from '../../models/vehicle-inventory.model';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { normalizeVehicleFilters } from '../../utils/vehicle-filter.util';

@Component({
  selector: 'vehicle-advanced-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 flex justify-end bg-black/30" (click)="close.emit()">
        <div class="h-full w-full max-w-md overflow-y-auto bg-white shadow-2xl" (click)="$event.stopPropagation()">
          <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
            <h3 class="text-lg font-bold text-fleet-text">Advanced Filters</h3>
            <button type="button" class="rounded-full p-1 hover:bg-fleet-surface-muted" (click)="close.emit()">
              <mat-icon>close</mat-icon>
            </button>
          </div>

          <div class="space-y-5 p-5">
            <label class="block text-sm">
              <span class="mb-1 block font-semibold text-fleet-text-muted">Fuel Type</span>
              <select class="w-full rounded-sm border border-fleet-border px-3 py-2 text-sm"
                      [ngModel]="draft().fuelType" (ngModelChange)="patch({ fuelType: $event })">
                <option [ngValue]="'ALL'">All</option>
                @for (ft of fuelTypes; track ft) {
                  <option [ngValue]="ft">{{ fuelLabel(ft) }}</option>
                }
              </select>
            </label>

            <label class="block text-sm">
              <span class="mb-1 block font-semibold text-fleet-text-muted">GPS Status</span>
              <select class="w-full rounded-sm border border-fleet-border px-3 py-2 text-sm"
                      [ngModel]="draft().gpsStatus" (ngModelChange)="patch({ gpsStatus: $event })">
                <option [ngValue]="'ALL'">All</option>
                <option [ngValue]="'ONLINE'">Online</option>
                <option [ngValue]="'OFFLINE'">Offline</option>
                <option [ngValue]="'UNASSIGNED'">No Tracker</option>
              </select>
            </label>

            <label class="block text-sm">
              <span class="mb-1 block font-semibold text-fleet-text-muted">Driver</span>
              <select class="w-full rounded-sm border border-fleet-border px-3 py-2 text-sm"
                      [ngModel]="draft().driverId" (ngModelChange)="patch({ driverId: $event })">
                <option [ngValue]="'ALL'">All</option>
                <option [ngValue]="'UNASSIGNED'">Unassigned</option>
                @for (opt of driverOptions(); track opt.value) {
                  <option [ngValue]="+opt.value">{{ opt.label }}</option>
                }
              </select>
            </label>

            <label class="block text-sm">
              <span class="mb-1 block font-semibold text-fleet-text-muted">Tracker Assigned</span>
              <select class="w-full rounded-sm border border-fleet-border px-3 py-2 text-sm"
                      [ngModel]="draft().trackerAssigned" (ngModelChange)="patch({ trackerAssigned: $event })">
                <option [ngValue]="'ALL'">All</option>
                <option [ngValue]="'ASSIGNED'">Assigned</option>
                <option [ngValue]="'UNASSIGNED'">Not Assigned</option>
              </select>
            </label>

            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" [ngModel]="draft().maintenanceDue" (ngModelChange)="patch({ maintenanceDue: $event })" />
              <span>Maintenance due or overdue</span>
            </label>

            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" [ngModel]="draft().insuranceExpiring" (ngModelChange)="patch({ insuranceExpiring: $event })" />
              <span>Insurance expiring within 30 days</span>
            </label>
          </div>

          <div class="flex gap-2 border-t border-fleet-border px-5 py-4">
            <ui-button variant="ghost" (clicked)="reset()">Reset</ui-button>
            <ui-button variant="primary" class="ml-auto" (clicked)="apply()">Apply Filters</ui-button>
          </div>
        </div>
      </div>
    }
  `
})
export class VehicleAdvancedFiltersComponent {
  readonly open = input(false);
  readonly filters = input.required<VehicleFilters>();
  readonly driverOptions = input<UiSelectOption[]>([]);

  readonly close = output<void>();
  readonly filtersChange = output<VehicleFilters>();

  readonly draft = signal<VehicleFilters>({ ...EMPTY_VEHICLE_FILTERS });
  private wasOpen = false;

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (isOpen && !this.wasOpen) {
        this.draft.set(normalizeVehicleFilters({ ...this.filters() }));
      }
      this.wasOpen = isOpen;
    });
  }

  fuelLabel(ft: FuelType): string {
    return FuelTypeLabels[ft];
  }

  patch(partial: Partial<VehicleFilters>): void {
    this.draft.update(d => ({ ...d, ...partial }));
  }

  reset(): void {
    this.draft.set(normalizeVehicleFilters({
      ...this.filters(),
      fuelType: 'ALL',
      gpsStatus: 'ALL',
      driverId: 'ALL',
      trackerAssigned: 'ALL',
      maintenanceDue: false,
      insuranceExpiring: false
    }));
  }

  apply(): void {
    this.filtersChange.emit(normalizeVehicleFilters({ ...this.draft() }));
    this.close.emit();
  }
}
