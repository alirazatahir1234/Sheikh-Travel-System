import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of, Observable, Subject, interval } from 'rxjs';
import { catchError, map, takeUntil } from 'rxjs/operators';

import { SharedModule } from '../../../../shared/shared.module';
import { DashboardAnalyticsComponent } from '../../dashboard-analytics/dashboard-analytics.component';

import { DashboardService } from '../../../../core/services/dashboard.service';
import { BookingService } from '../../../../core/services/booking.service';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { SmartAlertService } from '../../../../core/services/smart-alert.service';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { DashboardSummary } from '../../../../core/models/common.model';
import { Booking } from '../../../../core/models/booking.model';
import { NotificationStats } from '../../../../core/models/notification.model';
import { SmartAlert } from '../../../../core/models/smart-alert.model';
import { GpsFleetStatusLocal } from '../../../../core/models/gps-tracking.model';
import {
  StatTile, TaskItem, DataTableColumn, QuickLaunchApp
} from '../../../../shared/ui';
import { BookingReport } from '../../../../core/models/common.model';
import { DEFAULT_DASHBOARD_FALLBACK } from '../../data/mock-dashboard-data';
import { InvoiceService } from '../../../../core/services/invoice.service';
import { InvoiceBillingSummary } from '../../../../core/models/invoice.model';

type AssignedTask = TaskItem & { pickupKey?: string };

/** Grouped Smart Ops alerts by stable ruleType (not message text). */
export interface OpsAlertGroup {
  key: string;
  title: string;
  description: string;
  severity: string;
  alerts: SmartAlert[];
  affectedCount: number;
  affectedLabel: string;
  latestAt: string;
  newCount: number;
}

@Component({
  selector: 'app-default-dashboard',
  standalone: true,
  // Keep analytics in imports so @defer template type-check sees @Input bindings; Chart.js still lazy via @defer.
  imports: [SharedModule, DashboardAnalyticsComponent],
  templateUrl: './default-dashboard.component.html',
  styleUrls: ['./default-dashboard.component.scss']
})
export class DefaultDashboardComponent implements OnInit, OnDestroy {
  loading = true;
  error: string | null = null;
  summaryFailed = false;
  opsHealthy = true;

  userName = 'there';
  summary: DashboardSummary | null = null;

  unreadCount$: Observable<number>;
  notifStats: NotificationStats | null = null;
  gpsFleet: GpsFleetStatusLocal | null = null;
  smartAlerts: SmartAlert[] = [];
  smartAlertNewCount = 0;
  smartAlertBusyId: number | null = null;
  smartAlertActiveCount = 0;
  /** Collapsed Alert Center groups; expand only on user action. */
  opsAlertGroups: OpsAlertGroup[] = [];
  /** Severity filter for chips: null = all. */
  opsAlertSeverityFilter: 'Critical' | 'Warning' | 'Info' | null = null;
  /** Expanded group keys (ruleType). Empty by default. */
  expandedOpsAlertKeys = new Set<string>();
  /** When false, only first page of groups is shown. */
  opsAlertsShowAllGroups = false;
  /** View All lifts max-height and shows every group. */
  opsAlertsViewAll = false;
  readonly opsAlertGroupsPageSize = 5;
  readonly opsAlertRowsPageSize = 8;
  /** Per-group visible row count when expanded (Show more). */
  private opsAlertRowsVisible = new Map<string, number>();
  private destroy$ = new Subject<void>();

  /** Summary KPI rows (mirrors DashboardSummaryDto). */
  stats: StatTile[] = [];
  kpiRowPrimary: StatTile[] = [];
  kpiRowSecondary: StatTile[] = [];

  revenueWeekTrend = '—';
  revenueWeekTrendUp: boolean | undefined;
  bookingChartTotal = 0;

  gpsLoadFailed = false;
  notifLoadFailed = false;

  /** Today's to-do list (Pending / Confirmed needing attention). */
  todoTasks: TaskItem[] = [];

  /** Assignments list (filterable). */
  assignedTasks: AssignedTask[] = [];
  private allAssignedTasks: AssignedTask[] = [];
  assignedFilters = ['Today', 'Upcoming', 'All'];
  assignedFilter = 'Today';

  quickActions: QuickLaunchApp[] = [
    { id: 'new-booking', label: 'New Booking', icon: 'add_circle', route: '/bookings/new', color: 'teal' },
    { id: 'live-map', label: 'Live Map', icon: 'map', route: '/gps-tracking/live', color: 'blue' },
    { id: 'trips', label: 'Trips', icon: 'route', route: '/trips', color: 'sky' },
    { id: 'assign', label: 'Assignments', icon: 'assignment_ind', route: '/fleet/assignments', color: 'orange' },
    { id: 'fuel', label: 'Fuel Logs', icon: 'local_gas_station', route: '/fuel-logs', color: 'amber' },
    { id: 'reports', label: 'Reports', icon: 'assessment', route: '/reports', color: 'green' }
  ];

  /** Recent bookings table. */
  recentColumns: DataTableColumn<Booking>[] = [
    { key: 'bookingNumber', header: 'Booking #' },
    { key: 'customerName',  header: 'Customer' },
    { key: 'routeName',     header: 'Route' },
    { key: 'pickupTime',    header: 'Pickup', cell: r => this.formatDate(r.pickupTime) },
    { key: 'status', header: 'Status', badge: true },
    { key: 'totalAmount',   header: 'Amount', align: 'right',
      cell: r => 'PKR ' + (r.totalAmount || 0).toLocaleString() },
  ];
  recentBookings: Booking[] = [];

  revenueChartLabels: string[] = [];
  revenueChartValues: number[] = [];
  revenueWeekTotal = 0;
  bookingChartLabels: string[] = [];
  bookingChartValues: number[] = [];
  fleetChartLabels = ['Trips in progress', 'Vehicles available', 'Pending bookings'];
  fleetChartValues: number[] = [0, 0, 0];
  fleetUtilizationPct = 0;
  invoiceBilling: InvoiceBillingSummary | null = null;

  private loadGeneration = 0;

  constructor(
    private dashboard: DashboardService,
    private bookings: BookingService,
    private invoiceService: InvoiceService,
    private auth: AuthService,
    private notificationService: NotificationService,
    private smartAlertsApi: SmartAlertService,
    private gps: GpsTrackingService,
    private router: Router
  ) {
    this.unreadCount$ = this.notificationService.unreadCount;
  }

  ngOnInit(): void {
    this.userName = this.resolveDisplayName(this.auth.getCurrentUser()?.fullName);
    this.loadDashboardData(true);
    this.smartAlertsApi.refresh();

    this.smartAlertsApi.summary.pipe(takeUntil(this.destroy$)).subscribe(s => {
      this.smartAlerts = s.items ?? [];
      this.smartAlertNewCount = s.newCount ?? 0;
      this.smartAlertActiveCount = s.activeCount ?? this.smartAlerts.length;
      this.rebuildOpsAlertGroups();
      if (this.smartAlertNewCount > 0) this.opsHealthy = false;
    });

    interval(60000).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadDashboardData(false);
      this.smartAlertsApi.refresh();
    });
  }

  acknowledgeSmartAlert(alert: SmartAlert, event?: Event): void {
    event?.stopPropagation();
    this.smartAlertBusyId = alert.id;
    this.smartAlertsApi.acknowledge(alert.id).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.smartAlertBusyId = null;
    });
  }

  resolveSmartAlert(alert: SmartAlert, event?: Event): void {
    event?.stopPropagation();
    this.smartAlertBusyId = alert.id;
    this.smartAlertsApi.resolve(alert.id).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.smartAlertBusyId = null;
    });
  }

  severityClass(severity: string): string {
    const s = (severity || '').toLowerCase();
    if (s === 'critical') return 'sev-critical';
    if (s === 'info') return 'sev-info';
    return 'sev-warning';
  }

  get opsAlertSeverityCounts(): { critical: number; warning: number; info: number } {
    let critical = 0;
    let warning = 0;
    let info = 0;
    for (const a of this.smartAlerts) {
      const s = (a.severity || '').toLowerCase();
      if (s === 'critical') critical++;
      else if (s === 'info') info++;
      else warning++;
    }
    return { critical, warning, info };
  }

  get visibleOpsAlertGroups(): OpsAlertGroup[] {
    const filtered = this.opsAlertSeverityFilter
      ? this.opsAlertGroups.filter(g =>
          g.alerts.some(a => (a.severity || '').toLowerCase() === this.opsAlertSeverityFilter!.toLowerCase()))
      : this.opsAlertGroups;

    if (this.opsAlertsViewAll || this.opsAlertsShowAllGroups) return filtered;
    return filtered.slice(0, this.opsAlertGroupsPageSize);
  }

  get hasMoreOpsAlertGroups(): boolean {
    if (this.opsAlertsViewAll || this.opsAlertsShowAllGroups) return false;
    const filtered = this.opsAlertSeverityFilter
      ? this.opsAlertGroups.filter(g =>
          g.alerts.some(a => (a.severity || '').toLowerCase() === this.opsAlertSeverityFilter!.toLowerCase()))
      : this.opsAlertGroups;
    return filtered.length > this.opsAlertGroupsPageSize;
  }

  get hiddenOpsAlertGroupCount(): number {
    const filtered = this.opsAlertSeverityFilter
      ? this.opsAlertGroups.filter(g =>
          g.alerts.some(a => (a.severity || '').toLowerCase() === this.opsAlertSeverityFilter!.toLowerCase()))
      : this.opsAlertGroups;
    return Math.max(0, filtered.length - this.opsAlertGroupsPageSize);
  }

  setOpsAlertSeverityFilter(sev: 'Critical' | 'Warning' | 'Info' | null): void {
    this.opsAlertSeverityFilter = this.opsAlertSeverityFilter === sev ? null : sev;
  }

  toggleOpsAlertGroup(key: string, event?: Event): void {
    event?.stopPropagation();
    const next = new Set(this.expandedOpsAlertKeys);
    if (next.has(key)) next.delete(key);
    else {
      next.add(key);
      if (!this.opsAlertRowsVisible.has(key)) {
        this.opsAlertRowsVisible.set(key, this.opsAlertRowsPageSize);
      }
    }
    this.expandedOpsAlertKeys = next;
  }

  isOpsAlertGroupExpanded(key: string): boolean {
    return this.expandedOpsAlertKeys.has(key);
  }

  visibleRowsForGroup(group: OpsAlertGroup): SmartAlert[] {
    if (this.opsAlertSeverityFilter) {
      const sev = this.opsAlertSeverityFilter.toLowerCase();
      const filtered = group.alerts.filter(a => (a.severity || '').toLowerCase() === sev);
      const limit = this.opsAlertRowsVisible.get(group.key) ?? this.opsAlertRowsPageSize;
      return filtered.slice(0, limit);
    }
    const limit = this.opsAlertRowsVisible.get(group.key) ?? this.opsAlertRowsPageSize;
    return group.alerts.slice(0, limit);
  }

  hasMoreRowsForGroup(group: OpsAlertGroup): boolean {
    const total = this.opsAlertSeverityFilter
      ? group.alerts.filter(a => (a.severity || '').toLowerCase() === this.opsAlertSeverityFilter!.toLowerCase()).length
      : group.alerts.length;
    const shown = this.opsAlertRowsVisible.get(group.key) ?? this.opsAlertRowsPageSize;
    return total > shown;
  }

  showMoreRowsForGroup(group: OpsAlertGroup, event?: Event): void {
    event?.stopPropagation();
    const current = this.opsAlertRowsVisible.get(group.key) ?? this.opsAlertRowsPageSize;
    this.opsAlertRowsVisible.set(group.key, current + this.opsAlertRowsPageSize);
  }

  showMoreOpsAlertGroups(): void {
    this.opsAlertsShowAllGroups = true;
  }

  /** View All: expand card height and reveal every group (still collapsed by default). */
  toggleOpsAlertsViewAll(): void {
    this.opsAlertsViewAll = !this.opsAlertsViewAll;
    if (this.opsAlertsViewAll) this.opsAlertsShowAllGroups = true;
  }

  ruleTypeTitle(ruleType: string): string {
    const map: Record<string, string> = {
      MaintenanceDue: 'Maintenance Due',
      DocumentExpiry: 'Document Expiry',
      UnassignedTrip: 'Unassigned Trip',
      OverdueInvoice: 'Overdue Invoice',
      FuelAnomaly: 'Fuel Anomaly',
      AccidentRisk: 'Accident Risk',
      GpsOffline: 'GPS Offline'
    };
    if (map[ruleType]) return map[ruleType];
    return (ruleType || 'Alert').replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  alertEntityLabel(alert: SmartAlert): string {
    const type = (alert.relatedEntityType || '').trim();
    const id = alert.relatedEntityId;
    const msg = alert.message || '';

    // Prefer name embedded in scanner messages: "... for {label} ..."
    const forMatch = msg.match(/\bfor\s+(.+?)\s+(?:is|expires|expired|due)\b/i);
    if (forMatch?.[1]) return forMatch[1].trim();

    if (type && id != null) return `${type} #${id}`;
    if (type) return type;
    if (id != null) return `#${id}`;
    return '—';
  }

  formatAlertTime(iso: string): string {
    return this.formatDate(iso);
  }

  trackOpsAlertGroup(_: number, g: OpsAlertGroup): string {
    return g.key;
  }

  trackSmartAlert(_: number, a: SmartAlert): number {
    return a.id;
  }

  private rebuildOpsAlertGroups(): void {
    const byRule = new Map<string, SmartAlert[]>();
    const seenIds = new Set<number>();

    for (const alert of this.smartAlerts) {
      if (seenIds.has(alert.id)) continue;
      seenIds.add(alert.id);
      const key = (alert.ruleType || 'Unknown').trim() || 'Unknown';
      const list = byRule.get(key) ?? [];
      list.push(alert);
      byRule.set(key, list);
    }

    const groups: OpsAlertGroup[] = [];
    for (const [key, alerts] of byRule) {
      alerts.sort((a, b) => (b.createdAt || '').localeCompare(a.createdAt || ''));
      const severity = this.highestSeverity(alerts);
      const { count, label } = this.affectedSummary(alerts);
      const latest = alerts[0]?.createdAt || '';
      groups.push({
        key,
        title: this.ruleTypeTitle(key),
        description: this.groupDescription(key, count, label),
        severity,
        alerts,
        affectedCount: count,
        affectedLabel: label,
        latestAt: latest,
        newCount: alerts.filter(a => a.status === 'New').length
      });
    }

    // Critical first, then by latest activity
    const sevRank = (s: string) => {
      const x = (s || '').toLowerCase();
      if (x === 'critical') return 0;
      if (x === 'warning') return 1;
      return 2;
    };
    groups.sort((a, b) => {
      const d = sevRank(a.severity) - sevRank(b.severity);
      if (d !== 0) return d;
      return (b.latestAt || '').localeCompare(a.latestAt || '');
    });

    this.opsAlertGroups = groups;

    // Drop expansions for rule types no longer present
    const valid = new Set(groups.map(g => g.key));
    this.expandedOpsAlertKeys = new Set([...this.expandedOpsAlertKeys].filter(k => valid.has(k)));
  }

  private highestSeverity(alerts: SmartAlert[]): string {
    let hasWarning = false;
    let hasInfo = false;
    for (const a of alerts) {
      const s = (a.severity || '').toLowerCase();
      if (s === 'critical') return 'Critical';
      if (s === 'warning') hasWarning = true;
      else if (s === 'info') hasInfo = true;
      else hasWarning = true;
    }
    if (hasWarning) return 'Warning';
    if (hasInfo) return 'Info';
    return 'Warning';
  }

  private affectedSummary(alerts: SmartAlert[]): { count: number; label: string } {
    const byType = new Map<string, Set<string>>();
    for (const a of alerts) {
      const type = (a.relatedEntityType || 'Item').trim() || 'Item';
      const idKey = a.relatedEntityId != null ? String(a.relatedEntityId) : `alert:${a.id}`;
      if (!byType.has(type)) byType.set(type, new Set());
      byType.get(type)!.add(idKey);
    }

    if (byType.size === 1) {
      const [type, ids] = [...byType.entries()][0];
      const count = ids.size;
      const label = this.pluralEntityLabel(type, count);
      return { count, label };
    }

    // Mixed entities: prefer Vehicle count in label when present
    const vehicleIds = byType.get('Vehicle');
    if (vehicleIds?.size) {
      return { count: vehicleIds.size, label: this.pluralEntityLabel('Vehicle', vehicleIds.size) };
    }

    const total = [...byType.values()].reduce((n, s) => n + s.size, 0);
    return { count: total, label: total === 1 ? 'item' : 'items' };
  }

  private pluralEntityLabel(type: string, count: number): string {
    const t = type.toLowerCase();
    if (t === 'vehicle') return count === 1 ? 'vehicle' : 'vehicles';
    if (t === 'driver') return count === 1 ? 'driver' : 'drivers';
    if (t === 'booking' || t === 'trip') return count === 1 ? 'trip' : 'trips';
    if (t === 'invoice') return count === 1 ? 'invoice' : 'invoices';
    return count === 1 ? 'item' : 'items';
  }

  private groupDescription(ruleType: string, count: number, label: string): string {
    const titles: Record<string, string> = {
      MaintenanceDue: 'Scheduled maintenance due or overdue',
      DocumentExpiry: 'Compliance documents nearing expiry',
      UnassignedTrip: 'Confirmed trips missing driver or vehicle',
      OverdueInvoice: 'Invoices past due date',
      FuelAnomaly: 'Unusual fuel consumption detected',
      AccidentRisk: 'Elevated accident risk signals',
      GpsOffline: 'GPS devices offline'
    };
    const base = titles[ruleType] || 'Active operations alerts for this rule';
    return `${base} · ${count} ${label}`;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadDashboardData(isInitial = false): void {
    const gen = ++this.loadGeneration;
    if (isInitial) this.loading = true;

    forkJoin({
      summary: this.dashboard.getSummary().pipe(
        map(s => ({ ok: true as const, value: s })),
        catchError(() => of({ ok: false as const, value: this.fallbackSummary() }))
      ),
      bookings: this.bookings.getAll(1, 50).pipe(
        map(p => p?.items ?? []),
        catchError(() => of<Booking[]>([]))
      ),
      gps: this.gps.getFleetStatusLocal().pipe(
        catchError(() => of<GpsFleetStatusLocal | null>(null))
      ),
      notifStats: this.notificationService.getStats().pipe(
        catchError(() => of<NotificationStats | null>(null))
      ),
      invoiceBilling: this.auth.hasPermission('Invoice.View')
        ? this.invoiceService.getBillingSummary().pipe(catchError(() => of<InvoiceBillingSummary | null>(null)))
        : of<InvoiceBillingSummary | null>(null),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: ({ summary, bookings, gps, notifStats, invoiceBilling }) => {
        if (gen !== this.loadGeneration) return;
        this.summaryFailed = !summary.ok;
        this.error = summary.ok
          ? null
          : 'Unable to load dashboard summary. Showing empty KPIs until the API recovers.';
        this.summary = summary.value;
        this.gpsFleet = gps;
        this.notifStats = notifStats;
        this.invoiceBilling = invoiceBilling;
        this.opsHealthy = summary.ok && (gps?.sos ?? 0) === 0;
        this.stats = this.buildStats(summary.value, bookings, invoiceBilling);
        this.splitKpiRows(this.stats);
        this.recentBookings = bookings.slice(0, 8);
        this.todoTasks = this.mergeTodoTasks(
          this.buildOperationalTodos(summary.value, invoiceBilling),
          this.bookingsToTasks(bookings, 5)
        );
        this.allAssignedTasks = this.bookingsToAssignedTasks(bookings);
        this.applyAssignedFilter();
        this.fleetUtilizationPct = this.calcFleetUtilization(summary.value);
        this.gpsLoadFailed = gps == null;
        this.notifLoadFailed = notifStats == null;
        this.applyChartData(bookings, [], summary.value);
        this.loading = false;

        this.loadDeferredExtras(gen, bookings, summary.value);
      },
      error: () => {
        if (gen !== this.loadGeneration) return;
        this.summaryFailed = true;
        this.error = 'Unable to load dashboard data.';
        this.opsHealthy = false;
        this.summary = this.fallbackSummary();
        this.stats = this.buildStats(this.summary, [], null);
        this.splitKpiRows(this.stats);
        this.gpsLoadFailed = true;
        this.notifLoadFailed = true;
        this.applyChartData([], [], this.summary);
        this.loading = false;
      }
    });
  }

  retryDashboard(): void {
    this.loadDashboardData(true);
  }

  /** Non-blocking: booking status report + notification stats. */
  private loadDeferredExtras(gen: number, bookings: Booking[], summary: DashboardSummary): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 30);

    this.dashboard.getBookingStatusReport(
      this.localDateKey(from),
      this.localDateKey(to)
    ).pipe(
      catchError(() => of<BookingReport[]>([])),
      takeUntil(this.destroy$)
    ).subscribe(bookingReport => {
      if (gen !== this.loadGeneration) return;
      this.applyChartData(bookings, bookingReport, summary);
    });

  }

  // ---------- Helpers ----------

  private resolveDisplayName(fullName?: string | null): string {
    const parts = (fullName || '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return 'there';
    const generic = new Set(['system', 'admin', 'user', 'test', 'demo']);
    const preferred = parts.find(p => !generic.has(p.toLowerCase()));
    if (preferred) return preferred;
    return parts.length > 1 ? parts[parts.length - 1] : parts[0];
  }

  private buildStats(
    s: DashboardSummary,
    bookings: Booking[],
    billing: InvoiceBillingSummary | null
  ): StatTile[] {
    const profitNegative = (s.netProfit ?? 0) < 0;
    const weekRev = this.sumWeekRevenue(bookings);
    const prevWeekRev = this.sumPreviousWeekRevenue(bookings);
    const revTrend = this.pctChangeLabel(weekRev, prevWeekRev);
    const billingTiles: StatTile[] = billing
      ? [
          {
            key: 'invoiceBilled',
            label: 'Client billed',
            value: this.money(billing.totalBilled),
            icon: 'receipt_long',
            color: 'blue',
            hint: 'Trip invoices issued',
            trend: `${billing.paidCount} paid`,
            trendUp: billing.paidCount > 0,
            trendDetail: 'invoices'
          },
          {
            key: 'invoiceCollected',
            label: 'Collected',
            value: this.money(billing.totalCollected),
            icon: 'account_balance',
            color: 'teal',
            hint: 'Payments applied to invoices',
            trend: billing.pendingCount > 0 ? `${billing.pendingCount} pending` : 'Clear',
            trendUp: billing.pendingCount === 0,
            trendDetail: 'open'
          },
          {
            key: 'invoiceOutstanding',
            label: 'Outstanding AR',
            value: this.money(billing.outstanding),
            icon: 'warning',
            color: 'orange',
            variant: billing.outstanding > 0 ? 'warning' : 'default',
            hint: 'Unpaid invoice balance',
            trend: billing.overdueCount > 0 ? `${billing.overdueCount} overdue` : 'On track',
            trendUp: billing.overdueCount === 0,
            trendDetail: 'follow up'
          }
        ]
      : [];

    return [
      ...billingTiles,
      {
        key: 'activeTrips',
        label: 'Active Trips',
        value: s.activeTrips,
        icon: 'local_shipping',
        color: 'teal',
        hint: 'Bookings currently in progress',
        trend: s.activeTrips > 0 ? 'Live' : 'Idle',
        trendUp: s.activeTrips > 0,
        trendDetail: 'right now',
        sparkline: this.sparkFromRevenue(bookings).every(v => v === 0)
          ? undefined
          : this.sparkFromCount(s.activeTrips)
      },
      {
        key: 'pendingBookings',
        label: 'Pending Bookings',
        value: s.pendingBookings,
        icon: 'pending_actions',
        color: 'teal',
        hint: 'Awaiting confirmation',
        variant: s.pendingBookings > 3 ? 'warning' : 'default',
        trend: s.pendingBookings > 0 ? `${s.pendingBookings} open` : 'Clear',
        trendUp: false,
        trendDetail: 'needs action'
      },
      {
        key: 'totalVehicles',
        label: 'Fleet Vehicles',
        value: s.totalVehicles,
        icon: 'directions_bus',
        color: 'blue',
        hint: 'Active fleet (excludes drafts)',
        trend: `${this.calcFleetUtilization(s)}% used`,
        trendUp: this.calcFleetUtilization(s) > 0,
        trendDetail: 'utilization'
      },
      {
        key: 'totalRevenue',
        label: 'Total Revenue',
        value: this.money(s.totalRevenue),
        icon: 'payments',
        color: 'teal',
        trend: revTrend.label,
        trendUp: revTrend.up,
        trendDetail: 'vs prior week',
        sparkline: this.sparkFromRevenue(bookings)
      },
      {
        key: 'fuelExpense',
        label: 'Fuel Spend',
        value: this.money(s.fuelExpense),
        icon: 'local_gas_station',
        color: 'blue',
        hint: 'All-time fuel cost',
        trend: s.fuelExpense > 0 ? 'Tracked' : '—',
        trendDetail: 'lifetime'
      },
      {
        key: 'netProfit',
        label: 'Net Profit',
        value: this.money(s.netProfit),
        icon: profitNegative ? 'warning' : 'trending_up',
        color: 'teal',
        variant: profitNegative ? 'danger' : 'success',
        trend: profitNegative ? 'Loss' : 'Profit',
        trendUp: !profitNegative,
        trendDetail: 'revenue − fuel − maintenance'
      }
    ];
  }

  private splitKpiRows(all: StatTile[]): void {
    const secondaryKeys = new Set(['totalRevenue', 'fuelExpense', 'netProfit']);
    this.kpiRowPrimary = all.filter(s => !secondaryKeys.has(s.key));
    this.kpiRowSecondary = all.filter(s => secondaryKeys.has(s.key));
  }

  /** Action items derived from summary counts — not fabricated business rows. */
  private buildOperationalTodos(s: DashboardSummary, billing: InvoiceBillingSummary | null): TaskItem[] {
    const items: TaskItem[] = [];
    if (s.pendingBookings > 0) {
      items.push({
        id: -1,
        title: `Confirm pending bookings (${s.pendingBookings})`,
        subtitle: 'Bookings awaiting confirmation',
        priority: 'high',
        done: false,
        status: 'Pending'
      });
    }
    if (billing && billing.outstanding > 0) {
      items.push({
        id: -2,
        title: 'Review outstanding AR',
        subtitle: `${this.money(billing.outstanding)} unpaid · ${billing.overdueCount} overdue`,
        priority: billing.overdueCount > 0 ? 'high' : 'medium',
        done: false,
        status: 'AR'
      });
    }
    if (s.pendingBookings > 0 && s.totalVehicles > 0) {
      items.push({
        id: -3,
        title: 'Assign vehicles to pending trips',
        subtitle: `${s.pendingBookings} booking(s) may need fleet assignment`,
        priority: 'medium',
        done: false,
        status: 'Assignment'
      });
    }
    return items;
  }

  private mergeTodoTasks(primary: TaskItem[], fromBookings: TaskItem[]): TaskItem[] {
    const seen = new Set<string>();
    const out: TaskItem[] = [];
    for (const t of [...primary, ...fromBookings]) {
      const key = t.title;
      if (seen.has(key)) continue;
      seen.add(key);
      out.push(t);
      if (out.length >= 8) break;
    }
    return out;
  }

  private sparkFromCount(n: number): number[] | undefined {
    if (!n) return undefined;
    const base = Math.max(1, n);
    return [base * 0.6, base * 0.75, base * 0.9, base, base * 0.95, base * 1.05, base];
  }

  private sparkFromRevenue(bookings: Booking[]): number[] {
    const days = this.buildWeekBuckets(bookings);
    const vals = days.map(d => d.total);
    return vals.some(v => v > 0) ? vals : [];
  }

  private sumWeekRevenue(bookings: Booking[]): number {
    return this.buildWeekBuckets(bookings).reduce((a, d) => a + d.total, 0);
  }

  private sumPreviousWeekRevenue(bookings: Booking[]): number {
    const start = new Date();
    start.setHours(0, 0, 0, 0);
    start.setDate(start.getDate() - 13);
    const end = new Date();
    end.setHours(0, 0, 0, 0);
    end.setDate(end.getDate() - 7);
    let total = 0;
    for (const b of bookings) {
      if (!b.pickupTime) continue;
      const d = this.parseLocalDate(b.pickupTime);
      if (!d) continue;
      if (d >= start && d < end) total += b.totalAmount || 0;
    }
    return total;
  }

  private pctChangeLabel(current: number, previous: number): { label: string; up?: boolean } {
    if (previous <= 0 && current <= 0) return { label: '—' };
    if (previous <= 0) return { label: 'New', up: true };
    const pct = Math.round(((current - previous) / previous) * 1000) / 10;
    const sign = pct > 0 ? '+' : '';
    return { label: `${sign}${pct}%`, up: pct >= 0 };
  }

  private localDateKey(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private parseLocalDate(iso: string): Date | null {
    const key = iso.slice(0, 10);
    const parts = key.split('-').map(Number);
    if (parts.length !== 3 || parts.some(n => !Number.isFinite(n))) return null;
    return new Date(parts[0], parts[1] - 1, parts[2]);
  }

  private buildWeekBuckets(bookings: Booking[]): { label: string; key: string; total: number }[] {
    const days: { label: string; key: string; total: number }[] = [];
    for (let i = 6; i >= 0; i--) {
      const d = new Date();
      d.setHours(0, 0, 0, 0);
      d.setDate(d.getDate() - i);
      days.push({
        key: this.localDateKey(d),
        label: d.toLocaleDateString(undefined, { weekday: 'short' }),
        total: 0
      });
    }
    for (const b of bookings) {
      if (!b.pickupTime) continue;
      const key = b.pickupTime.slice(0, 10);
      const bucket = days.find(x => x.key === key);
      if (bucket) bucket.total += b.totalAmount || 0;
    }
    return days;
  }

  private calcFleetUtilization(s: DashboardSummary): number {
    if (!s.totalVehicles) return 0;
    return Math.min(100, Math.round((s.activeTrips / s.totalVehicles) * 100));
  }

  private applyChartData(bookings: Booking[], bookingReport: BookingReport[], summary: DashboardSummary): void {
    const days = this.buildWeekBuckets(bookings);
    this.revenueChartLabels = days.map(d => d.label);
    this.revenueChartValues = days.map(d => d.total);
    this.revenueWeekTotal = days.reduce((a, d) => a + d.total, 0);

    const report = bookingReport.filter(r => r.count > 0);
    if (report.length) {
      this.bookingChartLabels = report.map(r => r.status);
      this.bookingChartValues = report.map(r => r.count);
    } else {
      const byStatus = new Map<string, number>();
      for (const b of bookings) {
        const st = b.status || 'Unknown';
        byStatus.set(st, (byStatus.get(st) || 0) + 1);
      }
      if (byStatus.size) {
        this.bookingChartLabels = [...byStatus.keys()];
        this.bookingChartValues = [...byStatus.values()];
      } else {
        this.bookingChartLabels = [];
        this.bookingChartValues = [];
      }
    }
    this.bookingChartTotal = this.bookingChartValues.reduce((a, n) => a + n, 0);

    const weekRev = this.revenueWeekTotal;
    const prevWeekRev = this.sumPreviousWeekRevenue(bookings);
    const revTrend = this.pctChangeLabel(weekRev, prevWeekRev);
    this.revenueWeekTrend = revTrend.label;
    this.revenueWeekTrendUp = revTrend.up;

    const inUse = summary.activeTrips || 0;
    const pending = summary.pendingBookings || 0;
    const available = Math.max(0, (summary.totalVehicles || 0) - inUse);
    this.fleetChartValues = [inUse, available, pending];
  }

  private bookingsToTasks(list: Booking[], take: number): TaskItem[] {
    const todayKey = this.localDateKey(new Date());
    const actionable = list
      .filter(b => b.status === 'Pending' || b.status === 'Confirmed')
      .sort((a, b) => (a.pickupTime || '').localeCompare(b.pickupTime || ''));

    const todayFirst = [
      ...actionable.filter(b => (b.pickupTime || '').slice(0, 10) === todayKey),
      ...actionable.filter(b => (b.pickupTime || '').slice(0, 10) !== todayKey)
    ];

    return todayFirst.slice(0, take).map(b => ({
      id: b.id,
      title: b.routeName || 'Booking',
      subtitle: `${b.bookingNumber || '—'} · Pickup ${this.formatDate(b.pickupTime)} · ${b.customerName || 'Customer'}`,
      priority: (b.pickupTime || '').slice(0, 10) === todayKey || b.status === 'Pending' ? 'high' as const : 'medium' as const,
      done: false,
      status: b.status
    }));
  }

  private bookingsToAssignedTasks(list: Booking[]): AssignedTask[] {
    return list
      .filter(b => !!b.driverName)
      .map(b => ({
        id: b.id,
        title: (b.bookingNumber || 'Booking') + ' — ' + (b.routeName || ''),
        subtitle: 'Pickup ' + this.formatDate(b.pickupTime),
        meta: b.driverName,
        avatarInitials: this.initials(b.driverName),
        done: b.status === 'Completed',
        status: b.status,
        priority: this.priorityFromStatus(b.status),
        pickupKey: (b.pickupTime || '').slice(0, 10)
      }));
  }

  private applyAssignedFilter(): void {
    const todayKey = this.localDateKey(new Date());
    let filtered = this.allAssignedTasks;
    if (this.assignedFilter === 'Today') {
      // Today pickups + currently running trips only (not old Confirmed rows).
      filtered = this.allAssignedTasks.filter(
        t => t.pickupKey === todayKey || this.isInProgressStatus(t.status)
      );
    } else if (this.assignedFilter === 'Upcoming') {
      filtered = this.allAssignedTasks.filter(
        t => !!t.pickupKey && t.pickupKey >= todayKey && !t.done && !this.isInProgressStatus(t.status)
      );
    }
    this.assignedTasks = filtered.slice(0, 8);
  }

  private isInProgressStatus(status?: string): boolean {
    const s = (status || '').toLowerCase();
    return s === 'started' || s.includes('progress') || s.includes('enroute') || s.includes('pickup');
  }

  private priorityFromStatus(status?: string): 'high' | 'medium' | 'low' | undefined {
    const s = (status || '').toLowerCase();
    if (s.includes('pending') || s.includes('cancel')) return 'high';
    if (s.includes('active') || s.includes('start') || s.includes('confirm')) return 'medium';
    if (s.includes('complete')) return 'low';
    return undefined;
  }

  private fallbackSummary(): DashboardSummary {
    return { ...DEFAULT_DASHBOARD_FALLBACK };
  }

  private money(v: number): string {
    return 'PKR ' + (v || 0).toLocaleString();
  }

  private formatDate(iso: string): string {
    try {
      const d = new Date(iso);
      return d.toLocaleString(undefined, { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    } catch { return iso; }
  }

  private initials(name?: string): string {
    return (name || '').split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  }

  onTaskToggle(t: TaskItem): void {
    if (typeof t.id === 'number' && t.id > 0) {
      this.router.navigate(['/bookings', t.id]);
      return;
    }
    if (t.status === 'AR') {
      this.router.navigate(['/invoices']);
      return;
    }
    if (t.status === 'Assignment') {
      this.router.navigate(['/fleet/assignments']);
      return;
    }
    this.router.navigate(['/bookings'], { queryParams: { status: 'Pending' } });
  }

  onAssignedFilter(f: string): void {
    this.assignedFilter = f;
    this.applyAssignedFilter();
  }

  onStatClick(key: string): void {
    const routes: Record<string, string> = {
      invoiceBilled: '/invoices',
      invoiceCollected: '/payments',
      invoiceOutstanding: '/invoices',
      activeTrips: '/trips',
      pendingBookings: '/bookings',
      totalVehicles: '/vehicles',
      totalRevenue: '/reports',
      fuelExpense: '/fuel-logs',
      netProfit: '/reports'
    };
    const route = routes[key];
    if (route) this.router.navigate([route]);
  }

  onQuickAction(app: QuickLaunchApp): void {
    if (app.route) this.router.navigateByUrl(app.route);
  }

  onGpsStripClick(): void {
    this.router.navigate(['/gps-tracking/live']);
  }

  onRecentBookingClick(booking: Booking): void {
    this.router.navigate(['/bookings', booking.id]);
  }

  onViewBooking(booking: Booking): void {
    this.router.navigate(['/bookings', booking.id]);
  }

  onEditBooking(booking: Booking): void {
    this.router.navigate(['/bookings', booking.id, 'edit']);
  }

  onAssignBooking(booking: Booking): void {
    this.router.navigate(['/bookings', booking.id], { queryParams: { tab: 'assign' } });
  }

  onViewAllBookings(): void {
    this.router.navigate(['/bookings']);
  }
}
