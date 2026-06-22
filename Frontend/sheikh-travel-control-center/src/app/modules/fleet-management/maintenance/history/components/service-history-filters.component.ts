import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { ServiceType } from '../../../../../core/models/maintenance.model';

export interface ServiceHistoryFilterState {
  vehicleId: number | null;
  from: string;
  to: string;
  serviceType: string;
}

export const SERVICE_HISTORY_TYPE_OPTIONS = [
  'Oil Change',
  'Brake Service',
  'Tire Replacement',
  'Battery Change',
  'Accident Repair'
] as const;

@Component({
  selector: 'service-history-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters-card">
      <label class="filter-group">
        <span class="filter-label">Vehicle</span>
        <select class="filter-control" [ngModel]="filters().vehicleId"
          (ngModelChange)="patch({ vehicleId: $event || null })">
          <option [ngValue]="null">All vehicles</option>
          @for (v of vehicles(); track v.id) {
            <option [ngValue]="v.id">{{ v.name }} ({{ v.registrationNumber }})</option>
          }
        </select>
      </label>
      <label class="filter-group">
        <span class="filter-label">From</span>
        <input class="filter-control" type="date" [ngModel]="filters().from"
          (ngModelChange)="patch({ from: $event })" />
      </label>
      <label class="filter-group">
        <span class="filter-label">To</span>
        <input class="filter-control" type="date" [ngModel]="filters().to"
          (ngModelChange)="patch({ to: $event })" />
      </label>
      <label class="filter-group">
        <span class="filter-label">Service Type</span>
        <select class="filter-control" [ngModel]="filters().serviceType"
          (ngModelChange)="patch({ serviceType: $event })">
          <option value="">All types</option>
          @for (t of typeOptions(); track t) {
            <option [value]="t">{{ t }}</option>
          }
        </select>
      </label>
      <div class="filter-actions">
        <button type="button" class="btn btn--primary" (click)="apply.emit()">
          <mat-icon>filter_alt</mat-icon> Apply
        </button>
        <button type="button" class="btn btn--outline" (click)="reset.emit()">
          <mat-icon>restart_alt</mat-icon> Reset
        </button>
      </div>
    </div>
  `,
  styles: [`
    .filters-card {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      align-items: flex-end;
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
      border: 1px solid #e2e8f0;
      padding: 1rem 1.25rem;
      margin-bottom: 1.5rem;
    }
    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      flex: 1;
      min-width: 150px;
    }
    .filter-label {
      font-size: 0.6875rem;
      font-weight: 700;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .filter-control {
      padding: 0.5rem 0.75rem;
      border: 1.5px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.8125rem;
      color: #334155;
      background: #fff;
      outline: none;
      transition: border-color 0.15s;
    }
    .filter-control:focus { border-color: #0B6B50; }
    .filter-actions {
      display: flex;
      gap: 0.5rem;
      align-items: flex-end;
      flex-shrink: 0;
    }
    .btn {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.5rem 1rem;
      border-radius: 8px;
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
      transition: all 0.15s;
    }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--primary:hover { background: #095a43; }
    .btn--outline {
      background: #fff;
      color: #475569;
      border: 1.5px solid #cbd5e1;
    }
    .btn--outline:hover { background: #f8fafc; border-color: #94a3b8; }
    @media (max-width: 560px) {
      .filter-group { min-width: 100%; }
      .filter-actions { width: 100%; }
      .filter-actions .btn { flex: 1; justify-content: center; }
    }
  `]
})
export class ServiceHistoryFiltersComponent {
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly serviceTypes = input<ServiceType[]>([]);
  readonly filters = input.required<ServiceHistoryFilterState>();
  readonly filtersChange = output<ServiceHistoryFilterState>();
  readonly apply = output<void>();
  readonly reset = output<void>();

  typeOptions(): string[] {
    const fromApi = this.serviceTypes().map(s => s.name);
    const merged = new Set<string>([...SERVICE_HISTORY_TYPE_OPTIONS, ...fromApi]);
    return [...merged].sort((a, b) => a.localeCompare(b));
  }

  patch(partial: Partial<ServiceHistoryFilterState>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }
}
