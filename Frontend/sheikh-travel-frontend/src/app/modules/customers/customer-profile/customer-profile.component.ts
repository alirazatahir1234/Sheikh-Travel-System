import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CustomerService } from '../../../core/services/customer.service';
import { BookingService } from '../../../core/services/booking.service';
import { Customer } from '../../../core/models/customer.model';
import { Booking } from '../../../core/models/booking.model';

@Component({
  selector: 'app-customer-profile',
  templateUrl: './customer-profile.component.html',
  styleUrls: ['./customer-profile.component.scss']
})
export class CustomerProfileComponent implements OnInit {
  customer: Customer | null = null;
  loading = true;
  error: string | null = null;

  recentBookings: Booking[] = [];
  totalBookings = 0;
  completedBookings = 0;
  totalSpent = 0;
  lastBookingDate: Date | null = null;
  isReturningCustomer = false;

  bookingColumns = ['bookingNumber', 'routeName', 'pickupTime', 'totalAmount', 'status'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private customerService: CustomerService,
    private bookingService: BookingService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.loadProfile(id);
  }

  private loadProfile(id: number): void {
    forkJoin({
      customer: this.customerService.getById(id),
      bookings: this.bookingService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).subscribe({
      next: ({ customer, bookings }) => {
        this.customer = customer;

        const customerBookings = bookings.items.filter(b => b.customerId === id);
        this.recentBookings = customerBookings.slice(0, 10);
        
        this.totalBookings = customerBookings.length;
        this.completedBookings = customerBookings.filter(b => b.status === 'Completed').length;
        this.totalSpent = customerBookings.reduce((sum, b) => sum + (b.totalAmount || 0), 0);
        this.isReturningCustomer = this.completedBookings > 0;

        if (customerBookings.length > 0) {
          const sorted = [...customerBookings].sort((a, b) => 
            new Date(b.pickupTime).getTime() - new Date(a.pickupTime).getTime()
          );
          this.lastBookingDate = new Date(sorted[0].pickupTime);
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load customer profile.';
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

  editCustomer(): void {
    this.router.navigate(['/customers', this.customer?.id, 'edit']);
  }

  createBooking(): void {
    this.router.navigate(['/bookings/new'], { queryParams: { customerId: this.customer?.id } });
  }
}
