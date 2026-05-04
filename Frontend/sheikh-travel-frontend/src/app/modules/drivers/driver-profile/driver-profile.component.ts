import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { DriverService } from '../../../core/services/driver.service';
import { BookingService } from '../../../core/services/booking.service';
import { Driver } from '../../../core/models/driver.model';
import { Booking } from '../../../core/models/booking.model';

@Component({
  selector: 'app-driver-profile',
  templateUrl: './driver-profile.component.html',
  styleUrls: ['./driver-profile.component.scss']
})
export class DriverProfileComponent implements OnInit {
  driver: Driver | null = null;
  loading = true;
  error: string | null = null;

  recentBookings: Booking[] = [];
  totalTrips = 0;
  completedTrips = 0;
  totalRevenue = 0;
  licenseExpiringSoon = false;

  bookingColumns = ['bookingNumber', 'customerName', 'pickupTime', 'totalAmount', 'status'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private driverService: DriverService,
    private bookingService: BookingService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.loadProfile(id);
  }

  private loadProfile(id: number): void {
    forkJoin({
      driver: this.driverService.getById(id),
      bookings: this.bookingService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).subscribe({
      next: ({ driver, bookings }) => {
        this.driver = driver;

        const driverBookings = bookings.items.filter(b => b.driverId === id);
        this.recentBookings = driverBookings.slice(0, 10);
        
        this.totalTrips = driverBookings.length;
        this.completedTrips = driverBookings.filter(b => b.status === 'Completed').length;
        this.totalRevenue = driverBookings.reduce((sum, b) => sum + (b.totalAmount || 0), 0);

        if (driver.licenseExpiryDate) {
          const expiry = new Date(driver.licenseExpiryDate);
          const today = new Date();
          const daysUntilExpiry = Math.ceil((expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
          this.licenseExpiringSoon = daysUntilExpiry <= 30;
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load driver profile.';
      }
    });
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Pending: '#f57f17', Confirmed: '#1565c0', Started: '#00695c',
      InProgress: '#00695c', Completed: '#2e7d32', Cancelled: '#c62828'
    };
    return colors[status] ?? '#666';
  }

  editDriver(): void {
    this.router.navigate(['/drivers', this.driver?.id, 'edit']);
  }

  get completionRate(): number {
    if (this.totalTrips === 0) return 0;
    return Math.round((this.completedTrips / this.totalTrips) * 100);
  }
}
