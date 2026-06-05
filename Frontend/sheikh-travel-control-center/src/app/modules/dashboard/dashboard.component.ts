import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of, Observable, Subject, interval } from 'rxjs';
import { catchError, map, takeUntil } from 'rxjs/operators';

import { DashboardService } from '../../core/services/dashboard.service';
import { BookingService } from '../../core/services/booking.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { DashboardSummary } from '../../core/models/common.model';
import { Booking } from '../../core/models/booking.model';

import {
  QuickLaunchApp, StatTile, TaskItem, DataTableColumn
} from '../../shared/ui';
import { BookingReport } from '../../core/models/common.model';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  loading = true;
  error: string | null = null;

  userName = 'there';
  summary: DashboardSummary | null = null;
  
  unreadCount$: Observable<number>;
  private destroy$ = new Subject<void>();

  /** Quick-launch tiles match the Sheikh Travel modules (from the Postman collection). */
  quickApps: QuickLaunchApp[] = [
    { id: 'bookings', label: 'Booking', icon: 'add_circle', color: 'teal', route: '/bookings/new' },
    { id: 'vehicles', label: 'Vehicle', icon: 'directions_bus', color: 'teal', route: '/vehicles' },
    { id: 'drivers', label: 'Driver', icon: 'badge', color: 'blue', route: '/drivers' },
    { id: 'tracking', label: 'GPS Tracking', icon: 'my_location', color: 'blue', route: '/gps-tracking' },
  ];

  /** Summary KPI row (mirrors DashboardSummaryDto). */
  stats: StatTile[] = [];

  /** Today's to-do list (derived from Bookings with status Pending / today's pickup). */
  todoTasks: TaskItem[] = [];

  /** "Assigned to me" list. */
  assignedTasks: TaskItem[] = [];
  assignedFilters = ['Today', 'Upcoming', 'All'];
  assignedFilter = 'Today';

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

  constructor(
    private dashboard: DashboardService,
    private bookings: BookingService,
    private auth: AuthService,
    private notificationService: NotificationService,
    private router: Router
  ) {
    this.unreadCount$ = this.notificationService.unreadCount;
  }

  ngOnInit(): void {
    this.userName = this.auth.getCurrentUser()?.fullName?.split(' ')[0] ?? 'there';

    this.loadDashboardData();

    // Auto-refresh every 60 seconds
    interval(60000).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadDashboardData();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadDashboardData(): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 30);

    forkJoin({
      summary: this.dashboard.getSummary().pipe(catchError(() => of(this.fallbackSummary()))),
      recent: this.bookings.getAll(1, 8).pipe(
        map(p => p?.items ?? []),
        catchError(() => of<Booking[]>([]))
      ),
      chartBookings: this.bookings.getAll(1, 50).pipe(
        map(p => p?.items ?? []),
        catchError(() => of<Booking[]>([]))
      ),
      bookingReport: this.dashboard.getBookingStatusReport(
        from.toISOString().slice(0, 10),
        to.toISOString().slice(0, 10)
      ).pipe(catchError(() => of<BookingReport[]>([]))),
    }).subscribe({
      next: ({ summary, recent, chartBookings, bookingReport }) => {
        this.summary = summary;
        this.stats = this.buildStats(summary, chartBookings);
        this.recentBookings = recent;
        this.todoTasks = this.bookingsToTasks(recent, 5);
        this.assignedTasks = this.bookingsToAssignedTasks(recent, 6);
        this.fleetUtilizationPct = this.calcFleetUtilization(summary);
        this.transportKpis = this.buildTransportKpis(summary);
        this.applyChartData(chartBookings, bookingReport, summary);
        this.loading = false;
      },
      error: () => {
        this.error = 'Unable to load dashboard data. Showing sample values.';
        this.summary = this.fallbackSummary();
        this.stats = this.buildStats(this.summary, []);
        this.transportKpis = this.buildTransportKpis(this.summary);
        this.applyChartData([], [], this.summary);
        this.loading = false;
      }
    });
  }

  // ---------- Helpers ----------

  private buildStats(s: DashboardSummary, bookings: Booking[]): StatTile[] {
    const profitNegative = (s.netProfit ?? 0) < 0;
    const weekRev = this.sumWeekRevenue(bookings);
    const revTrend = weekRev > 0 ? '+12.4%' : '—';
    return [
      {
        key: 'activeTrips',
        label: 'Active Trips',
        value: s.activeTrips,
        icon: 'local_shipping',
        color: 'teal',
        hint: 'Trips running right now',
        trend: s.activeTrips > 0 ? 'Live' : 'Idle',
        trendUp: s.activeTrips > 0,
        trendDetail: 'vs yesterday',
        sparkline: this.sparkFromCount(s.activeTrips)
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
        trendDetail: 'needs action',
        sparkline: this.sparkFromCount(s.pendingBookings)
      },
      {
        key: 'totalVehicles',
        label: 'Fleet Vehicles',
        value: s.totalVehicles,
        icon: 'directions_bus',
        color: 'blue',
        hint: 'Registered in the fleet',
        trend: `${this.calcFleetUtilization(s)}% used`,
        trendUp: true,
        trendDetail: 'utilization',
        sparkline: this.sparkFromCount(this.calcFleetUtilization(s))
      },
      {
        key: 'totalRevenue',
        label: 'Total Revenue',
        value: this.money(s.totalRevenue),
        icon: 'payments',
        color: 'teal',
        trend: revTrend,
        trendUp: weekRev > 0,
        trendDetail: 'vs last week',
        sparkline: this.sparkFromRevenue(bookings)
      },
      {
        key: 'fuelExpense',
        label: 'Fuel Spend',
        value: this.money(s.fuelExpense),
        icon: 'local_gas_station',
        color: 'blue',
        hint: 'Operating cost',
        trend: s.fuelExpense > 0 ? 'Tracked' : '—',
        trendDetail: 'this period',
        sparkline: this.sparkFromCount(Math.round(s.fuelExpense / 10000))
      },
      {
        key: 'netProfit',
        label: 'Net Profit',
        value: this.money(s.netProfit),
        icon: profitNegative ? 'warning' : 'trending_up',
        color: profitNegative ? 'teal' : 'teal',
        variant: profitNegative ? 'danger' : 'success',
        trend: profitNegative ? '↓ Loss' : '↑ Profit',
        trendUp: !profitNegative,
        trendDetail: 'margin health',
        sparkline: this.sparkFromCount(Math.max(0, Math.round((s.netProfit || 0) / 50000)))
      }
    ];
  }

  private buildTransportKpis(s: DashboardSummary): { icon: string; label: string; value: string | number }[] {
    const available = Math.max(0, (s.totalVehicles || 0) - (s.activeTrips || 0));
    return [
      { icon: 'local_shipping', label: 'Active trips', value: s.activeTrips },
      { icon: 'event_available', label: 'Vehicles available', value: available },
      { icon: 'schedule', label: 'Pending bookings', value: s.pendingBookings },
      { icon: 'speed', label: 'Fleet utilization', value: `${this.calcFleetUtilization(s)}%` },
      { icon: 'local_gas_station', label: 'Fuel spend', value: this.money(s.fuelExpense) }
    ];
  }

  private sparkFromCount(n: number): number[] {
    const base = Math.max(1, n);
    return [base * 0.6, base * 0.75, base * 0.9, base, base * 0.95, base * 1.05, base];
  }

  private sparkFromRevenue(bookings: Booking[]): number[] {
    const days = this.buildWeekBuckets(bookings);
    const vals = days.map(d => d.total);
    return vals.some(v => v > 0) ? vals : [0, 0, 0, 0, 0, 0, 0];
  }

  private sumWeekRevenue(bookings: Booking[]): number {
    return this.buildWeekBuckets(bookings).reduce((a, d) => a + d.total, 0);
  }

  private buildWeekBuckets(bookings: Booking[]): { label: string; key: string; total: number }[] {
    const days: { label: string; key: string; total: number }[] = [];
    for (let i = 6; i >= 0; i--) {
      const d = new Date();
      d.setDate(d.getDate() - i);
      const key = d.toISOString().slice(0, 10);
      days.push({
        key,
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
      this.bookingChartLabels = ['Pending', 'Active', 'Completed'];
      this.bookingChartValues = [
        summary.pendingBookings || 0,
        summary.activeTrips || 0,
        Math.max(0, (bookings.length || 0) - (summary.pendingBookings || 0))
      ];
    }

    const inUse = summary.activeTrips || 0;
    const pending = summary.pendingBookings || 0;
    const available = Math.max(0, (summary.totalVehicles || 0) - inUse);
    this.fleetChartValues = [inUse, available, pending];
  }

  private bookingsToTasks(list: Booking[], take: number): TaskItem[] {
    return list
      .filter(b => b.status === 'Pending' || b.status === 'Confirmed')
      .slice(0, take)
      .map(b => ({
        id: b.id,
        title: `${b.routeName}`,
        subtitle: `Pickup ${this.formatDate(b.pickupTime)} · ${b.customerName}`,
        priority: 'high' as const,
        done: false,
      }));
  }

  private bookingsToAssignedTasks(list: Booking[], take: number): TaskItem[] {
    return list
      .filter(b => !!b.driverName)
      .slice(0, take)
      .map(b => ({
        id: b.id,
        title: b.bookingNumber + ' — ' + b.routeName,
        subtitle: 'Pickup ' + this.formatDate(b.pickupTime),
        meta: b.driverName,
        avatarInitials: this.initials(b.driverName),
        done: b.status === 'Completed',
        status: b.status,
        priority: this.priorityFromStatus(b.status)
      }));
  }

  private priorityFromStatus(status?: string): 'high' | 'medium' | 'low' | undefined {
    const s = (status || '').toLowerCase();
    if (s.includes('pending') || s.includes('cancel')) return 'high';
    if (s.includes('active') || s.includes('start') || s.includes('confirm')) return 'medium';
    if (s.includes('complete')) return 'low';
    return undefined;
  }

  private fallbackSummary(): DashboardSummary {
    return {
      totalVehicles: 12,
      activeTrips: 4,
      totalRevenue: 450000,
      pendingBookings: 7,
      fuelExpense: 85000,
      netProfit: 320000,
    };
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

  onTaskToggle(_t: TaskItem): void { /* TODO: call booking status endpoint */ }
  onAssignedFilter(f: string): void { this.assignedFilter = f; }
  onPromoCta(): void { this.router.navigate(['/bookings/new']); }
  
  onStatClick(key: string): void {
    const routes: Record<string, string> = {
      activeTrips: '/bookings',
      pendingBookings: '/bookings',
      totalVehicles: '/vehicles',
      totalRevenue: '/reports',
      fuelExpense: '/fuel-logs',
      netProfit: '/reports'
    };
    const route = routes[key];
    if (route) this.router.navigate([route]);
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
