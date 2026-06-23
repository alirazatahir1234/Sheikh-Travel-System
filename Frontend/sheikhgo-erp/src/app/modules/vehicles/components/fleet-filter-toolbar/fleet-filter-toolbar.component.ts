import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiInputComponent } from '../../../../shared/components/ui/input/ui-input.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { VehicleFilters } from '../../models/vehicle-inventory.model';
import { VehicleStatus } from '../../../../core/models/vehicle.model';

@Component({
  selector: 'fleet-filter-toolbar',
  standalone: true,
  imports: [FormsModule, MatIconModule, UiInputComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-3 rounded-xl border border-fleet-border bg-white px-4 py-3">
      <div class="flex flex-wrap items-center gap-3">
        <div class="min-w-[220px] flex-1">
          <ui-input
            type="search"
            placeholder="Search plate, driver, IMEI, SIM, VIN..."
            [ngModel]="filters().search"
            (ngModelChange)="patch({ search: $event })">
          </ui-input>
        </div>

        <label class="flex items-center gap-2 text-sm">
          <span class="font-semibold text-fleet-text-muted">Vehicle Type:</span>
          <select class="rounded-sm border border-fleet-border bg-white px-3 py-1.5 text-sm"
                  [ngModel]="filters().vehicleType" (ngModelChange)="patch({ vehicleType: $event })">
            <option value="ALL">All</option>
            @for (opt of vehicleTypeOptions(); track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <label class="flex items-center gap-2 text-sm">
          <span class="font-semibold text-fleet-text-muted">Status:</span>
          <select class="rounded-sm border border-fleet-border bg-white px-3 py-1.5 text-sm"
                  [ngModel]="statusValue()" (ngModelChange)="onStatusChange($event)">
            @for (opt of statusOptions(); track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <label class="flex items-center gap-2 text-sm">
          <span class="font-semibold text-fleet-text-muted">Branch:</span>
          <select class="rounded-sm border border-fleet-border bg-white px-3 py-1.5 text-sm"
                  [ngModel]="filters().branchId" (ngModelChange)="patch({ branchId: $event })">
            <option [ngValue]="null">All Regions</option>
            @for (opt of branchOptions(); track opt.value) {
              <option [ngValue]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <button type="button"
                class="inline-flex items-center gap-1.5 rounded-sm border border-fleet-border px-3 py-1.5 text-sm font-semibold text-fleet-text-muted hover:bg-fleet-surface-muted"
                (click)="advancedFilters.emit()">
          <mat-icon class="!text-[18px]">tune</mat-icon>
          Advanced Filters
        </button>
      </div>

      <div class="flex items-center gap-2 text-xs text-fleet-text-muted">
        <span>{{ resultSummary() }}</span>
        <button type="button" class="ml-auto rounded-full p-1 hover:bg-fleet-surface-muted" (click)="refresh.emit()">
          <mat-icon class="!text-[18px]">refresh</mat-icon>
        </button>
      </div>
    </div>
  `,
  styles: [`mat-icon { display: inline-flex; }`]
})
export class FleetFilterToolbarComponent {
  readonly filters = input.required<VehicleFilters>();
  readonly vehicleTypeOptions = input<UiSelectOption[]>([]);
  readonly statusOptions = input<UiSelectOption[]>([]);
  readonly branchOptions = input<UiSelectOption<string | number>[]>([]);
  readonly resultSummary = input('');

  readonly filtersChange = output<VehicleFilters>();
  readonly advancedFilters = output<void>();
  readonly refresh = output<void>();

  patch(partial: Partial<VehicleFilters>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }

  statusValue(): string {
    const s = this.filters().status;
    return s === 'ALL' ? 'ALL' : String(s);
  }

  onStatusChange(value: string): void {
    const status: VehicleStatus | 'ALL' = value === 'ALL' ? 'ALL' : (Number(value) as VehicleStatus);
    this.patch({ status });
  }
}
