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
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { DashboardSummary } from '../../../../core/models/common.model';
import { Booking } from '../../../../core/models/booking.model';
import { NotificationStats } from '../../../../core/models/notification.model';
import { GpsFleetStatusLocal } from '../../../../core/models/gps-tracking.model';
import {
  StatTile, TaskItem, DataTableColumn, QuickLaunchApp
} from '../../../../shared/ui';
import { BookingReport } from '../../../../core/models/common.model';
import { DEFAULT_DASHBOARD_FALLBACK } from '../../data/mock-dashboard-data';

type AssignedTask = TaskItem & { pickupKey?: string };

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
  private destroy$ = new Subject<void>();

  /** Summary KPI row (mirrors DashboardSummaryDto). */
  stats: StatTile[] = [];

  /** Today's to-do list (Pending / Confirmed needing attention). */
  todoTasks: TaskItem[] = [];

  /** Assignments list (filterable). */
  assignedTasks: AssignedTask[] = [];
  private allAssignedTasks: AssignedTask[] = [];
  assignedFilters = ['Today', 'Upcoming', 'All'];
  assignedFilter = 'Today';

  quickActions: QuickLaunchApp[] = [
    { id: 'new-booking', label: 'New booking', icon: 'add_circle', route: '/bookings/new', color: 'teal' },
    { id: 'live-map', label: 'Live map', icon: 'map', route: '/gps-tracking/live', color: 'blue' },
    { id: 'trips', label: 'Trips', icon: 'route', route: '/trips', color: 'sky' },
    { id: 'assign', label: 'Assignments', icon: 'assignment_ind', route: '/fleet/assignments', color: 'orange' },
    { id: 'fuel', label: 'Fuel logs', icon: 'local_gas_station', route: '/fuel-logs', color: 'amber' },
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
  transportKpis: { icon: string; label: string; value: string | number }[] = [];

  private loadGeneration = 0;

  constructor(
    private dashboard: DashboardService,
    private bookings: BookingService,
    private auth: AuthService,
    private notificationService: NotificationService,
    private gps: GpsTrackingService,
    private router: Router
  ) {
    this.unreadCount$ = this.notificationService.unreadCount;
  }

  ngOnInit(): void {
    this.userName = this.resolveDisplayName(this.auth.getCurrentUser()?.fullName);
    this.loadDashboardData(true);

    interval(60000).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadDashboardData(false);
    });
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
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: ({ summary, bookings, gps, notifStats }) => {
        if (gen !== this.loadGeneration) return;
        this.summaryFailed = !summary.ok;
        this.error = summary.ok
          ? null
          : 'Unable to load dashboard summary. Showing empty KPIs until the API recovers.';
        this.summary = summary.value;
        this.gpsFleet = gps;
        this.notifStats = notifStats;
        this.opsHealthy = summary.ok && (gps?.sos ?? 0) === 0;
        this.stats = this.buildStats(summary.value, bookings);
        this.recentBookings = bookings.slice(0, 8);
        this.todoTasks = this.bookingsToTasks(bookings, 5);
        this.allAssignedTasks = this.bookingsToAssignedTasks(bookings);
        this.applyAssignedFilter();
        this.fleetUtilizationPct = this.calcFleetUtilization(summary.value);
        this.transportKpis = this.buildTransportKpis(summary.value, gps);
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
        this.stats = this.buildStats(this.summary, []);
        this.transportKpis = this.buildTransportKpis(this.summary, null);
        this.applyChartData([], [], this.summary);
        this.loading = false;
      }
    });
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

  private buildStats(s: DashboardSummary, bookings: Booking[]): StatTile[] {
    const profitNegative = (s.netProfit ?? 0) < 0;
    const weekRev = this.sumWeekRevenue(bookings);
    const prevWeekRev = this.sumPreviousWeekRevenue(bookings);
    const revTrend = this.pctChangeLabel(weekRev, prevWeekRev);
    return [
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

  private buildTransportKpis(
    s: DashboardSummary,
    gps: GpsFleetStatusLocal | null
  ): { icon: string; label: string; value: string | number }[] {
    const available = Math.max(0, (s.totalVehicles || 0) - (s.activeTrips || 0));
    const kpis: { icon: string; label: string; value: string | number }[] = [
      { icon: 'local_shipping', label: 'Active trips', value: s.activeTrips },
      { icon: 'event_available', label: 'Vehicles available', value: available },
      { icon: 'schedule', label: 'Pending bookings', value: s.pendingBookings },
      { icon: 'speed', label: 'Fleet utilization', value: `${this.calcFleetUtilization(s)}%` },
      { icon: 'local_gas_station', label: 'Fuel spend', value: this.money(s.fuelExpense) }
    ];
    if (gps) {
      kpis.push({ icon: 'sensors', label: 'GPS online', value: `${gps.online}/${gps.totalVehicles}` });
    }
    return kpis;
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
        this.bookingChartLabels = ['Pending', 'Active', 'Completed'];
        this.bookingChartValues = [
          summary.pendingBookings || 0,
          summary.activeTrips || 0,
          0
        ];
      }
    }

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
    this.router.navigate(['/bookings', t.id]);
  }

  onAssignedFilter(f: string): void {
    this.assignedFilter = f;
    this.applyAssignedFilter();
  }

  onStatClick(key: string): void {
    const routes: Record<string, string> = {
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
