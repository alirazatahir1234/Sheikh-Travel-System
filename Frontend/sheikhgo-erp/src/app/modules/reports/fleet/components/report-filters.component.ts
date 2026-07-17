import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FleetReportFilters } from '../../../../core/models/fleet-report.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { Branch, Department } from '../../../../core/models/platform.model';
import { TRIP_DATE_PRESETS, TripDatePreset, applyTripDatePreset } from '../../../gps-tracking/utils/trip-date-preset.util';
import { MAINTENANCE_SUB_REPORT_CATALOG, ReportCatalogId } from '../utils/report-column.util';

interface DriverOption { id: number; fullName: string; }

@Component({
  selector: 'fleet-report-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule, SlicePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters-card">
      <div class="preset-row">
        @for (p of presets; track p.id) {
          <button type="button" class="preset-chip" [class.preset-chip--active]="preset() === p.id" (click)="onPreset(p.id)">{{ p.label }}</button>
        }
      </div>

      <div class="fields-row">
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
          <span>Driver</span>
          <select [ngModel]="filters().driverId" (ngModelChange)="patch({ driverId: $event || null })">
            <option [ngValue]="null">All drivers</option>
            @for (d of drivers(); track d.id) {
              <option [ngValue]="d.id">{{ d.fullName }}</option>
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
          <span>Department</span>
          <select [ngModel]="filters().departmentId" (ngModelChange)="patch({ departmentId: $event || null })">
            <option [ngValue]="null">All departments</option>
            @for (d of departments(); track d.id) {
              <option [ngValue]="d.id">{{ d.name }}</option>
            }
          </select>
        </label>
        @if (preset() === 'custom') {
          <label>
            <span>From</span>
            <input type="date" [ngModel]="filters().from | slice:0:10" (ngModelChange)="patch({ from: $event })" />
          </label>
          <label>
            <span>To</span>
            <input type="date" [ngModel]="filters().to | slice:0:10" (ngModelChange)="patch({ to: $event })" />
          </label>
        }
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
        @if (reportType() === 'maintenance') {
          <label>
            <span>Maintenance Type</span>
            <select [ngModel]="filters().maintenanceReportType" (ngModelChange)="patch({ maintenanceReportType: $event })">
              @for (opt of maintenanceSubTypes; track opt.id) {
                <option [value]="opt.id">{{ opt.label }}</option>
              }
            </select>
          </label>
        }
      </div>

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
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.25rem; margin-bottom: 1rem;
      display: flex; flex-direction: column; gap: 0.75rem;
    }
    .preset-row { display: flex; flex-wrap: wrap; gap: 0.4rem; }
    .preset-chip {
      border: 1px solid #d1d5db; background: #fff; border-radius: 999px; padding: 0.3rem 0.75rem;
      font-size: 0.75rem; font-weight: 600; color: #374151; cursor: pointer;
    }
    .preset-chip--active { background: #f0fdf8; border-color: #0B6B50; color: #0B6B50; }
    .fields-row { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: flex-end; }
    label { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; min-width: 140px; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    select, input { padding: 0.5rem 0.75rem; border: 1.5px solid #e2e8f0; border-radius: 8px; font-size: 0.8125rem; }
    .actions { display: flex; gap: 0.5rem; }
    .btn { display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.8125rem; font-weight: 600; cursor: pointer; border: none; }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--outline { background: #fff; color: #475569; border: 1.5px solid #cbd5e1; }
  `]
})
export class FleetReportFiltersComponent {
  readonly presets = TRIP_DATE_PRESETS;
  readonly maintenanceSubTypes = MAINTENANCE_SUB_REPORT_CATALOG;

  readonly filters = input.required<FleetReportFilters>();
  readonly reportType = input<ReportCatalogId>('trip');
  readonly preset = input<TripDatePreset>('last7Days');
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly drivers = input<DriverOption[]>([]);
  readonly branches = input<Branch[]>([]);
  readonly departments = input<Department[]>([]);
  readonly showStatus = input(false);
  readonly statusOptions = input<{ value: string; label: string }[]>([]);

  readonly filtersChange = output<FleetReportFilters>();
  readonly presetChange = output<TripDatePreset>();
  readonly apply = output<void>();
  readonly reset = output<void>();

  patch(partial: Partial<FleetReportFilters>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }

  onPreset(preset: TripDatePreset): void {
    this.presetChange.emit(preset);
    if (preset === 'custom') return;
    const range = applyTripDatePreset(preset);
    this.patch({ from: range.from.toISOString(), to: range.to.toISOString() });
  }
}
