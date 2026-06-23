import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceReportFilters } from '../../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { Branch } from '../../../../../core/models/platform.model';

@Component({
  selector: 'report-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters-card">
      <label>
        <span>Vehicle</span>
        <select [ngModel]="filters().vehicleId" (ngModelChange)="patch({ vehicleId: $event || null })">
          <option [ngValue]="null">All vehicles</option>
          @for (v of vehicles(); track v.id) {
            <option [ngValue]="v.id">{{ v.name }}</option>
          }
        </select>
      </label>
      <label>
        <span>Branch</span>
        <select [ngModel]="filters().branchId" (ngModelChange)="patch({ branchId: $event || null })">
          <option [ngValue]="null">All branches</option>
          @for (b of branches(); track b.id) {
            <option [ngValue]="b.id">{{ b.name }}</option>
          }
        </select>
      </label>
      <label>
        <span>From</span>
        <input type="date" [ngModel]="filters().from" (ngModelChange)="patch({ from: $event })" />
      </label>
      <label>
        <span>To</span>
        <input type="date" [ngModel]="filters().to" (ngModelChange)="patch({ to: $event })" />
      </label>
      @if (showStatus()) {
        <label>
          <span>Status</span>
          <select [ngModel]="filters().status" (ngModelChange)="patch({ status: $event })">
            @for (opt of statusOptions(); track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        </label>
      }
      <div class="actions">
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
      display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: flex-end;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.25rem; margin-bottom: 1rem;
    }
    label { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; min-width: 140px; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    select, input { padding: 0.5rem 0.75rem; border: 1.5px solid #e2e8f0; border-radius: 8px; font-size: 0.8125rem; }
    .actions { display: flex; gap: 0.5rem; }
    .btn { display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.8125rem; font-weight: 600; cursor: pointer; border: none; }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--outline { background: #fff; color: #475569; border: 1.5px solid #cbd5e1; }
  `]
})
export class ReportFiltersComponent {
  readonly filters = input.required<MaintenanceReportFilters>();
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly branches = input<Branch[]>([]);
  readonly showStatus = input(false);
  readonly statusOptions = input<{ value: string; label: string }[]>([]);
  readonly filtersChange = output<MaintenanceReportFilters>();
  readonly apply = output<void>();
  readonly reset = output<void>();

  patch(partial: Partial<MaintenanceReportFilters>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }
}
