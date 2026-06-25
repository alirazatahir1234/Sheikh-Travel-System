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
    <div class="fleet-toolbar rounded-xl border border-fleet-border bg-white px-4 py-3">
      <div class="fleet-toolbar__filters">
        <div class="fleet-toolbar__search">
          <ui-input
            type="search"
            placeholder="Search plate, driver, IMEI, SIM, VIN..."
            [ngModel]="filters().search"
            (ngModelChange)="patch({ search: $event })">
          </ui-input>
        </div>

        <label class="fleet-toolbar__field">
          <span class="fleet-toolbar__label">Vehicle Type</span>
          <select class="fleet-toolbar__select"
                  [ngModel]="filters().vehicleType" (ngModelChange)="patch({ vehicleType: $event })">
            <option value="ALL">All</option>
            @for (opt of vehicleTypeOptions(); track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <label class="fleet-toolbar__field">
          <span class="fleet-toolbar__label">Status</span>
          <select class="fleet-toolbar__select"
                  [ngModel]="statusValue()" (ngModelChange)="onStatusChange($event)">
            @for (opt of statusOptions(); track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <label class="fleet-toolbar__field">
          <span class="fleet-toolbar__label">Branch</span>
          <select class="fleet-toolbar__select"
                  [ngModel]="filters().branchId" (ngModelChange)="patch({ branchId: $event })">
            <option [ngValue]="null">All Regions</option>
            @for (opt of branchOptions(); track opt.value) {
              <option [ngValue]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>

        <button type="button" class="fleet-toolbar__advanced"
                (click)="advancedFilters.emit()">
          <mat-icon>tune</mat-icon>
          Advanced Filters
        </button>
      </div>

      <div class="fleet-toolbar__summary">
        <span>{{ resultSummary() }}</span>
        <button type="button" class="fleet-toolbar__refresh" (click)="refresh.emit()">
          <mat-icon>refresh</mat-icon>
        </button>
      </div>
    </div>
  `,
  styles: [`
    mat-icon { display: inline-flex; }
    .fleet-toolbar { display: flex; flex-direction: column; gap: 0.75rem; }
    .fleet-toolbar__filters {
      display: grid;
      grid-template-columns: 1fr;
      gap: 0.75rem;
    }
    .fleet-toolbar__search { min-width: 0; }
    .fleet-toolbar__field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.8125rem;
    }
    .fleet-toolbar__label {
      font-weight: 600;
      color: #64748b;
    }
    .fleet-toolbar__select {
      width: 100%;
      border: 1px solid #dae3ee;
      border-radius: 6px;
      background: #fff;
      padding: 0.5rem 0.625rem;
      font: inherit;
    }
    .fleet-toolbar__advanced {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.375rem;
      width: 100%;
      border: 1px solid #dae3ee;
      border-radius: 6px;
      padding: 0.5rem 0.75rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: #64748b;
      background: #fff;
      cursor: pointer;
    }
    .fleet-toolbar__summary {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.75rem;
      color: #64748b;
    }
    .fleet-toolbar__refresh {
      margin-left: auto;
      display: inline-flex;
      padding: 0.25rem;
      border: none;
      background: transparent;
      border-radius: 999px;
      cursor: pointer;
      color: #64748b;
    }
    @media (min-width: 640px) {
      .fleet-toolbar__filters {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .fleet-toolbar__search { grid-column: 1 / -1; }
    }
    @media (min-width: 1024px) {
      .fleet-toolbar__filters {
        grid-template-columns: minmax(220px, 1.4fr) repeat(3, minmax(0, 1fr)) auto;
        align-items: end;
      }
      .fleet-toolbar__search { grid-column: auto; }
      .fleet-toolbar__advanced { width: auto; white-space: nowrap; }
    }
  `]
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
