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
            <input type="date" [ngModel]="filters().from | slice:0:10" (ngModelChange)="onFromDate($event)" />
          </label>
          <label>
            <span>To</span>
            <input type="date" [ngModel]="filters().to | slice:0:10" [attr.min]="filters().from | slice:0:10" (ngModelChange)="onToDate($event)" />
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

  onFromDate(value: string): void {
    if (!value) return;
    const from = new Date(`${value}T00:00:00`);
    this.patch({ from: from.toISOString() });
  }

  onToDate(value: string): void {
    if (!value) return;
    const to = new Date(`${value}T23:59:59.999`);
    this.patch({ to: to.toISOString() });
  }

  onPreset(preset: TripDatePreset): void {
    this.presetChange.emit(preset);
    if (preset === 'custom') return;
    // Management / fuel / maintenance reports need full calendar windows.
    // GPS trip/speed/idle/stop still clamp via applyTripDatePreset (30-day MaxRange).
    const gpsLimited = ['trip', 'speed', 'idle', 'stop', 'event'].includes(this.reportType());
    const range = applyTripDatePreset(preset);
    if (!gpsLimited && (preset === 'thisYear' || preset === 'thisMonth' || preset === 'previousMonth')) {
      // Re-expand without the trip 30-day clamp for non-GPS report families.
      const unclamped = applyManagementDatePreset(preset);
      this.patch({ from: unclamped.from.toISOString(), to: unclamped.to.toISOString() });
      return;
    }
    this.patch({ from: range.from.toISOString(), to: range.to.toISOString() });
  }
}

function applyManagementDatePreset(preset: TripDatePreset, now = new Date()): { from: Date; to: Date } {
  const endOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
  switch (preset) {
    case 'thisMonth':
      return { from: new Date(now.getFullYear(), now.getMonth(), 1), to: endOfDay(now) };
    case 'previousMonth': {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      const end = new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59, 999);
      return { from: start, to: end };
    }
    case 'thisYear':
      return { from: new Date(now.getFullYear(), 0, 1), to: endOfDay(now) };
    default:
      return applyTripDatePreset(preset, now);
  }
}
