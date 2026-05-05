import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BookingService } from '../../../core/services/booking.service';
import { RouteService } from '../../../core/services/route.service';
import { CustomerService } from '../../../core/services/customer.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { PaymentService } from '../../../core/services/payment.service';
import { Booking, UpdateBookingDto } from '../../../core/models/booking.model';
import { Payment } from '../../../core/models/payment.model';
import { calculateLedgerRemaining } from '../../../shared/utils/booking-ledger.util';
import { Route } from '../../../core/models/route.model';
import { Customer } from '../../../core/models/customer.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';

@Component({
  selector: 'app-booking-edit',
  templateUrl: './booking-edit.component.html',
  styleUrls: ['./booking-edit.component.scss']
})
export class BookingEditComponent implements OnInit {
  form: FormGroup;
  loading = true;
  submitting = false;
  bookingId: number | null = null;
  booking: Booking | null = null;
  /** Ledger total (Paid + PartiallyPaid) for edit-mode rules. */
  totalPaid = 0;
  /** Cancelled / completed — no edits. */
  editBlocked = false;
  /** Confirmed (paid up) or in progress — notes only. */
  notesOnlyMode = false;

  customers: Customer[] = [];
  routes: Route[] = [];
  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private bookingService: BookingService,
    private routeService: RouteService,
    private customerService: CustomerService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private paymentService: PaymentService,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      customerId: [null, Validators.required],
      routeId: [null, Validators.required],
      pickupTime: [null, Validators.required],
      passengerCount: [1, [Validators.required, Validators.min(1)]],
      totalAmount: [0, [Validators.required, Validators.min(0)]],
      vehicleId: [null],
      driverId: [null],
      notes: ['']
    });
  }

  ngOnInit(): void {
    this.bookingId = +this.route.snapshot.paramMap.get('id')!;

    forkJoin({
      booking: this.bookingService.getById(this.bookingId),
      payments: this.paymentService.getByBookingId(this.bookingId).pipe(catchError(() => of([] as Payment[]))),
      customers: this.customerService.getAll(1, 500),
      routes: this.routeService.getAll(1, 500),
      vehicles: this.vehicleService.getAll(1, 500),
      drivers: this.driverService.getAll(1, 500)
    }).subscribe({
      next: ({ booking, payments, customers, routes, vehicles, drivers }) => {
        this.booking = booking;
        this.totalPaid = payments
          .filter(p => p.status === 'Paid' || p.status === 'PartiallyPaid')
          .reduce((sum, p) => sum + p.amount, 0);
        this.customers = customers.items;
        this.routes = routes.items;
        this.vehicles = vehicles.items;
        // Keep all drivers in edit mode so existing assignments remain valid/editable.
        this.drivers = drivers.items;

        this.form.patchValue({
          customerId: booking.customerId,
          routeId: booking.routeId,
          pickupTime: new Date(booking.pickupTime),
          passengerCount: booking.passengerCount,
          totalAmount: booking.totalAmount,
          vehicleId: booking.vehicleId ?? null,
          driverId: booking.driverId ?? null,
          notes: booking.notes ?? ''
        });

        this.applyEditModeRules();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load booking data.', 'Close', { duration: 3000 });
        this.router.navigate(['/bookings']);
      }
    });
  }

  private applyEditModeRules(): void {
    if (!this.booking) return;

    this.editBlocked = false;
    this.notesOnlyMode = false;
    this.form.enable();

    if (this.booking.status === 'Cancelled' || this.booking.status === 'Completed') {
      this.editBlocked = true;
      this.form.disable();
      return;
    }

    const balance = calculateLedgerRemaining(this.booking.totalAmount, this.totalPaid);
    const paidUp = balance <= 0.009;

    if (this.booking.status === 'Started' || (this.booking.status === 'Confirmed' && paidUp)) {
      this.notesOnlyMode = true;
      ['customerId', 'routeId', 'pickupTime', 'passengerCount', 'totalAmount', 'vehicleId', 'driverId'].forEach(name => {
        this.form.get(name)?.disable({ emitEvent: false });
      });
      this.form.get('notes')?.enable({ emitEvent: false });
    }
  }

  submit(): void {
    if (this.editBlocked || !this.bookingId) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const f = this.form.getRawValue();

    const dto: UpdateBookingDto = {
      customerId: f.customerId,
      routeId: f.routeId,
      pickupTime: f.pickupTime instanceof Date ? f.pickupTime.toISOString() : f.pickupTime,
      passengerCount: f.passengerCount,
      totalAmount: f.totalAmount,
      vehicleId: this.normalizeOptionalEntityId(f.vehicleId, this.vehicles.map(v => v.id)),
      driverId: this.normalizeOptionalEntityId(f.driverId, this.drivers.map(d => d.id)),
      notes: f.notes?.trim() || null
    };

    this.bookingService.update(this.bookingId, { booking: dto }).subscribe({
      next: () => {
        this.submitting = false;
        this.snackBar.open('Booking updated successfully.', 'Close', { duration: 2000 });
        this.router.navigate(['/bookings', this.bookingId]);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        const message =
          (err.error && typeof err.error === 'object' && 'message' in err.error && (err.error as { message?: string }).message) ||
          'Failed to update booking.';
        this.snackBar.open(message, 'Close', { duration: 3500 });
      }
    });
  }

  private normalizeOptionalEntityId(raw: unknown, validIds: number[]): number | null {
    const id = Number(raw);
    if (!Number.isFinite(id) || id <= 0) return null;
    return validIds.includes(id) ? id : null;
  }
}
