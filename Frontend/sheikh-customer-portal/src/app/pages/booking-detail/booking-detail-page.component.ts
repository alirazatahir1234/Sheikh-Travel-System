import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { take } from 'rxjs/operators';
import { PortalBookingDetailDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { paymentLineStatusLabel } from '../../core/utils/portal-display.util';
import { BookingTrackingComponent } from '../../shared/booking-tracking/booking-tracking.component';
import { PaymentSummaryComponent } from '../../shared/payment-summary/payment-summary.component';
import { StatusBadgeComponent } from '../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-booking-detail-page',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    PaymentSummaryComponent,
    StatusBadgeComponent,
    BookingTrackingComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      @if (!session.isAuthenticated()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-5 text-sm shadow-card">
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Add your phone</a>
          to view this booking.
        </div>
      } @else if (error()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
      } @else if (detail()) {
        <div class="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p class="text-xs font-semibold uppercase text-slate-500">Booking</p>
            <h1 class="text-xl font-bold text-slate-900">{{ detail()!.bookingNumber }}</h1>
          </div>
          <app-status-badge [bookingStatus]="detail()!.bookingStatus" />
        </div>

        @if (detail()!.driver; as drv) {
          <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card">
            <h2 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Your driver</h2>
            <p class="mt-2 font-bold text-slate-900">{{ drv.fullName || 'Assigned driver' }}</p>
            @if (drv.rating != null) {
              <p class="text-sm text-amber-700">★ {{ drv.rating | number : '1.1-1' }}</p>
            }
            @if (drv.yearsExperience != null) {
              <p class="text-xs text-slate-600">{{ drv.yearsExperience }} years experience</p>
            }
            @if (drv.isVerified) {
              <p class="mt-1 text-xs font-semibold text-emerald-700">Verified chauffeur</p>
            }
          </section>
        }

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card">
          <h2 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Trip</h2>
          <dl class="mt-3 space-y-2 text-sm">
            <div class="flex justify-between gap-4">
              <dt class="text-slate-600">Route</dt>
              <dd class="font-medium text-slate-900">{{ detail()!.routeLabel }}</dd>
            </div>
            @if (detail()!.pickupAddress) {
              <div class="flex justify-between gap-4">
                <dt class="text-slate-600">Pickup</dt>
                <dd class="font-medium text-slate-900 text-right max-w-[60%]">{{ detail()!.pickupAddress }}</dd>
              </div>
            }
            @if (detail()!.dropoffAddress) {
              <div class="flex justify-between gap-4">
                <dt class="text-slate-600">Drop-off</dt>
                <dd class="font-medium text-slate-900 text-right max-w-[60%]">{{ detail()!.dropoffAddress }}</dd>
              </div>
            }
            @if (detail()!.seats?.length) {
              <div class="flex justify-between gap-4">
                <dt class="text-slate-600">Seats</dt>
                <dd class="font-medium text-slate-900">{{ detail()!.seats!.join(', ') }}</dd>
              </div>
            }
            <div class="flex justify-between gap-4">
              <dt class="text-slate-600">Pickup</dt>
              <dd class="font-medium text-slate-900">{{ detail()!.pickupTime | date : 'medium' }}</dd>
            </div>
            <div class="flex justify-between gap-4">
              <dt class="text-slate-600">Passengers</dt>
              <dd class="font-medium text-slate-900">{{ detail()!.passengerCount }}</dd>
            </div>
            <div class="flex justify-between gap-4">
              <dt class="text-slate-600">Vehicle</dt>
              <dd class="font-medium text-slate-900">{{ detail()!.vehicleName || '—' }}</dd>
            </div>
          </dl>
        </section>

        <app-payment-summary
          [total]="detail()!.totalAmount"
          [paid]="detail()!.paidAmount"
          [remaining]="detail()!.remaining"
          [payState]="detail()!.payState"
        />

        <app-booking-tracking [bookingId]="detail()!.id" [bookingStatus]="detail()!.bookingStatus" />

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card">
          <h2 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Payment history</h2>
          <ul class="mt-3 divide-y divide-slate-100 text-sm">
            @for (p of detail()!.payments; track p.id) {
              <li class="flex flex-wrap justify-between gap-2 py-2">
                <span class="text-slate-600">{{ p.paymentDate | date : 'mediumDate' }}</span>
                <span class="font-semibold">PKR {{ p.amount | number : '1.2-2' }}</span>
                <span class="text-xs text-slate-500">{{ lineStatus(p.status) }} · {{ p.paymentMethod }}</span>
              </li>
            }
            @if (detail()!.payments.length === 0) {
              <li class="py-3 text-slate-500">No payments recorded yet.</li>
            }
          </ul>
        </section>

        @if (detail()!.remaining > 0 && detail()!.bookingStatus !== 5) {
          <form
            id="pay-anchor"
            [formGroup]="payForm"
            (ngSubmit)="submitPay()"
            class="rounded-2xl border border-primary-200 bg-primary-50/40 p-4 space-y-3"
          >
            <h3 class="text-sm font-bold text-primary-900">
              Pay remaining (PKR {{ detail()!.remaining | number : '1.2-2' }})
            </h3>
            <label class="block text-xs font-semibold text-slate-600">Amount</label>
            <input type="number" formControlName="amount" class="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm" />
            <label class="block text-xs font-semibold text-slate-600">Method</label>
            <select formControlName="paymentMethod" class="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm">
              <option value="Cash">Cash</option>
              <option value="BankTransfer">Bank transfer</option>
              <option value="CustomerPortal">Card / online (manual)</option>
            </select>
            @if (payError()) {
              <p class="text-sm text-rose-700">{{ payError() }}</p>
            }
            <button
              type="submit"
              [disabled]="payBusy()"
              class="w-full rounded-xl bg-primary-600 py-2.5 text-sm font-bold text-white disabled:opacity-50"
            >
              {{ payBusy() ? 'Processing…' : 'Pay now' }}
            </button>
          </form>
        }

        <div class="flex flex-col gap-2 sm:flex-row">
          <button
            type="button"
            (click)="downloadInvoice()"
            [disabled]="invoiceBusy()"
            class="flex-1 rounded-xl border border-slate-200 bg-white py-2.5 text-sm font-semibold text-slate-800 hover:bg-slate-50 disabled:opacity-50"
          >
            {{ invoiceBusy() ? 'Preparing…' : 'Download invoice' }}
          </button>
          @if (canCancel()) {
            <button
              type="button"
              (click)="cancelBooking()"
              [disabled]="cancelBusy()"
              class="flex-1 rounded-xl border border-rose-200 bg-rose-50 py-2.5 text-sm font-semibold text-rose-800 disabled:opacity-50"
            >
              {{ cancelBusy() ? 'Cancelling…' : 'Cancel booking' }}
            </button>
          }
        </div>
      } @else if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `
})
export class BookingDetailPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly detail = signal<PortalBookingDetailDto | null>(null);
  readonly payBusy = signal(false);
  readonly payError = signal<string | null>(null);
  readonly invoiceBusy = signal(false);
  readonly cancelBusy = signal(false);

  readonly payForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentMethod: ['Cash', Validators.required]
  });

  readonly lineStatus = paymentLineStatusLabel;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((pm) => {
      const id = Number(pm.get('id'));
      if (!id) {
        this.error.set('Invalid booking.');
        this.loading.set(false);
        return;
      }
      this.load(id);
    });
  }

  canCancel(): boolean {
    const d = this.detail();
    return !!d && (d.bookingStatus === 1 || d.bookingStatus === 2);
  }

  downloadInvoice(): void {
    const d = this.detail();
    if (!d) return;
    this.invoiceBusy.set(true);
    this.api
      .downloadInvoice(d.id)
      .pipe(take(1))
      .subscribe({
        next: (res) => {
          this.invoiceBusy.set(false);
          const blob = res.body;
          if (!blob) return;
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `invoice-${d.bookingNumber}.html`;
          a.click();
          URL.revokeObjectURL(url);
        },
        error: () => this.invoiceBusy.set(false)
      });
  }

  cancelBooking(): void {
    const d = this.detail();
    if (!d || !confirm('Cancel this booking?')) return;
    this.cancelBusy.set(true);
    this.api
      .cancelBooking(d.id)
      .pipe(take(1))
      .subscribe({
        next: () => {
          this.cancelBusy.set(false);
          this.load(d.id);
        },
        error: () => this.cancelBusy.set(false)
      });
  }

  private load(id: number): void {
    if (!this.session.isAuthenticated()) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getBooking(id)
      .pipe(take(1))
      .subscribe({
        next: (d) => {
          this.detail.set(d);
          this.payForm.patchValue({ amount: d.remaining > 0 ? d.remaining : 0 });
          this.loading.set(false);
          const q = this.route.snapshot.queryParamMap;
          if (q.get('pay') === '1' && d.remaining > 0) {
            setTimeout(() => document.getElementById('pay-anchor')?.scrollIntoView({ behavior: 'smooth' }), 100);
          }
          if (q.get('track') === '1') {
            setTimeout(() => document.querySelector('app-booking-tracking')?.scrollIntoView({ behavior: 'smooth' }), 100);
          }
        },
        error: (e: unknown) => {
          const msg =
            e instanceof HttpErrorResponse
              ? (e.error as { message?: string })?.message
              : undefined;
          this.error.set(
            msg ||
              'This booking is not linked to your signed-in phone. Sign out on Profile, sign in again with the same number used when booking, then open the trip from My bookings.'
          );
          this.loading.set(false);
        }
      });
  }

  submitPay(): void {
    const d = this.detail();
    if (!d || !this.session.isAuthenticated()) return;
    this.payError.set(null);
    if (this.payForm.invalid) {
      this.payForm.markAllAsTouched();
      return;
    }
    const amt = Number(this.payForm.value.amount);
    if (amt <= 0 || amt > d.remaining) {
      this.payError.set('Amount must be between 0.01 and the remaining balance.');
      return;
    }
    this.payBusy.set(true);
    this.api
      .createPayment(d.id, {
        amount: amt,
        paymentMethod: this.payForm.value.paymentMethod!
      })
      .pipe(take(1))
      .subscribe({
        next: () => {
          this.payBusy.set(false);
          this.load(d.id);
        },
        error: (e: unknown) => {
          this.payBusy.set(false);
          if (e instanceof HttpErrorResponse) {
            const m = (e.error as { message?: string })?.message;
            this.payError.set(m || e.message);
          } else this.payError.set('Payment failed.');
        }
      });
  }
}
