import { ChangeDetectionStrategy, Component, computed, inject, model, signal, OnInit, DestroyRef } from '@angular/core';
import { NgClass } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, forkJoin, catchError, of, switchMap, debounceTime, distinctUntilChanged, EMPTY, map, take } from 'rxjs';
import { ChartData, ChartOptions } from 'chart.js';
import { DriverService } from '../../../core/services/driver.service';
import { PlatformService } from '../../../core/services/platform.service';
import { DriverListItem, DriverStats, DriverStatus, DriverStatusLabels } from '../../../core/models/driver.model';
import { PagedResult } from '../../../core/models/common.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { UiConfirmService } from '../../../shared/components/ui/confirm-dialog/ui-confirm.service';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../../shared/components/ui/page-header/ui-page-header.component';
import { UiChartComponent } from '../../../shared/components/ui/chart/ui-chart.component';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { FleetSummaryCardComponent } from '../../vehicles/components/fleet-summary-card/fleet-summary-card.component';
import { QuickActionsCardComponent } from '../../fleet-management/fleet-dashboard/widgets/quick-actions-card.component';
import { QuickAction } from '../../fleet-management/fleet-dashboard/fleet-dashboard.model';
import { DriverTableComponent } from '../components/driver-table/driver-table.component';
import { DriverDetailsDrawerComponent } from '../driver-details-drawer/driver-details-drawer.component';
import { EMPTY_DRIVER_FILTERS, DriverFilters, DriverPagination, DEFAULT_DRIVER_PAGE_SIZE, buildDriverKpiCards } from '../models/driver-inventory.model';

@Component({
  selector: 'app-driver-inventory-page',
  standalone: true,
  imports: [
    NgClass,
    RouterModule,
    MatIconModule,
    UiButtonComponent,
    UiPageHeaderComponent,
    UiChartComponent,
    FleetSummaryCardComponent,
    QuickActionsCardComponent,
    DriverTableComponent,
    DriverDetailsDrawerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-inventory-page.component.html',
  styleUrls: ['./driver-inventory-page.component.scss']
})
export class DriverInventoryPageComponent implements OnInit {
  private readonly driverService = inject(DriverService);
  private readonly platformService = inject(PlatformService);
  private readonly confirm = inject(UiConfirmService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();
  private readonly searchInput$ = new Subject<string>();

  /** Performance chart uses sample data until a driver analytics API is available. */
  readonly chartIsSampleData = true;

  readonly rows = signal<DriverListItem[]>([]);
  readonly stats = signal<DriverStats | null>(null);
  readonly complianceDrivers = signal<DriverListItem[]>([]);
  readonly filters = signal<DriverFilters>({ ...EMPTY_DRIVER_FILTERS });
  readonly pagination = signal<DriverPagination>({ page: 1, pageSize: DEFAULT_DRIVER_PAGE_SIZE, total: 0 });
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly drawerOpen = model(false);
  readonly selectedDriverId = signal<number | null>(null);
  readonly branchOptions = signal<UiSelectOption[]>([]);
  readonly chartPeriod = signal<'monthly' | 'quarterly' | 'yearly'>('monthly');

  readonly kpiCards = computed(() => buildDriverKpiCards(this.stats()));

  readonly assignmentCoverage = computed(() => {
    const s = this.stats();
    const total    = s?.totalDrivers    ?? 0;
    const assigned = s?.assignedDrivers ?? 0;
    return {
      assignedDrivers:   assigned,
      unassignedDrivers: Math.max(0, total - assigned)
    };
  });

  readonly feedItems = computed(() =>
    this.rows().slice(0, 5).map(d => ({
      id: d.id,
      name: d.fullName,
      icon: d.status === DriverStatus.OnTrip    ? 'directions_car'
          : d.status === DriverStatus.Available  ? 'check_circle'
          : d.status === DriverStatus.OffDuty    ? 'bedtime'
          : d.status === DriverStatus.OnLeave    ? 'event_busy'
          : d.status === DriverStatus.Suspended  ? 'block'
          : 'person',
      iconClass: d.status === DriverStatus.OnTrip    ? 'feed-icon--trip'
               : d.status === DriverStatus.Available  ? 'feed-icon--available'
               : d.status === DriverStatus.OffDuty    ? 'feed-icon--offduty'
               : d.status === DriverStatus.OnLeave    ? 'feed-icon--leave'
               : d.status === DriverStatus.Suspended  ? 'feed-icon--suspended'
               : 'feed-icon--default',
      event: d.status === DriverStatus.OnTrip    ? 'started a trip'
           : d.status === DriverStatus.Available  ? 'is available for dispatch'
           : d.status === DriverStatus.OffDuty    ? 'went off duty'
           : d.status === DriverStatus.OnLeave    ? 'is on scheduled leave'
           : d.status === DriverStatus.Suspended  ? 'has been suspended'
           : 'updated status',
      isLive: d.status === DriverStatus.OnTrip,
      time: 'Recently'
    }))
  );

  private readonly chartSampleData: Record<'monthly' | 'quarterly' | 'yearly', { labels: string[]; safety: number[]; efficiency: number[]; fuel: number[]; trips: number[] }> = {
    monthly:   { labels: ['JAN','FEB','MAR','APR','MAY','JUN'], safety: [82,75,88,91,85,93], efficiency: [70,68,79,83,76,88], fuel: [74,71,80,78,82,85], trips: [45,38,52,61,58,67] },
    quarterly: { labels: ['Q1','Q2','Q3','Q4'],                 safety: [80,88,85,91],        efficiency: [72,81,78,86],        fuel: [75,80,77,84],        trips: [135,172,168,198] },
    yearly:    { labels: ['2022','2023','2024','2025'],          safety: [76,82,87,91],        efficiency: [68,74,80,86],        fuel: [70,76,80,85],        trips: [520,610,680,750] }
  };

  readonly chartData = computed<ChartData>(() => {
    const d = this.chartSampleData[this.chartPeriod()];
    return {
      labels: d.labels,
      datasets: [
        { label: 'Safety',       data: d.safety,     backgroundColor: '#14b8a6', borderRadius: 4, barPercentage: 0.35 },
        { label: 'Efficiency',   data: d.efficiency, backgroundColor: '#f59e0b', borderRadius: 4, barPercentage: 0.35 },
        { label: 'Fuel Score',   data: d.fuel,       backgroundColor: '#6366f1', borderRadius: 4, barPercentage: 0.35 },
        { label: 'Trips (×10)', data: d.trips.map(v => Math.round(v / 10)), backgroundColor: '#22c55e', borderRadius: 4, barPercentage: 0.35 }
      ]
    };
  });

  readonly chartOptions: ChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 11 } } },
      y: { beginAtZero: true, max: 100, ticks: { stepSize: 20 } }
    }
  };

  readonly statusOptions: UiSelectOption[] = [
    { value: 'ALL', label: 'All statuses' },
    ...Object.values(DriverStatus).filter((v): v is DriverStatus => typeof v === 'number')
      .map(s => ({ value: String(s), label: DriverStatusLabels[s] }))
  ];

  readonly quickActions: QuickAction[] = [
    { id: 'add',      label: 'Add Driver',      icon: 'person_add',     route: '/drivers/new' },
    { id: 'assign',   label: 'Assign Vehicle',  icon: 'directions_car' },
    { id: 'verify',   label: 'Verify Driver',   icon: 'verified_user' },
    { id: 'export',   label: 'Generate Report', icon: 'file_download' },
    { id: 'incident', label: 'Log Incident',    icon: 'report_problem' },
    { id: 'suspend',  label: 'Suspend Driver',  icon: 'block' },
  ];

  ngOnInit(): void {
    this.platformService.getBranches().pipe(
      catchError(() => of([])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(branches => {
      this.branchOptions.set([
        { value: 'ALL', label: 'All branches' },
        ...branches.map(b => ({ value: String(b.id), label: b.name }))
      ]);
    });

    this.searchInput$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.pagination.update(p => ({ ...p, page: 1 }));
      this.reload$.next();
    });

    this.reload$.pipe(
      switchMap(() => this.fetchPage()),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => {
      if (!result) return;
      const { list, stats, compliance, page } = result;
      this.rows.set(list.items);
      this.pagination.set({ ...page, total: list.totalCount });
      this.stats.set(stats);
      this.complianceDrivers.set(compliance.items);
      this.loading.set(false);
    });

    this.load();
  }

  load(): void {
    this.reload$.next();
  }

  private fetchPage() {
    this.loading.set(true);
    this.error.set(null);
    const f = this.filters();
    const p = this.pagination();
    const emptyPaged: PagedResult<DriverListItem> = { items: [], totalCount: 0, page: 1, pageSize: 5, totalPages: 0 };

    return forkJoin({
      list: this.driverService.getAll({
        page: p.page,
        pageSize: p.pageSize,
        q: f.search.trim() || undefined,
        status: f.status === 'ALL' ? undefined : f.status,
        branchId: f.branchId === 'ALL' ? undefined : Number(f.branchId),
        licenseExpiry: f.licenseExpiry === 'ALL' ? undefined : f.licenseExpiry,
        verificationStatus: f.verificationStatus === 'ALL' ? undefined : f.verificationStatus
      }),
      stats: this.driverService.getStats().pipe(catchError(() => of(null))),
      compliance: this.driverService.getAll({ licenseExpiry: 'EXPIRING', pageSize: 5 })
        .pipe(catchError(() => of(emptyPaged)))
    }).pipe(
      map(payload => ({ ...payload, page: p })),
      catchError(err => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, 'Failed to load drivers.'));
        return EMPTY;
      })
    );
  }

  statusValue(status: DriverStatus | 'ALL'): string {
    return String(status);
  }

  initials(name: string): string {
    return name.split(' ').slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase();
  }

  daysUntilExpiry(dateStr: string | Date | null | undefined): number | null {
    if (!dateStr) return null;
    return Math.ceil((new Date(dateStr).getTime() - Date.now()) / 86_400_000);
  }

  expiryDescription(dateStr: string | Date | null | undefined): string {
    const days = this.daysUntilExpiry(dateStr);
    if (days === null) return 'No expiry date';
    if (days < 0) {
      const overdue = Math.abs(days);
      return `Expired ${overdue} day${overdue === 1 ? '' : 's'} ago`;
    }
    if (days === 0) return 'Expires today';
    return `License expires in ${days} day${days === 1 ? '' : 's'}`;
  }

  isExpiryUrgent(dateStr: string | Date | null | undefined): boolean {
    const days = this.daysUntilExpiry(dateStr);
    return days !== null && days <= 7;
  }

  onSearchInput(value: string): void {
    this.filters.update(f => ({ ...f, search: value }));
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filters.update(f => ({ ...f, status: value === 'ALL' ? 'ALL' : Number(value) as DriverStatus }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  onBranchChange(value: string): void {
    this.filters.update(f => ({ ...f, branchId: value === 'ALL' ? 'ALL' : Number(value) }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  onLicenseFilterChange(value: string): void {
    this.filters.update(f => ({ ...f, licenseExpiry: value as DriverFilters['licenseExpiry'] }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  openDrawer(row: DriverListItem): void {
    this.selectedDriverId.set(row.id);
    this.drawerOpen.set(true);
  }

  onDrawerOpenChange(open: boolean): void {
    this.drawerOpen.set(open);
    if (!open) {
      this.selectedDriverId.set(null);
    }
  }

  onDrawerClosed(): void {
    this.drawerOpen.set(false);
    this.selectedDriverId.set(null);
  }

  onEdit(row: DriverListItem): void {
    void this.router.navigate(['/drivers', row.id, 'edit']);
  }

  async onDelete(row: DriverListItem): Promise<void> {
    const label = row.driverCode || row.fullName;
    const ok = await this.confirm.confirmDelete(
      `Delete driver "${label}"? This cannot be undone.`,
      'Delete driver'
    );
    if (!ok) return;

    this.driverService.delete(row.id).pipe(take(1)).subscribe({
      next: () => {
        this.snackBar.open('Driver deleted', 'Close', { duration: 2000 });
        if (this.selectedDriverId() === row.id) {
          this.onDrawerClosed();
        }
        this.load();
      },
      error: err => this.snackBar.open(apiErrorMessage(err, 'Delete failed'), 'Close', { duration: 3000 })
    });
  }

  onPageChange(page: number): void {
    this.pagination.update(p => ({ ...p, page }));
    this.load();
  }

  onPageSizeChange(pageSize: number): void {
    this.pagination.update(p => ({ ...p, pageSize, page: 1 }));
    this.load();
  }

  onDrawerChanged(): void {
    this.load();
  }

  exportReport(): void {
    this.snackBar.open('Export not yet implemented', 'Close', { duration: 2000 });
  }

  onQuickAction(actionId: string): void {
    switch (actionId) {
      case 'assign': {
        const first = this.rows().find(d => !d.assignedVehicleId);
        if (first) {
          this.openDrawer(first);
          return;
        }
        this.snackBar.open('Select a driver from the table to assign a vehicle', 'Close', { duration: 3000 });
        break;
      }
      case 'verify': {
        const pending = this.rows().find(d => d.verificationStatus !== 'Verified');
        if (pending) {
          this.openDrawer(pending);
          return;
        }
        this.snackBar.open('All drivers are verified', 'Close', { duration: 2500 });
        break;
      }
      case 'export':
        this.exportReport();
        break;
    }
  }

  viewCoverageMap(): void {
    this.snackBar.open('Coverage map coming soon', 'Close', { duration: 2000 });
  }
}
