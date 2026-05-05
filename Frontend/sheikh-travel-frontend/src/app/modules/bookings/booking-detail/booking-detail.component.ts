import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';
import { Payment } from '../../../core/models/payment.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';

@Component({
  selector: 'app-booking-detail',
  templateUrl: './booking-detail.component.html',
  styleUrls: ['./booking-detail.component.scss']
})
export class BookingDetailComponent implements OnInit {
  booking: Booking | null = null;
  payments: Payment[] = [];
  loading = true;
  error: string | null = null;

  totalPaid = 0;

  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];
  showReassignPanel = false;
  selectedVehicleId: number | null = null;
  selectedDriverId: number | null = null;
  reassigning = false;

  statusTransitions: Partial<Record<BookingStatus, BookingStatus[]>> = {
    Pending: ['Confirmed', 'Cancelled'],
    Confirmed: ['Started', 'Cancelled'],
    Started: ['Completed', 'Cancelled']
  };

  statusColors: Record<string, string> = {
    Pending: '#f57f17', Confirmed: '#1565c0', Started: '#00695c',
    Completed: '#2e7d32', Cancelled: '#c62828'
  };

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService,
    private paymentService: PaymentService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;

    forkJoin({
      booking: this.bookingService.getById(id),
      payments: this.paymentService.getByBookingId(id).pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ booking, payments }) => {
        this.booking = booking;
        this.payments = payments;
        this.calculatePaymentSummary();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load booking details.';
      }
    });
  }

  private calculatePaymentSummary(): void {
    this.totalPaid = this.payments
      .filter(p => p.status === 'Paid' || p.status === 'PartiallyPaid')
      .reduce((sum, p) => sum + p.amount, 0);
  }

  getNextStatuses(): BookingStatus[] {
    if (!this.booking) return [];
    return this.statusTransitions[this.booking.status] ?? [];
  }

  updateStatus(status: BookingStatus): void {
    if (!this.booking) return;

    if (status === 'Cancelled') {
      const reason = prompt('Please enter a reason for cancellation:');
      if (!reason || !reason.trim()) {
        this.snackBar.open('Cancellation reason is required.', 'Close', { duration: 3000 });
        return;
      }
      this.bookingService.updateStatus({ bookingId: this.booking.id, status, cancellationReason: reason.trim() }).subscribe({
        next: () => {
          this.snackBar.open(`Booking cancelled`, 'Close', { duration: 2000 });
          this.booking!.status = status;
        },
        error: () => this.snackBar.open('Failed to cancel booking', 'Close', { duration: 3000 })
      });
      return;
    }

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

  trackByPaymentId(_index: number, payment: Payment): number {
    return payment.id;
  }

  getPaymentStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Pending: '#f57f17',
      PartiallyPaid: '#1565c0',
      Paid: '#2e7d32',
      Refunded: '#c62828'
    };
    return colors[status] ?? '#666';
  }

  canEdit(): boolean {
    if (!this.booking) return false;
    return this.booking.status !== 'Completed' && this.booking.status !== 'Cancelled';
  }

  /** Invoice / print — not offered for cancelled bookings. */
  canPrintInvoice(): boolean {
    return !!this.booking && this.booking.status !== 'Cancelled';
  }

  /** Recording new payments — not when trip is done or booking cancelled. */
  canRecordPayment(): boolean {
    if (!this.booking) return false;
    return this.booking.status !== 'Cancelled' && this.booking.status !== 'Completed';
  }

  openReassignPanel(): void {
    if (this.vehicles.length === 0 || this.drivers.length === 0) {
      forkJoin({
        vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))),
        drivers: this.driverService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
      }).subscribe(({ vehicles, drivers }) => {
        this.vehicles = vehicles.items.filter(v => v.status === 1 || v.status === 2);
        this.drivers = drivers.items.filter(d => d.isActive);
      });
    }

    this.selectedVehicleId = this.booking?.vehicleId ?? null;
    this.selectedDriverId = this.booking?.driverId ?? null;
    this.showReassignPanel = true;
  }

  closeReassignPanel(): void {
    this.showReassignPanel = false;
  }

  reassignVehicle(): void {
    if (!this.booking || this.selectedVehicleId === null) return;
    this.reassigning = true;

    this.bookingService.assignVehicle({ bookingId: this.booking.id, vehicleId: this.selectedVehicleId }).subscribe({
      next: () => {
        const vehicle = this.vehicles.find(v => v.id === this.selectedVehicleId);
        this.booking!.vehicleId = this.selectedVehicleId!;
        this.booking!.vehicleName = vehicle?.name ?? '';
        this.snackBar.open('Vehicle reassigned.', 'Close', { duration: 2000 });
        this.reassigning = false;
      },
      error: () => {
        this.snackBar.open('Failed to reassign vehicle.', 'Close', { duration: 3000 });
        this.reassigning = false;
      }
    });
  }

  reassignDriver(): void {
    if (!this.booking || this.selectedDriverId === null) return;
    this.reassigning = true;

    this.bookingService.assignDriver({ bookingId: this.booking.id, driverId: this.selectedDriverId }).subscribe({
      next: () => {
        const driver = this.drivers.find(d => d.id === this.selectedDriverId);
        this.booking!.driverId = this.selectedDriverId!;
        this.booking!.driverName = driver?.fullName ?? '';
        this.snackBar.open('Driver reassigned.', 'Close', { duration: 2000 });
        this.reassigning = false;
      },
      error: () => {
        this.snackBar.open('Failed to reassign driver.', 'Close', { duration: 3000 });
        this.reassigning = false;
      }
    });
  }
}
