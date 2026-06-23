import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
import { Booking } from '../../../core/models/booking.model';
import { Payment } from '../../../core/models/payment.model';
import { COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

@Component({
  selector: 'app-booking-invoice',
  templateUrl: './booking-invoice.component.html',
  styleUrls: ['./booking-invoice.component.scss']
})
export class BookingInvoiceComponent implements OnInit {
  readonly companyName = COMPANY_NAME;
  readonly logoPath = APP_LOGO_PATH;
  booking: Booking | null = null;
  payments: Payment[] = [];
  loading = true;
  error: string | null = null;
  invoiceDate = new Date();

  get totalPaid(): number {
    return this.payments
      .filter(p => p.status === 'Paid' || p.status === 'PartiallyPaid')
      .reduce((s, p) => s + p.amount, 0);
  }

  get balanceDue(): number {
    return (this.booking?.totalAmount ?? 0) - this.totalPaid;
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private bookingService: BookingService,
    private paymentService: PaymentService
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
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load invoice data.'; }
    });
  }

  print(): void { window.print(); }

  back(): void { this.router.navigate(['/bookings', this.booking?.id]); }
}
