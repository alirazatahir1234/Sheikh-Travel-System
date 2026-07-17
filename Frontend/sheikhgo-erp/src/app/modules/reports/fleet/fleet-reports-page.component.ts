import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { FleetReportService } from '../../../core/services/fleet-report.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { PlatformService } from '../../../core/services/platform.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { FleetReport, FleetReportFilters } from '../../../core/models/fleet-report.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { Branch, Department } from '../../../core/models/platform.model';
import { APP_LOGO_PATH, COMPANY_NAME, COMPANY_ADDRESS } from '../../../core/constants/app-brand';
import { applyTripDatePreset, TripDatePreset } from '../../gps-tracking/utils/trip-date-preset.util';
import {
  ReportCatalogId,
  formatFieldValue,
  showStatusFilter,
  statusOptionsForReport
} from './utils/report-column.util';
import { FleetReportCatalogComponent } from './components/report-catalog.component';
import { FleetReportFiltersComponent } from './components/report-filters.component';
import { FleetReportPreviewComponent } from './components/report-preview.component';
import { FleetReportExportActionsComponent } from './components/report-export-actions.component';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface DriverOption { id: number; fullName: string; }

function defaultFilters(): FleetReportFilters {
  const range = applyTripDatePreset('last7Days');
  return { vehicleId: null, driverId: null, branchId: null, departmentId: null, from: range.from.toISOString(), to: range.to.toISOString(), status: '' };
}

@Component({
  selector: 'app-fleet-reports-page',
  standalone: true,
  imports: [
    FleetReportCatalogComponent,
    FleetReportFiltersComponent,
    FleetReportPreviewComponent,
    FleetReportExportActionsComponent
  ],
  templateUrl: './fleet-reports-page.component.html',
  styleUrls: ['./fleet-reports-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FleetReportsPageComponent implements OnInit {
  private readonly fleetReportService = inject(FleetReportService);
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly platformService = inject(PlatformService);
  private readonly exportService = inject(ExportService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(UiToastService);

  readonly selectedReport = signal<ReportCatalogId>('trip');
  readonly datePreset = signal<TripDatePreset>('last7Days');
  readonly draftFilters = signal<FleetReportFilters>(defaultFilters());
  readonly appliedFilters = signal<FleetReportFilters>(defaultFilters());
  readonly report = signal<FleetReport | null>(null);
  readonly loading = signal(false);
  readonly hasPreview = signal(false);

  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly drivers = signal<DriverOption[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly departments = signal<Department[]>([]);

  readonly showStatus = computed(() => showStatusFilter(this.selectedReport()));
  readonly statusOptions = computed(() => statusOptionsForReport(this.selectedReport()));
  readonly canExport = computed(() => !!this.report()?.rows?.length);

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => this.vehicles.set(r.items),
      error: () => this.vehicles.set([])
    });
    this.driverService.getAll(1, 500).subscribe({
      next: r => this.drivers.set(r.items.map(d => ({ id: d.id, fullName: d.fullName }))),
      error: () => this.drivers.set([])
    });
    this.platformService.getBranches().pipe(catchError(() => of([]))).subscribe(b => this.branches.set(b));
    this.platformService.getDepartments().pipe(catchError(() => of([]))).subscribe(d => this.departments.set(d));
  }

  onSelectReport(id: ReportCatalogId): void {
    this.selectedReport.set(id);
    this.hasPreview.set(false);
    this.report.set(null);
    this.draftFilters.update(f => ({ ...f, status: '', maintenanceReportType: id === 'maintenance' ? 'cost-analysis' : undefined }));
  }

  onDraftChange(filters: FleetReportFilters): void {
    this.draftFilters.set(filters);
  }

  onPresetChange(preset: TripDatePreset): void {
    this.datePreset.set(preset);
  }

  onApply(): void {
    this.appliedFilters.set({ ...this.draftFilters() });
    this.loadPreview();
  }

  onReset(): void {
    const reset = defaultFilters();
    this.datePreset.set('last7Days');
    this.draftFilters.set(reset);
    this.appliedFilters.set(reset);
    this.hasPreview.set(false);
    this.report.set(null);
  }

  onPreview(): void {
    this.appliedFilters.set({ ...this.draftFilters() });
    this.loadPreview();
  }

  print(): void {
    if (!this.canExport()) return;
    window.print();
  }

  exportPdf(): void {
    void this.exportReport('pdf');
  }

  exportExcel(): void {
    void this.exportReport('excel');
  }

  exportCsv(): void {
    void this.exportReport('csv');
  }

  private loadPreview(): void {
    this.loading.set(true);
    this.hasPreview.set(true);
    const f = this.appliedFilters();
    this.fleetReportService.getReport(this.selectedReport(), { ...f, status: f.status || undefined }).subscribe({
      next: r => { this.report.set(r); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load report'));
      }
    });
  }

  private async exportReport(format: 'excel' | 'pdf' | 'csv'): Promise<void> {
    const r = this.report();
    if (!r?.rows?.length) {
      this.toast.warning('Preview the report first');
      return;
    }

    type ExportRow = { fields: Record<string, unknown> };
    const cols: ExportColumn<ExportRow>[] = r.columns.map(c => ({
      header: c.label,
      accessor: row => formatFieldValue(row.fields?.[c.key], c.format)
    }));

    const meta = {
      title: r.title,
      filename: `fleet-${r.reportType}`,
      logo: APP_LOGO_PATH,
      companyName: COMPANY_NAME,
      companyAddress: COMPANY_ADDRESS,
      generatedBy: this.authService.getCurrentUser()?.fullName,
      generatedAt: new Date()
    };

    if (format === 'excel') this.exportService.exportExcel(r.rows, cols, meta);
    else if (format === 'csv') this.exportService.exportCsv(r.rows, cols, meta);
    else await this.exportService.exportPdf(r.rows, cols, meta);

    this.toast.success(`${format.toUpperCase()} exported`);
  }
}
