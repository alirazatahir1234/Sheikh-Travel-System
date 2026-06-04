import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { PortalApiService } from '../../core/services/portal-api.service';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-booking-card',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card"
      [class.opacity-60]="card().bookingStatus === 5"
    >
      <div class="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p class="text-sm font-semibold text-slate-900">{{ card().routeLabel || 'Route' }}</p>
          <p class="text-xs text-slate-500">{{ card().pickupTime | date : 'medium' }}</p>
          <p class="mt-1 text-xs font-medium text-slate-600">{{ card().bookingNumber }}</p>
        </div>
        <app-status-badge [payState]="card().payState" />
      </div>
      <div class="mt-3 grid grid-cols-2 gap-2 text-sm">
        <div>
          <p class="text-xs text-slate-500">Paid</p>
          <p class="font-semibold text-emerald-700">PKR {{ card().paidAmount | number : '1.2-2' }}</p>
        </div>
        <div>
          <p class="text-xs text-slate-500">Remaining</p>
          <p class="font-semibold" [class.text-rose-600]="card().remaining > 0" [class.text-slate-400]="card().remaining <= 0">
            PKR {{ card().remaining | number : '1.2-2' }}
          </p>
        </div>
      </div>
      <div class="mt-2 flex flex-wrap gap-2">
        <app-status-badge [bookingStatus]="card().bookingStatus" />
      </div>
      <div class="mt-4 flex flex-wrap gap-2">
        <a
          [routerLink]="['/bookings', card().id]"
          class="inline-flex flex-1 min-w-[7rem] justify-center rounded-xl bg-slate-100 px-3 py-2.5 text-sm font-semibold text-slate-800 ring-1 ring-slate-200 hover:bg-slate-200"
        >
          Details
        </a>
        @if (card().bookingStatus === 3) {
          <a
            [routerLink]="['/bookings', card().id]"
            [queryParams]="{ track: '1' }"
            class="inline-flex flex-1 min-w-[7rem] justify-center rounded-xl bg-primary-600 px-3 py-2.5 text-sm font-semibold text-white hover:bg-primary-700"
          >
            Track
          </a>
        }
        @if (canCancel()) {
          <button
            type="button"
            (click)="cancel()"
            [disabled]="cancelBusy()"
            class="inline-flex flex-1 min-w-[7rem] justify-center rounded-xl border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm font-semibold text-rose-800 disabled:opacity-50"
          >
            {{ cancelBusy() ? '…' : 'Cancel' }}
          </button>
        }
        <button
          type="button"
          (click)="downloadInvoice()"
          [disabled]="invoiceBusy()"
          class="inline-flex flex-1 min-w-[7rem] justify-center rounded-xl border border-slate-200 px-3 py-2.5 text-sm font-semibold text-slate-800 disabled:opacity-50"
        >
          {{ invoiceBusy() ? '…' : 'Invoice' }}
        </button>
        @if (card().remaining > 0 && card().bookingStatus !== 5) {
          <a
            [routerLink]="['/bookings', card().id]"
            [queryParams]="{ pay: '1' }"
            class="inline-flex flex-1 min-w-[7rem] justify-center rounded-xl bg-emerald-600 px-3 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700"
          >
            Pay
          </a>
        }
      </div>
    </article>
  `
})
export class BookingCardComponent {
  readonly card = input.required<PortalBookingCardDto>();
  readonly cancelled = output<void>();

  private readonly api = inject(PortalApiService);
  private readonly router = inject(Router);

  readonly cancelBusy = signal(false);
  readonly invoiceBusy = signal(false);

  canCancel(): boolean {
    const s = this.card().bookingStatus;
    return s === 1 || s === 2;
  }

  cancel(): void {
    if (!confirm('Cancel this booking?')) return;
    this.cancelBusy.set(true);
    this.api.cancelBooking(this.card().id).subscribe({
      next: () => {
        this.cancelBusy.set(false);
        this.cancelled.emit();
      },
      error: () => this.cancelBusy.set(false)
    });
  }

  downloadInvoice(): void {
    this.invoiceBusy.set(true);
    this.api.downloadInvoice(this.card().id).subscribe({
      next: (res) => {
        this.invoiceBusy.set(false);
        const blob = res.body;
        if (!blob) return;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `invoice-${this.card().bookingNumber}.html`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.invoiceBusy.set(false)
    });
  }
}
