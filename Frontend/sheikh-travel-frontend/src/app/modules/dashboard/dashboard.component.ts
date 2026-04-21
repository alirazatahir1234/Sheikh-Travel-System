import { Component, OnInit } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { DashboardService } from '../../core/services/dashboard.service';
import { BookingService } from '../../core/services/booking.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardSummary } from '../../core/models/common.model';
import { Booking } from '../../core/models/booking.model';

import {
  QuickLaunchApp, StatTile, TaskItem, DataTableColumn,
  ArticleLink, PrayerTime, WeatherInfo
} from '../../shared/ui';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  loading = true;
  error: string | null = null;

  userName = 'there';
  summary: DashboardSummary | null = null;

  /** Quick-launch tiles match the Sheikh Travel modules (from the Postman collection). */
  quickApps: QuickLaunchApp[] = [
    { id: 'bookings',   label: 'Bookings',   icon: 'confirmation_number', color: 'blue',   route: '/bookings' },
    { id: 'vehicles',   label: 'Vehicles',   icon: 'directions_bus',      color: 'green',  route: '/vehicles' },
    { id: 'drivers',    label: 'Drivers',    icon: 'badge',               color: 'teal',   route: '/drivers' },
    { id: 'routes',     label: 'Routes',     icon: 'alt_route',           color: 'purple', route: '/routes' },
    { id: 'tracking',   label: 'Tracking',   icon: 'my_location',         color: 'orange', route: '/tracking' },
    { id: 'payments',   label: 'Payments',   icon: 'account_balance_wallet', color: 'rose', route: '/payments' },
    { id: 'reports',    label: 'Reports',    icon: 'insights',            color: 'sky',    route: '/reports' },
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
    { key: 'status',        header: 'Status' },
    { key: 'totalAmount',   header: 'Amount', align: 'right',
      cell: r => 'AED ' + (r.totalAmount || 0).toLocaleString() },
  ];
  recentBookings: Booking[] = [];

  /** Popular articles — static content appropriate for a travel-fleet team. */
  articles: ArticleLink[] = [
    { id: 1, title: 'How to create a new booking in 3 steps', icon: 'help_outline' },
    { id: 2, title: 'Assigning a driver & vehicle to a trip',  icon: 'help_outline' },
    { id: 3, title: 'Understanding the pricing engine',        icon: 'calculate' },
    { id: 4, title: 'Recording a fuel log against a vehicle',  icon: 'local_gas_station' },
    { id: 5, title: 'Running the monthly revenue report',      icon: 'insights' },
  ];

  /** Prayer times — ready to be wired to a live feed later. */
  prayerTimes: PrayerTime[] = [
    { name: 'Fajr',    time: '05:12 AM' },
    { name: 'Dhuhr',   time: '12:21 PM' },
    { name: 'Asr',     time: '03:42 PM' },
    { name: 'Maghrib', time: '06:19 PM' },
    { name: 'Isha',    time: '07:49 PM' },
  ];

  /** Weather demo values for the Sharjah HQ. */
  weather: WeatherInfo = {
    city: 'Sharjah',
    temperatureC: 36,
    condition: 'Sunny',
    dateLabel: new Date().toDateString()
  };

  constructor(
    private dashboard: DashboardService,
    private bookings: BookingService,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.userName = this.auth.getCurrentUser()?.fullName?.split(' ')[0] ?? 'there';

    forkJoin({
      summary: this.dashboard.getSummary().pipe(catchError(() => of(this.fallbackSummary()))),
      recent:  this.bookings.getAll(1, 8).pipe(
        map(p => p?.items ?? []),
        catchError(() => of<Booking[]>([])),
      ),
    }).subscribe({
      next: ({ summary, recent }) => {
        this.summary = summary;
        this.stats = this.buildStats(summary);
        this.recentBookings = recent;
        this.todoTasks = this.bookingsToTasks(recent, 5);
        this.assignedTasks = this.bookingsToAssignedTasks(recent, 4);
        this.loading = false;
      },
      error: () => {
        this.error = 'Unable to load dashboard data. Showing sample values.';
        this.summary = this.fallbackSummary();
        this.stats = this.buildStats(this.summary);
        this.loading = false;
      }
    });
  }

  // ---------- Helpers ----------

  private buildStats(s: DashboardSummary): StatTile[] {
    return [
      { key: 'activeTrips',     label: 'Active Trips',     value: s.activeTrips,     icon: 'local_shipping',        color: 'blue',   hint: 'Trips running right now' },
      { key: 'pendingBookings', label: 'Pending Bookings', value: s.pendingBookings, icon: 'pending_actions',        color: 'orange', hint: 'Awaiting confirmation' },
      { key: 'totalVehicles',   label: 'Fleet Vehicles',   value: s.totalVehicles,   icon: 'directions_bus',        color: 'green',  hint: 'Registered in the fleet' },
      { key: 'totalRevenue',    label: 'Total Revenue',    value: this.money(s.totalRevenue), icon: 'payments',      color: 'teal' },
      { key: 'fuelExpense',     label: 'Fuel Spend',       value: this.money(s.fuelExpense),  icon: 'local_gas_station', color: 'rose' },
      { key: 'netProfit',       label: 'Net Profit',       value: this.money(s.netProfit),    icon: 'trending_up',       color: 'purple' },
    ];
  }

  private bookingsToTasks(list: Booking[], take: number): TaskItem[] {
    return list
      .filter(b => b.status === 'Pending' || b.status === 'Confirmed')
      .slice(0, take)
      .map(b => ({
        id: b.id,
        title: `Confirm ${b.bookingNumber} — ${b.routeName}`,
        subtitle: `${b.customerName} · ${this.formatDate(b.pickupTime)}`,
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
      }));
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
    return 'AED ' + (v || 0).toLocaleString();
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
  onArticleOpen(_a: ArticleLink): void { /* TODO: open help article */ }
  onPromoCta(): void { /* TODO: wire to onboarding flow */ }
}
