import { ChangeDetectionStrategy, Component, computed, inject, signal, ViewChild, OnInit, DestroyRef } from '@angular/core';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe, DecimalPipe } from '@angular/common';
import { catchError, forkJoin, of, filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { VehicleService } from '../../../core/services/vehicle.service';
import { PlatformService } from '../../../core/services/platform.service';
import { DriverService } from '../../../core/services/driver.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { UiConfirmService } from '../../../shared/components/ui/confirm-dialog/ui-confirm.service';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../../shared/components/ui/page-header/ui-page-header.component';
import { FleetService } from '../../fleet-management/services/fleet.service';
import { FleetDashboardSummary } from '../../fleet-management/models/fleet.model';
import {
  VehicleListItem,
  VehicleStatus,
  VehicleStatusLabels,
  FuelTypeLabels
} from '../../../core/models/vehicle.model';
import { formatRelativeTime } from '../../../core/utils/relative-time.util';
import { FleetSummaryCardComponent, FleetSummaryCardData } from '../components/fleet-summary-card/fleet-summary-card.component';
import { FleetFilterToolbarComponent } from '../components/fleet-filter-toolbar/fleet-filter-toolbar.component';
import { VehicleTableComponent } from '../components/vehicle-table/vehicle-table.component';
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
  isInsuranceExpiringSoon,
  isGpsOnline,
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
    DatePipe,
    DecimalPipe,
    UiButtonComponent,
    UiPageHeaderComponent,
    FleetSummaryCardComponent,
    FleetFilterToolbarComponent,
    VehicleTableComponent,
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
  private readonly driverService = inject(DriverService);
  private readonly exportService = inject(ExportService);
  private readonly confirm = inject(UiConfirmService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly datePipe = inject(DatePipe);
  private readonly destroyRef = inject(DestroyRef);

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

  readonly kpiCards = computed<FleetSummaryCardData[]>(() => {
    const d = this.dashboard();
    const rows = this.allRows();
    const onTrip = rows.filter(r => r.status === VehicleStatus.OnTrip).length;
    const available = rows.filter(r => r.status === VehicleStatus.Available).length;
    const total = d?.totalVehicles ?? rows.length;
    const maintenance = d?.maintenanceDue ?? rows.filter(r => r.status === VehicleStatus.Maintenance).length;
    const utilization = total > 0 ? Math.round((onTrip / total) * 100) : 0;
    const gpsConnected = rows.filter(r => isGpsOnline(r)).length;
    const gpsOffline = rows.filter(r => vehicleHasTracker(r) && !isGpsOnline(r)).length;
    const insuranceExpiring = rows.filter(r => isInsuranceExpiringSoon(r)).length;
    const fuelAlerts = rows.filter(r =>
      r.serviceAlert?.toLowerCase().includes('fuel') || r.serviceAlert?.toLowerCase().includes('oil')
    ).length;

    return [
      { icon: 'local_shipping', title: 'Total Fleet Assets', value: total.toLocaleString(), subtext: 'Registered vehicles' },
      { icon: 'route', title: 'Active / On Route', value: onTrip.toLocaleString(), progress: utilization, subtext: `${utilization}% utilization` },
      { icon: 'warning', title: 'Maintenance (Critical)', value: maintenance.toLocaleString(), alert: maintenance > 0 },
      { icon: 'check_circle', title: 'Available / Ready', value: available.toLocaleString(), subtext: 'Fleet ready' },
      { icon: 'gps_fixed', title: 'GPS Connected', value: gpsConnected.toLocaleString(), subtext: 'Online trackers' },
      { icon: 'signal_wifi_off', title: 'Vehicles Offline', value: gpsOffline.toLocaleString(), alert: gpsOffline > 0, subtext: 'Tracker offline' },
      { icon: 'policy', title: 'Insurance Expiring', value: insuranceExpiring.toLocaleString(), alert: insuranceExpiring > 0, subtext: 'Within 30 days' },
      { icon: 'local_gas_station', title: 'Fuel Alerts', value: fuelAlerts.toLocaleString(), alert: fuelAlerts > 0, subtext: 'Service reminders' }
    ];
  });

  constructor() {
    this.loadBranches();
    this.loadDrivers();
  }

  ngOnInit(): void {
    this.load();
    setInterval(() => this.syncTick.set(Date.now()), 30_000);

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      filter(event => event.urlAfterRedirects === '/vehicles' || event.urlAfterRedirects.startsWith('/vehicles?')),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.load());
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 500 }))),
      dashboard: this.fleetService.getDashboard().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ vehicles, dashboard }) => {
        this.allRows.set(vehicles.items);
        this.dashboard.set(dashboard);
        this.pagination.update(pg => ({
          ...pg,
          total: vehicles.totalCount,
          page: Math.min(pg.page, Math.max(1, Math.ceil(vehicles.totalCount / pg.pageSize)))
        }));
        this.lastSyncedAt.set(new Date());
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load vehicles.');
      }
    });
  }

  onFiltersChange(f: VehicleFilters): void {
    this.filters.set(normalizeVehicleFilters(f));
    this.pagination.update(p => ({ ...p, page: 1 }));
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
    this.drawerOpen.set(true);
  }

  onEdit(row: VehicleListItem): void {
    void this.router.navigate(['/vehicles', row.id, 'edit']);
  }

  onTrack(row: VehicleListItem): void {
    void this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: row.id } });
  }

  onAssign(row: VehicleListItem): void {
    this.assignDialog?.show(row.id);
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
        this.snackBar.open('Vehicle deleted', 'Close', { duration: 2000 });
        this.load();
      },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  onBulkAssign(): void {
    const ids = [...this.selection()];
    if (!ids.length) return;
    this.assignDialog?.show(ids[0]);
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
    this.snackBar.open(
      failed ? `Deleted with ${failed} failure(s)` : 'Vehicles deleted',
      'Close',
      { duration: 3000 }
    );
    this.clearSelection();
    this.load();
  }

  onBulkScheduleMaintenance(): void {
    const ids = [...this.selection()];
    if (!ids.length) return;
    void this.router.navigate(['/maintenance/new'], { queryParams: { vehicleIds: ids.join(',') } });
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
    this.platformService.getBranches().pipe(catchError(() => of([]))).subscribe(branches => {
      this.branchOptions.set(branches.map(b => ({ value: b.id, label: b.name })));
    });
  }

  private loadDrivers(): void {
    this.driverService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))).subscribe(res => {
      this.driverOptions.set(res.items.map(d => ({ value: String(d.id), label: d.fullName })));
    });
  }
}
