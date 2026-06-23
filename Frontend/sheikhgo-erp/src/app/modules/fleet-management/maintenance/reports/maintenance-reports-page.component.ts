import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { ExportService, ExportColumn } from '../../../../core/services/export.service';
import { MaintenanceReport, MaintenanceReportFilters } from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { Branch } from '../../../../core/models/platform.model';
import { ReportCatalogComponent } from './components/report-catalog.component';
import { ReportFiltersComponent } from './components/report-filters.component';
import { ReportPreviewComponent } from './components/report-preview.component';
import { ReportExportActionsComponent } from './components/report-export-actions.component';
import { ReportScheduleDialogComponent } from './components/report-schedule-dialog.component';
import { ReportCatalogId, formatFieldValue, showStatusFilter, statusOptionsForReport } from './utils/report-column.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

function defaultDateRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  from.setMonth(from.getMonth() - 1);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

function defaultFilters(): MaintenanceReportFilters {
  return { vehicleId: null, branchId: null, ...defaultDateRange(), status: '' };
}

@Component({
  selector: 'app-maintenance-reports-page',
  standalone: true,
  imports: [
    RouterLink,
    MatIconModule,
    ReportCatalogComponent,
    ReportFiltersComponent,
    ReportPreviewComponent,
    ReportExportActionsComponent,
    ReportScheduleDialogComponent
  ],
  templateUrl: './maintenance-reports-page.component.html',
  styleUrls: ['./maintenance-reports-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaintenanceReportsPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly platformService = inject(PlatformService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly selectedReport = signal<ReportCatalogId>('cost-analysis');
  readonly draftFilters = signal<MaintenanceReportFilters>(defaultFilters());
  readonly appliedFilters = signal<MaintenanceReportFilters>(defaultFilters());
  readonly report = signal<MaintenanceReport | null>(null);
  readonly loading = signal(false);
  readonly hasPreview = signal(false);
  readonly scheduleOpen = signal(false);

  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly branches = signal<Branch[]>([]);

  readonly showStatus = computed(() => showStatusFilter(this.selectedReport()));
  readonly statusOptions = computed(() => statusOptionsForReport(this.selectedReport()));
  readonly canExport = computed(() => !!this.report()?.rows?.length);

  ngOnInit(): void {
    const reportParam = this.route.snapshot.queryParamMap.get('report');
    if (reportParam) this.selectedReport.set(reportParam as ReportCatalogId);

    this.vehicleService.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.vehicles.set(r.items),
      error: () => this.vehicles.set([])
    });

    this.platformService.getBranches().pipe(
      catchError(() => of([])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(b => this.branches.set(b));
  }

  onSelectReport(id: ReportCatalogId): void {
    this.selectedReport.set(id);
    this.hasPreview.set(false);
    this.report.set(null);
    this.draftFilters.update(f => ({ ...f, status: '' }));
  }

  onDraftChange(filters: MaintenanceReportFilters): void {
    this.draftFilters.set(filters);
  }

  onApply(): void {
    this.appliedFilters.set({ ...this.draftFilters() });
    this.loadPreview();
  }

  onReset(): void {
    const reset = defaultFilters();
    this.draftFilters.set(reset);
    this.appliedFilters.set(reset);
    this.hasPreview.set(false);
    this.report.set(null);
  }

  onPreview(): void {
    this.appliedFilters.set({ ...this.draftFilters() });
    this.loadPreview();
  }

  openSchedule(): void {
    this.scheduleOpen.set(true);
  }

  exportExcel(): void {
    this.exportReport('excel');
  }

  exportPdf(): void {
    this.exportReport('pdf');
  }

  private loadPreview(): void {
    this.loading.set(true);
    this.hasPreview.set(true);
    const f = this.appliedFilters();
    this.maintenanceService.getReport(this.selectedReport(), {
      ...f,
      status: f.status || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { this.report.set(r); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load report'));
      }
    });
  }

  private exportReport(format: 'excel' | 'pdf'): void {
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

    const opts = { title: r.title, filename: `maintenance-${r.reportType}` };
    if (format === 'excel') this.exportService.exportExcel(r.rows, cols, opts);
    else this.exportService.exportPdf(r.rows, cols, opts);
    this.toast.success(`${format === 'excel' ? 'Excel' : 'PDF'} exported`);
  }
}
