import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { Workshop } from '../../../../../core/models/maintenance.model';

export interface WoFilterState {
  vehicleId: number;
  workshopId: number;
  status: string;
  priority: string;
}

@Component({
  selector: 'wo-filters',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters">
      <label>Vehicle
        <select [ngModel]="filters().vehicleId" (ngModelChange)="patch({ vehicleId: +$event })">
          <option [ngValue]="0">All Vehicles</option>
          @for (v of vehicles(); track v.id) {
            <option [ngValue]="v.id">{{ v.name }}</option>
          }
        </select>
      </label>
      <label>Workshop
        <select [ngModel]="filters().workshopId" (ngModelChange)="patch({ workshopId: +$event })">
          <option [ngValue]="0">All Workshops</option>
          @for (w of workshops(); track w.id) {
            <option [ngValue]="w.id">{{ w.name }}</option>
          }
        </select>
      </label>
      <label>Status
        <select [ngModel]="filters().status" (ngModelChange)="patch({ status: $event })">
          <option value="">All Statuses</option>
          @for (s of statusOptions(); track s.value) {
            <option [value]="s.value">{{ s.label }}</option>
          }
        </select>
      </label>
      <label>Priority
        <select [ngModel]="filters().priority" (ngModelChange)="patch({ priority: $event })">
          <option value="">All Priorities</option>
          @for (p of priorityOptions; track p) {
            <option [value]="p">{{ p }}</option>
          }
        </select>
      </label>
      <div class="actions">
        <button type="button" class="btn-apply" (click)="apply.emit()">Apply</button>
        <button type="button" class="btn-reset" (click)="reset.emit()">Reset</button>
      </div>
    </div>
  `,
  styles: [`
    .filters {
      display: flex; gap: 0.75rem; align-items: flex-end; flex-wrap: wrap;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 0.875rem 1rem;
    }
    label { display: grid; gap: 0.25rem; flex: 1; min-width: 140px; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    select {
      border: 1px solid #e2e8f0; border-radius: 7px; padding: 0.4375rem 0.625rem;
      font-size: 0.8125rem; color: #1e293b; background: #fff;
    }
    select:focus { outline: none; border-color: #0b6b50; }
    .actions { display: flex; gap: 0.5rem; }
    .btn-apply { background: #0b6b50; color: #fff; border: none; border-radius: 7px; padding: 0.5rem 0.875rem; font-weight: 600; font-size: 0.8125rem; cursor: pointer; }
    .btn-reset { background: #fff; color: #64748b; border: 1px solid #e2e8f0; border-radius: 7px; padding: 0.5rem 0.875rem; font-weight: 600; font-size: 0.8125rem; cursor: pointer; }
    @media (max-width: 768px) { .actions { width: 100%; } .btn-apply, .btn-reset { flex: 1; } }
  `]
})
export class WoFiltersComponent {
  readonly filters = input.required<WoFilterState>();
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly workshops = input<Workshop[]>([]);
  readonly statusOptions = input<{ value: string; label: string }[]>([]);

  readonly filtersChange = output<WoFilterState>();
  readonly apply = output<void>();
  readonly reset = output<void>();

  readonly priorityOptions = ['Low', 'Medium', 'High', 'Critical'];

  patch(partial: Partial<WoFilterState>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }
}
