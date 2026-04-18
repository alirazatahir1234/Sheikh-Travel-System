import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BookingService } from '../../../core/services/booking.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';

@Component({
  selector: 'app-booking-detail',
  templateUrl: './booking-detail.component.html',
  styleUrls: ['./booking-detail.component.scss']
})
export class BookingDetailComponent implements OnInit {
  booking: Booking | null = null;
  loading = true;
  error: string | null = null;

  statusTransitions: Partial<Record<BookingStatus, BookingStatus[]>> = {
    Pending: ['Confirmed', 'Cancelled'],
    Confirmed: ['InProgress', 'Cancelled'],
    InProgress: ['Completed']
  };

  statusColors: Record<string, string> = {
    Pending: '#f57f17', Confirmed: '#1565c0', InProgress: '#00695c',
    Completed: '#2e7d32', Cancelled: '#c62828'
  };

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.bookingService.getById(id).subscribe({
      next: b => { this.booking = b; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load booking details.'; }
    });
  }

  getNextStatuses(): BookingStatus[] {
    if (!this.booking) return [];
    return this.statusTransitions[this.booking.status] ?? [];
  }

  updateStatus(status: BookingStatus): void {
    if (!this.booking) return;
    this.bookingService.updateStatus({ bookingId: this.booking.id, status }).subscribe({
      next: () => {
        this.snackBar.open(`Status updated to ${status}`, 'Close', { duration: 2000 });
        this.booking!.status = status;
      },
      error: () => this.snackBar.open('Failed to update status', 'Close', { duration: 3000 })
    });
  }

  getColor(status: BookingStatus): string {
    return this.statusColors[status] ?? '#666';
  }

  trackByStatus(_index: number, status: BookingStatus): string {
    return status;
  }
}
