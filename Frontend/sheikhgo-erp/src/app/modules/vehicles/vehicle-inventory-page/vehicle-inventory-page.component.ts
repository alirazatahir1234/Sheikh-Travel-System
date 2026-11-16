import { ChangeDetectionStrategy, Component, computed, inject, signal, ViewChild, OnInit, DestroyRef } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { BulkVehicleImportDialogComponent } from '../bulk-vehicle-import-dialog/bulk-vehicle-import-dialog.component';
import { catchError, forkJoin, of, filter, interval, Observable, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PagedResult } from '../../../core/models/common.model';
import { VehicleService } from '../../../core/services/vehicle.service';
import { PlatformService } from '../../../core/services/platform.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { DriverService } from '../../../core/services/driver.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { UiConfirmService } from '../../../shared/components/ui/confirm-dialog/ui-confirm.service';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../../shared/components/ui/page-header/ui-page-header.component';
import { FleetService } from '../../fleet-management/services/fleet.service';
import { FleetDashboardSummary } from '../../fleet-management/models/fleet.model';
import {
  VehicleListItem,
  VehicleStatus,
  VehicleStatusLabels,
  normalizeVehicleStatus
} from '../../../core/models/vehicle.model';
import { formatRelativeTime } from '../../../core/utils/relative-time.util';
import { FleetSummaryCardComponent, FleetSummaryCardData } from '../components/fleet-summary-card/fleet-summary-card.component';
import { FleetFilterToolbarComponent } from '../components/fleet-filter-toolbar/fleet-filter-toolbar.component';
import { VehicleTableComponent } from '../components/vehicle-table/vehicle-table.component';
import { VehicleMobileCardsComponent } from '../components/vehicle-mobile-cards/vehicle-mobile-cards.component';
import { VehicleDetailsDrawerComponent } from '../vehicle-details-drawer/vehicle-details-drawer.component';
import { VehicleAssignDialogComponent } from '../components/vehicle-assign-dialog/vehicle-assign-dialog.component';
import { VehicleBulkToolbarComponent } from '../components/vehicle-bulk-toolbar/vehicle-bulk-toolbar.component';
import { VehicleAdvancedFiltersComponent } from '../components/vehicle-advanced-filters/vehicle-advanced-filters.component';
import { VehicleRegistryFabComponent } from '../components/vehicle-registry-fab/vehicle-registry-fab.component';
import {
  EMPTY_VEHICLE_FILTERS,
  VehicleFilters,
  VehiclePagination
} from '../models/vehicle-inventory.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import {
  isInsuranceAttention,
  isGpsOnline,
  isMaintenanceDue,
  matchesVehicleFilters,
  normalizeVehicleFilters,
  vehicleHasTracker
} from '../utils/vehicle-filter.util';

@Component({
  selector: 'app-vehicle-inventory-page',
  standalone: true,
  imports: [
    RouterModule,
    MatIconModule,
    MatMenuModule,
    MatButtonModule,
    MatDialogModule,
    UiButtonComponent,
    UiPageHeaderComponent,
    FleetSummaryCardComponent,
    FleetFilterToolbarComponent,
    VehicleTableComponent,
    VehicleMobileCardsComponent,
    VehicleDetailsDrawerComponent,
    VehicleAssignDialogComponent,
    VehicleBulkToolbarComponent,
    VehicleAdvancedFiltersComponent,
    VehicleRegistryFabComponent
  ],
  providers: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicle-inventory-page.component.html',
  styleUrls: ['./vehicle-inventory-page.component.scss']
})
export class VehicleInventoryPageComponent implements OnInit {
  private readonly vehicleService = inject(VehicleService);
  private readonly fleetService = inject(FleetService);
  private readonly platformService = inject(PlatformService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly driverService = inject(DriverService);
  private readonly exportService = inject(ExportService);
  private readonly confirm = inject(UiConfirmService);
  private readonly toast = inject(UiToastService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly datePipe = inject(DatePipe);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * When the switcher is available: only load for a concrete company (never JWT-home while
   * All/unset). Ordinary tenant users keep null and rely on JWT + interceptor (no override).
   */
  private companyTenantId: number | null = null;
  readonly needsCompanyPick = signal(false);

  @ViewChild(VehicleAssignDialogComponent) assignDialog!: VehicleAssignDialogComponent;

  readonly allRows = signal<VehicleListItem[]>([]);
  readonly dashboard = signal<FleetDashboardSummary | null>(null);
  readonly filters = signal<VehicleFilters>({ ...EMPTY_VEHICLE_FILTERS });
  readonly pagination = signal<VehiclePagination>({ page: 1, pageSize: 25, total: 0 });
  readonly selection = signal<Set<number>>(new Set());
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly drawerOpen = signal(false);
  readonly selectedVehicleId = signal<number | null>(null);
  readonly selectedListItem  = signal<VehicleListItem | null>(null);
  readonly branchOptions = signal<UiSelectOption<number>[]>([]);
  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly advancedFiltersOpen = signal(false);
  readonly lastSyncedAt = signal<Date | null>(null);
  readonly syncTick = signal(Date.now());

  readonly statusOptions: UiSelectOption[] = [
    { value: 'ALL', label: 'All' },
    ...Object.values(VehicleStatus)
      .filter((v): v is VehicleStatus => typeof v === 'number')
      .map(s => ({ value: String(s), label: VehicleStatusLabels[s] }))
  ];

  readonly vehicleTypeOptions = computed<UiSelectOption[]>(() => {
    const types = new Set(
      this.allRows().map(v => v.vehicleType).filter((t): t is string => !!t?.trim())
    );
    return [...types].sort().map(t => ({ value: t, label: t }));
  });

  readonly lastSyncLabel = computed(() => {
    this.syncTick();
    return formatRelativeTime(this.lastSyncedAt());
  });

  readonly driverNameById = computed(() => {
    const map = new Map<number, string>();
    for (const opt of this.driverOptions()) {
      const id = Number(opt.value);
      if (Number.isFinite(id)) map.set(id, opt.label);
    }
    return map;
  });

  readonly filteredRows = computed(() => {
    const f = normalizeVehicleFilters(this.filters());
    const drivers = this.driverNameById();
    return this.allRows().filter(row => matchesVehicleFilters(row, f, drivers));
  });

  readonly displayRows = computed(() => {
    const f = this.filteredRows();
    const p = this.pagination();
    const start = (p.page - 1) * p.pageSize;
    return f.slice(start, start + p.pageSize);
  });

  readonly resultSummary = computed(() => {
    const shown = this.filteredRows().length;
    return `Showing ${this.displayRows().length} of ${shown} vehicles`;
  });

  /** Active fleet for KPIs — excludes Draft + Retired (matches default dashboard). */
  readonly fleetRows = computed(() =>
    this.allRows().filter(r => {
      const s = normalizeVehicleStatus(r.status);
      return s !== VehicleStatus.Draft && s !== VehicleStatus.Retired;
    })
  );

  readonly kpiCards = computed<FleetSummaryCardData[]>(() => {
    const rows = this.fleetRows();
    const onTrip = rows.filter(r => normalizeVehicleStatus(r.status) === VehicleStatus.OnTrip).length;
    const available = rows.filter(r => normalizeVehicleStatus(r.status) === VehicleStatus.Available).length;
    const total = rows.length;
    const maintenance = rows.filter(r => isMaintenanceDue(r)).length;
    const utilization = total > 0 ? Math.round((onTrip / total) * 100) : 0;
    const gpsConnected = rows.filter(r => isGpsOnline(r)).length;
    const gpsOffline = rows.filter(r => vehicleHasTracker(r) && !isGpsOnline(r)).length;
    const insuranceAlerts = rows.filter(r => isInsuranceAttention(r)).length;
    const noTracker = rows.filter(r => !vehicleHasTracker(r)).length;

    return [
      {
        icon: 'local_shipping',
        title: 'Total Fleet Assets',
        value: total.toLocaleString(),
        subtext: 'Excludes drafts & retired',
        variant: 'neutral'
      },
      {
        icon: 'route',
        title: 'Active / On Route',
        value: onTrip.toLocaleString(),
        progress: utilization,
        subtext: `${onTrip} on route`,
        variant: 'operational'
      },
      {
        icon: 'warning',
        title: 'Maintenance Due',
        value: maintenance.toLocaleString(),
        alert: maintenance > 0,
        subtext: 'Status or service overdue',
        variant: 'risk'
      },
      {
        icon: 'check_circle',
        title: 'Available / Ready',
        value: available.toLocaleString(),
        subtext: 'Fleet ready',
        variant: 'operational'
      },
      {
        icon: 'gps_fixed',
        title: 'GPS Connected',
        value: gpsConnected.toLocaleString(),
        subtext: 'Online trackers',
        variant: 'operational'
      },
      {
        icon: 'signal_wifi_off',
        title: 'Vehicles Offline',
        value: gpsOffline.toLocaleString(),
        alert: gpsOffline > 0,
        subtext: 'Tracker offline',
        variant: 'risk'
      },
      {
        icon: 'policy',
        title: 'Insurance Alerts',
        value: insuranceAlerts.toLocaleString(),
        alert: insuranceAlerts > 0,
        subtext: 'Expired or within 30 days',
        variant: 'risk'
      },
      {
        icon: 'gps_off',
        title: 'No GPS Tracker',
        value: noTracker.toLocaleString(),
        alert: noTracker > 0,
        subtext: 'Untracked assets',
        variant: 'risk'
      }
    ];
  });

  constructor() {
    this.resolveCompanyTenantFromRoute();
    this.loadBranches();
    this.loadDrivers();
  }

  ngOnInit(): void {
    this.load();
    interval(30_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.syncTick.set(Date.now()));

    this.tenantContext.tenantId$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.resolveCompanyTenantFromRoute();
      this.loadBranches();
      this.loadDrivers();
      this.load();
    });
    this.tenantContext.isAllCompanies$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.resolveCompanyTenantFromRoute();
      this.loadBranches();
      this.loadDrivers();
      this.load();
    });

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      filter(event => event.urlAfterRedirects === '/vehicles' || event.urlAfterRedirects.startsWith('/vehicles?')),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.resolveCompanyTenantFromRoute();
      this.loadBranches();
      this.loadDrivers();
      this.load();
    });
  }

  private resolveCompanyTenantFromRoute(): void {
    const raw = this.route.snapshot.queryParamMap.get('tenantId');
    const fromRoute = raw ? Number(raw) : NaN;
    if (Number.isFinite(fromRoute) && fromRoute > 0) {
      this.tenantContext.selectTenantById(fromRoute);
    }

    if (this.tenantContext.canUseSwitcher) {
      if (this.tenantContext.hasConcreteTenant) {
        this.companyTenantId = this.tenantContext.currentTenantId;
        this.needsCompanyPick.set(false);
      } else {
        this.companyTenantId = null;
        this.needsCompanyPick.set(true);
      }
      return;
    }

    this.companyTenantId = null;
    this.needsCompanyPick.set(false);
  }

  load(): void {
    if (this.needsCompanyPick()) {
      this.allRows.set([]);
      this.dashboard.set(null);
      this.loading.set(false);
      this.error.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      vehicles: this.loadAllVehicles().pipe(
        catchError(err => {
          this.error.set(apiErrorMessage(err, 'Failed to load vehicles.'));
          return of({ items: [] as VehicleListItem[], totalCount: 0, page: 1, pageSize: 0, totalPages: 0 });
        })
      ),
      dashboard: this.fleetService.getDashboard().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ vehicles, dashboard }) => {
        this.allRows.set(vehicles.items);
        this.dashboard.set(dashboard);
        this.clampPageToFiltered();
        this.lastSyncedAt.set(new Date());
        this.loading.set(false);
        // Dashboard is tenant-wide; list is data-scoped — surface a mismatch instead of a blank grid.
        const dashTotal = dashboard?.totalVehicles ?? 0;
        if (!this.error() && vehicles.items.length === 0 && dashTotal > 0) {
          this.error.set(
            `Fleet reports ${dashTotal} vehicle(s), but none are in the branch/department your role can see. ` +
              `Ask a company admin to assign these vehicles to your branch, or set your role's data scope to Company.`
          );
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load vehicles.');
      }
    });
  }

  /** Fetch every page so KPIs / filters / export are not silently capped at 500. */
  private loadAllVehicles(): Observable<PagedResult<VehicleListItem>> {
    const pageSize = 200;
    const tenantId = this.companyTenantId;
    const loadPage = (page: number, acc: VehicleListItem[]): Observable<PagedResult<VehicleListItem>> =>
      this.vehicleService.getAll(page, pageSize, true, tenantId).pipe(
        switchMap(res => {
          const items = [...acc, ...res.items];
          const done = res.items.length === 0 || items.length >= res.totalCount;
          if (done) {
            const size = items.length || pageSize;
            return of({
              items,
              totalCount: res.totalCount,
              page: 1,
              pageSize: size,
              totalPages: Math.max(1, Math.ceil(Math.max(res.totalCount, 1) / size))
            });
          }
          return loadPage(page + 1, items);
        })
      );
    return loadPage(1, []);
  }

  private clampPageToFiltered(): void {
    this.pagination.update(pg => {
      const total = this.filteredRows().length;
      const maxPage = Math.max(1, Math.ceil(Math.max(total, 1) / pg.pageSize) || 1);
      return { ...pg, total, page: Math.min(pg.page, maxPage) };
    });
  }

  onFiltersChange(f: VehicleFilters): void {
    this.filters.set(normalizeVehicleFilters(f));
    this.pagination.update(p => ({ ...p, page: 1 }));
    // Defer clamp so filteredRows() sees the new filters.
    queueMicrotask(() => this.clampPageToFiltered());
  }

  onPageChange(page: number): void {
    this.pagination.update(p => ({ ...p, page }));
  }

  onPageSizeChange(pageSize: number): void {
    this.pagination.update(p => ({ ...p, pageSize, page: 1 }));
  }

  onSelectionChange(ids: ReadonlySet<number>): void {
    this.selection.set(new Set(ids));
  }

  clearSelection(): void {
    this.selection.set(new Set());
  }

  openDrawer(row: VehicleListItem): void {
    this.selectedVehicleId.set(row.id);
    this.selectedListItem.set(row);
    this.drawerOpen.set(true);
  }

  onDrawerOpenChange(open: boolean): void {
    this.drawerOpen.set(open);
    if (!open) {
      this.selectedVehicleId.set(null);
      this.selectedListItem.set(null);
    }
  }

  onDrawerClosed(): void {
    this.drawerOpen.set(false);
    this.selectedVehicleId.set(null);
    this.selectedListItem.set(null);
  }

  onEdit(row: VehicleListItem): void {
    const q =
      this.tenantContext.canUseSwitcher && this.tenantContext.currentTenantId != null
        ? { tenantId: this.tenantContext.currentTenantId }
        : undefined;
    void this.router.navigate(['/vehicles', row.id, 'edit'], { queryParams: q });
  }

  onTrack(row: VehicleListItem): void {
    void this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: row.id } });
  }

  importVehicles(): void {
    this.dialog.open(BulkVehicleImportDialogComponent, { width: '800px' })
      .afterClosed()
      .subscribe(result => {
        if (result?.created) this.load();
      });
  }

  onAssign(row: VehicleListItem): void {
    this.assignDialog?.show(row.id, row.driverName ?? null);
  }

  async onDelete(row: VehicleListItem): Promise<void> {
    const label = row.vehicleCode || row.registrationNumber;
    const ok = await this.confirm.confirmDelete(
      `Delete vehicle "${label}"? This cannot be undone.`,
      'Delete vehicle'
    );
    if (!ok) return;

    this.vehicleService.delete(row.id).subscribe({
      next: () => {
        this.toast.success('Vehicle deleted');
        this.load();
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Delete failed'))
    });
  }

  onBulkAssign(): void {
    const ids = [...this.selection()];
    if (!ids.length) return;
    const assignees: Record<number, string> = {};
    for (const row of this.filteredRows()) {
      if (!ids.includes(row.id)) continue;
      const name = (row.driverName ?? '').trim();
      if (name) assignees[row.id] = name;
    }
    this.assignDialog?.show(ids, assignees);
  }

  onBulkExport(): void {
    const ids = this.selection();
    const rows = this.filteredRows().filter(r => ids.has(r.id));
    if (!rows.length) return;
    const { columns, meta } = this.buildExport(rows.length);
    this.exportService.exportExcel(rows, columns, meta);
  }

  async onBulkDelete(): Promise<void> {
    const ids = [...this.selection()];
    if (!ids.length) return;
    const ok = await this.confirm.confirmDelete(
      `Delete ${ids.length} selected vehicle(s)? This cannot be undone.`,
      'Delete vehicles'
    );
    if (!ok) return;

    let failed = 0;
    for (const id of ids) {
      await new Promise<void>(resolve => {
        this.vehicleService.delete(id).subscribe({
          next: () => resolve(),
          error: () => { failed++; resolve(); }
        });
      });
    }
    this.toast.success(failed ? `Deleted with ${failed} failure(s)` : 'Vehicles deleted');
    this.clearSelection();
    this.load();
  }

  onBulkScheduleMaintenance(): void {
    const ids = [...this.selection()];
    if (!ids.length) return;
    void this.router.navigate(['/fleet/maintenance/requests'], {
      queryParams: { create: true, vehicleId: ids[0] }
    });
  }

  exportExcel(): void {
    const { columns, meta } = this.buildExport(this.filteredRows().length);
    this.exportService.exportExcel(this.filteredRows(), columns, meta);
  }

  exportPdf(): void {
    const { columns, meta } = this.buildExport(this.filteredRows().length);
    this.exportService.exportPdf(this.filteredRows(), columns, meta);
  }

  private buildExport(count: number): { columns: ExportColumn<VehicleListItem>[]; meta: { filename: string; title: string; subtitle: string; sheetName: string } } {
    const columns: ExportColumn<VehicleListItem>[] = [
      { header: 'Code', accessor: v => v.vehicleCode ?? '', excelWidth: 14 },
      { header: 'Registration', accessor: v => v.registrationNumber, excelWidth: 16 },
      { header: 'Make', accessor: v => v.make ?? '', excelWidth: 14 },
      { header: 'Model', accessor: v => v.model ?? '', excelWidth: 14 },
      { header: 'Year', accessor: v => v.year ?? '', excelWidth: 8 },
      { header: 'Status', accessor: v => VehicleStatusLabels[v.status], excelWidth: 14 },
      { header: 'Driver', accessor: v => v.driverName ?? '', excelWidth: 18 },
      { header: 'Mileage', accessor: v => v.currentMileage, excelWidth: 12 },
      { header: 'IMEI', accessor: v => v.gpsImei ?? '', excelWidth: 18 },
      { header: 'GPS', accessor: v => v.gpsOnline ? 'Online' : 'Offline', excelWidth: 10 }
    ];
    const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
    return {
      columns,
      meta: {
        filename: `vehicle-registry-${stamp}`,
        title: 'Vehicle Inventory Registry',
        subtitle: `${count} vehicle(s)`,
        sheetName: 'Vehicles'
      }
    };
  }

  private loadBranches(): void {
    const tenantId = this.companyTenantId;
    const source = tenantId != null
      ? this.platformService.getBranchesForTenant(tenantId)
      : this.platformService.getBranches();
    source.pipe(catchError(() => of([]))).subscribe(branches => {
      this.branchOptions.set(branches.map(b => ({ value: b.id, label: b.name })));
    });
  }

  private loadDrivers(): void {
    const params = this.companyTenantId != null
      ? { page: 1, pageSize: 500, tenantId: this.companyTenantId }
      : { page: 1, pageSize: 500 };
    this.driverService.getAll(params).pipe(catchError(() => of({ items: [] }))).subscribe(res => {
      this.driverOptions.set(res.items.map(d => ({ value: String(d.id), label: d.fullName })));
    });
  }
}
