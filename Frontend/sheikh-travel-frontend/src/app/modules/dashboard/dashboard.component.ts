import { Component, OnInit } from '@angular/core';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardStats } from '../../core/models/common.model';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  stats: DashboardStats | null = null;
  loading = true;
  error: string | null = null;

  statCards = [
    { key: 'totalBookings',    label: 'Total Bookings',    icon: 'book_online',    color: '#1a237e' },
    { key: 'totalRevenue',     label: 'Total Revenue',     icon: 'payments',       color: '#2e7d32', prefix: 'PKR ' },
    { key: 'activeVehicles',   label: 'Active Vehicles',   icon: 'directions_bus', color: '#e65100' },
    { key: 'activeDrivers',    label: 'Active Drivers',    icon: 'person',         color: '#4a148c' },
    { key: 'pendingBookings',  label: 'Pending Bookings',  icon: 'pending',        color: '#f57f17' },
    { key: 'todayBookings',    label: "Today's Bookings",  icon: 'today',          color: '#00695c' },
    { key: 'monthlyRevenue',   label: 'Monthly Revenue',   icon: 'trending_up',    color: '#1565c0', prefix: 'PKR ' },
    { key: 'completedBookings',label: 'Completed',         icon: 'check_circle',   color: '#2e7d32' },
  ];

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.getStats().subscribe({
      next: data => { this.stats = data; this.loading = false; },
      error: () => {
        this.error = 'Failed to load live stats. Showing sample data.';
        this.stats = {
          totalBookings: 128, totalRevenue: 450000, activeVehicles: 12,
          activeDrivers: 18, pendingBookings: 7, todayBookings: 5,
          monthlyRevenue: 85000, completedBookings: 95
        };
        this.loading = false;
      }
    });
  }

  getValue(key: string): number {
    return this.stats ? (this.stats as unknown as Record<string, number>)[key] ?? 0 : 0;
  }

  trackByKey(_index: number, card: { key: string }): string {
    return card.key;
  }
}
