import { ChangeDetectionStrategy, Component, computed, inject, model, signal, OnInit, DestroyRef, ViewChild } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { BulkDriverImportDialogComponent } from '../bulk-driver-import-dialog/bulk-driver-import-dialog.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, forkJoin, catchError, of, switchMap, debounceTime, distinctUntilChanged, EMPTY, map, take } from 'rxjs';
import { ChartData } from 'chart.js';
import { UiChartOptions } from '../../../shared/components/ui';
import { DriverService } from '../../../core/services/driver.service';
import { AuthService } from '../../../core/services/auth.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { PlatformService } from '../../../core/services/platform.service';
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
import { EMPTY_DRIVER_FILTERS, DriverFilters, DriverPagination, DEFAULT_DRIVER_PAGE_SIZE, buildDriverKpiGroups, buildOperationsSummary, buildAssignmentCoverage, buildComplianceAttention, computeDriverScore, scoreTone } from '../models/driver-inventory.model';

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

  private readonly driverService = inject(DriverService);
  private readonly auth = inject(AuthService);
  private readonly exportService = inject(ExportService);
  private readonly platformService = inject(PlatformService);
  private readonly confirm = inject(UiConfirmService);
  private readonly datePipe = inject(DatePipe);
  private readonly toast = inject(UiToastService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();
  private readonly searchInput$ = new Subject<string>();

  /** Performance chart uses fleet stats when available. */
  readonly chartIsSampleData = false;

  readonly rows = signal<DriverListItem[]>([]);
  /** Larger unfiltered sample used only for Top Drivers ranking (not the table page). */
  readonly rankPool = signal<DriverListItem[]>([]);
  readonly stats = signal<DriverStats | null>(null);
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

  readonly kpiGroups = computed(() => buildDriverKpiGroups(this.stats()));
  readonly operationsSummary = computed(() => buildOperationsSummary(this.stats()));
  readonly assignmentCoverage = computed(() => buildAssignmentCoverage(this.stats()));

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

  readonly topDrivers = computed(() =>
    this.rankPool()
      .map(d => ({ driver: d, score: computeDriverScore(d) }))
      .sort((a, b) => b.score - a.score)
      .slice(0, 5)
  );

  /** License compliance urgency buckets (not trip incidents). */
  readonly complianceSeverity = computed(() => {
    const s = this.stats();
    const expired = s?.licensesExpired ?? 0;
    const urgent7 = s?.licensesExpiringIn7Days ?? 0;
    const expiring30 = s?.licensesExpiringSoon ?? 0;
    const suspended = s?.suspended ?? 0;
    return {
      critical: expired,
      high: urgent7,
      medium: Math.max(0, expiring30 - urgent7),
      low: suspended
    };
  });

  readonly complianceAttention = computed(() => buildComplianceAttention(this.stats()));

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
          isLive: false,
          time: 'Compliance'
        };
      }
      if (d.assignedVehicleId && d.status === DriverStatus.Available) {
        return {
          id: d.id,
          eventLabel: 'Vehicle Assigned',
          eventDetail: `${d.fullName} · ${d.assignedVehicleRegistration || d.assignedVehicleCode || 'Vehicle linked'}`,
          icon: 'directions_car',
          iconClass: 'feed-icon--trip',
          isLive: false,
          time: 'Recently'
        };
      }
      const meta = {
        [DriverStatus.OnTrip]: { eventLabel: 'Trip Started', icon: 'trip_origin', iconClass: 'feed-icon--trip', isLive: true },
        [DriverStatus.Available]: { eventLabel: 'Driver Available', icon: 'check_circle', iconClass: 'feed-icon--available', isLive: false },
        [DriverStatus.OffDuty]: { eventLabel: 'Off Duty', icon: 'bedtime', iconClass: 'feed-icon--offduty', isLive: false },
        [DriverStatus.OnLeave]: { eventLabel: 'On Leave', icon: 'event_busy', iconClass: 'feed-icon--leave', isLive: false },
        [DriverStatus.Suspended]: { eventLabel: 'Driver Suspended', icon: 'block', iconClass: 'feed-icon--suspended', isLive: false }
      }[d.status] ?? { eventLabel: 'Status Update', icon: 'person', iconClass: 'feed-icon--default', isLive: false };

      return {
        id: d.id,
        eventLabel: meta.eventLabel,
        eventDetail: d.fullName,
        icon: meta.icon,
        iconClass: meta.iconClass,
        isLive: meta.isLive,
        time: meta.isLive ? 'Live now' : 'Recently'
      };
    })
  );

  readonly chartData = computed<ChartData>(() => {
    const s = this.stats();
    const labels = ['Available', 'Busy', 'On Trip', 'Unavailable'];
    const values = s
      ? [
          s.available ?? 0,
          s.busy ?? 0,
          s.onTrip ?? 0,
          Math.max(0, (s.totalDrivers ?? 0) - (s.available ?? 0) - (s.busy ?? 0) - (s.onTrip ?? 0))
        ]
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
    return this.auth.hasPermission('Driver.Create');
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
        { id: 'incident', label: 'Log Incident', icon: 'report_problem', tone: 'red' },
        { id: 'suspend', label: 'Suspend Driver', icon: 'block', tone: 'red' }
      );
    }
    actions.push({ id: 'export', label: 'Generate Report', icon: 'file_download', tone: 'neutral' });
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
      const { list, stats, compliance, availability, rankPool, page } = result;
      this.rows.set(list.items);
      this.rankPool.set(rankPool.items);
      this.pagination.set({ ...page, total: list.totalCount });
      this.stats.set(stats);
      this.availabilitySummary.set(availability);
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

    const branchId = f.branchId === 'ALL' ? undefined : Number(f.branchId);

    return forkJoin({
      list: this.driverService.getAll({
        page: p.page,
        pageSize: p.pageSize,
        q: f.search.trim() || undefined,
        status: f.status === 'ALL' ? undefined : f.status,
        branchId,
        licenseExpiry: f.licenseExpiry === 'ALL' ? undefined : f.licenseExpiry,
        verificationStatus: f.verificationStatus === 'ALL' ? undefined : f.verificationStatus,
        availability: f.availability === 'ALL' ? undefined : f.availability
      }),
      rankPool: this.driverService.getAll({ page: 1, pageSize: 100, branchId }).pipe(catchError(() => of(emptyPaged))),
      stats: this.driverService.getStats().pipe(catchError(() => of(null))),
      availability: this.driverService.getAvailabilitySummary(branchId).pipe(catchError(() => of(null))),
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
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
  }

  clearFilters(): void {
    if (!this.hasActiveFilters()) return;
    this.filters.set({ ...EMPTY_DRIVER_FILTERS });
    this.searchInput$.next('');
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
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

  onAvailabilityChange(value: string): void {
    this.filters.update(f => ({ ...f, availability: value as DriverFilters['availability'] }));
    this.pagination.update(p => ({ ...p, page: 1 }));
    this.load();
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
    const rows = this.rows();
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

  exportExcel(): void {
    this.exportReport();
  }

  onQuickAction(actionId: string): void {
    switch (actionId) {
      case 'assign': {
        const first = this.rows().find(d =>
          !d.assignedVehicleId &&
          d.isActive !== false &&
          (d.verificationStatus ?? '').toLowerCase() === 'verified'
        );
        if (first) {
          this.openDrawer(first, 'vehicle');
          return;
        }
        this.bulkAssign();
        break;
      }
      case 'verify': {
        const pending = this.rows().find(d => d.verificationStatus !== 'Verified');
        if (pending) {
          void this.router.navigate(['/drivers', pending.id, 'verify']);
          return;
        }
        this.toast.info('All drivers in the current view are verified');
        break;
      }
      case 'export':
        this.exportReport();
        break;
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
    const target = this.selectedDriverId()
      ? this.rows().find(d => d.id === this.selectedDriverId() && d.status !== DriverStatus.Suspended)
      : this.rows().find(d => d.status !== DriverStatus.Suspended && d.isActive !== false);

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
