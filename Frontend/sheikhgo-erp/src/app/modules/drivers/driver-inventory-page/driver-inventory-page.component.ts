import { ChangeDetectionStrategy, Component, computed, inject, model, signal, OnInit, DestroyRef, ElementRef, ViewChild } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { BulkDriverImportDialogComponent } from '../bulk-driver-import-dialog/bulk-driver-import-dialog.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, forkJoin, catchError, of, switchMap, debounceTime, distinctUntilChanged, EMPTY, map, take, Observable } from 'rxjs';
import { ChartData } from 'chart.js';
import { UiChartOptions } from '../../../shared/components/ui';
import { DriverService, DriverListParams } from '../../../core/services/driver.service';
import { AuthService } from '../../../core/services/auth.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { PlatformService } from '../../../core/services/platform.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { DriverListItem, DriverStats, DriverStatus, DriverStatusLabels, DriversAvailabilitySummary } from '../../../core/models/driver.model';
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
import { DriverBulkAssignDialogComponent } from '../components/driver-bulk-assign-dialog/driver-bulk-assign-dialog.component';
import { DriverLogIncidentDialogComponent, LogIncidentResult } from '../components/driver-log-incident-dialog/driver-log-incident-dialog.component';
import { DriverDetailsDrawerComponent } from '../driver-details-drawer/driver-details-drawer.component';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { DriverScore } from '../../../core/models/gps-tracking.model';
import { EMPTY_DRIVER_FILTERS, DriverFilters, DriverPagination, DEFAULT_DRIVER_PAGE_SIZE, buildDriverKpiGroups, buildOperationsSummary, buildAssignmentCoverage, buildComplianceAttention, blendDriverTopScore, computeDriverScore, exclusiveLicenseWindows, scoreTone, TopDriverRankItem } from '../models/driver-inventory.model';


@Component({
  selector: 'app-driver-inventory-page',
  standalone: true,
  providers: [DatePipe],
  imports: [
    NgClass,
    RouterModule,
        MatIconModule,
    MatMenuModule,
    MatButtonModule,
    MatDialogModule,
    UiButtonComponent,
    UiPageHeaderComponent,
    UiChartComponent,
    FleetSummaryCardComponent,
    QuickActionsCardComponent,
    DriverTableComponent,
    DriverBulkAssignDialogComponent,
    DriverLogIncidentDialogComponent,
    DriverDetailsDrawerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-inventory-page.component.html',
  styleUrls: ['./driver-inventory-page.component.scss']
})
export class DriverInventoryPageComponent implements OnInit {
  @ViewChild(DriverBulkAssignDialogComponent) bulkAssignDialog?: DriverBulkAssignDialogComponent;
  @ViewChild(DriverLogIncidentDialogComponent) logIncidentDialog?: DriverLogIncidentDialogComponent;
  @ViewChild(DriverDetailsDrawerComponent) detailsDrawer?: DriverDetailsDrawerComponent;
  @ViewChild('tableSection') tableSection?: ElementRef<HTMLElement>;

  private readonly driverService = inject(DriverService);
  private readonly gpsTracking = inject(GpsTrackingService);
  private readonly auth = inject(AuthService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly exportService = inject(ExportService);
  private readonly platformService = inject(PlatformService);
  private readonly confirm = inject(UiConfirmService);
  private readonly datePipe = inject(DatePipe);
  private readonly toast = inject(UiToastService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();
  /** Cancels in-flight Top Drivers / compliance fetches when the page reloads. */
  private readonly deferredReload$ = new Subject<void>();
  private readonly searchInput$ = new Subject<string>();
  private exporting = false;

  /** Chart uses scoped availability buckets (same as Availability panel). */
  readonly chartIsSampleData = false;

  readonly rows = signal<DriverListItem[]>([]);
  /** Broader sample for Top Drivers (branch-scoped, up to 500). */
  readonly rankPool = signal<DriverListItem[]>([]);
  /** GPS Analytics safety ranking (last 7 days) — blended 60/40 with compliance. */
  readonly gpsRanking = signal<DriverScore[]>([]);
  readonly stats = signal<DriverStats | null>(null);
  readonly statsError = signal<string | null>(null);
  readonly availabilitySummary = signal<DriversAvailabilitySummary | null>(null);
  readonly complianceDrivers = signal<DriverListItem[]>([]);
  readonly filters = signal<DriverFilters>({ ...EMPTY_DRIVER_FILTERS });
  readonly pagination = signal<DriverPagination>({ page: 1, pageSize: DEFAULT_DRIVER_PAGE_SIZE, total: 0 });
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly drawerOpen = model(false);
  readonly selectedDriverId = signal<number | null>(null);
  readonly drawerPreferredTab = signal<'performance' | 'vehicle' | null>(null);
  readonly branchOptions = signal<UiSelectOption[]>([]);

  readonly availabilityOptions: UiSelectOption[] = [
    { value: 'ALL', label: 'All availability' },
    { value: 'Available', label: 'Available' },
    { value: 'Busy', label: 'Busy' },
    { value: 'OnTrip', label: 'On Trip' },
    { value: 'Unavailable', label: 'Unavailable' }
  ];

  readonly verificationOptions: UiSelectOption[] = [
    { value: 'ALL', label: 'All verification' },
    { value: 'Verified', label: 'Verified' },
    { value: 'Pending', label: 'Pending' },
    { value: 'UnderReview', label: 'Under Review' },
    { value: 'Rejected', label: 'Rejected' }
  ];

  readonly kpiGroups = computed(() => buildDriverKpiGroups(this.stats()));
  readonly operationsSummary = computed(() => buildOperationsSummary(this.stats()));
  readonly assignmentCoverage = computed(() => buildAssignmentCoverage(this.stats()));
  readonly licenseWindows = computed(() => exclusiveLicenseWindows(this.stats()));

  readonly hasActiveFilters = computed(() => {
    const f = this.filters();
    return !!(
      f.search.trim() ||
      f.status !== 'ALL' ||
      f.branchId !== 'ALL' ||
      f.licenseExpiry !== 'ALL' ||
      f.verificationStatus !== 'ALL' ||
      f.availability !== 'ALL'
    );
  });

  readonly resultSummary = computed(() => {
    const total = this.pagination().total;
    const label = total === 1 ? 'driver' : 'drivers';
    if (this.loading() && !this.rows().length && total === 0) {
      return 'Loading drivers…';
    }
    return this.hasActiveFilters()
      ? `${total} matching ${label}`
      : `${total} ${label}`;
  });

  readonly topDrivers = computed((): TopDriverRankItem[] => {
    const gpsMap = new Map(this.gpsRanking().map(r => [r.driverId, r]));
    return this.rankPool()
      .map(d => {
        const complianceScore = computeDriverScore(d);
        const gps = gpsMap.get(d.id);
        const safetyScore = gps?.score ?? null;
        return {
          driver: d,
          complianceScore,
          safetyScore,
          isPartial: gps?.isPartial ?? false,
          score: blendDriverTopScore(complianceScore, safetyScore)
        };
      })
      .sort((a, b) => b.score - a.score)
      .slice(0, 5);
  });

  /** Exclusive license urgency tiles (Expired / ≤7d / 8–30d / 31–60d). */
  readonly complianceSeverity = computed(() => {
    const w = this.licenseWindows();
    return {
      critical: w.expired,
      high: w.within7,
      medium: w.days8to30,
      watch: w.days31to60
    };
  });

  readonly complianceAttention = computed(() => buildComplianceAttention(this.stats()));

  /** Snapshot of current table page — not a live SignalR feed. */
  readonly feedItems = computed(() =>
    this.rows().slice(0, 5).map(d => {
      const licenseAlert = d.licenseExpiringSoon || d.licenseExpired;
      if (licenseAlert) {
        return {
          id: d.id,
          eventLabel: d.licenseExpired ? 'License Expired' : 'License Expiring',
          eventDetail: `${d.fullName} · ${this.expiryDescription(d.licenseExpiryDate)}`,
          icon: 'warning',
          iconClass: 'feed-icon--warning',
          time: 'From roster'
        };
      }
      if (d.assignedVehicleId && (d.availabilityBucket === 'Busy' || d.availabilityBucket === 'Available')) {
        return {
          id: d.id,
          eventLabel: 'Vehicle Assigned',
          eventDetail: `${d.fullName} · ${d.assignedVehicleRegistration || d.assignedVehicleCode || 'Vehicle linked'}`,
          icon: 'directions_car',
          iconClass: 'feed-icon--trip',
          time: 'From roster'
        };
      }
      const meta = {
        [DriverStatus.OnTrip]: { eventLabel: 'On Trip', icon: 'trip_origin', iconClass: 'feed-icon--trip' },
        [DriverStatus.Available]: { eventLabel: 'Duty Available', icon: 'check_circle', iconClass: 'feed-icon--available' },
        [DriverStatus.OffDuty]: { eventLabel: 'Off Duty', icon: 'bedtime', iconClass: 'feed-icon--offduty' },
        [DriverStatus.OnLeave]: { eventLabel: 'On Leave', icon: 'event_busy', iconClass: 'feed-icon--leave' },
        [DriverStatus.Suspended]: { eventLabel: 'Driver Suspended', icon: 'block', iconClass: 'feed-icon--suspended' }
      }[d.status] ?? { eventLabel: 'Status Update', icon: 'person', iconClass: 'feed-icon--default' };

      return {
        id: d.id,
        eventLabel: meta.eventLabel,
        eventDetail: d.fullName,
        icon: meta.icon,
        iconClass: meta.iconClass,
        time: 'From roster'
      };
    })
  );

  /**
   * Exclusive dispatch buckets for chart + Availability panel.
   * Prefer dedicated summary when loaded; otherwise reuse /stats (same bucket rules) so KPIs aren't blocked.
   */
  readonly dispatchAvailability = computed((): DriversAvailabilitySummary | null => {
    const a = this.availabilitySummary();
    if (a) return a;
    const s = this.stats();
    if (!s) return null;
    return {
      available: s.available ?? 0,
      busy: s.busy ?? 0,
      onTrip: s.onTrip ?? 0,
      unavailable: s.unavailable ?? 0
    };
  });

  /** Bars use exclusive dispatch-availability buckets (same as Availability panel). */
  readonly chartData = computed<ChartData>(() => {
    const a = this.dispatchAvailability();
    const labels = ['Available', 'Busy', 'On Trip', 'Unavailable'];
    const values = a
      ? [a.available, a.busy, a.onTrip, a.unavailable]
      : [0, 0, 0, 0];
    return {
      labels,
      datasets: [{
        label: 'Drivers',
        data: values,
        backgroundColor: ['#22c55e', '#f59e0b', '#0ea5e9', '#94a3b8'],
        borderRadius: 4
      }]
    };
  });

  readonly chartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 11 } } },
      y: { beginAtZero: true, ticks: { stepSize: 1 } }
    }
  };

  readonly statusOptions: UiSelectOption[] = [
    { value: 'ALL', label: 'All statuses' },
    ...Object.values(DriverStatus).filter((v): v is DriverStatus => typeof v === 'number')
      .map(s => ({ value: String(s), label: DriverStatusLabels[s] }))
  ];

  get canCreateDriver(): boolean {
    return this.auth.hasPermission('Driver.Create') && this.tenantContext.canMutate;
  }

  /** Platform switcher without a concrete company — do not load JWT-home drivers. */
  get needsCompanyPick(): boolean {
    return this.tenantContext.canUseSwitcher && !this.tenantContext.hasConcreteTenant;
  }

  get canUpdateDriver(): boolean {
    return this.auth.hasPermission('Driver.Update');
  }

  get canDeleteDriver(): boolean {
    return this.auth.hasPermission('Driver.Delete');
  }

  get canAssignDriver(): boolean {
    return this.auth.hasPermission('Driver.Assign');
  }

  get canManageStatus(): boolean {
    return this.auth.hasPermission('Driver.ManageStatus');
  }

  get quickActions(): QuickAction[] {
    const actions: QuickAction[] = [];
    if (this.canCreateDriver) {
      actions.push({ id: 'add', label: 'Add Driver', icon: 'person_add', route: '/drivers/new', tone: 'green' });
    }
    if (this.canAssignDriver) {
      actions.push({ id: 'assign', label: 'Assign Vehicle', icon: 'directions_car', tone: 'blue' });
    }
    if (this.canUpdateDriver) {
      actions.push(
        { id: 'verify', label: 'Verify Driver', icon: 'verified_user', tone: 'orange' },
        { id: 'incident', label: 'Log Incident', icon: 'report_problem', tone: 'red' }
      );
    }
    if (this.canManageStatus) {
      actions.push({ id: 'suspend', label: 'Suspend Driver', icon: 'block', tone: 'red' });
    }
    return actions;
  }

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

    this.tenantContext.tenantId$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.reload$.next());
    this.tenantContext.isAllCompanies$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.reload$.next());

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
      const { list, stats, statsFailed, page } = result;
      this.rows.set(list.items);
      this.pagination.set({ ...page, total: list.totalCount });
      this.stats.set(stats);
      this.statsError.set(statsFailed ? 'Could not load driver stats for this scope.' : null);
      // Derive dispatch panel from stats immediately (same exclusive buckets).
      if (stats) {
        this.availabilitySummary.set({
          available: stats.available ?? 0,
          busy: stats.busy ?? 0,
          onTrip: stats.onTrip ?? 0,
          unavailable: stats.unavailable ?? 0
        });
      }
      this.loading.set(false);
      // Top Drivers / GPS ranking / compliance lists are non-critical — load after KPIs paint.
      this.deferredReload$.next();
    });

    this.deferredReload$.pipe(
      switchMap(() => this.fetchDeferredWidgets()),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(deferred => {
      if (!deferred) return;
      this.rankPool.set(deferred.rankPool.items);
      this.gpsRanking.set(deferred.gpsRanking);
      this.complianceDrivers.set(deferred.compliance.items);
    });

    this.load();
  }

  load(): void {
    this.reload$.next();
  }

  private listParams(overrides: Partial<DriverListParams> = {}): DriverListParams {
    const f = this.filters();
    const p = this.pagination();
    return {
      page: p.page,
      pageSize: p.pageSize,
      q: f.search.trim() || undefined,
      status: f.status === 'ALL' ? undefined : f.status,
      branchId: f.branchId === 'ALL' ? undefined : Number(f.branchId),
      licenseExpiry: f.licenseExpiry === 'ALL' ? undefined : f.licenseExpiry,
      verificationStatus: f.verificationStatus === 'ALL' ? undefined : f.verificationStatus,
      availability: f.availability === 'ALL' ? undefined : f.availability,
      ...overrides
    };
  }

  private fetchPage() {
    if (this.needsCompanyPick) {
      this.loading.set(false);
      this.error.set(null);
      this.rows.set([]);
      return of(null);
    }

    this.loading.set(true);
    this.error.set(null);
    const p = this.pagination();
    const branchId = this.listParams().branchId;

    // Critical path only: table page + Operational/Risk stats. Do not wait on GPS ranking or 500-row rank pool.
    return forkJoin({
      list: this.driverService.getAll(this.listParams()),
      statsResult: this.driverService.getStats(branchId).pipe(
        map(stats => ({ stats, failed: false as const })),
        catchError(() => of({ stats: null as DriverStats | null, failed: true as const }))
      )
    }).pipe(
      map(({ list, statsResult }) => ({
        list,
        stats: statsResult.stats,
        statsFailed: statsResult.failed,
        page: p
      })),
      catchError(err => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, 'Failed to load drivers.'));
        return EMPTY;
      })
    );
  }

  /** Secondary widgets: Top Drivers blend + compliance attention list. Failures are non-blocking. */
  private fetchDeferredWidgets() {
    const emptyPaged: PagedResult<DriverListItem> = { items: [], totalCount: 0, page: 1, pageSize: 5, totalPages: 0 };
    const branchId = this.listParams().branchId;

    return forkJoin({
      rankPool: this.driverService.getAll({ page: 1, pageSize: 500, branchId }).pipe(catchError(() => of(emptyPaged))),
      gpsRanking: this.gpsTracking.getDriverScoreRanking({ branchId }).pipe(catchError(() => of([] as DriverScore[]))),
      compliance: forkJoin({
        expiring: this.driverService.getAll({ licenseExpiry: 'EXPIRING', pageSize: 5, branchId }).pipe(catchError(() => of(emptyPaged))),
        expired: this.driverService.getAll({ licenseExpiry: 'EXPIRED', pageSize: 5, branchId }).pipe(catchError(() => of(emptyPaged)))
      }).pipe(
        map(({ expiring, expired }) => {
          const merged = [...expired.items, ...expiring.items]
            .sort((a, b) => (this.daysUntilExpiry(a.licenseExpiryDate) ?? 999) - (this.daysUntilExpiry(b.licenseExpiryDate) ?? 999))
            .slice(0, 5);
          return { ...emptyPaged, items: merged, totalCount: merged.length };
        }),
        catchError(() => of(emptyPaged))
      )
    }).pipe(
      catchError(() => EMPTY)
    );
  }

  /** Fetches the full filtered set (capped) for export / quick actions — not page-bound. */
  private fetchFilteredDrivers(max = 2000): Observable<DriverListItem[]> {
    const pageSize = Math.min(500, max);
    return this.driverService.getAll(this.listParams({ page: 1, pageSize })).pipe(
      switchMap(first => {
        const total = Math.min(first.totalCount, max);
        if (total <= first.items.length) {
          return of(first.items);
        }
        const pages = Math.ceil(total / pageSize);
        const requests = Array.from({ length: pages - 1 }, (_, i) =>
          this.driverService.getAll(this.listParams({ page: i + 2, pageSize })).pipe(
            map(r => r.items),
            catchError(() => of([] as DriverListItem[]))
          )
        );
        return forkJoin(requests).pipe(
          map(rest => [...first.items, ...rest.flat()].slice(0, max))
        );
      }),
      catchError(() => of([] as DriverListItem[]))
    );
  }

  statusValue(status: DriverStatus | 'ALL'): string {
    return String(status);
  }

  branchFilterValue(): string {
    const branchId = this.filters().branchId;
    return branchId === 'ALL' ? 'ALL' : String(branchId);
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

  clearSearch(): void {
    if (!this.filters().search) return;
    this.filters.update(f => ({ ...f, search: '' }));
    this.searchInput$.next('');
  }

  clearFilters(): void {
    if (!this.hasActiveFilters()) return;
    const hadSearch = !!this.filters().search.trim();
    this.filters.set({ ...EMPTY_DRIVER_FILTERS });
    this.pagination.update(p => ({ ...p, page: 1 }));
    // Prefer a single fetch: search debounce when search was cleared; otherwise reload now.
    if (hadSearch) {
      this.searchInput$.next('');
    } else {
      this.load();
    }
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

  onVerificationChange(value: string): void {
    this.filters.update(f => ({ ...f, verificationStatus: value === 'ALL' ? 'ALL' : value }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  onAvailabilityChange(value: string): void {
    this.filters.update(f => ({ ...f, availability: value as DriverFilters['availability'] }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  viewExpiredLicenses(): void {
    this.onLicenseFilterChange('EXPIRED');
    queueMicrotask(() => {
      this.tableSection?.nativeElement?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  onComplianceKeydown(event: KeyboardEvent, driver: DriverListItem): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openDrawer(driver);
    }
  }

  openDrawer(row: DriverListItem, preferredTab: 'performance' | 'vehicle' | null = null): void {
    this.selectedDriverId.set(row.id);
    this.drawerPreferredTab.set(preferredTab);
    this.drawerOpen.set(true);
  }

  onDrawerOpenChange(open: boolean): void {
    this.drawerOpen.set(open);
    if (!open) {
      this.selectedDriverId.set(null);
      this.drawerPreferredTab.set(null);
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
        this.toast.success('Driver deleted');
        if (this.selectedDriverId() === row.id) {
          this.onDrawerClosed();
        }
        const p = this.pagination();
        const nextTotal = Math.max(0, p.total - 1);
        const maxPage = Math.max(1, Math.ceil(nextTotal / p.pageSize) || 1);
        if (p.page > maxPage) {
          this.pagination.update(cur => ({ ...cur, page: maxPage, total: nextTotal }));
        }
        this.load();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Delete failed'))
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
    if (this.exporting) return;
    this.exporting = true;
    this.toast.info('Preparing export…');
    this.fetchFilteredDrivers().pipe(take(1)).subscribe({
      next: rows => {
        this.exporting = false;
        if (!rows.length) {
          this.toast.info('No drivers available to export');
          return;
        }

        const columns: ExportColumn<DriverListItem>[] = [
          { header: 'Name', accessor: row => row.fullName, excelWidth: 24, pdfWeight: 2.2 },
          { header: 'Phone', accessor: row => row.phone, excelWidth: 16, pdfWeight: 1.3 },
          { header: 'License #', accessor: row => row.licenseNumber, excelWidth: 16, pdfWeight: 1.3 },
          { header: 'License Expiry', accessor: row => row.licenseExpiryDate ? this.datePipe.transform(row.licenseExpiryDate, 'mediumDate') ?? '' : '', excelWidth: 16, pdfWeight: 1.2 },
          { header: 'Status', accessor: row => DriverStatusLabels[row.status] ?? 'Unknown', excelWidth: 14, pdfWeight: 1 },
          { header: 'Availability', accessor: row => row.availabilityBucket ?? '', excelWidth: 14, pdfWeight: 1 },
          { header: 'Branch', accessor: row => row.branchName ?? '', excelWidth: 18, pdfWeight: 1.2 },
          { header: 'Vehicle', accessor: row => row.assignedVehicleRegistration || row.assignedVehicleCode || row.assignedVehicleName || '', excelWidth: 16, pdfWeight: 1.2 },
          { header: 'Verified', accessor: row => row.verificationStatus, excelWidth: 16, pdfWeight: 1 }
        ];

        const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
        const filters = this.filters();
        const filterSummary = [
          filters.search?.trim() ? `Search: ${filters.search.trim()}` : null,
          filters.status === 'ALL' ? null : `Status: ${DriverStatusLabels[filters.status as DriverStatus] ?? filters.status}`,
          filters.branchId === 'ALL' ? null : `Branch: ${this.branchOptions().find(option => option.value === String(filters.branchId))?.label ?? filters.branchId}`,
          filters.licenseExpiry === 'ALL' ? null : `License: ${filters.licenseExpiry}`,
          filters.verificationStatus === 'ALL' ? null : `Verification: ${filters.verificationStatus}`,
          filters.availability === 'ALL' ? null : `Availability: ${filters.availability}`
        ].filter(Boolean).join(' · ');

        this.exportService.exportExcel(rows, columns, {
          filename: `drivers-report-${stamp}`,
          title: 'Drivers Report',
          subtitle: [
            `${rows.length} driver(s)`,
            filterSummary || 'All drivers'
          ].filter(Boolean).join(' · '),
          sheetName: 'Drivers'
        });

        this.toast.success('Drivers report downloaded');
      },
      error: () => {
        this.exporting = false;
        this.toast.error('Export failed');
      }
    });
  }

  importDrivers(): void {
    this.dialog.open(BulkDriverImportDialogComponent, { width: '800px' })
      .afterClosed()
      .subscribe(result => {
        if (result?.created) this.load();
      });
  }

  bulkAssign(): void {
    this.bulkAssignDialog?.show();
  }

  onIncidentLogged(result: LogIncidentResult): void {
    this.load();
    this.selectedDriverId.set(result.driverId);
    this.drawerPreferredTab.set('performance');
    this.drawerOpen.set(true);
    queueMicrotask(() => this.detailsDrawer?.load(result.driverId));
  }

  onQuickAction(actionId: string): void {
    switch (actionId) {
      case 'assign': {
        this.fetchFilteredDrivers(500).pipe(take(1)).subscribe(drivers => {
          const first = drivers.find(d =>
            !d.assignedVehicleId &&
            d.isActive !== false &&
            (d.verificationStatus ?? '').toLowerCase() === 'verified'
          );
          if (first) {
            this.openDrawer(first, 'vehicle');
            return;
          }
          this.bulkAssign();
        });
        break;
      }
      case 'verify': {
        this.fetchFilteredDrivers(500).pipe(take(1)).subscribe(drivers => {
          const pending = drivers.find(d => (d.verificationStatus ?? '').toLowerCase() !== 'verified');
          if (pending) {
            void this.router.navigate(['/drivers', pending.id, 'verify']);
            return;
          }
          this.toast.info('All matching drivers are verified');
        });
        break;
      }
      case 'suspend':
        void this.confirmAndSuspend();
        break;
      case 'incident': {
        if (!this.rows().length) {
          this.toast.warning('No drivers in current view');
          break;
        }
        const preselect = this.selectedDriverId() ?? this.rows()[0]?.id ?? null;
        this.logIncidentDialog?.show(preselect);
        break;
      }
    }
  }

  private async confirmAndSuspend(): Promise<void> {
    if (!this.canManageStatus) {
      this.toast.error('You do not have permission to change driver status');
      return;
    }

    this.fetchFilteredDrivers(500).pipe(take(1)).subscribe(async drivers => {
      const target = this.selectedDriverId()
        ? drivers.find(d => d.id === this.selectedDriverId() && d.status !== DriverStatus.Suspended)
        : drivers.find(d => d.status !== DriverStatus.Suspended && d.isActive !== false);

      if (!target) {
        this.toast.warning('Select a non-suspended driver from the table first');
        return;
      }

      const ok = await this.confirm.open({
        variant: 'delete',
        title: 'Suspend driver',
        message: `Suspend "${target.fullName}"? They will be unavailable for dispatch until reactivated.`,
        confirmText: 'Suspend',
        cancelText: 'Cancel'
      });
      if (!ok) return;

      this.driverService.changeStatus(target.id, DriverStatus.Suspended).pipe(take(1)).subscribe({
        next: () => {
          this.toast.warning(`${target.fullName} suspended`);
          this.load();
        },
        error: err => this.toast.error(apiErrorMessage(err, 'Suspend failed'))
      });
    });
  }

  topDriverScoreTone(score: number): 'success' | 'warning' | 'error' {
    return scoreTone(score);
  }

  viewCoverageMap(): void {
    void this.router.navigate(['/gps-tracking/live']);
  }

  openUnassignedVehicles(): void {
    void this.router.navigate(['/vehicles']);
  }
}
