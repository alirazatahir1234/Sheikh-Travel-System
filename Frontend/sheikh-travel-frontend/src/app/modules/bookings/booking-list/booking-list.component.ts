import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';

@Component({
  selector: 'app-booking-list',
  templateUrl: './booking-list.component.html',
  styleUrls: ['./booking-list.component.scss']
})
export class BookingListComponent implements OnInit {
  displayedColumns = ['bookingNumber', 'customerName', 'routeName', 'pickupTime', 'passengerCount', 'totalAmount', 'status', 'actions'];
  dataSource = new MatTableDataSource<Booking>();
  loading = true;
  error: string | null = null;
  totalCount = 0;
  selectedStatus = '';

  statusOptions: Array<{ value: string; label: string }> = [
    { value: '', label: 'All' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Confirmed', label: 'Confirmed' },
    { value: 'InProgress', label: 'In Progress' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Cancelled', label: 'Cancelled' }
  ];

  statusColors: Record<string, string> = {
    Pending: '#f57f17', Confirmed: '#1565c0', InProgress: '#00695c',
    Completed: '#2e7d32', Cancelled: '#c62828'
  };

  constructor(private bookingService: BookingService, private router: Router, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(page = 1, pageSize = 10): void {
    this.loading = true;
    this.error = null;
    this.bookingService.getAll(page, pageSize, this.selectedStatus || undefined).subscribe({
      next: r => { this.dataSource.data = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load bookings.'; }
    });
  }

  view(id: number): void { this.router.navigate(['/bookings', id]); }

  getStatusColor(status: BookingStatus): string {
    return this.statusColors[status] ?? '#666';
  }

  trackByValue(_index: number, item: { value: string }): string {
    return item.value;
  }
}
