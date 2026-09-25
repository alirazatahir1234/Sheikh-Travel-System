import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { catchError } from 'rxjs/operators';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { TripService } from '../../../core/services/trip.service';
import { AuthService } from '../../../core/services/auth.service';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';
import { Payment } from '../../../core/models/payment.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';
import { WhatsAppAutomationTimelineItem } from '../../../core/models/whatsapp.model';

@Component({
  standalone: false,
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
  creatingTrip = false;

  waTimeline: WhatsAppAutomationTimelineItem[] = [];
  waTimelineLoading = false;
  resendingEventId: number | null = null;

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
    private tripService: TripService,
    private auth: AuthService,
    private waApi: WhatsAppInboxService,
    private toast: UiToastService,
    private router: Router
  ) {}

  get canResendWa(): boolean {
    return this.auth.hasPermission('WhatsApp.Reply');
  }

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
        this.loadWaTimeline(id);
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load booking details.';
      }
    });
  }

  loadWaTimeline(bookingId: number): void {
    this.waTimelineLoading = true;
    this.waApi.getBookingWhatsAppTimeline(bookingId).subscribe({
      next: rows => {
        this.waTimeline = rows ?? [];
        this.waTimelineLoading = false;
      },
      error: () => {
        this.waTimeline = [];
        this.waTimelineLoading = false;
      }
    });
  }

  waStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Pending';
      case 1: return 'Processing';
      case 2: return 'Sent';
      case 3: return 'Skipped';
      case 4: return 'Failed';
      case 5: return 'Cancelled';
      default: return String(status);
    }
  }

  resendWaEvent(e: WhatsAppAutomationTimelineItem): void {
    if (!this.canResendWa || this.resendingEventId != null) return;
    this.resendingEventId = e.id;
    this.waApi.resendAutomationEvent(e.id).subscribe({
      next: () => {
        this.resendingEventId = null;
        this.toast.success('Resend queued');
        if (this.booking) this.loadWaTimeline(this.booking.id);
      },
      error: err => {
        this.resendingEventId = null;
        this.toast.error(err?.error?.message || 'Resend failed');
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
        this.toast.warning('Cancellation reason is required.');
        return;
      }
      this.bookingService.updateStatus({ bookingId: this.booking.id, status, cancellationReason: reason.trim() }).subscribe({
        next: () => {
          this.toast.success(`Booking cancelled`);
          this.booking!.status = status;
        },
        error: () => this.toast.error('Failed to cancel booking')
      });
      return;
    }

    this.bookingService.updateStatus({ bookingId: this.booking.id, status }).subscribe({
      next: () => {
        this.toast.success(`Status updated to ${status}`);
        this.booking!.status = status;
      },
      error: () => this.toast.error('Failed to update status')
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

  /** Create / open operational trip from this booking (dispatch path). */
  canCreateTrip(): boolean {
    if (!this.booking) return false;
    if (this.booking.status === 'Cancelled') return false;
    return this.auth.hasPermission('Trip.Create');
  }

  createTripFromBooking(): void {
    if (!this.booking || this.creatingTrip) return;
    this.creatingTrip = true;
    this.tripService.createFromBooking(this.booking.id).subscribe({
      next: tripId => {
        this.creatingTrip = false;
        this.toast.success('Trip ready — opening trip detail');
        void this.router.navigate(['/trips', tripId]);
      },
      error: () => {
        this.creatingTrip = false;
        this.toast.error('Failed to create trip from booking');
      }
    });
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
        this.toast.success('Vehicle reassigned.');
        this.reassigning = false;
      },
      error: () => {
        this.toast.error('Failed to reassign vehicle.');
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
        this.toast.success('Driver reassigned.');
        this.reassigning = false;
      },
      error: () => {
        this.toast.error('Failed to reassign driver.');
        this.reassigning = false;
      }
    });
  }
}
